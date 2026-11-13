using Patina.Contracts;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>A lazy mouse, through a real Worker: a distance the brush trails by, whatever the pointer's rate.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A lazy brush paints less of a drag than one that follows it, paints the same however the drag was split into
    ///     parts, and leaves a line alone: a line has to reach its last point.
    /// </summary>
    [Fact]
    public async Task A_lazy_brush_trails_by_a_distance_whatever_the_parts() {
        using var deadline = Deadline(240);
        var (_, plain) = await StartAsync("Plain.vxlayers", deadline.Token);
        var whole = await CreateAsync("Whole.vxlayers", deadline.Token);
        var parts = await CreateAsync("Parts.vxlayers", deadline.Token);
        StackBrush lazy = new(16f, Spacing: 0.25f, Lazy: 60f);
        StackBrush follows = new(16f, Spacing: 0.25f);
        var drag = Line(100, 300, 60);

        await EditAsync(plain.Id, deadline.Token, new StackStroke("a", "paint", "baseColor", follows, Red, drag));
        await EditAsync(whole.Id, deadline.Token, new StackStroke("a", "paint", "baseColor", lazy, Red, drag));
        for (var at = 0; at < drag.Count; at += 7) {
            await EditAsync(
                parts.Id,
                deadline.Token,
                new StackStroke(
                    "a",
                    "paint",
                    "baseColor",
                    lazy,
                    Red,
                    [..drag.Skip(at).Take(7)],
                    at + 7 < drag.Count
                )
            );
        }

        var trailed = Digest(await PixelsAsync(whole.Id, false, deadline.Token), "baseColor");
        Assert.NotEqual(Digest(await PixelsAsync(plain.Id, false, deadline.Token), "baseColor"), trailed);
        Assert.Equal(trailed, Digest(await PixelsAsync(parts.Id, false, deadline.Token), "baseColor"));

        var straight = await CreateAsync("Straight.vxlayers", deadline.Token);
        var lazyLine = await CreateAsync("LazyLine.vxlayers", deadline.Token);
        IReadOnlyList<StackStamp> ends = [new(100f, 600f), new(700f, 640f)];
        await EditAsync(
            straight.Id,
            deadline.Token,
            new StackStroke("l", "paint", "baseColor", follows, Red, ends, Curve: "line")
        );
        await EditAsync(
            lazyLine.Id,
            deadline.Token,
            new StackStroke("l", "paint", "baseColor", lazy, Red, ends, Curve: "line")
        );
        Assert.Equal(
            Digest(await PixelsAsync(straight.Id, false, deadline.Token), "baseColor"),
            Digest(await PixelsAsync(lazyLine.Id, false, deadline.Token), "baseColor")
        );
    }

    /// <summary>
    ///     A pen turns and widens a brush that asks it to (#123): a stretched stamp under a barrel turned a quarter
    ///     stands up instead of lying down, and a pen laid flat paints wider than one held straight.
    /// </summary>
    [Fact]
    public async Task A_pen_turns_and_widens_the_brush_that_follows_it() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Pen.vxlayers", deadline.Token);
        StackBrush chisel = new(24f, Falloff: 0f, Rotation: "pen", Aspect: 8f);
        StackBrush pencil = new(24f, Falloff: 0f, TiltSize: 0.75f);
        const float Quarter = MathF.PI / 2f;
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("lying", "paint", "baseColor", chisel, Red, [new(400f, 300f)]),
            new StackStroke("standing", "paint", "roughness", chisel, Red, [new(400f, 300f, Twist: Quarter)])
        );
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke("flat", "paint", "baseColor", pencil, Red, [new(400f, 700f, Tilt: 1f)]),
            new StackStroke("upright", "paint", "roughness", pencil, Red, [new(400f, 700f)])
        );
        var maps = await ExportAsync(state.Id, Path.Combine(folder.FullName, "pen"), deadline.Token);
        var (lying, standing) = (Extent(maps, "baseColor", 300), Extent(maps, "roughness", 300));
        var (flat, upright) = (Extent(maps, "baseColor", 700), Extent(maps, "roughness", 700));

        Assert.True(lying.Wide > lying.High * 4, $"Unturned, the chisel is {lying.Wide}×{lying.High}.");
        Assert.True(standing.High > standing.Wide * 4, $"Turned a quarter, it is {standing.Wide}×{standing.High}.");
        Assert.True(flat.Wide > upright.Wide * 3, $"Laid flat it is {flat.Wide} wide, and upright {upright.Wide}.");

        // How wide and tall the paint is through a place, along its row and its column.
        static (int Wide, int High) Extent(IReadOnlyList<StackPixels> maps, string usage, int at) {
            var map = PngCodec.Decode(File.ReadAllBytes(maps.Single(map => map.Usage == usage).File!));
            var blank = map.Pixels.AsSpan(0, 4).ToArray();
            return (
                Enumerable.Range(0, map.Width).Count(x => Painted(x, at)),
                Enumerable.Range(at - 200, 400).Count(y => Painted(400, y))
            );

            bool Painted(int x, int y) => !map.Pixels.AsSpan((y * map.Width + x) * 4, 4).SequenceEqual(blank);
        }
    }

    /// <summary>
    ///     A brush's picture is a stroke of it drawn small (#123): the same brush draws the same picture, another shape
    ///     another one, its size does not change it, and a brush no stroke could take is refused with its reason. One
    ///     stamp of it is drawn alone for a cursor.
    /// </summary>
    [Fact]
    public async Task A_brush_preview_draws_a_stroke_of_the_brush_at_a_size_of_its_own() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Preview.vxlayers", deadline.Token);

        async Task<Bitmap> DrawnAsync(StackBrush brush, bool stamp = false) {
            using var response = await http.PostAsJsonAsync(
                $"/api/v1/stacks/{state.Id}/brush-preview{(stamp ? "?stamp=true" : "")}",
                brush,
                ContractsJsonContext.Default.StackBrush,
                deadline.Token
            );
            await SucceededAsync(response, deadline.Token);
            var hash = (await response.Content.ReadFromJsonAsync(
                ContractsJsonContext.Default.StackBrushPreview,
                deadline.Token
            ))!.Hash;
            return PngCodec.Decode(await http.GetByteArrayAsync($"/api/v1/previews/{hash}", deadline.Token));
        }

        static int Painted(Bitmap picture) =>
            Enumerable.Range(0, picture.Width * picture.Height).Count(at => picture.Pixels[at * 4 + 3] > 0);

        var round = await DrawnAsync(StackBrushes.Default);
        var again = await DrawnAsync(StackBrushes.Default with { Radius = 400f });
        var chisel = await DrawnAsync(StackBrushes.Default with { Alpha = "Chisel", Spacing = 1.5f });

        Assert.Equal((256, 64), (round.Width, round.Height));
        Assert.InRange(Painted(round), 2000, 256 * 64 - 2000);
        Assert.Equal(round.Pixels, again.Pixels);
        Assert.NotEqual(round.Pixels, chisel.Pixels);

        // One stamp, as a cursor shows it (#137): the round one fills the disc inside its square, a stretched one is as
        // long and narrower, and nothing random moves it.
        var dab = await DrawnAsync(StackBrushes.Default with { Falloff = 0f }, true);
        var jittered = await DrawnAsync(StackBrushes.Default with { Falloff = 0f, PositionJitter = 1f }, true);
        var stretched = await DrawnAsync(StackBrushes.Default with { Falloff = 0f, Aspect = 4f }, true);
        Assert.Equal((96, 96), (dab.Width, dab.Height));
        Assert.InRange(Painted(dab), Math.PI * 48 * 48 * 0.9, Math.PI * 48 * 48 * 1.05);
        Assert.Equal(dab.Pixels, jittered.Pixels);
        Assert.InRange(Painted(stretched), Painted(dab) * 0.2, Painted(dab) * 0.3);

        using var refused = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{state.Id}/brush-preview",
            new(0f),
            ContractsJsonContext.Default.StackBrush,
            deadline.Token
        );
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(Api.Refusals.BrushRefused, await refused.Content.ReadAsStringAsync(deadline.Token));
    }

    /// <summary>
    ///     A stroke that carries the last one on starts where the brush stood: a lazy one put down within its string of
    ///     there paints nothing until it is pulled past it, where a fresh stroke lays a stamp under the pointer.
    ///     Undone, it takes nothing of the stroke before it.
    /// </summary>
    [Fact]
    public async Task A_stroke_carried_on_starts_where_the_brush_stood() {
        using var deadline = Deadline(240);
        var (_, first) = await StartAsync("First.vxlayers", deadline.Token);
        var carried = await CreateAsync("Carried.vxlayers", deadline.Token);
        var fresh = await CreateAsync("Fresh.vxlayers", deadline.Token);
        StackBrush lazy = new(16f, Spacing: 0.25f, Lazy: 60f, Resume: true);
        var drag = Line(100, 300, 40);
        var stood = new StackStamp(300f, 300f);
        IReadOnlyList<StackStamp> within = [new(320f, 310f), new(330f, 300f)];

        foreach (var stack in new[] { first.Id, carried.Id, fresh.Id }) {
            await EditAsync(stack, deadline.Token, new StackStroke("a", "paint", "baseColor", lazy, Red, drag));
        }

        await EditAsync(
            carried.Id,
            deadline.Token,
            new StackStroke("b", "paint", "baseColor", lazy, Red, within, Resume: [stood.X, stood.Y])
        );
        await EditAsync(fresh.Id, deadline.Token, new StackStroke("b", "paint", "baseColor", lazy, Red, within));

        var before = Digest(await PixelsAsync(first.Id, false, deadline.Token), "baseColor");
        Assert.Equal(before, Digest(await PixelsAsync(carried.Id, false, deadline.Token), "baseColor"));
        Assert.NotEqual(before, Digest(await PixelsAsync(fresh.Id, false, deadline.Token), "baseColor"));

        // Pulled past the string, it paints on from where it stood.
        await EditAsync(
            carried.Id,
            deadline.Token,
            new StackStroke("c", "paint", "baseColor", lazy, Red, [new(500f, 300f)], Resume: [stood.X, stood.Y])
        );
        Assert.NotEqual(before, Digest(await PixelsAsync(carried.Id, false, deadline.Token), "baseColor"));
    }
}
