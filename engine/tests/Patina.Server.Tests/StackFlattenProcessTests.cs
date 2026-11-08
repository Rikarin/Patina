using Patina.Contracts;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>Flattening with a real Worker: the canvas is the picture the folder made, and the journal makes it again.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A folder flattened exports what it exported before, to a canvas's rounding; a Worker that dies writes the same
    ///     canvas from the journal; unflattening and undoing are the picture before, exactly.
    /// </summary>
    [Fact]
    public async Task A_flattened_folder_exports_what_its_layers_did() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Flat.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await PaintAsync(state.Id, "first", Line(100, 100, 30), false, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("group", "Folder"));
        var folder = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer("base", AnchorPoint: "Base"),
            new StackAddLayer("fill", "Rust", Tint, folder),
            new StackNestLayer("paint", folder),
            new StackSetLayer(folder, Opacity: 0.8f)
        );
        var rust = Layer(await StateAsync(state.Id, deadline.Token), folder).Children[0].Id;
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(rust, Opacity: 0.6f, Mask: new("Constant", 0.5f))
        );
        var before = await ExportAsync(state.Id, Path.Combine(this.folder.FullName, "before"), deadline.Token);

        var flattened = await EditAsync(state.Id, deadline.Token, new StackFlattenLayer(folder));
        await EvaluatedAsync(socket, flattened.Revision, deadline.Token);
        var layer = Layer(await StateAsync(state.Id, deadline.Token), folder);
        Assert.True(layer.Flattened);
        Assert.True(layer.Painted);
        Close(before, await ExportAsync(state.Id, Path.Combine(this.folder.FullName, "after"), deadline.Token));

        // From the journal alone: a new Worker writes the canvas the first one wrote.
        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        await EvaluatedAsync(socket, (await StateAsync(state.Id, deadline.Token)).Revision, deadline.Token);
        Close(before, await ExportAsync(state.Id, Path.Combine(this.folder.FullName, "replayed"), deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackUnflattenLayer(folder));
        Assert.Equal(
            Digest(before, "baseColor"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
        );

        await ActAsync(state.Id, "undo", deadline.Token);
        var undone = await ActAsync(state.Id, "undo", deadline.Token);
        Assert.Equal("Flatten folder", undone.History.RedoLabel);
        Assert.False(Layer(await StateAsync(state.Id, deadline.Token), folder).Flattened);
        Assert.Equal(
            Digest(before, "baseColor"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
        );
    }

    /// <summary>Each map's file, within the rounding a canvas adds, and different from nothing.</summary>
    static void Close(IReadOnlyList<StackPixels> before, IReadOnlyList<StackPixels> after) {
        foreach (var map in before) {
            var expected = PngCodec.Decode(File.ReadAllBytes(map.File!)).Pixels;
            var actual = PngCodec.Decode(File.ReadAllBytes(after.Single(other => other.Usage == map.Usage).File!)).Pixels;
            var worst = expected.Zip(actual, static (a, b) => Math.Abs(a - b)).Max();
            Assert.True(worst <= 3, $"'{map.Usage}' is {worst} of 255 away from what the folder's layers made.");
        }
    }
}
