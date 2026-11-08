using Patina.Contracts;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>What the vocabulary offers is what a real stack runs: every filter, as a layer and on a mask.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     The whole list and not a sample, since it comes from the registry and a node nobody tried would be offered
    ///     all the same. Each runs over a soft stroke and a half mask, so that it has something to change, and says
    ///     nothing is wrong.
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
        // A height channel, so that every filter also runs beside the set's own relief stage, which greys the height
        // for Height to Normal while the stack greys a measuring filter for itself.
        await EditAsync(state.Id, deadline.Token, new StackAddChannel("height"));
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
            var set = await EditAsync(
                state.Id,
                deadline.Token,
                new StackSetLayer(filter, FilterNode: kind.Node, Inputs: Fed(kind))
            );
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
                new StackSetLayer("paint", Mask: new("Paint", Effects: [new(kind.Node, Inputs: Fed(kind))]))
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
                    new StackSetLayer(
                        "paint",
                        Mask: new("Paint", Effects: [new(kind.Node, true, numbers, texts, Fed(kind))])
                    )
                );
                await EvaluatedAsync(socket, nudged.Revision, deadline.Token);
                if (Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor") == plain
                    && !KeepsAHardEdge.Contains(kind.Node)) {
                    failures.Add($"mask {kind.Node} changes nothing, even with {Described(numbers, texts)}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    ///     A noise for every image a filter reads beside its subject and cannot do without, as a person fills a warp's
    ///     field before it does anything. An input named after a channel reads that channel beneath by itself.
    /// </summary>
    static Dictionary<string, StackInput>? Fed(StackFilterKind kind) {
        Dictionary<string, StackInput> inputs = [];
        foreach (var input in kind.Inputs ?? []) {
            if (input is { Subject: false, Optional: false, Channel: "" }) {
                inputs[input.Port] = new(
                    "Generator",
                    Generator: "Source/Noise",
                    Values: new Dictionary<string, IReadOnlyList<float>> { ["Scale"] = [6f] }
                );
            }
        }

        return inputs.Count == 0 ? null : inputs;
    }

    /// <summary>
    ///     Filters whose whole point is to leave a step where it is, so a mask of hard-edged strokes at two values
    ///     comes through them unchanged however they are set. Each is held to moving a soft picture on the layer side.
    /// </summary>
    static readonly HashSet<string> KeepsAHardEdge = ["Filters/Median", "Filters/Painterly Smooth"];

    /// <summary>
    ///     Away from a default that leaves the picture alone, as a person would move it: a curve that bends, a choice
    ///     that differs and every number moved, within the slider's own range. Each colour is a different colour, so a
    ///     filter that moves one colour onto another is not handed the same one twice.
    /// </summary>
    static (Dictionary<string, IReadOnlyList<float>> Numbers, Dictionary<string, string> Texts) Nudged(
        StackFilterKind kind
    ) {
        Dictionary<string, IReadOnlyList<float>> numbers = [];
        var colours = 0;
        foreach (var port in kind.Ports) {
            var start = port.Default.Count > 0 ? port.Default[0] : 0f;
            numbers[port.Name] = port.Kind switch {
                "Bool" => [start == 0f ? 1f : 0f],
                "Int" => [start + 1f],
                "Float4" => colours++ % 2 == 0 ? [0.2f, 0.9f, 0.4f, 1f] : [0.9f, 0.6f, 0.7f, 1f],
                _ => [Moved(start, port.Range)]
            };
        }

        Dictionary<string, string> texts = [];
        foreach (var setting in kind.Settings) {
            if (setting.Name == "Curve") {
                texts[setting.Name] = "curve:r=0 0.3,1 1;g=0 0.3,1 1;b=0 0.3,1 1";
            } else if (setting.Name == "Ramp") {
                texts[setting.Name] = "ramp:0 ffffffff,1 000000ff";
            } else if (OtherThanDefault(setting) is { } other) {
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

    /// <summary>
    ///     A number moved by 0.3, up unless that leaves the slider's range, down unless that leaves the hard one, and
    ///     otherwise half way to the top of the slider.
    /// </summary>
    static float Moved(float start, GraphPortRange? range) {
        var top = range?.Maximum ?? range?.SoftMaximum;
        if (top is not { } ceiling || start + 0.3f <= ceiling) {
            return start + 0.3f;
        }

        return range?.Minimum is { } floor && start - 0.3f < floor ? (start + ceiling) / 2f : start - 0.3f;
    }

    /// <summary>The first choice a setting takes that is not where it starts, or null for one with no list.</summary>
    static string? OtherThanDefault(GraphSettingDefinition setting) =>
        setting.Accepted.FirstOrDefault(value => value != setting.Default);

    static string Described(Dictionary<string, IReadOnlyList<float>> numbers, Dictionary<string, string> texts) =>
        string.Join(
            ", ",
            [
                ..numbers.Select(static pair => $"{pair.Key}={string.Join(' ', pair.Value)}"),
                ..texts.Select(static pair => $"{pair.Key}={pair.Value}")
            ]
        );
}
