using Patina.Contracts;
using Patina.Library;
using System.Net.Http.Json;
using System.Text.Json;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     Polygon fill, smudge, clone, projection, stencils and a material brush with a real Worker, counted in the
///     exported map (#125, #127, #128, #129).
/// </summary>
public sealed partial class StackProcessTests {
    static readonly IReadOnlyList<float> Nothing = [0f, 0f, 0f, 1f];

    /// <summary>
    ///     A click on one of the housing's flats: a triangle is about half of the flat, the polygon is the flat, the
    ///     flat is its own UV shell, and the mesh part is the whole body of ten flats.
    /// </summary>
    [Fact]
    public async Task A_polygon_fill_takes_the_piece_of_the_mesh_its_mode_says() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Fill.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var view = await AimAsync(0.5, 0.5, deadline.Token);
        StackStroke Click(string mode) =>
            new(
                "fill",
                "paint",
                "baseColor",
                new(1f),
                Red,
                [new(Picture / 2f, Picture / 2f)],
                View: view,
                Curve: "fill",
                Fill: mode
            );

        var triangle = await RedAfterAsync(state.Id, "triangle", Click("triangle"), deadline.Token);
        var polygon = await RedAfterAsync(state.Id, "polygon", Click("polygon"), deadline.Token);
        var shell = await RedAfterAsync(state.Id, "shell", Click("shell"), deadline.Token);
        var part = await RedAfterAsync(state.Id, "part", Click("part"), deadline.Token);

