using Patina.Contracts;
using System.Net.Http.Json;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>Symmetry and a brush's size space with a real Worker, counted in the exported map (#132, #123).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A stroke on the housing mirrored through its centre paints about twice what it does alone, and four copies
    ///     round its up axis about four times: each copy is a camera moved with it, so none is hidden behind the model.
    ///     The housing is centred on its origin, so the model's centre against the origin is Vixen's
    ///     PaintSymmetryTests'.
    /// </summary>
    [Fact]
    public async Task A_symmetric_stroke_on_the_mesh_paints_each_of_its_copies() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Mirror.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var view = await AimAsync(0.5, 0.5, deadline.Token);
        var stroke = Across("one", view, 300, 360, 330, false) with { Brush = new(12f, Spacing: 0.25f) };

        var alone = await RedAfterAsync(state.Id, "alone", stroke, deadline.Token);
        var mirrored = await RedAfterAsync(state.Id, "mirror", stroke with { Symmetry = new() }, deadline.Token);
        var moved = await RedAfterAsync(state.Id, "moved", stroke with { Symmetry = new(Offset: 1f) }, deadline.Token);
        var radial = await RedAfterAsync(
            state.Id,
            "radial",
            stroke with { Symmetry = new("radial", "y", Count: 4) },
            deadline.Token
        );

        Assert.True(alone > 50, $"The stroke alone painted {alone} texels, so the comparison says nothing.");
        Assert.InRange(mirrored, alone * 1.6, alone * 2.4);
        Assert.InRange(radial, alone * 3.2, alone * 4.8);

        // A plane moved a bounding radius off the centre reflects the stroke into empty space.
        Assert.InRange(moved, alone * 0.9, alone * 1.1);
    }

    /// <summary>
    ///     The same dab seen from twice as far: in the screen's units it covers four times as much of the model, and
    ///     in the model's own it covers the same.
    /// </summary>
    [Fact]
    public async Task A_size_in_the_model_is_the_same_on_the_mesh_at_any_zoom() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Zoom.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var near = await ZoomedAsync(1.05, deadline.Token);
        var far = await ZoomedAsync(2.1, deadline.Token);

        static StackStroke Dab(StackView view, string space) =>
            new(
                "dab",
                "paint",
                "baseColor",
                new(24f, SizeSpace: space),
                Red,
                [new(Picture / 2f, Picture / 2f)],
                View: view
            );

        var screenNear = await RedAfterAsync(state.Id, "screen-near", Dab(near, "screen"), deadline.Token);
        var screenFar = await RedAfterAsync(state.Id, "screen-far", Dab(far, "screen"), deadline.Token);
        var modelNear = await RedAfterAsync(state.Id, "model-near", Dab(near, "model"), deadline.Token);
        var modelFar = await RedAfterAsync(state.Id, "model-far", Dab(far, "model"), deadline.Token);

        Assert.True(
            screenFar > 2.5 * screenNear,
            $"{screenNear} texels near and {screenFar} far, in the screen's units."
        );
        Assert.InRange(modelFar, modelNear * 0.8, modelNear * 1.25);
    }

    /// <summary>Paints one stroke, counts the red texels of the exported base colour, and undoes it.</summary>
    async Task<int> RedAfterAsync(string id, string name, StackStroke stroke, CancellationToken token) {
        await EditAsync(id, token, stroke);
        var exported = await ExportAsync(id, Path.Combine(folder.FullName, name), token);
        var file = PngCodec.Decode(File.ReadAllBytes(exported.Single(static map => map.Usage == "baseColor").File!));
        var red = 0;
        for (var at = 0; at < file.Width * file.Height; at++) {
            red += file.Pixels[at * 4] - file.Pixels[at * 4 + 1] > 100 ? 1 : 0;
        }

        await ActAsync(id, "undo", token);
        return red;
    }

    async Task<StackView> ZoomedAsync(double distance, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/viewport/pick",
            new(0.5, 0.5, new(0.6, 0.5, distance), Picture, Picture),
            ContractsJsonContext.Default.ViewportPickRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        var picked = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ViewportPick, token))!;
        Assert.True(picked.Found, "The middle of the picture should be on the housing.");
        return picked.View;
    }
}
