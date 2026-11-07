using Patina.Contracts;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>
///     Duplicate, copy, cut and paste with a real Worker: the pixels go with a layer, and are never shared.
/// </summary>
public sealed partial class StackProcessTests {
    static readonly IReadOnlyList<float> Blue = [0.1f, 0.2f, 0.9f, 1f];

    /// <summary>
    ///     A duplicate shows what its source shows, a stroke on it leaves the source alone, a Worker that dies brings
    ///     both back from the journal, and one undo takes the duplicate away.
    /// </summary>
    [Fact]
    public async Task A_duplicated_paint_layer_has_a_canvas_of_its_own() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Twice.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await PaintAsync(state.Id, "first", Line(100, 100, 20), false, deadline.Token);
        await EditAsync(state.Id, deadline.Token, MaskStroke("hole", "paint", 130f, 100f, [0f, 0f, 0f, 1f]));
        var painted = await PixelsAsync(state.Id, false, deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackDuplicateLayers(["paint"]));
        var copy = (await StateAsync(state.Id, deadline.Token)).Layers[^1];
        var source = Layer(await StateAsync(state.Id, deadline.Token), "paint");
        Assert.NotEqual("paint", copy.Id);
        Assert.True(copy.Painted);

        // In its source's place, the copy is the same picture: the same pixels under the same mask.
        Assert.Equal(source.Kind, copy.Kind);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", Enabled: false));
        Assert.Equal(
            Digest(painted, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", Enabled: true));

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("second", copy.Id, "baseColor", new(24f, Spacing: 0.25f), Blue, Line(100, 600, 20))
        );
        var both = await PixelsAsync(state.Id, false, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(copy.Id, Enabled: false));
        Assert.Equal(
            Digest(painted, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(copy.Id, Enabled: true));

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        await EvaluatedAsync(socket, (await StateAsync(state.Id, deadline.Token)).Revision, deadline.Token);
        Assert.Equal(
            Digest(both, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );

        // Five edits came after it: four eyes and the stroke on the copy.
        for (var step = 0; step < 5; step++) {
            await ActAsync(state.Id, "undo", deadline.Token);
        }

        var undone = await ActAsync(state.Id, "undo", deadline.Token);
        Assert.Equal("Duplicate layers", undone.History.RedoLabel);
        Assert.DoesNotContain((await StateAsync(state.Id, deadline.Token)).Layers, layer => layer.Id == copy.Id);
        Assert.Equal(
            Digest(painted, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     A copy is the pixels as they stood: a stroke after it is not in it, a cut layer still pastes, and it pastes
    ///     into another stack of the same size. Each paste is one undo step.
    /// </summary>
    [Fact]
    public async Task A_copy_pastes_what_stood_when_it_was_copied_into_any_stack() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Source.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await PaintAsync(state.Id, "first", Line(100, 100, 20), false, deadline.Token);
        var copied = await PixelsAsync(state.Id, false, deadline.Token);
        var clip = await ClipAsync(state.Id, ["paint"], deadline.Token);
        Assert.Equal(1, clip.Layers);

        // Cut: copied, then removed, then a stroke on what is left that the copy must not hold.
        await EditAsync(state.Id, deadline.Token, new StackRemoveLayer("paint"));
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("paint", "Paint"));
        var later = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("later", later, "baseColor", new(24f, Spacing: 0.25f), Blue, Line(100, 600, 20))
        );
        await EditAsync(state.Id, deadline.Token, new StackRemoveLayer(later));
        await EditAsync(state.Id, deadline.Token, new StackPasteLayers(clip.Id));
        Assert.Equal(
            Digest(copied, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
        Assert.Equal("Paste layers", (await StateAsync(state.Id, deadline.Token)).History.UndoLabel);

        // Another stack of the same size, whose base is the same grey, shows the same picture with the paste.
        var other = await CreateAsync("Other.vxlayers", deadline.Token);
        await EditAsync(other.Id, deadline.Token, new StackRemoveLayer("paint"));
        var blank = await PixelsAsync(other.Id, false, deadline.Token);
        await EditAsync(other.Id, deadline.Token, new StackPasteLayers(clip.Id));
        Assert.Equal(
            Digest(copied, "baseColor"),
            Digest(await PixelsAsync(other.Id, false, deadline.Token), "baseColor")
        );

        await ActAsync(other.Id, "undo", deadline.Token);
        Assert.Equal(
            Digest(blank, "baseColor"),
            Digest(await PixelsAsync(other.Id, false, deadline.Token), "baseColor")
        );

        // A stack of another size takes no paint, and says why.
        var small = await CreateAsync("Small.vxlayers", deadline.Token, 512);
        using var refused = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{small.Id}/commands",
            new([new StackPasteLayers(clip.Id)]),
            ContractsJsonContext.Default.StackCommandBatch,
            deadline.Token
        );
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, refused.StatusCode);
    }

    async Task<StackClip> ClipAsync(string id, IReadOnlyList<string> layers, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/clip",
            new(layers),
            ContractsJsonContext.Default.StackClipRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackClip, token))!;
    }
}
