using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>Painting as a client sees it: the real Server and Worker, with strokes sent as lists of stamps.</summary>
public sealed partial class StackProcessTests : IDisposable {
    const int Side = 1024;

    /// <summary>The size of the picture a stroke on the mesh is measured on.</summary>
    const int Picture = 800;

    static readonly IReadOnlyList<float> Red = [0.9f, 0.1f, 0.05f, 1f];

    static readonly Dictionary<string, IReadOnlyList<float>> Tint = new() { ["baseColor"] = [0.8f, 0.7f, 0.5f, 1f] };

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-stack-");
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
    readonly List<Process> started = [];
    readonly HttpClient http = new();

    string Library => Directory.CreateDirectory(Path.Combine(folder.FullName, "Game.ptlib")).FullName;

    string DiscoveryFile => Path.Combine(folder.FullName, "servers.json");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() {
        foreach (var process in started) {
            if (!process.HasExited) {
                process.Kill(true);
                process.WaitForExit();
            }

            process.Dispose();
        }

        http.Dispose();
        File.Delete(socketPath);
        try {
            folder.Delete(true);
        } catch (IOException) {
            // A Worker's last write can land while the folder is going, and a temp folder left behind is no failure.
        }
    }

    /// <summary>
    ///     A drag sent in parts is one undo step, composited as tiles through the plan, and an undo and a redo come
    ///     back
    ///     to the same picture, down to the digest.
    /// </summary>
    [Fact]
    public async Task A_drag_in_parts_is_one_undo_step_and_repairs_its_tiles() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Crate.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        var blank = await PixelsAsync(state.Id, false, deadline.Token);

        await PaintAsync(state.Id, "drag", Line(100, 100, 12), true, deadline.Token);
        var part = await PaintAsync(state.Id, "drag", Line(220, 100, 12), false, deadline.Token);
        var painted = await EvaluatedAsync(socket, part.Revision, deadline.Token);

        Assert.True(painted.Repaired > 0, "The stroke ran whole, so this never reached the tiles.");
        var after = await PixelsAsync(state.Id, false, deadline.Token);
        Assert.NotEqual(Digest(blank, "baseColor"), Digest(after, "baseColor"));
        Assert.Equal(Digest(blank, "roughness"), Digest(after, "roughness"));

        var undone = await ActAsync(state.Id, "undo", deadline.Token);
        Assert.False(undone.History.CanUndo);
        await EvaluatedAsync(socket, undone.Revision, deadline.Token);
        Assert.Equal(
            Digest(blank, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );

        var redone = await ActAsync(state.Id, "redo", deadline.Token);
        await EvaluatedAsync(socket, redone.Revision, deadline.Token);
        Assert.Equal(
            Digest(after, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     The exit criterion of P5: what the strokes left, what a clean evaluation makes and what the exported file
    ///     holds are one picture. The digest is the shared hash a viewport compares against.
    /// </summary>
    const string MaterialGraph = """
                                 name: Rust
                                 nodes:
                                   - id: 1
                                     type: Source/Uniform
                                     values: { Colour: [0.8, 0.2, 0.1, 1] }
                                   - id: 2
                                     type: Source/Uniform
                                     values: { Colour: [0.9, 0.9, 0.9, 1] }
                                   - { id: 3, type: Output/Output, texts: { Usage: baseColor } }
                                   - { id: 4, type: Output/Output, texts: { Usage: roughness } }
                                 edges:
                                   - { fromNode: 1, fromPort: Out, toNode: 3, toPort: Input }
                                   - { fromNode: 2, fromPort: Out, toNode: 4, toPort: Input }
                                 groups: []
                                 comments: []
                                 interface: []
                                 settings: { baseWidth: '256', baseHeight: '256', seed: '1' }
                                 parameters: []
                                 """;

    /// <summary>
    ///     The join between the two halves of the app: a graph's maps fill a stack's channels by usage, through a real
    ///     Worker, and a channel the graph has no map for stays what it was.
    /// </summary>
    [Fact]
    public async Task A_material_graph_fills_each_channel_it_has_a_map_for() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Crate", kind = "layered-material" },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        await File.WriteAllTextAsync(
            Path.Combine(Library, "projects", "crate", "rust.vxtexgraph"),
            MaterialGraph,
            deadline.Token
        );
        var used = await http.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new { project = "crate", source = "projects/crate/rust.vxtexgraph" },
            deadline.Token
        );
        var reply = await used.Content.ReadAsStringAsync(deadline.Token);
        Assert.True(used.IsSuccessStatusCode, reply);
        var graph = JsonDocument.Parse(reply).RootElement.GetProperty("graph").GetString();

        var state = await OpenAsync("projects/crate/crate.vxlayers", deadline.Token);
        var before = await PixelsAsync(state.Id, true, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Rust"));
        var layer = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(layer, Fill: "Graph", Graph: graph));

        var after = await PixelsAsync(state.Id, true, deadline.Token);

        Assert.NotEqual(Digest(before, "baseColor"), Digest(after, "baseColor"));
        Assert.NotEqual(Digest(before, "roughness"), Digest(after, "roughness"));
        Assert.Equal(Digest(before, "metalness"), Digest(after, "metalness"));
    }

    /// <summary>One grey pixel, which is all a template needs to show it makes a map per channel.</summary>
    const string Pixel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR4nGP4DwABAQEAG7buVgAAAABJRU5ErkJggg==";

