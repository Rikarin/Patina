using Patina.Contracts;
using Patina.Library;
using Vixen.Texturing.Painting;

namespace Patina.Server.Tests;

/// <summary>
///     A decal on a texture set laid out in its mesh's second UV set (#133 with #210): the bake the set is left to
///     choose lands in that UV set, and the decal, which reads the bake's position and world normal maps, lands in it
///     too, where the first UV set would have put it somewhere else.
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     One tilted triangle laid out twice: its first UV set is the lower-left half of the square, its second a
    ///     small triangle in the middle of it, a thirty-second of the square.
    /// </summary>
    static string TwoLayoutTriangle(string into) {
        float[] positions = [0, 0, 0, 1, 0, 0.5f, 0, 1, 0.5f];
        float[] first = [0, 0, 1, 0, 0, 1];
        float[] second = [0.5f, 0.25f, 0.75f, 0.25f, 0.5f, 0.5f];
        var lean = 1f / MathF.Sqrt(1.5f);
        float[] normals = [
            -0.5f * lean, -0.5f * lean, lean, -0.5f * lean, -0.5f * lean, lean, -0.5f * lean,
            -0.5f * lean, lean
        ];
        ushort[] indices = [0, 1, 2];
        var buffer = new byte[36 + 24 + 24 + 36 + 6];
        Buffer.BlockCopy(positions, 0, buffer, 0, 36);
        Buffer.BlockCopy(first, 0, buffer, 36, 24);
        Buffer.BlockCopy(second, 0, buffer, 60, 24);
        Buffer.BlockCopy(normals, 0, buffer, 84, 36);
        Buffer.BlockCopy(indices, 0, buffer, 120, 6);
        Directory.CreateDirectory(into);
        var path = Path.Combine(into, "ramp.gltf");
        File.WriteAllText(
            path,
            $$"""
              {
                "asset": { "version": "2.0" },
                "scene": 0,
                "scenes": [{ "nodes": [0] }],
                "nodes": [{ "mesh": 0 }],
                "materials": [{ "name": "Body" }],
                "meshes": [{ "name": "Ramp", "primitives": [{
                  "attributes": { "POSITION": 0, "TEXCOORD_0": 1, "TEXCOORD_1": 2, "NORMAL": 3 },
                  "indices": 4,
                  "material": 0
                }] }],
                "buffers": [{
                  "byteLength": {{buffer.Length}},
                  "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String(buffer)}}"
                }],
                "bufferViews": [
                  { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                  { "buffer": 0, "byteOffset": 36, "byteLength": 24 },
                  { "buffer": 0, "byteOffset": 60, "byteLength": 24 },
                  { "buffer": 0, "byteOffset": 84, "byteLength": 36 },
                  { "buffer": 0, "byteOffset": 120, "byteLength": 6 }
                ],
                "accessors": [
                  {
                    "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3",
                    "min": [0, 0, 0], "max": [1, 1, 0.5]
                  },
                  { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC2" },
                  { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                  { "bufferView": 3, "componentType": 5126, "count": 3, "type": "VEC3" },
                  { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
                ]
              }
              """
        );
        return path;
    }

    /// <summary>
    ///     The set is moved to UV set 2 and baked without naming one, so the bake takes the set's. A decal wide enough
    ///     to cover the whole triangle then covers the second layout's small triangle in the middle of the map: about a
    ///     thirty-second of it, and nothing in the left half that the first layout would have filled.
    /// </summary>
    [Fact]
    public async Task A_decal_on_a_set_in_its_second_uv_set_lands_in_that_uv_set() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = TwoLayoutTriangle(Path.Combine(folder.FullName, "meshes"));
        _ = await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        var projectFolder = Path.Combine(Library, "projects", project);
        var stack = Path.GetRelativePath(Library, Directory.EnumerateFiles(projectFolder, "*.vxlayers").Single())
            .Replace('\\', '/');

        var opened = await OpenAsync(stack, deadline.Token);
        await EditAsync(opened.Id, deadline.Token, new StackSetSettings(UvSet: 1));
        await SaveAsync(opened.Id, deadline.Token);
        using (var closed = await http.DeleteAsync($"/api/v1/stacks/{opened.Id}", deadline.Token)) {
            Assert.True(closed.IsSuccessStatusCode);
        }

        await BakeAsync(
            new() {
                High = [Path.Combine(projectFolder, "ramp.gltf")],
                Low = Path.Combine(projectFolder, "ramp.gltf"),
                Material = "Body",
                Output = Path.Combine(projectFolder, "bakes", "maps"),
                Options = new() { Resolution = ExportSide, Maps = ["position", "world"], Format = "png16" }
            },
            deadline.Token
        );

        var state = await OpenAsync(stack, deadline.Token);
        Assert.Equal(1, state.UvSet);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("fill", "Badge", new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = Red })
        );
        var badge = (await StateAsync(state.Id, deadline.Token)).Layers.Single(static layer => layer.Name == "Badge")
            .Id;
        var lean = 1f / MathF.Sqrt(1.5f);
        var decal = new StackDecal(
            [1f / 3, 1f / 3, 1f / 3],
            [-0.5f * lean, -0.5f * lean, lean],
            [0, 1, 0.5f],
            4,
            4,
            2,
            Box: [0, 0, 0, 1, 1, 0.5f]
        );
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(badge, Projection: "Decal", Decal: decal));

        var map = await BaseColourAsync(state.Id, "uv-set-2", deadline.Token);
        var (count, leftmost) = (0, map.Width);
        for (var y = 0; y < map.Height; y++) {
            for (var x = 0; x < map.Width; x++) {
                var at = (y * map.Width + x) * 4;
                if (Reddish(map.Pixels[at], map.Pixels[at + 1], map.Pixels[at + 2])) {
                    count++;
                    leftmost = Math.Min(leftmost, x);
                }
            }
        }

        var whole = map.Width * map.Height;
        Assert.InRange(count, whole / 64, whole / 16);
        Assert.True(
            leftmost >= map.Width * 45 / 100,
            $"A red texel at {leftmost} of {map.Width} is in UV set 1's half."
        );
    }

    /// <summary>
    ///     A pick on a set laid out in its mesh's second UV set says the place in that UV set (#210), which Find Layer,
    ///     the color picker and the clone source read: before, it said the first UV set's, a place the set does not
    ///     paint. The point it met is the triangle's, so its place in either layout follows from where it is.
    /// </summary>
    [Fact]
    public async Task A_pick_on_a_set_in_its_second_uv_set_says_the_place_in_that_uv_set() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = TwoLayoutTriangle(Path.Combine(folder.FullName, "meshes"));
        var (instance, _) = await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        var stack = Path.GetRelativePath(
                Library,
                Directory.EnumerateFiles(Path.Combine(Library, "projects", project), "*.vxlayers").Single()
            )
            .Replace('\\', '/');
        var state = await OpenAsync(stack, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetSettings(UvSet: 1));
        using var frames = await WearAsync(instance, state.Id, deadline.Token);

        var picked = await FirstHitAsync(deadline.Token);

        // The ramp's corners are (0, 0, 0), (1, 0, 0.5) and (0, 1, 0.5), so a point's x and y are its weights.
        var (x, y) = (picked.Position![0], picked.Position[1]);
        Assert.Equal(0.5f + 0.25f * x, picked.U, 0.01f);
        Assert.Equal(0.25f + 0.25f * y, picked.V, 0.01f);
    }

    /// <summary>
    ///     Paint carried from UV set 1 into UV set 2 with the move (#210): painted all over in the first layout, where
    ///     the triangle is half the square, it lands in the second's small triangle in the middle of the map and nowhere
    ///     else, as an evaluation from nothing shows it too; the canvas it was on stays as it was, for an undo.
    /// </summary>
    [Fact]
    public async Task Paint_carried_into_the_second_uv_set_lands_in_its_layout() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = TwoLayoutTriangle(Path.Combine(folder.FullName, "meshes"));
        _ = await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        var projectFolder = Path.Combine(Library, "projects", project);
        var stack = Path.GetRelativePath(Library, Directory.EnumerateFiles(projectFolder, "*.vxlayers").Single())
            .Replace('\\', '/');
        var state = await OpenAsync(stack, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("paint", "Ink"));
        var ink = (await StateAsync(state.Id, deadline.Token)).Layers.Single(static layer => layer.Name == "Ink").Id;
        var brush = new StackBrush(state.Width, Falloff: 0f);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("all", ink, "baseColor", brush, Red, [new(state.Width / 2f, state.Height / 2f)])
        );
        await SaveAsync(state.Id, deadline.Token);
        var painted = Directory.EnumerateFiles(projectFolder, "*.vxpaint").Single();
        var held = File.ReadAllBytes(painted);

        await EditAsync(state.Id, deadline.Token, new StackSetSettings(UvSet: 1), new StackCarryPaint(0));

        var shown = await PixelsAsync(state.Id, false, deadline.Token);
        var clean = await PixelsAsync(state.Id, true, deadline.Token);
        Assert.Equal(Digest(clean, "baseColor"), Digest(shown, "baseColor"));
        await SaveAsync(state.Id, deadline.Token);
        Assert.Equal(held, File.ReadAllBytes(painted));
        var carried = Directory.EnumerateFiles(projectFolder, "*.vxpaint").Single(file => file != painted);
        using var read = new MemoryStream(await File.ReadAllBytesAsync(carried, deadline.Token));
        var canvas = PaintCanvas.Read(read);

        var image = canvas.Channel("baseColor");
        var covered = 0;
        for (var y = 0; y < image.Height; y++) {
            for (var x = 0; x < image.Width; x++) {
                covered += image.At(x, y) != image.Background ? 1 : 0;
            }
        }

        var whole = image.Width * image.Height;
        Assert.InRange(covered, whole / 64, whole / 16);
    }

    /// <summary>The first of a few places on the picture, from a few sides, that meets the ramp.</summary>
    async Task<ViewportPick> FirstHitAsync(CancellationToken token) {
        foreach (var camera in new ViewportCameraState[] { new(0, 0, 1.05), new(0, 1.2, 1.05), new(0, -1.2, 1.05) }) {
            foreach (var (x, y) in new[] { (0.5, 0.5), (0.45, 0.55), (0.4, 0.6), (0.55, 0.45) }) {
                using var response = await http.PostAsJsonAsync(
                    "/api/v1/viewport/pick",
                    new(x, y, camera, Picture, Picture),
                    ContractsJsonContext.Default.ViewportPickRequest,
                    token
                );
                Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
                var picked = await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ViewportPick, token);
                if (picked!.Found) {
                    return picked;
                }
            }
        }

        Assert.Fail("No place on the picture met the ramp.");
        return null!;
    }
}
