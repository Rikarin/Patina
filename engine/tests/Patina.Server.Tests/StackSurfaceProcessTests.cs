using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>
///     A decal and a path with a real Worker, on the housing and its own bake (#130, #131, #133, #134): each lays its
///     fill where it was placed, an edit moves it, and an undo puts back the same pixels, because neither is stamped.
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A decal placed where the viewport's middle meets the housing covers some of the map and not all of it;
    ///     making it twice as wide covers more; undoing that is the same map to the byte; and a path through two picked
    ///     points covers a band that widens with its radius.
    /// </summary>
    [Fact]
    public async Task A_decal_and_a_path_lay_a_fill_where_they_are_placed_and_move_with_an_edit() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.05f);
        var (instance, _) = await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(Path.Combine(meshes, BakeFixture.LowFile), deadline.Token);
        var low = Path.Combine(Library, "projects", project, BakeFixture.LowFile);
        await BakeAsync(
            new() {
                High = [Path.Combine(meshes, BakeFixture.HighFile)],
                Low = low,
                Output = Path.Combine(Library, "projects", project, "bakes", "maps"),
                Options = new() { Resolution = ExportSide, Maps = ["position", "world"], Format = "png16" }
            },
            deadline.Token
        );

        var state = await OpenAsync($"projects/{project}/housing.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var middle = await PickAsync(0.5, 0.5, deadline.Token);
        var bounds = middle.Bounds!;
        var size = MathF.Sqrt(
            MathF.Pow(bounds[3] - bounds[0], 2)
            + MathF.Pow(bounds[4] - bounds[1], 2)
            + MathF.Pow(bounds[5] - bounds[2], 2)
        );
        Assert.Equal(0, await CountAsync(state.Id, "before", Reddish, deadline.Token));

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("fill", "Badge", new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = Red })
        );
        var badge = (await StateAsync(state.Id, deadline.Token)).Layers.Single(static layer => layer.Name == "Badge")
            .Id;

        StackDecal Decal(float width) =>
            new(middle.Position!, middle.Normal!, middle.View.Up, width, width, size * 0.2f, Box: bounds);

        // Not placed yet, the layer covers nothing rather than the whole housing.
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(badge, Projection: "Decal"));
        Assert.Equal(0, await CountAsync(state.Id, "unplaced", Reddish, deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(badge, Decal: Decal(size * 0.1f)));
        var placed = await CountAsync(state.Id, "placed", Reddish, deadline.Token);
        var whole = ExportSide * ExportSide;
        Assert.InRange(placed, 50, whole / 4);
        var first = Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor");

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(badge, Decal: Decal(size * 0.2f)));
        var wider = await CountAsync(state.Id, "wider", Reddish, deadline.Token);
        Assert.True(wider > placed * 1.5, $"A decal twice as wide covered {wider} texels against {placed}.");

        await ActAsync(state.Id, "undo", deadline.Token);
        Assert.Equal(first, Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor"));
        var history = (await StateAsync(state.Id, deadline.Token)).History;
        Assert.NotNull(history);

        // The same layer along a path through two places either side of the middle.
        var left = await PickAsync(0.45, 0.5, deadline.Token);
        var right = await PickAsync(0.55, 0.5, deadline.Token);

        StackSurfacePath Through(float radius) =>
            new(
                [
                    new(left.Position!, left.Normal!), new(middle.Position!, middle.Normal!),
                    new(right.Position!, right.Normal!)
                ],
                radius,
                1f,
                Box: bounds
            );

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(badge, Projection: "Path", Path: Through(size * 0.01f))
        );
        var thin = await CountAsync(state.Id, "thin", Reddish, deadline.Token);
        Assert.InRange(thin, 20, whole / 4);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(badge, Path: Through(size * 0.03f)));
        var thick = await CountAsync(state.Id, "thick", Reddish, deadline.Token);
        Assert.True(thick > thin * 2, $"A path three times as wide covered {thick} texels against {thin}.");

        // And the document holds the path as points, which is what the Server sends back.
        var kept = (await StateAsync(state.Id, deadline.Token)).Layers.Single(layer => layer.Id == badge);
        Assert.Equal("Path", kept.Projection);
        Assert.Equal(3, kept.Path!.Points.Count);
        Assert.NotNull(kept.Decal);
    }

    async Task<ViewportPick> PickAsync(double x, double y, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/viewport/pick",
            new(x, y, null, Picture, Picture),
            ContractsJsonContext.Default.ViewportPickRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        var picked = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ViewportPick, token))!;
        Assert.True(picked.Found, $"({x}, {y}) of the picture should be on the housing.");
        Assert.NotNull(picked.Normal);
        Assert.Equal(1f, MathF.Sqrt(picked.Normal.Sum(static lane => lane * lane)), 3);
        return picked;
    }
}
