using Patina.Contracts;
using System.Net;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     Masks under a brush, and the brush itself: what a first stroke starts from, what a removed mask leaves, and what
///     a half set layer still lets through.
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A mask belongs to any layer, so a fill, a filter and a folder each take a brush on it, while their own
    ///     content still refuses one: only a paint layer has pixels of its own.
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
        Assert.Equal(
            Digest(before.Maps, "baseColor"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
        );
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

    /// <summary>
    ///     What a mask started as is in the journal, so a new Worker fills the canvas with the same thing.
    /// </summary>
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

        Assert.Equal(
            Digest(painted, "baseColor"),
            Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor")
        );
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

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(fill, Mask: new("None", Effects: [new("Colour/Invert")]))
        );
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

    /// <summary>
    ///     A brush on a mask that is a picture paints over it and never replaces it (#72): the source stays, live, and
    ///     a paint layer on top of the mask's own stack takes the strokes, the next stroke as well as the first.
    ///     Undone, the mask is as it was.
    /// </summary>
    [Theory]
    [InlineData("Generator")]
    [InlineData("Texture")]
    [InlineData("Bake")]
    [InlineData("Anchor")]
    public async Task A_brush_paints_over_a_mask_that_is_a_picture(string source) {
        using var deadline = Deadline(120);
        var (_, state) = await StartAsync("Picture.vxlayers", deadline.Token);
        var fill = await MaskedFillAsync(
            state.Id,
            new(
                source,
                Asset: "any.png",
                Anchor: new("Paint", "baseColor"),
                Generator: "Generators/Dirt",
                Map: "curvature"
            ),
            deadline.Token
        );

        await EditAsync(state.Id, deadline.Token, MaskStroke("first", fill, 300f, 300f, [0f, 0f, 0f, 1f]));
        await EditAsync(state.Id, deadline.Token, MaskStroke("second", fill, 600f, 600f, [1f, 1f, 1f, 1f]));

        var mask = Layer(await StateAsync(state.Id, deadline.Token), fill).Mask;
        Assert.Equal(source, mask.Source);
        var entry = Assert.Single(mask.Layers!);
        Assert.Equal(("Paint", true), (entry.Source, entry.Painted));

        await ActAsync(state.Id, "undo", deadline.Token);
        await ActAsync(state.Id, "undo", deadline.Token);
        mask = Layer(await StateAsync(state.Id, deadline.Token), fill).Mask;
        Assert.Equal(source, mask.Source);
        Assert.Empty(mask.Layers!);
    }

    /// <summary>
    ///     Over a picture, a stroke changes only what it touches: elsewhere the picture still masks the layer, and
    ///     where the brush laid black the layer is as hidden as a mask of nothing would hide it.
    /// </summary>
    [Fact]
    public async Task A_stroke_over_a_picture_mask_changes_only_what_it_touches() {
        using var deadline = Deadline(180);
        var (_, state) = await StartAsync("OverPicture.vxlayers", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer("paint", AnchorPoint: "Paint"));
        await PaintAsync(state.Id, "picture", Line(250, 280, 40), false, deadline.Token);
        var fill = await MaskedFillAsync(state.Id, new("Constant", 0f), deadline.Token);
        var hidden = await TexelsAsync(state.Id, "hidden", deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(fill, Mask: new("Anchor", Anchor: new("Paint", "baseColor")))
        );
        var before = await TexelsAsync(state.Id, "before", deadline.Token);
        Assert.NotEqual(Texel(hidden, 800, 800), Texel(before, 800, 800));

        await EditAsync(state.Id, deadline.Token, MaskStroke("hole", fill, 300f, 300f, [0f, 0f, 0f, 1f]));

        var after = await TexelsAsync(state.Id, "after", deadline.Token);
        Near(Texel(before, 800, 800), Texel(after, 800, 800));
        Near(Texel(hidden, 350, 300), Texel(after, 350, 300));
        Assert.NotEqual(Texel(before, 350, 300), Texel(after, 350, 300));
        Assert.Equal("Anchor", Layer(await StateAsync(state.Id, deadline.Token), fill).Mask.Source);

        await ActAsync(state.Id, "undo", deadline.Token);
        Assert.Equal(
            Digest(before.Maps, "baseColor"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
        );
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
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer(
                "fill",
                "Rough",
                new Dictionary<string, IReadOnlyList<float>> { ["roughness"] = [0.8f, 0.8f, 0.8f, 1f] }
            )
        );
        var fill = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var before = await PixelsAsync(state.Id, true, deadline.Token);

        // No layer publishes an anchor point named "paint", so an anchor onto it has nothing to read.
        var set = await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(fill, Mask: new(source, Anchor: anchor.Length > 0 ? new(anchor, "roughness") : null))
        );

        var evaluation = await EvaluatedAsync(socket, set.Revision, deadline.Token);
        var problem = Assert.Single(evaluation.Problems, problem => problem.Layer == fill);
        Assert.Equal("Warning", problem.Severity);
        Assert.Equal(
            Digest(before, "roughness"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "roughness")
        );

        var painted = await PaintAsync(state.Id, "still", Line(100, 100, 12), false, deadline.Token);
        Assert.NotEmpty((await EvaluatedAsync(socket, painted.Revision, deadline.Token)).Previews);
        Assert.NotEqual(
            Digest(before, "baseColor"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
        );
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
        Assert.Equal(
            Digest(before, "baseColor"),
            Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     What is wrong rather than unfinished still stops the stack, and now says so against its layer.
    /// </summary>
    [Fact]
    public async Task A_stack_that_cannot_compile_says_which_layer_stopped_it() {
        using var deadline = Deadline(120);
        var (instance, state) = await StartAsync("Loop.vxlayers", deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Top", Tint));
        var top = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;

        // Under the layer it reads, so its own result is part of what it masks by.
        var set = await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(top, AnchorPoint: "Top"),
            new StackSetLayer("base", Mask: new("Anchor", Anchor: new("Top", "baseColor")))
        );

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

    /// <summary>
    ///     Spacing means what it says: far enough apart, stamps stop touching and a row of them is a pattern.
    /// </summary>
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
            new StackStroke(
                "hard",
                "paint",
                "baseColor",
                brush,
                Red,
                [new(100f, 200f, 1f), new(700f, 200f, 1f)],
                Curve: curve
            ),
            new StackStroke(
                "light",
                "paint",
                "roughness",
                brush,
                Red,
                [new(100f, 200f, 0.1f), new(700f, 200f, 0.1f)],
                Curve: curve
            )
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
    async Task<(IReadOnlyList<StackPixels> Maps, Bitmap BaseColor)> TexelsAsync(
        string id,
        string name,
        CancellationToken token
    ) {
        var maps = await ExportAsync(id, Path.Combine(folder.FullName, $"{id}-{name}"), token);
        return (maps, PngCodec.Decode(File.ReadAllBytes(maps.Single(static map => map.Usage == "baseColor").File!)));
    }

    static byte[] Texel((IReadOnlyList<StackPixels> Maps, Bitmap BaseColor) picture, int x, int y) =>
        [..picture.BaseColor.Pixels.AsSpan((y * picture.BaseColor.Width + x) * 4, 4)];

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
}