    [Fact]
    public async Task An_image_material_makes_a_map_for_each_channel_it_has() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);
        var photo = Path.Combine(folder.FullName, "wall.png");
        await File.WriteAllBytesAsync(photo, Convert.FromBase64String(Pixel), deadline.Token);
        foreach (var body in new object[] {
                     new { name = "Wall", kind = "image-material", image = photo },
                     new { name = "Crate", kind = "layered-material" }
                 }) {
            var made = await http.PostAsJsonAsync("/api/v1/library/projects", body, deadline.Token);
            Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        }

        var used = await http.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new { project = "crate", source = "projects/wall/wall.vxtexgraph" },
            deadline.Token
        );
        var reply = await used.Content.ReadAsStringAsync(deadline.Token);
        Assert.True(used.IsSuccessStatusCode, reply);

        var state = await OpenAsync("projects/crate/crate.vxlayers", deadline.Token);
        var before = await PixelsAsync(state.Id, true, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Wall"));
        var layer = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var graph = JsonDocument.Parse(reply).RootElement.GetProperty("graph").GetString();
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(layer, Fill: "Graph", Graph: graph));

        var after = await PixelsAsync(state.Id, true, deadline.Token);

        Assert.NotEqual(Digest(before, "roughness"), Digest(after, "roughness"));
    }

    [Fact]
    public async Task A_shipped_material_unfolds_into_layers_and_undoes_as_one_step() {
        using var deadline = Deadline(180);
        var (_, state) = await StartAsync("Smart.vxlayers", deadline.Token);
        var layers = state.Layers.Count;

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("material", string.Empty, Material: "smart:Plastic")
        );

        Assert.True((await StateAsync(state.Id, deadline.Token)).Layers.Count > layers);
        await ActAsync(state.Id, "undo", deadline.Token);
        Assert.Equal(layers, (await StateAsync(state.Id, deadline.Token)).Layers.Count);
    }

    [Fact]
    public async Task What_you_paint_is_what_exports() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Export.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);

        // The modes the old pane could not reproduce, with a layer over the stroke to composite through.
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", null, null, 0.7f, "Overlay"));
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Top", Tint));
        var top = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(top, null, null, 0.4f, "Multiply"));
        var first = await PaintAsync(state.Id, "a", Line(80, 300, 30), false, deadline.Token);
        await EvaluatedAsync(socket, first.Revision, deadline.Token);
        var second = await PaintAsync(state.Id, "b", Line(600, 700, 30), false, deadline.Token);
        await EvaluatedAsync(socket, second.Revision, deadline.Token);

        var composite = await PixelsAsync(state.Id, false, deadline.Token);
        var exported = await ExportAsync(state.Id, Path.Combine(folder.FullName, "out"), deadline.Token);

        Assert.Equal(state.Channels.Count, exported.Count);
        foreach (var map in exported) {
            Assert.Equal(map.Digest, Digest(composite, map.Usage));
            var file = PngCodec.Decode(File.ReadAllBytes(map.File!));
            Assert.Equal(map.Digest, Convert.ToHexStringLower(SHA256.HashData(file.Pixels)));
        }
    }

    /// <summary>
    ///     The same criterion on a mesh, through the viewport. Viewport pixels are tonemapped, so what is compared is
    ///     the picture the plan means: the top level of the texture each material slot samples, read back from the
    ///     viewport's own device, as 8-bit RGBA. Its digest must equal that of the file an export wrote for the
    ///     channel, from a clean evaluation of the whole stack, after strokes that cross the mesh's seams, an undo and
    ///     a redo.
    /// </summary>
    [Fact]
    public async Task What_the_viewport_samples_is_what_exports() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Sampled.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", null, null, 0.7f, "Overlay"));
        using var frames = await WearAsync(instance, state.Id, deadline.Token);

        var view = await AimAsync(0.5, 0.5, deadline.Token);
        var first = await EditAsync(state.Id, deadline.Token, Across("one", view, 300, 500, 300, false));
        await EvaluatedAsync(socket, first.Revision, deadline.Token);

        // A first stroke named the layer's paint file, so undoing it compiles the plan from before it.
        await ActAsync(state.Id, "undo", deadline.Token);
        await SameAsExportAsync(state, "before", deadline.Token);
        var again = await ActAsync(state.Id, "redo", deadline.Token);
        await EvaluatedAsync(socket, again.Revision, deadline.Token);

        var live = await EditAsync(state.Id, deadline.Token, Across("two", view, 200, 620, 420, true));
        var repaired = await EvaluatedAsync(socket, live.Revision, deadline.Token);
        Assert.True(repaired.Repaired > 0, "The stroke ran whole, so this never reached the viewport as tiles.");
        var closed = await EditAsync(state.Id, deadline.Token, Across("two", view, 620, 200, 200, false));
        await EvaluatedAsync(socket, closed.Revision, deadline.Token);

        // And one in the atlas, as a UV pane sends it, which carries on over the seams it meets.
        var flat = await PaintAsync(state.Id, "flat", Line(20, 20, 40), false, deadline.Token);
        await EvaluatedAsync(socket, flat.Revision, deadline.Token);

        await SameAsExportAsync(state, "first", deadline.Token);

        await ActAsync(state.Id, "undo", deadline.Token);
        await SameAsExportAsync(state, "undone", deadline.Token);

        await ActAsync(state.Id, "redo", deadline.Token);
        await SameAsExportAsync(state, "redone", deadline.Token);
    }

    /// <summary>
    ///     Every kind of layer the stack has, through the API alone: a fill, a filter, a group holding a layer that
    ///     moved into it, and masks of each source that needs no bake. What the viewport samples stays what exports,
    ///     whatever compiled or repaired on the way.
    /// </summary>
    [Fact]
    public async Task Every_kind_of_layer_stays_what_exports() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Layers.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var words = (await http.GetFromJsonAsync(
            "/api/v1/stacks/vocabulary",
            ContractsJsonContext.Default.StackVocabulary,
            deadline.Token
        ))!;
        Assert.Equal(16, words.Blends.Count);
        Assert.Contains("Generators/Dirt", words.Generators);
        Assert.Contains(words.GeneratorParameters["Generators/Metal Edge Wear"], static knob => knob.Name == "amount");

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("group", "Details"),
            new StackAddLayer("fill", "Tint", Tint),
            new StackAddLayer("filter", "Soft", Filter: "Hsl")
        );
        var layers = (await StateAsync(state.Id, deadline.Token)).Layers;
        var (group, fill, filter) = (layers[^3].Id, layers[^2].Id, layers[^1].Id);
        await EditAsync(state.Id, deadline.Token, new StackNestLayer(fill, group));
        Assert.Equal(
            fill,
            (await StateAsync(state.Id, deadline.Token)).Layers.Single(l => l.Id == group).Children[0].Id
        );

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(
                filter,
                Settings: new Dictionary<string, IReadOnlyList<float>> { ["Saturation"] = [0.2f] }
            ),
            new StackSetLayer(fill, Mask: new("Constant", 0.6f)),
            new StackSetLayer(group, Mask: new("Anchor", Anchor: "paint"))
        );
        var painted = await EditAsync(
            state.Id,
            deadline.Token,
            Across("one", await AimAsync(0.5, 0.5, deadline.Token), 300, 500, 300, false)
        );
        await EvaluatedAsync(socket, painted.Revision, deadline.Token);
        await SameAsExportAsync(state, "stack", deadline.Token);

        // A brush on the paint layer's mask names a canvas of its own.
        var masked = await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("mask", "paint", "baseColor", new(30f), Red, Line(60, 60, 20), Mask: true)
        );
        await EvaluatedAsync(socket, masked.Revision, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", Blend: "ColourDodge"));
        await SameAsExportAsync(state, "masked", deadline.Token);
    }

    /// <summary>
    ///     A mask belongs to any layer, so a fill, a filter and a folder each take a brush on it, while their own content
    ///     still refuses one: only a paint layer has pixels of its own.
    /// </summary>
    [Fact]
    public async Task A_mask_on_any_kind_of_layer_takes_a_brush() {
        using var deadline = Deadline(180);
        var (_, state) = await StartAsync("Masks.vxlayers", deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("fill", "Tint", Tint),
            new StackAddLayer("filter", "Flip", Filter: "Invert"),
            new StackAddLayer("group", "Folder")
        );
        var layers = (await StateAsync(state.Id, deadline.Token)).Layers;
        var (fill, filter, group) = (layers[^3].Id, layers[^2].Id, layers[^1].Id);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Inside", Tint, group));

        // Top down, or the layers over a mask would hide what it did.
        foreach (var layer in new[] { group, filter, fill }) {
            var before = await PixelsAsync(state.Id, true, deadline.Token);
            await EditAsync(state.Id, deadline.Token, MaskStroke($"on-{layer}", layer, 300f, 300f, [0f, 0f, 0f, 1f]));
            Assert.NotEqual(
                Digest(before, "baseColor"),
                Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
            );
        }

        foreach (var layer in new[] { fill, filter, group }) {
            await RefusedAsync(
                state.Id,
                deadline.Token,
                new StackStroke("content", layer, "baseColor", new(30f), Red, Line(60, 60, 20))
            );
        }
    }

    /// <summary>
    ///     The first stroke turns a flat mask into a canvas, and a canvas that started anywhere but where the mask
    ///     stood would hide or show everything the brush never touched. Undone, the flat mask is back.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0f)]
    public async Task A_stroke_on_a_flat_mask_changes_only_what_it_touches(float value) {
        using var deadline = Deadline(180);
        var (_, state) = await StartAsync("Flat.vxlayers", deadline.Token);
        var fill = await MaskedFillAsync(state.Id, new("Constant", value), deadline.Token);
        var before = await TexelsAsync(state.Id, "before", deadline.Token);

        var brush = value < 0.5f ? new[] { 1f, 1f, 1f, 1f } : [0f, 0f, 0f, 1f];
        await EditAsync(state.Id, deadline.Token, MaskStroke("hole", fill, 300f, 300f, brush));

        var after = await TexelsAsync(state.Id, "after", deadline.Token);
        Near(Texel(before, 800, 800), Texel(after, 800, 800));
        Assert.NotEqual(Texel(before, 350, 300), Texel(after, 350, 300));
        Assert.Equal("Paint", Layer(await StateAsync(state.Id, deadline.Token), fill).Mask.Source);

        await ActAsync(state.Id, "undo", deadline.Token);
        var undone = await StateAsync(state.Id, deadline.Token);
        Assert.Equal("Constant", Layer(undone, fill).Mask.Source);
        Assert.Equal(Digest(before.Maps, "baseColor"), Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor"));
    }

    /// <summary>A paint mask nothing has painted yet hides nothing, so its canvas starts as all of it.</summary>
    [Fact]
    public async Task A_new_paint_mask_starts_showing_the_whole_layer() {
        using var deadline = Deadline(180);
        var (_, state) = await StartAsync("Fresh.vxlayers", deadline.Token);
        var fill = await MaskedFillAsync(state.Id, new("Paint"), deadline.Token);
        var before = await TexelsAsync(state.Id, "before", deadline.Token);

        await EditAsync(state.Id, deadline.Token, MaskStroke("hole", fill, 300f, 300f, [0f, 0f, 0f, 1f]));

        var after = await TexelsAsync(state.Id, "after", deadline.Token);
        Near(Texel(before, 800, 800), Texel(after, 800, 800));
        Assert.NotEqual(Texel(before, 350, 300), Texel(after, 350, 300));
    }

    /// <summary>What a mask started as is in the journal, so a new Worker fills the canvas with the same thing.</summary>
    [Fact]
    public async Task A_worker_killed_after_a_mask_stroke_starts_the_canvas_again() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Replay.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var fill = await MaskedFillAsync(state.Id, new("Constant", 0.5f), deadline.Token);
        var stroke = await EditAsync(state.Id, deadline.Token, MaskStroke("hole", fill, 300f, 300f, [0f, 0f, 0f, 1f]));
        await EvaluatedAsync(socket, stroke.Revision, deadline.Token);
        var painted = await PixelsAsync(state.Id, false, deadline.Token);

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();

        Assert.Equal(Digest(painted, "baseColor"), Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor"));
    }

    /// <summary>
    ///     A removed mask is gone: nothing of it comes back with the next mask, whose first stroke starts a canvas of
    ///     its own. The old pixels stay on their own canvas, for an undo to reach.
    /// </summary>
    [Fact]
    public async Task A_removed_mask_leaves_nothing_for_the_next_one() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Removed.vxlayers", deadline.Token);
        var fill = await MaskedFillAsync(state.Id, new("Constant"), deadline.Token);
        var clean = await TexelsAsync(state.Id, "clean", deadline.Token);
        await EditAsync(state.Id, deadline.Token, MaskStroke("first", fill, 100f, 100f, [0f, 0f, 0f, 1f]));
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(
                fill,
                Mask: new("Paint", Effects: [new("Colour/Invert")])
            )
        );
        var inverted = await TexelsAsync(state.Id, "inverted", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(fill, Mask: new("Paint")));
        var first = await TexelsAsync(state.Id, "first", deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(fill, Mask: new("None", Effects: [new("Colour/Invert")])));
        var removed = Layer(await StateAsync(state.Id, deadline.Token), fill).Mask;
        Assert.Equal("None", removed.Source);
        Assert.Empty(removed.Effects!);

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(fill, Mask: new("Constant")));
        Assert.Empty(Layer(await StateAsync(state.Id, deadline.Token), fill).Mask.Effects!);
        await EditAsync(state.Id, deadline.Token, MaskStroke("second", fill, 600f, 600f, [0f, 0f, 0f, 1f]));

        var second = await TexelsAsync(state.Id, "second", deadline.Token);
        Near(Texel(clean, 150, 100), Texel(second, 150, 100));
        Assert.NotEqual(Texel(clean, 650, 600), Texel(second, 650, 600));
        Assert.NotEqual(Texel(inverted, 800, 800), Texel(second, 800, 800));

        foreach (var _ in Enumerable.Range(0, 3)) {
            await ActAsync(state.Id, "undo", deadline.Token);
        }

        Assert.Equal(
            Digest(first.Maps, "baseColor"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
        );
    }

    /// <summary>A stroke cannot keep a picture it would replace, so it says so instead of taking it.</summary>
    [Theory]
    [InlineData("Generator")]
    [InlineData("Texture")]
    [InlineData("Bake")]
    [InlineData("Anchor")]
    public async Task A_brush_leaves_a_mask_that_is_a_picture_alone(string source) {
        using var deadline = Deadline(120);
        var (_, state) = await StartAsync("Picture.vxlayers", deadline.Token);
        var fill = await MaskedFillAsync(
            state.Id,
            new(source, Asset: "any.png", Anchor: "paint", Generator: "Generators/Dirt", Map: "curvature"),
            deadline.Token
        );

        await RefusedAsync(state.Id, deadline.Token, MaskStroke("hole", fill, 300f, 300f, [0f, 0f, 0f, 1f]));

        Assert.Equal(source, Layer(await StateAsync(state.Id, deadline.Token), fill).Mask.Source);
    }

    /// <summary>
    ///     A slot nobody has filled yet leaves the layer out and says so against it. Everything else keeps working,
    ///     which is the point: a stroke made while one layer is half set up still reaches the picture.
    /// </summary>
    [Theory]
    [InlineData("Generator", "")]
    [InlineData("Texture", "")]
    [InlineData("Anchor", "")]
    [InlineData("Bake", "")]
    [InlineData("Anchor", "paint")]
    public async Task A_half_set_mask_leaves_the_stack_working_and_names_the_layer(string source, string anchor) {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Half.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Rough", new Dictionary<string, IReadOnlyList<float>> { ["roughness"] = [0.8f, 0.8f, 0.8f, 1f] }));
        var fill = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var before = await PixelsAsync(state.Id, true, deadline.Token);

        // "paint" is unpainted, so an anchor onto it has nothing to read.
        var set = await EditAsync(state.Id, deadline.Token, new StackSetLayer(fill, Mask: new(source, Anchor: anchor)));

        var evaluation = await EvaluatedAsync(socket, set.Revision, deadline.Token);
        var problem = Assert.Single(evaluation.Problems, problem => problem.Layer == fill);
        Assert.Equal("Warning", problem.Severity);
        Assert.Equal(Digest(before, "roughness"), Digest(await PixelsAsync(state.Id, true, deadline.Token), "roughness"));

        var painted = await PaintAsync(state.Id, "still", Line(100, 100, 12), false, deadline.Token);
        Assert.NotEmpty((await EvaluatedAsync(socket, painted.Revision, deadline.Token)).Previews);
        Assert.NotEqual(Digest(before, "baseColor"), Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor"));
    }

    /// <summary>A texture fill with no pictures yet writes nothing, channel by channel, and says which.</summary>
    [Fact]
    public async Task A_texture_fill_with_an_empty_channel_passes_that_channel_through() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Half.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Pictures"));
        var fill = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var before = await PixelsAsync(state.Id, true, deadline.Token);

        var set = await EditAsync(state.Id, deadline.Token, new StackSetLayer(fill, Fill: "Texture"));

        var evaluation = await EvaluatedAsync(socket, set.Revision, deadline.Token);
        Assert.Equal(state.Channels.Count, evaluation.Problems.Count(problem => problem.Layer == fill));
        Assert.Equal(Digest(before, "baseColor"), Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor"));
    }

    /// <summary>What is wrong rather than unfinished still stops the stack, and now says so against its layer.</summary>
    [Fact]
    public async Task A_stack_that_cannot_compile_says_which_layer_stopped_it() {
        using var deadline = Deadline(120);
        var (instance, state) = await StartAsync("Loop.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Top", Tint));
        var top = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;

        // Under the layer it reads, so its own result is part of what it masks by.
        var set = await EditAsync(state.Id, deadline.Token, new StackSetLayer("base", Mask: new("Anchor", Anchor: top)));

        var evaluation = await EvaluatedAsync(socket, set.Revision, deadline.Token);
        Assert.Contains(evaluation.Problems, static problem => problem is { Layer: "base", Severity: "Error" });
        Assert.Contains(
            (await StateAsync(state.Id, deadline.Token)).Evaluation!.Problems,
            static problem => problem is { Layer: "base", Severity: "Error" }
        );
    }

    /// <summary>A warning leaves the Worker: this filter has no such number, and the value goes nowhere.</summary>
    [Fact]
    public async Task A_value_a_filter_drops_is_reported() {
        using var deadline = Deadline(120);
        var (instance, state) = await StartAsync("Dropped.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("filter", "Soft", Filter: "Blur"));
        var filter = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;

        var set = await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(filter, Settings: new Dictionary<string, IReadOnlyList<float>> { ["Nonsense"] = [1f] })
        );

        var problems = (await EvaluatedAsync(socket, set.Revision, deadline.Token)).Problems;
        Assert.True(problems.Count == 1, string.Join(" | ", problems));
        Assert.Equal(filter, problems[0].Layer);
        Assert.Contains("Nonsense", problems[0].Message, StringComparison.Ordinal);
    }

    /// <summary>Spacing means what it says: far enough apart, stamps stop touching and a row of them is a pattern.</summary>
    [Fact]
    public async Task A_brush_spaced_past_its_size_leaves_gaps() {
        using var deadline = Deadline(120);
        var (_, state) = await StartAsync("Spaced.vxlayers", deadline.Token);
        var clean = await TexelsAsync(state.Id, "clean", deadline.Token);

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke(
                "dots",
                "paint",
                "baseColor",
                new(12f, Spacing: 4f, Falloff: 0f),
                Red,
                [new(100f, 500f), new(700f, 500f)]
            )
        );

        var painted = await TexelsAsync(state.Id, "dots", deadline.Token);
        var runs = 0;
        var inside = false;
        for (var x = 100; x < 700; x++) {
            var touched = !Texel(clean, x, 500).SequenceEqual(Texel(painted, x, 500));
            runs += touched && !inside ? 1 : 0;
            inside = touched;
        }

        Assert.True(runs > 5, $"{runs} separate marks along the row.");
    }

    /// <summary>A line carries the pressure of its ends, as a drag carries every stamp's.</summary>
    [Theory]
    [InlineData("line")]
    [InlineData("path")]
    public async Task A_curve_keeps_its_pressure(string curve) {
        using var deadline = Deadline(120);
        var (_, state) = await StartAsync("Pressed.vxlayers", deadline.Token);
        StackBrush brush = new(40f, Falloff: 0f, PressureSize: 1f);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("hard", "paint", "baseColor", brush, Red, [new(100f, 200f, 1f), new(700f, 200f, 1f)], Curve: curve),
            new StackStroke("light", "paint", "roughness", brush, Red, [new(100f, 200f, 0.1f), new(700f, 200f, 0.1f)], Curve: curve)
        );
        var maps = await ExportAsync(state.Id, Path.Combine(folder.FullName, "pressed"), deadline.Token);

        var (hard, light) = (Height(maps, "baseColor"), Height(maps, "roughness"));

        Assert.True(hard > light * 2, $"Pressed hard it is {hard} texels tall, and lightly {light}.");

        static int Height(IReadOnlyList<StackPixels> maps, string usage) {
            var map = PngCodec.Decode(File.ReadAllBytes(maps.Single(map => map.Usage == usage).File!));
            var first = map.Pixels.AsSpan(0, 4).ToArray();
            return Enumerable.Range(0, map.Height)
                .Count(y => !map.Pixels.AsSpan((y * map.Width + 400) * 4, 4).SequenceEqual(first));
        }
    }

    async Task<string> MaskedFillAsync(string id, StackMask mask, CancellationToken token) {
        await EditAsync(id, token, new StackAddLayer("fill", "Tint", Tint));
        var fill = (await StateAsync(id, token)).Layers[^1].Id;
        await EditAsync(id, token, new StackSetLayer(fill, Mask: mask));
        return fill;
    }

    static StackLayer Layer(StackState state, string id) => state.Layers.Single(layer => layer.Id == id);

    /// <summary>What each map holds, as digests and as the base colour's texels, from a clean evaluation.</summary>
    async Task<(IReadOnlyList<StackPixels> Maps, Bitmap BaseColor)> TexelsAsync(string id, string name, CancellationToken token) {
        var maps = await ExportAsync(id, Path.Combine(folder.FullName, $"{id}-{name}"), token);
        return (maps, PngCodec.Decode(File.ReadAllBytes(maps.Single(static map => map.Usage == "baseColor").File!)));
    }

    static byte[] Texel((IReadOnlyList<StackPixels> Maps, Bitmap BaseColor) picture, int x, int y) =>
        picture.BaseColor.Pixels.AsSpan((y * picture.BaseColor.Width + x) * 4, 4).ToArray();

    /// <summary>Equal to the rounding of one 8-bit canvas read through a mask.</summary>
    static void Near(byte[] expected, byte[] actual) {
        Assert.True(
            expected.Zip(actual).All(static lanes => Math.Abs(lanes.First - lanes.Second) <= 2),
            $"[{string.Join(", ", expected)}] and [{string.Join(", ", actual)}] are different pictures."
        );
    }

    static StackStroke MaskStroke(string stroke, string layer, float x, float y, IReadOnlyList<float> colour) =>
        new(stroke, layer, "baseColor", new(30f, Falloff: 0f), colour, Line(x, y, 20), Mask: true);

    async Task RefusedAsync(string id, CancellationToken token, params StackCommand[] commands) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/commands",
            new(commands),
            ContractsJsonContext.Default.StackCommandBatch,
            token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>A stroke on a mesh is positions and a camera, so a new Worker lands it in the same texels.</summary>
    [Fact]
    public async Task A_worker_killed_in_a_stroke_on_the_mesh_loses_nothing() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Mesh.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        using var frames = await WearAsync(instance, state.Id, deadline.Token);
        var view = await AimAsync(0.5, 0.5, deadline.Token);

        await EditAsync(state.Id, deadline.Token, Across("drag", view, 200, 400, 300, true));
        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        var last = await EditAsync(state.Id, deadline.Token, Across("drag", view, 400, 620, 300, false));
        StartWorker();
        await EvaluatedAsync(socket, last.Revision, deadline.Token);
        var replayed = await PixelsAsync(state.Id, false, deadline.Token);

        var other = await CreateAsync("Whole.vxlayers", deadline.Token);
        await BindMeshAsync(other.Id, deadline.Token);
        await EditAsync(other.Id, deadline.Token, Across("drag", view, 200, 400, 300, true));
        await EditAsync(other.Id, deadline.Token, Across("drag", view, 400, 620, 300, false));
        Assert.Equal(
            Digest(await PixelsAsync(other.Id, false, deadline.Token), "baseColor"),
            Digest(replayed, "baseColor")
        );
    }

    /// <summary>
    ///     Spike S5 for painting (ADR 0009): the Worker dies in the middle of a drag, more stamps arrive with nobody
    ///     there, and the next Worker makes the same bytes as a session that was never interrupted.
    /// </summary>
    [Fact]
    public async Task A_worker_killed_mid_stroke_loses_nothing() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Killed.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);

        await PaintAsync(state.Id, "before", Line(100, 500, 10), false, deadline.Token);
        await PaintAsync(state.Id, "drag", Line(100, 200, 10), true, deadline.Token);
        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);

        await PaintAsync(state.Id, "drag", Line(400, 200, 10), false, deadline.Token);
        var clock = Stopwatch.StartNew();
        StartWorker();
        var after = await EvaluatedAsync(socket, (await StateAsync(state.Id, deadline.Token)).Revision, deadline.Token);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"S5: the pixels were back {clock.ElapsedMilliseconds} ms after the new Worker started"
        );
        Assert.True(after.Compiled);
        var replayed = await PixelsAsync(state.Id, false, deadline.Token);

        // The same history, on a Worker that never died, from the same file.
        var control = await CreateAsync("Control.vxlayers", deadline.Token);
        await PaintAsync(control.Id, "before", Line(100, 500, 10), false, deadline.Token);
        await PaintAsync(control.Id, "drag", Line(100, 200, 10), true, deadline.Token);
        await PaintAsync(control.Id, "drag", Line(400, 200, 10), false, deadline.Token);
        Assert.Equal(
            Digest(await PixelsAsync(control.Id, false, deadline.Token), "baseColor"),
            Digest(replayed, "baseColor")
        );

        // History came through as well: the drag undoes as one step, on a Worker that never held it.
        await ActAsync(state.Id, "undo", deadline.Token);
        await ActAsync(control.Id, "undo", deadline.Token);
        Assert.Equal(
            Digest(await PixelsAsync(control.Id, false, deadline.Token), "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     What ADR 0009's end to end figure is: from sending stamps to holding the map they changed, as a client does
    ///     it, with the Server, the socket and the picture's file in the way. It prints and asserts nothing about time.
    /// </summary>
    [Fact]
    public async Task A_stamp_reaches_a_client_as_a_preview() {
        using var deadline = Deadline(300);
        var size = int.TryParse(Environment.GetEnvironmentVariable("PATINA_S4_SIZE"), out var chosen) ? chosen : Side;
        var worker = StartWorker();
        var (instance, state) = await AttachAsync("Live.vxlayers", deadline.Token, size);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        for (var layer = 0; layer < 18; layer++) {
            await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", $"Over {layer}", Tint));
        }

        var layers = (await StateAsync(state.Id, deadline.Token)).Layers;
        var last = await EditAsync(
            state.Id,
            deadline.Token,
            [
                ..layers.Skip(2)
                    .Select(static (layer, index) => (StackCommand)new StackSetLayer(
                            layer.Id,
                            null,
                            null,
                            0.3f,
                            index % 2 == 0 ? "Multiply" : "Overlay"
                        )
                    )
            ]
        );
        await EvaluatedAsync(socket, last.Revision, deadline.Token);
        var compiled = await PaintAsync(state.Id, "first", Line(100, 100, 2), false, deadline.Token);
        await EvaluatedAsync(socket, compiled.Revision, deadline.Token);

        List<double> times = [];
        var repaired = 0;
        for (var stamp = 0; stamp < 120; stamp++) {
            var clock = Stopwatch.GetTimestamp();
            var sent = await PaintAsync(state.Id, "drag", [new(100f + stamp * 6f, 300f)], true, deadline.Token);
            var evaluation = await EvaluatedAsync(socket, sent.Revision, deadline.Token);
            foreach (var preview in evaluation.Previews) {
                _ = await http.GetByteArrayAsync($"/api/v1/previews/{preview.Hash}", deadline.Token);
            }

            repaired = evaluation.Repaired;
            if (stamp > 10) {
                times.Add(Stopwatch.GetElapsedTime(clock).TotalMilliseconds);
            }
        }

        times.Sort();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"S4 end to end, {size}², 20 layers: p50 {times[times.Count / 2]:F1} ms, p95 "
            + $"{times[times.Count * 95 / 100]:F1} ms, {repaired} ops repaired, Worker {worker.Id}"
        );
        Assert.True(repaired > 0);
    }

    /// <summary>A stack that saved comes back as the same picture, from the files alone.</summary>
    [Fact]
    public async Task A_saved_stack_reads_back_as_the_same_picture() {
        using var deadline = Deadline(180);
        var (instance, state) = await StartAsync("Saved.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var painted = await PaintAsync(state.Id, "a", Line(50, 60, 20), false, deadline.Token);
        await EvaluatedAsync(socket, painted.Revision, deadline.Token);
        var before = await PixelsAsync(state.Id, false, deadline.Token);

        using (var saved = await http.PostAsync($"/api/v1/stacks/{state.Id}/save", null, deadline.Token)) {
            saved.EnsureSuccessStatusCode();
        }

        Assert.False((await StateAsync(state.Id, deadline.Token)).History.Dirty);
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(state.Path)!, "*.vxpaint"));
        using (var closed = await http.DeleteAsync($"/api/v1/stacks/{state.Id}", deadline.Token)) {
            closed.EnsureSuccessStatusCode();
        }

        var again = await OpenAsync(state.Path, deadline.Token);
        Assert.NotEqual(state.Id, again.Id);
        Assert.Equal(
            Digest(before, "baseColor"),
            Digest(await PixelsAsync(again.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     A long session is bounded by checkpoints, and a checkpoint is not allowed to take undo away: a new Worker
    ///     asked
    ///     to go behind one starts from further back.
    /// </summary>
    [Fact]
    public async Task A_worker_lost_after_a_checkpoint_can_still_undo_past_it() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Long.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        var blank = await PixelsAsync(state.Id, false, deadline.Token);

        // More stamps than a checkpoint allows, in strokes.
        StackEditResult last = null!;
        for (var stroke = 0; stroke < 5; stroke++) {
            last = await PaintAsync(state.Id, $"s{stroke}", Line(20, 100 + stroke * 40, 500), false, deadline.Token);
        }

        await EvaluatedAsync(socket, last.Revision, deadline.Token);
        await PixelsAsync(state.Id, false, deadline.Token);
        Assert.NotEmpty(Directory.GetDirectories(Path.Combine(Library, ".ptcache", "paint", state.Id)));

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        for (var stroke = 0; stroke < 5; stroke++) {
            await ActAsync(state.Id, "undo", deadline.Token);
        }

        Assert.Equal(
            Digest(blank, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     A first stroke makes a file, and a save writes it. A new Worker replaying that stroke must not start from
    ///     the
    ///     file, or the stroke lands twice and its undo reaches a picture nobody painted.
    /// </summary>
    [Fact]
    public async Task A_worker_lost_after_a_save_undoes_to_before_the_file_existed() {
        using var deadline = Deadline(240);
        var (instance, state) = await StartAsync("Fresh.vxlayers", deadline.Token, out var worker);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        await EvaluatedAsync(socket, state.Revision, deadline.Token);
        var blank = await PixelsAsync(state.Id, false, deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("soft", "paint", "baseColor", new(30f, 0.3f), Red, Line(100, 100, 40))
        );
        var painted = await PixelsAsync(state.Id, false, deadline.Token);
        using (var saved = await http.PostAsync($"/api/v1/stacks/{state.Id}/save", null, deadline.Token)) {
            saved.EnsureSuccessStatusCode();
        }

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        Assert.Equal(
            Digest(painted, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
        await ActAsync(state.Id, "undo", deadline.Token);
        Assert.Equal(
            Digest(blank, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
    }

    async Task SameAsExportAsync(StackState state, string when, CancellationToken token) {
        var exported = await ExportAsync(state.Id, Path.Combine(folder.FullName, when), token);
        var sampled = await SampledAsync(state.Id, token);

        // The viewport hears which stack it wears on its own loop, so on a slow machine it can still be on its way.
        while (sampled.Count < exported.Count) {
            await Task.Delay(50, token);
            sampled = await SampledAsync(state.Id, token);
        }

        foreach (var map in exported) {
            var file = PngCodec.Decode(File.ReadAllBytes(map.File!));
            Assert.Equal(map.Digest, Convert.ToHexStringLower(SHA256.HashData(file.Pixels)));
            Assert.Equal(map.Digest, Digest(sampled, map.Usage));
        }

        Assert.Equal(exported.Count, sampled.Count);
    }

    async Task BindMeshAsync(string id, CancellationToken token) {
        var meshes = Directory.CreateDirectory(Path.Combine(folder.FullName, "meshes")).FullName;
        if (!File.Exists(Path.Combine(meshes, BakeFixture.LowFile))) {
            BakeFixture.Write(meshes, 0.05f);
        }

        await EditAsync(id, token, new StackSetModel(Path.Combine(meshes, BakeFixture.LowFile)));
    }

    /// <summary>Puts a stack on the viewport and holds a socket open for frames, which is what makes it draw.</summary>
    async Task<IDisposable> WearAsync(ServerInstance instance, string id, CancellationToken token) {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/viewport?access_token={instance.Token}"),
            token
        );
        var drain = new byte[1 << 20];
        var draining = Task.Run(
            async () => {
                while (socket.State == WebSocketState.Open) {
                    await socket.ReceiveAsync(drain, token);
                }
            },
            token
        );
        using var response = await http.PutAsJsonAsync(
            "/api/v1/viewport/stack",
            new(id),
            ContractsJsonContext.Default.ViewportStackRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return new Drained(socket, draining);
    }

    async Task<StackView> AimAsync(double x, double y, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/viewport/pick",
            new(x, y, null, Picture, Picture),
            ContractsJsonContext.Default.ViewportPickRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        var picked = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ViewportPick, token))!;
        Assert.True(picked.Found, "The middle of the picture should be on the housing.");
        return picked.View;
    }

    /// <summary>A stroke straight across the picture, over whatever flats and seams are under it.</summary>
    static StackStroke Across(string stroke, StackView view, int from, int to, int row, bool open) =>
        new(
            stroke,
            "paint",
            "baseColor",
            new(30f, Spacing: 0.25f),
            Red,
            [..Enumerable.Range(0, 8).Select(step => new StackStamp(from + (to - from) * step / 7f, row, 1f))],
            open,
            View: view
        );

    async Task<IReadOnlyList<StackPixels>> SampledAsync(string id, CancellationToken token) {
        using var response = await http.GetAsync($"/api/v1/stacks/{id}/pixels?sampled=true", token);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.IReadOnlyListStackPixels,
            token
        ))!;
    }

    sealed class Drained(ClientWebSocket socket, Task draining) : IDisposable {
        public void Dispose() {
            socket.Abort();
            socket.Dispose();
            _ = draining.ContinueWith(static _ => { }, TaskScheduler.Default);
        }
    }

    static string Digest(IReadOnlyList<StackPixels> maps, string usage) =>
        maps.Single(m => string.Equals(m.Usage, usage, StringComparison.Ordinal)).Digest;

    static IReadOnlyList<StackStamp> Line(float x, float y, int count) =>
        [..Enumerable.Range(0, count).Select(index => new StackStamp(x + index * 6f, y + index % 3))];

    static CancellationTokenSource Deadline(int seconds) {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return deadline;
    }

    Process Start(string dll, params string[] arguments) {
        var info = new ProcessStartInfo("dotnet") {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add(dll);
        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        var process = Process.Start(info)!;
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        started.Add(process);
        return process;
    }

    Process StartWorker() =>
        Start(Engine.Dll("Patina.Worker", "patina-worker"), "--socket", socketPath, "--watch-stdin");

    async Task<(ServerInstance, StackState)> StartAsync(string name, CancellationToken token) {
        var (instance, state, _) = await BootAsync(name, token);
        return (instance, state);
    }

    Task<(ServerInstance, StackState)> StartAsync(string name, CancellationToken token, out Process worker) {
        worker = StartWorker();
        return AttachAsync(name, token);
    }

    async Task<(ServerInstance, StackState, Process)> BootAsync(string name, CancellationToken token) {
        var worker = StartWorker();
        var (instance, state) = await AttachAsync(name, token);
        return (instance, state, worker);
    }

    async Task<(ServerInstance, StackState)> AttachAsync(string name, CancellationToken token, int size = Side) {
        Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Library,
            $"--Patina:DiscoveryFile={DiscoveryFile}",
            $"--Patina:Worker:SocketPath={socketPath}",
            "--Patina:WatchStdin=true"
        );
        var registry = new InstanceRegistry(DiscoveryFile);
        while (true) {
            if (await registry.FindAsync(Library, token) is { } instance) {
                http.BaseAddress = new($"http://127.0.0.1:{instance.Port}");
                http.DefaultRequestHeaders.Authorization = new("Bearer", instance.Token);
                return (instance, await CreateAsync(name, token, size));
            }

            await Task.Delay(50, token);
        }
    }

    async Task<StackState> CreateAsync(string name, CancellationToken token, int size = Side) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/stacks/new",
            new(name, size, size),
            ContractsJsonContext.Default.StackCreateRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackState, token))!;
    }

    async Task<StackState> OpenAsync(string path, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/stacks",
            new(path),
            ContractsJsonContext.Default.StackOpenRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackState, token))!;
    }

    async Task<StackState> StateAsync(string id, CancellationToken token) =>
        (await http.GetFromJsonAsync($"/api/v1/stacks/{id}", ContractsJsonContext.Default.StackState, token))!;

    Task<StackEditResult> PaintAsync(
        string id,
        string stroke,
        IReadOnlyList<StackStamp> stamps,
        bool open,
        CancellationToken token
    ) =>
        EditAsync(
            id,
            token,
            new StackStroke(stroke, "paint", "baseColor", new(24f, Spacing: 0.25f), Red, stamps, open)
        );

    async Task<StackEditResult> EditAsync(string id, CancellationToken token, params StackCommand[] commands) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/commands",
            new(commands),
            ContractsJsonContext.Default.StackCommandBatch,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackEditResult, token))!;
    }

    async Task<StackEditResult> ActAsync(string id, string action, CancellationToken token) {
        using var response = await http.PostAsync($"/api/v1/stacks/{id}/{action}", null, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackEditResult, token))!;
    }

    async Task<IReadOnlyList<StackPixels>> PixelsAsync(string id, bool clean, CancellationToken token) {
        using var response = await http.GetAsync($"/api/v1/stacks/{id}/pixels?clean={clean}", token);
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity) {
            // Only a machine with no device is a reason to skip: every other refusal is a failure to see.
            var refusal = await response.Content.ReadAsStringAsync(token);
            Gpu.Skip(refusal);
        }

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.IReadOnlyListStackPixels,
            token
        ))!;
    }

    async Task<IReadOnlyList<StackPixels>> ExportAsync(string id, string to, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/export",
            new(to),
            ContractsJsonContext.Default.StackExportRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.IReadOnlyListStackPixels,
            token
        ))!;
    }

    static async Task<ClientWebSocket> FollowAsync(ServerInstance instance, StackState state, CancellationToken token) {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(
            new($"ws://127.0.0.1:{instance.Port}/ws/stacks/{state.Id}?since=0&access_token={instance.Token}"),
            token
        );
        return socket;
    }

    static async Task<StackEvaluation> EvaluatedAsync(ClientWebSocket socket, long revision, CancellationToken token) {
        var buffer = new byte[1024 * 1024];
        while (true) {
            var length = 0;
            ValueWebSocketReceiveResult part;
            do {
                part = await socket.ReceiveAsync(buffer.AsMemory(length), token);
                length += part.Count;
            } while (!part.EndOfMessage);

            var item = JsonSerializer.Deserialize(buffer.AsSpan(0, length), ContractsJsonContext.Default.StackEvent)!;
            if (item.Evaluation is not { } evaluation || evaluation.Revision < revision) {
                continue;
            }

            if (evaluation.Error is { } error) {
                if (error.StartsWith("No Vulkan", StringComparison.Ordinal)) {
                    Gpu.Skip(error);
                }

                Assert.Fail(error);
            }

            return evaluation;
        }
    }
}