        Assert.True(triangle > 1000, $"A triangle took {triangle} texels, so the click missed.");
        Assert.InRange(triangle, polygon * 0.35, polygon * 0.65);
        Assert.InRange(shell, polygon * 0.98, polygon * 1.02);
        Assert.True(part > polygon * 5, $"The body took {part} texels against the flat's {polygon}.");
    }

    /// <summary>
    ///     In the atlas: a picture paints its colours where it stands, a stencil's black half holds the brush back, a
    ///     clone copies what lies at its offset, and a smudge drags paint along and undoes to what was there.
    /// </summary>
    [Fact]
    public async Task Pictures_stencils_clones_and_smudges_paint_what_they_read() {
        using var deadline = Deadline(300);
        var (instance, state) = await StartAsync("Tools.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var blue = WritePicture("blue.png", 2, 2, static _ => [20, 40, 230, 255]);
        var half = WritePicture("half.png", 2, 2, static column => column == 0 ? [0, 0, 0, 255] : [255, 255, 255, 255]);
        StackStroke Dab(string name, float x, float y) =>
            new(name, "paint", "baseColor", new(30f, Falloff: 0f), Red, [new(x, y)]);

        var projected = await CountAfterAsync(
            state.Id,
            "picture",
            Dab("picture", 200, 200) with {
                Source = new("picture", Picture: new(blue, [200f, 200f], 40f))
            },
            Bluish,
            deadline.Token
        );
        Assert.InRange(projected, Math.PI * 30 * 30 * 0.9, Math.PI * 30 * 30 * 1.1);

        var whole = await CountAfterAsync(state.Id, "whole", Dab("whole", 400, 400), Reddish, deadline.Token);
        var stencilled = await CountAfterAsync(
            state.Id,
            "stencilled",
            Dab("stencilled", 400, 400) with { Stencil = new(half, [400f, 400f], 40f) },
            Reddish,
            deadline.Token
        );
        Assert.InRange(stencilled, whole * 0.4, whole * 0.6);

        await EditAsync(state.Id, deadline.Token, Dab("first", 100, 600));
        var one = await CountAsync(state.Id, "one", Reddish, deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            Dab("clone", 300, 600) with { Colour = Nothing, Source = new("clone", Offset: [-200f, 0f]) }
        );
        var two = await CountAsync(state.Id, "two", Reddish, deadline.Token);
        Assert.InRange(two, one * 1.9, one * 2.1);

        var before = Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor");
        var smear = Dab("smear", 100, 600) with {
            Brush = new(20f, Spacing: 0.1f, Falloff: 0f),
            Colour = Nothing,
            Source = new("smudge"),
            Stamps = [new(100, 600), new(160, 600)]
        };
        await EditAsync(state.Id, deadline.Token, smear);
        var dragged = await CountAsync(state.Id, "dragged", Reddish, deadline.Token);
        Assert.True(dragged > two * 1.1, $"A smudge across the red left {dragged} red texels of {two}.");
        await ActAsync(state.Id, "undo", deadline.Token);
        Assert.Equal(before, Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor"));
    }

    /// <summary>
    ///     A material brush lays what its graph makes: under a dab, the base colour is what a fill of the same graph
    ///     shows there, and not the stroke's colour.
    /// </summary>
    [Fact]
    public async Task A_material_brush_lays_what_its_graph_makes() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Crate", kind = "layered-material" },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        await File.WriteAllTextAsync(
            Path.Combine(Library, "projects", "crate", "cells.vxtexgraph"),
            CellGraph("{ id: 1, type: Source/Uniform, values: { Colour: [0.1, 0.7, 0.3, 1] } }"),
            deadline.Token
        );
        var used = await http.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new { project = "crate", source = "projects/crate/cells.vxtexgraph" },
            deadline.Token
        );
        var reply = await used.Content.ReadAsStringAsync(deadline.Token);
        Assert.True(used.IsSuccessStatusCode, reply);
        var graph = JsonDocument.Parse(reply).RootElement.GetProperty("graph").GetString()!;
        var state = await OpenAsync("projects/crate/crate.vxlayers", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("paint", "Paint"));
        var paint = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var size = (await PixelsAsync(state.Id, false, deadline.Token)).First(static map => map.Usage == "baseColor").Width;
        var at = size / 2;

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke(
                "material",
                paint,
                "baseColor",
                new(size / 8f, Falloff: 0f),
                Red,
                [new(at, at)],
                Source: new("material", graph)
            )
        );
        var painted = await TexelAsync(state.Id, "painted", at, at, deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Cells"));
        var layer = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(layer, Fill: "Graph", Graph: graph));
        var filled = await TexelAsync(state.Id, "filled", at, at, deadline.Token);

        Assert.Equal(filled, painted);
    }

    static bool Bluish(byte red, byte green, byte blue) => blue - red > 100;

    static bool Reddish(byte red, byte green, byte blue) => red - green > 100;

    /// <summary>Writes a picture into the test's folder, and answers its path.</summary>
    string WritePicture(string name, int width, int height, Func<int, byte[]> column) {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                column(x).CopyTo(pixels, ((y * width) + x) * 4);
            }
        }

        var file = Path.Combine(folder.FullName, name);
        File.WriteAllBytes(file, PngCodec.Encode(new(width, height, pixels)));
        return file;
    }

    /// <summary>Paints a stroke, counts the texels of the exported base colour that pass, and undoes it.</summary>
    async Task<int> CountAfterAsync(
        string id,
        string name,
        StackStroke stroke,
        Func<byte, byte, byte, bool> passes,
        CancellationToken token
    ) {
        await EditAsync(id, token, stroke);
        var count = await CountAsync(id, name, passes, token);
        await ActAsync(id, "undo", token);
        return count;
    }

    async Task<int> CountAsync(string id, string name, Func<byte, byte, byte, bool> passes, CancellationToken token) {
        var file = await BaseColourAsync(id, name, token);
        var count = 0;
        for (var at = 0; at < file.Width * file.Height; at++) {
            count += passes(file.Pixels[at * 4], file.Pixels[at * 4 + 1], file.Pixels[at * 4 + 2]) ? 1 : 0;
        }

        return count;
    }

    async Task<uint> TexelAsync(string id, string name, int x, int y, CancellationToken token) {
        var file = await BaseColourAsync(id, name, token);
        return BitConverter.ToUInt32(file.Pixels, ((y * file.Width) + x) * 4);
    }

    async Task<Bitmap> BaseColourAsync(string id, string name, CancellationToken token) {
        var exported = await ExportAsync(id, Path.Combine(folder.FullName, name), token);
        return PngCodec.Decode(File.ReadAllBytes(exported.Single(static map => map.Usage == "baseColor").File!));
    }
}
