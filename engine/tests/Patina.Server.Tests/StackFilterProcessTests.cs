using Patina.Contracts;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>What the vocabulary offers is what a real stack runs: every filter, as a layer and on a mask.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     The whole list and not a sample, since it comes from the registry and a node nobody tried would be offered all
    ///     the same. Each runs over a soft stroke and a half mask, so that it has something to change, and says nothing is
    ///     wrong.
    /// </summary>
    [Fact]
    public async Task Every_offered_filter_runs_through_a_real_stack() {
        using var deadline = Deadline(900);
        var (instance, state) = await StartAsync("Filters.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var words = (await http.GetFromJsonAsync(
            "/api/v1/stacks/vocabulary",
            ContractsJsonContext.Default.StackVocabulary,
            deadline.Token
        ))!;
        await PaintAsync(state.Id, "dot", Line(300, 300, 30), false, deadline.Token);
        await EditAsync(state.Id, deadline.Token, MaskStroke("hole", "paint", 330f, 300f, [0.5f, 0.5f, 0.5f, 1f]));
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("filter", "Filter", Filter: "Levels"));
        var filter = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var off = await EditAsync(state.Id, deadline.Token, new StackSetLayer(filter, Enabled: false));
        await EvaluatedAsync(socket, off.Revision, deadline.Token);
        var plain = Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor");
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(filter, Enabled: true));

        List<string> failures = [];
        foreach (var kind in words.Filters) {
            var set = await EditAsync(state.Id, deadline.Token, new StackSetLayer(filter, FilterNode: kind.Node));
            var evaluation = await EvaluatedAsync(socket, set.Revision, deadline.Token);
            if (evaluation.Problems.Count > 0) {
                failures.Add(
                    $"layer {kind.Node}: {string.Join(" | ", evaluation.Problems.Select(static p => p.Message))}"
                );
                continue;
            }

            if (Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor") == plain) {
                var (numbers, texts) = Nudged(kind);
                var nudged = await EditAsync(
                    state.Id,
                    deadline.Token,
                    new StackSetLayer(filter, Settings: numbers, Parameters: texts)
                );
                await EvaluatedAsync(socket, nudged.Revision, deadline.Token);
                if (Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor") == plain) {
                    failures.Add($"layer {kind.Node} changes nothing, even with {Described(numbers, texts)}");
                }
            }
        }

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(filter, Enabled: false));
        foreach (var kind in words.MaskEffects) {
            var set = await EditAsync(
                state.Id,
                deadline.Token,
                new StackSetLayer("paint", Mask: new("Paint", Effects: [new(kind.Node)]))
            );
            var evaluation = await EvaluatedAsync(socket, set.Revision, deadline.Token);
            if (evaluation.Problems.Count > 0) {
                failures.Add(
                    $"mask {kind.Node}: {string.Join(" | ", evaluation.Problems.Select(static p => p.Message))}"
                );
                continue;
            }

            if (Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor") == plain) {
                var (numbers, texts) = Nudged(kind);
                var nudged = await EditAsync(
                    state.Id,
                    deadline.Token,
                    new StackSetLayer("paint", Mask: new("Paint", Effects: [new(kind.Node, true, numbers, texts)]))
                );
                await EvaluatedAsync(socket, nudged.Revision, deadline.Token);
                if (Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor") == plain) {
                    failures.Add($"mask {kind.Node} changes nothing, even with {Described(numbers, texts)}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    ///     Away from a default that leaves the picture alone, as a person would move it: a curve that bends, a choice
    ///     that differs and every number moved.
    /// </summary>
    static (Dictionary<string, IReadOnlyList<float>> Numbers, Dictionary<string, string> Texts) Nudged(
        StackFilterKind kind
    ) {
        Dictionary<string, IReadOnlyList<float>> numbers = [];
        foreach (var port in kind.Ports) {
            var start = port.Default.Count > 0 ? port.Default[0] : 0f;
            numbers[port.Name] = port.Kind switch {
                "Bool" => [start == 0f ? 1f : 0f],
                "Int" => [start + 1f],
                "Float4" => [0.2f, 0.9f, 0.4f, 1f],
                _ => [port.Range?.Maximum is { } top && start + 0.3f > top ? start - 0.3f : start + 0.3f]
            };
        }

        Dictionary<string, string> texts = [];
        foreach (var setting in kind.Settings) {
            if (setting.Name == "Curve") {
                texts[setting.Name] = "curve:r=0 0.3,1 1;g=0 0.3,1 1;b=0 0.3,1 1";
            } else if (setting.Name == "Ramp") {
                texts[setting.Name] = "ramp:0 ffffffff,1 000000ff";
            } else if (setting.Accepted.FirstOrDefault(value => value != setting.Default) is { } other) {
                texts[setting.Name] = other;
            } else if (setting.Kind is "Float" or "Int"
                       && float.TryParse(
                           setting.Default,
                           System.Globalization.CultureInfo.InvariantCulture,
                           out var number
                       )) {
                texts[setting.Name] = (number + 0.3f).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return (numbers, texts);
    }

    static string Described(Dictionary<string, IReadOnlyList<float>> numbers, Dictionary<string, string> texts) =>
        string.Join(
            ", ",
            [
                ..numbers.Select(static pair => $"{pair.Key}={string.Join(' ', pair.Value)}"),
                ..texts.Select(static pair => $"{pair.Key}={pair.Value}")
            ]
        );
}
