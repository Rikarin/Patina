using Patina.Contracts;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>Flattening with a real Worker: the canvas is the folder's picture, and the journal makes it anew.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A folder flattened exports what it exported before, to a canvas's rounding; a Worker that dies writes the
    ///     same canvas from the journal; unflattening and undoing are the picture before, exactly.
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
        Assert.Equal("Base", Assert.Single((await StateAsync(state.Id, deadline.Token)).Anchors!).Name);
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

    /// <summary>
    ///     A flattened folder exports on its own (#107): its files are named after it and hold what the stack holds
    ///     with every other layer switched off, which is not what the whole stack holds. A layer the stack lacks is
    ///     refused.
    /// </summary>
    [Fact]
    public async Task A_flattened_folder_exports_on_its_own() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Alone.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await PaintAsync(state.Id, "first", Line(100, 100, 30), false, deadline.Token);
        // A base unlike the channel's default, so the whole stack is a picture the folder alone is not.
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(
                "base",
                Values: new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = [0.1f, 0.6f, 0.2f, 1f] }
            ),
            new StackAddLayer("group", "Rust Folder")
        );
        var folder = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("fill", "Rust", Tint, folder),
            new StackNestLayer("paint", folder)
        );
        var rust = Layer(await StateAsync(state.Id, deadline.Token), folder).Children[0].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(rust, Mask: new("Constant", 0.5f)));
        var flattened = await EditAsync(state.Id, deadline.Token, new StackFlattenLayer(folder));
        await EvaluatedAsync(socket, flattened.Revision, deadline.Token);
        var whole = await ExportAsync(state.Id, Path.Combine(this.folder.FullName, "whole"), deadline.Token);

        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{state.Id}/export",
            new(Path.Combine(this.folder.FullName, "alone"), folder),
            ContractsJsonContext.Default.StackExportRequest,
            deadline.Token
        );
        await SucceededAsync(response, deadline.Token);
        var alone = (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.IReadOnlyListStackPixels,
            deadline.Token
        ))!;
        Assert.All(
            alone,
            static map => Assert.Contains(".Rust Folder.", Path.GetFileName(map.File!), StringComparison.Ordinal)
        );

        await EditAsync(state.Id, deadline.Token, new StackSetLayer("base", Enabled: false));
        var only = await ExportAsync(state.Id, Path.Combine(this.folder.FullName, "only"), deadline.Token);
        Close(only, alone);
        Assert.NotEqual(Digest(whole, "baseColor"), Digest(alone, "baseColor"));

        using var refused = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{state.Id}/export",
            new(Path.Combine(this.folder.FullName, "none"), "nowhere"),
            ContractsJsonContext.Default.StackExportRequest,
            deadline.Token
        );
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(
            "stack-layer-missing",
            await refused.Content.ReadAsStringAsync(deadline.Token),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     Any layer exports on its own, not only a flattened folder, and so does its mask (#153): one grey map,
    ///     <c>mask</c>, named after the layer, that holds the mask and nothing of the layer's content or opacity. A mask
    ///     with no layer named is refused.
    /// </summary>
    [Fact]
    public async Task A_layer_and_its_mask_export_on_their_own() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Masked.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Spot", Tint));
        var spot = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var masked = await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(spot, null, null, 0.5f, Mask: new("Constant", 0.25f))
        );
        await EvaluatedAsync(socket, masked.Revision, deadline.Token);

        var layer = await ExportAloneAsync(state.Id, new(Path.Combine(folder.FullName, "spot"), spot), deadline.Token);
        var mask = await ExportAloneAsync(
            state.Id,
            new(Path.Combine(folder.FullName, "mask"), spot) { Mask = true },
            deadline.Token
        );

        Assert.Contains(layer, static map => map.Usage == "baseColor");
        var only = Assert.Single(mask);
        Assert.Equal("mask", only.Usage);
        Assert.EndsWith(".Spot.mask.png", only.File, StringComparison.Ordinal);
        var pixels = PngCodec.Decode(File.ReadAllBytes(only.File!)).Pixels;
        Assert.All(pixels.Where(static (_, at) => at % 4 == 0), static value => Assert.InRange(value, 62, 66));

        using var refused = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{state.Id}/export",
            new(Path.Combine(folder.FullName, "none")) { Mask = true },
            ContractsJsonContext.Default.StackExportRequest,
            deadline.Token
        );
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, refused.StatusCode);
    }

    async Task<IReadOnlyList<StackPixels>> ExportAloneAsync(
        string id,
        StackExportRequest request,
        CancellationToken token
    ) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/export",
            request,
            ContractsJsonContext.Default.StackExportRequest,
            token
        );
        await SucceededAsync(response, token);
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.IReadOnlyListStackPixels, token))!;
    }

    /// <summary>Each map's file, within the rounding a canvas adds.</summary>
    static void Close(IReadOnlyList<StackPixels> before, IReadOnlyList<StackPixels> after) {
        var files = after.ToDictionary(static map => map.Usage, static map => map.File!);
        foreach (var map in before) {
            var expected = PngCodec.Decode(File.ReadAllBytes(map.File!)).Pixels;
            var actual = PngCodec.Decode(File.ReadAllBytes(files[map.Usage])).Pixels;
            var worst = expected.Zip(actual, static (a, b) => Math.Abs(a - b)).Max();
            Assert.True(worst <= 3, $"'{map.Usage}' is {worst} of 255 away from what the folder's layers made.");
        }
    }
}
