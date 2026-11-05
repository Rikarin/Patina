using Vixen.Core.Mathematics;

namespace Patina.Viewport.Tests;

/// <summary>
///     The renderer on a real device, judged by its pixels: a test that only asks whether a frame came back would pass
///     on a black frame, as this renderer's first draft did. Skipped where there is no Vulkan.
/// </summary>
public sealed class ViewportRendererTests : IDisposable {
    const int Width = 320;
    const int Height = 200;

    static readonly string Fixture = Path.Combine(Root(), "content", "fixtures", "viewport-test.glb");

    readonly DirectoryInfo cache = Directory.CreateTempSubdirectory("patina-render-");

    public void Dispose() => cache.Delete(true);

    static string Root() {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "skala.jsonc"))) {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new DirectoryNotFoundException("No repository above the test output.");
    }

    ViewportRenderer Open() {
        if (!ViewportRenderer.TryCreate(new(cache.FullName), out var renderer, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
        }

        return renderer!;
    }

    static byte[] Render(ViewportRenderer renderer, OrbitCamera camera) {
        var pixels = new byte[ViewportRenderer.FrameBytes(Width, Height)];
        renderer.Render(camera, Width, Height, pixels);
        return pixels;
    }

    static (byte R, byte G, byte B, byte A) At(byte[] pixels, int x, int y) {
        var at = (y * Width + x) * 4;
        return (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]);
    }

    [Fact]
    public void The_fixture_comes_out_lit_over_the_sky() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);

        var pixels = Render(renderer, OrbitCamera.Default);

        // Every pixel is opaque, and the sky in the corner is a mid grey rather than the clear colour.
        Assert.All(Enumerable.Range(0, Width * Height), i => Assert.Equal(255, pixels[i * 4 + 3]));
        var corner = At(pixels, 2, 2);
        Assert.InRange((int)corner.R, 30, 220);

        // Gold is a warm pixel: more red than blue by a wide margin, which grey sky never is. The sphere is the only
        // saturated red.
        var gold = Enumerable.Range(0, Width * Height)
            .Count(i => pixels[i * 4] > pixels[i * 4 + 2] + 40 && pixels[i * 4 + 1] > 80);
        var red = Enumerable.Range(0, Width * Height).Count(i => pixels[i * 4] > 150 && pixels[i * 4 + 1] < 110);
        Assert.InRange(gold, Width * Height / 50, Width * Height / 2);
        Assert.InRange(red, 20, Width * Height / 5);
    }

    [Fact]
    public void An_orbit_moves_the_picture() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);

        var before = Render(renderer, OrbitCamera.Default);
        var same = Render(renderer, OrbitCamera.Default);
        var after = Render(renderer, OrbitCamera.Default with { Yaw = OrbitCamera.Default.Yaw + 1f });

        // One level of rounding comes and goes under load on the same input, so the same picture means within that.
        Assert.Equal(before.Length, same.Length);
        Assert.True(before.Zip(same).All(static pair => Math.Abs(pair.First - pair.Second) <= 1));
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Rendering_with_no_mesh_is_refused() {
        using var renderer = Open();

        Assert.False(renderer.HasMesh);
        Assert.Throws<InvalidOperationException>(() => Render(renderer, OrbitCamera.Default));
    }

    [Fact]
    public void The_eye_sits_at_the_distance_asked_for() {
        var bounds = new BoundingBox(new(-1f, -2f, 0f), new(1f, 2f, 4f));

        var eye = OrbitCamera.Default.Eye(bounds);

        Assert.Equal(OrbitCamera.Default.Distance * bounds.Extent.Length(), (eye - bounds.Center).Length(), 3);
    }
}
