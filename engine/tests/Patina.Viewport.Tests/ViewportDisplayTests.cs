using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Viewport.Tests;

/// <summary>
///     The display settings, judged by the numbers in the frames they make: framing, the views of stored values, the
///     curves, the wireframe and the post effects. Skipped where there is no Vulkan.
/// </summary>
public sealed class ViewportDisplayTests : IDisposable {
    const int Map = 64;

    static readonly string Fixture = Path.Combine(Root(), "content", "fixtures", "viewport-test.glb");

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-display-");

    public void Dispose() => folder.Delete(true);

    string In(string name) => Path.Combine(folder.FullName, name);

    static string Root() {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "skala.jsonc"))) {
            at = at.Parent;
        }

        return at?.FullName ?? throw new DirectoryNotFoundException("No repository above the test output.");
    }

    ViewportRenderer Open() {
        if (!ViewportRenderer.TryCreate(new(In("shaders")), out var renderer, out var refusal)) {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATINA_REQUIRE_VULKAN"))) {
                Assert.Fail($"Vulkan is required here: {refusal}");
            }

            Assert.Skip($"No Vulkan device: {refusal}");
        }

        return renderer!;
    }

    static byte[] Render(ViewportRenderer renderer, OrbitCamera camera, int width, int height) {
        var pixels = new byte[ViewportRenderer.FrameBytes(width, height)];
        renderer.Render(camera, width, height, pixels);
        return pixels;
    }

    static (int R, int G, int B) At(byte[] pixels, int width, int x, int y) {
        var at = (y * width + x) * 4;
        return (pixels[at], pixels[at + 1], pixels[at + 2]);
    }

    /// <summary>A square facing the camera at yaw zero, filling the frame, with tangents for a normal map.</summary>
    string Square() {
        var path = In("square.glb");
        ModelWriter.Write(
            path,
            [
                new MeshData {
                    Name = "square",
                    Positions = [new(-1f, -1f, 0f), new(1f, -1f, 0f), new(1f, 1f, 0f), new(-1f, 1f, 0f)],
                    Normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
                    TexCoords = [new(0f, 1f), new(1f, 1f), new(1f, 0f), new(0f, 0f)],
                    Indices = [0, 1, 2, 0, 2, 3]
                }
            ],
            "square"
        );
        return path;
    }

    static byte[] Solid(byte r, byte g, byte b) {
        var pixels = new byte[Map * Map * 4];
        for (var at = 0; at < pixels.Length; at += 4) {
            (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (r, g, b, 255);
        }

        return pixels;
    }

    /// <summary>
    ///     The model is whole in any shape of pane: with no background, every pixel on the frame's border is black. The
    ///     opening camera used to sit 2.2 bounding radii away whatever the pane, which cut the fixture's sides off in a
    ///     tall one.
    /// </summary>
    [Theory]
    [InlineData(320, 180)]
    [InlineData(180, 360)]
    [InlineData(120, 400)]
    public void The_opening_view_shows_the_whole_model_in_any_shape_of_pane(int width, int height) {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        renderer.Shading = new() { BackgroundOpacity = 0f };

        var pixels = Render(renderer, OrbitCamera.Default, width, height);

        var border = Enumerable.Range(0, width)
            .SelectMany(x => new[] { (x, 0), (x, height - 1) })
            .Concat(Enumerable.Range(0, height).SelectMany(y => new[] { (0, y), (width - 1, y) }))
            .Count(spot => At(pixels, width, spot.Item1, spot.Item2) is var (r, g, b) && r + g + b > 6);
        var model = Enumerable.Range(0, width * height).Count(i => pixels[i * 4] + pixels[i * 4 + 1] > 40);
        Assert.Equal(0, border);
        Assert.InRange(model, width * height / 40, width * height);
    }

    /// <summary>
    ///     A view of a stored value shows the number in the file, whatever the exposure, the curve or the effects: a
    ///     frame pixel is a map pixel. Base colour and emission are colours and the rest are measurements, so this
    ///     holds the sRGB arithmetic of both.
    /// </summary>
    [Fact]
    public void A_view_of_a_stored_value_shows_the_number_in_the_file() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        renderer.SetLive(ViewportSlot.BaseColor, Map, Map, Solid(200, 90, 30));
        renderer.SetLive(ViewportSlot.Roughness, Map, Map, Solid(0, 77, 0));
        renderer.SetLive(ViewportSlot.Metallic, Map, Map, Solid(0, 0, 180));
        renderer.SetLive(ViewportSlot.Occlusion, Map, Map, Solid(140, 0, 0));
        renderer.SetLive(ViewportSlot.Emissive, Map, Map, Solid(10, 60, 250));
        renderer.SetLive(ViewportSlot.Normal, Map, Map, Solid(100, 150, 230));
        var picture = In("mesh-map.png");
        File.WriteAllBytes(picture, PngCodec.Encode(new(Map, Map, Solid(33, 166, 222))));
        renderer.SetPicture(new(picture, "mesh map"));
        var camera = new OrbitCamera(0f, 0f, 0.5f);
        var tinted = new ViewportShading {
            Exposure = 2f, ToneMapping = ViewportToneMapping.AgX, Vignette = 1f, Contrast = 1.4f
        };

        (ViewportView View, (int, int, int) Stored)[] cases = [
            (ViewportView.BaseColor, (200, 90, 30)),
            (ViewportView.Roughness, (77, 77, 77)),
            (ViewportView.Metallic, (180, 180, 180)),
            (ViewportView.Occlusion, (140, 140, 140)),
            (ViewportView.Emissive, (10, 60, 250)),
            (ViewportView.Normal, (100, 150, 230)),
            (ViewportView.Picture, (33, 166, 222)),
            (ViewportView.Unlit, (Added(200, 10), Added(90, 60), Added(30, 250)))
        ];
        foreach (var (view, (r, g, b)) in cases) {
            renderer.Shading = tinted with { View = view };
            var (shownR, shownG, shownB) = At(Render(renderer, camera, 64, 64), 64, 32, 32);
            Assert.True(
                Math.Abs(shownR - r) <= 1 && Math.Abs(shownG - g) <= 1 && Math.Abs(shownB - b) <= 1,
                $"{view} showed ({shownR}, {shownG}, {shownB}) for ({r}, {g}, {b})."
            );
        }
    }

    /// <summary>Two stored sRGB colours added as light, as an unlit view adds base colour and emission.</summary>
    static int Added(int one, int other) {
        var linear = Math.Min(Decoded(one) + Decoded(other), 1.0);
        return (int)Math.Round(
            255 * (linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055)
        );

        static double Decoded(int value) {
            var stored = value / 255.0;
            return stored <= 0.04045 ? stored / 12.92 : Math.Pow((stored + 0.055) / 1.055, 2.4);
        }
    }

    static long Brightness(byte[] pixels) {
        var sum = 0L;
        for (var at = 0; at < pixels.Length; at += 4) {
            sum += pixels[at] + pixels[at + 1] + pixels[at + 2];
        }

        return sum;
    }

    /// <summary>Clay is grey on every pixel of the model, whatever colour the material is.</summary>
    [Fact]
    public void Clay_shows_the_shape_in_grey() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        renderer.SetLive(ViewportSlot.BaseColor, Map, Map, Solid(220, 20, 20));

        renderer.Shading = new() { View = ViewportView.Clay, ToneMapping = ViewportToneMapping.None };
        var (r, g, b) = At(Render(renderer, new(0f, 0f, 0.5f), 64, 64), 64, 32, 32);
        renderer.Shading = new() { ToneMapping = ViewportToneMapping.None };
        var (litR, litG, _) = At(Render(renderer, new(0f, 0f, 0.5f), 64, 64), 64, 32, 32);

        Assert.InRange(Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)), 0, 12);
        Assert.InRange(litR - litG, 60, 255);
    }

    /// <summary>
    ///     Each of Vixen's curves draws its own picture of about the same brightness, a stop of exposure brightens,
    ///     and with no curve anything over white clips, which a curve exists to stop.
    /// </summary>
    [Fact]
    public void Each_curve_and_each_stop_of_exposure_draws_its_own_picture() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        var frames = Enum.GetValues<ViewportToneMapping>()
            .Select(curve => {
                    renderer.Shading = new() { ToneMapping = curve };
                    return Render(renderer, OrbitCamera.Default, 160, 100);
                }
            )
            .ToArray();
        for (var one = 0; one < frames.Length; one++) {
            for (var other = one + 1; other < frames.Length; other++) {
                Assert.NotEqual(frames[one], frames[other]);
            }
        }

        // Each is a picture of the model and not a black frame, which a curve given none of its constants draws.
        var reference = Brightness(frames[(int)ViewportToneMapping.Aces]);
        Assert.All(frames, frame => Assert.InRange(Brightness(frame), reference / 2, reference * 2));

        renderer.Shading = new() { Exposure = 1f };
        var brighter = Render(renderer, OrbitCamera.Default, 160, 100);
        var aces = frames[(int)ViewportToneMapping.Aces];
        Assert.True(Brightness(brighter) > Brightness(aces) * 1.1);

        renderer.Shading = new() { ToneMapping = ViewportToneMapping.None, Exposure = 2f };
        var clipped = Render(renderer, OrbitCamera.Default, 160, 100);
        renderer.Shading = new() { Exposure = 2f };
        var curved = Render(renderer, OrbitCamera.Default, 160, 100);
        Assert.True(Count(clipped, 255) > Count(curved, 255) * 2, "No curve clipped no more than ACES did.");

        static int Count(byte[] pixels, byte value) =>
            Enumerable.Range(0, pixels.Length / 4).Count(i => pixels[i * 4] == value);
    }

    /// <summary>
    ///     The wireframe draws the edges the eye can see and not those behind the model. A small square stands in
    ///     front of a large one whose diagonal runs the other way: the large one's diagonal shows outside the small one
    ///     and is hidden inside it, where the small one's own diagonal shows.
    /// </summary>
    [Fact]
    public void The_wireframe_draws_the_edges_in_sight_and_hides_the_rest() {
        using var renderer = Open();
        renderer.OpenMesh(Layered());
        var camera = new OrbitCamera(0f, 0f, 0.7f);
        renderer.Shading = new() { Shadows = 0f };
        var plain = Render(renderer, camera, 128, 128);
        renderer.Shading = new() { Shadows = 0f, Wireframe = true };
        var wired = Render(renderer, camera, 128, 128);

        int Darkened(IEnumerable<(int X, int Y)> spots) =>
            spots.Count(spot => At(wired, 128, spot.X, spot.Y).R + 10 < At(plain, 128, spot.X, spot.Y).R);

        // The back square's diagonal runs down the frame's x = y, the front square's across x + y = 127, and the front
        // square covers 32 to 96 each way. Pixels near the corners of the front square are left out, where edges meet.
        var behindOutside = Enumerable.Range(12, 14).Select(static t => (t, t)).ToArray();
        var behindInside = Enumerable.Range(40, 16).Select(static t => (t, t)).ToArray();
        var inFront = Enumerable.Range(40, 16).Select(static t => (t, 127 - t)).ToArray();
        Assert.InRange(Darkened(behindOutside), behindOutside.Length * 7 / 10, behindOutside.Length);
        Assert.InRange(Darkened(inFront), inFront.Length * 7 / 10, inFront.Length);
        Assert.Equal(0, Darkened(behindInside));
    }

    /// <summary>A unit square half a unit in front of one twice its size, split along the other diagonal.</summary>
    string Layered() {
        var path = In("layered.glb");
        ModelWriter.Write(
            path,
            [
                new MeshData {
                    Name = "layered",
                    Positions = [
                        new(-1f, -1f, 0.5f), new(1f, -1f, 0.5f), new(1f, 1f, 0.5f), new(-1f, 1f, 0.5f),
                        new(-2f, -2f, -0.5f), new(2f, -2f, -0.5f), new(2f, 2f, -0.5f), new(-2f, 2f, -0.5f)
                    ],
                    Normals = [..Enumerable.Repeat(Vector3.UnitZ, 8)],
                    TexCoords = [..Enumerable.Repeat(Vector2.Zero, 8)],
                    Indices = [0, 1, 2, 0, 2, 3, 4, 5, 7, 5, 6, 7]
                }
            ],
            "layered"
        );
        return path;
    }

    /// <summary>
    ///     The vignette darkens the corners and not the middle; grain, aberration and sharpening each change the frame;
    ///     and none of them runs when it is at zero.
    /// </summary>
    [Fact]
    public void Each_post_effect_changes_the_frame_and_none_runs_at_zero() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        renderer.Shading = new();
        var plain = Render(renderer, OrbitCamera.Default, 160, 100);
        renderer.Shading = new() { Vignette = 0f, Grain = 0f, Aberration = 0f, Sharpen = 0f };
        Assert.Equal(plain, Render(renderer, OrbitCamera.Default, 160, 100));

        renderer.Shading = new() { Vignette = 0.8f };
        var vignette = Render(renderer, OrbitCamera.Default, 160, 100);
        Assert.True(At(vignette, 160, 1, 1).R + 20 < At(plain, 160, 1, 1).R);
        Assert.InRange(At(vignette, 160, 80, 50).R - At(plain, 160, 80, 50).R, -2, 2);

        foreach (var shading in new ViewportShading[] {
                     new() { Grain = 0.5f }, new() { Aberration = 1f }, new() { Sharpen = 1f }
                 }) {
            renderer.Shading = shading;
            Assert.NotEqual(plain, Render(renderer, OrbitCamera.Default, 160, 100));
        }
    }
}
