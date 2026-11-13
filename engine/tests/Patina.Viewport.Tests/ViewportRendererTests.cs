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
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATINA_REQUIRE_VULKAN"))) {
                Assert.Fail($"Vulkan is required here: {refusal}");
            }

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
    public void The_model_casts_a_shadow_on_itself_that_moves_with_the_light() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);

        var camera = OrbitCamera.Default;
        renderer.Shading = new() { Shadows = 0f };
        var unshadowed = Render(renderer, camera);
        renderer.Shading = new();
        var shadowed = Render(renderer, camera);
        renderer.Shading = new() { LightRotation = 2f };
        var turned = Render(renderer, camera);

        // A shadow only ever takes light away, and turning the light moves it. The model is metal, which takes little
        // of its light from the sun, so a shadow is a small change on few pixels.
        var darker = Enumerable.Range(0, Width * Height).Count(i => shadowed[i * 4] + 2 < unshadowed[i * 4]);
        var brighter = Enumerable.Range(0, Width * Height).Count(i => shadowed[i * 4] > unshadowed[i * 4] + 2);
        Assert.InRange(darker, 20, Width * Height / 3);
        Assert.Equal(0, brighter);
        Assert.NotEqual(shadowed, turned);
    }

    [Fact]
    public void The_environment_turns_apart_from_the_light() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);

        var still = Render(renderer, OrbitCamera.Default with { Yaw = 0f });
        renderer.Shading = new() { EnvironmentRotation = 1.2f };
        var turned = Render(renderer, OrbitCamera.Default with { Yaw = 1.2f });
        renderer.Shading = new();
        var apart = Render(renderer, OrbitCamera.Default with { Yaw = 1.2f });
        renderer.Shading = new() { LightRotation = 1.2f };
        var lit = Render(renderer, OrbitCamera.Default with { Yaw = 1.2f });

        // Going round with the environment sees the same sky; going round without it sees another. Turning the light
        // moves its shading on the model and leaves the sky where it was.
        Assert.InRange(At(turned, 2, 2).R - At(still, 2, 2).R, -3, 3);
        Assert.InRange(At(turned, 2, 2).B - At(still, 2, 2).B, -3, 3);
        Assert.NotEqual(At(still, 2, 2), At(apart, 2, 2));
        Assert.Equal(At(apart, 2, 2), At(lit, 2, 2));
        Assert.NotEqual(apart, lit);
    }

    [Fact]
    public void A_room_or_a_panorama_changes_what_lights_the_model() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        var studio = Render(renderer, OrbitCamera.Default);

        renderer.SetEnvironment("sunset");
        var sunset = Render(renderer, OrbitCamera.Default);
        Assert.NotEqual(studio, sunset);

        var panorama = Path.Combine(cache.FullName, "red.hdr");
        File.WriteAllBytes(panorama, RedPanorama());
        renderer.SetEnvironment(panorama);
        var red = At(Render(renderer, OrbitCamera.Default), 2, 2);
        Assert.True(red.R > red.B + 40, $"A red panorama made {red}.");

        Assert.ThrowsAny<Exception>(() => renderer.SetEnvironment(Path.Combine(cache.FullName, "none.hdr")));
    }

    /// <summary>An 8 by 4 Radiance picture of plain red, with no run-length coding.</summary>
    static byte[] RedPanorama() {
        var header = "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 4 +X 8\n"u8.ToArray();
        byte[] red = [160, 0, 0, 129];
        return [..header, ..Enumerable.Repeat(red, 32).SelectMany(static pixel => pixel)];
    }

    /// <summary>
    ///     Isolation draws the materials it names and nothing else (#136): all of them together is the whole model to
    ///     the byte, one alone is neither the whole nor nothing, and a name the model lacks leaves only the sky.
    /// </summary>
    [Fact]
    public void An_isolated_material_is_drawn_alone() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        var camera = OrbitCamera.Default;
        var whole = Render(renderer, camera);
        var materials = ViewportGeometry.Materials(Fixture);
        Assert.True(materials.Count > 1, "The fixture should have more than one material to isolate.");

        renderer.Isolate([], ["no such material"]);
        var none = Render(renderer, camera);
        var gold = Enumerable.Range(0, Width * Height)
            .Count(i => none[i * 4] > none[i * 4 + 2] + 40 && none[i * 4 + 1] > 80);
        Assert.Equal(0, gold);
        Assert.NotEqual(whole, none);

        foreach (var material in materials) {
            renderer.Isolate([], [material]);
            var alone = Render(renderer, camera);
            Assert.NotEqual(whole, alone);
            Assert.NotEqual(none, alone);
        }

        renderer.Isolate([], materials);
        Assert.Equal(whole, Render(renderer, camera));
        renderer.Isolate([], []);
        Assert.Equal(whole, Render(renderer, camera));
    }

    /// <summary>
    ///     A mark washes what it names in a warm colour over the shaded model (#136), marks nothing of a part an
    ///     isolation hides, and taken off leaves the picture as it was to the byte.
    /// </summary>
    [Fact]
    public void A_marked_material_is_washed_and_the_mark_comes_off() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        var camera = OrbitCamera.Default;
        var whole = Render(renderer, camera);
        var materials = ViewportGeometry.Materials(Fixture);

        renderer.Highlight([], [materials[0]]);
        var marked = Render(renderer, camera);
        Assert.NotEqual(whole, marked);
        var warmer = Enumerable.Range(0, Width * Height).Count(i => marked[i * 4] > whole[i * 4] + 8);
        Assert.True(warmer > 50, $"Only {warmer} pixels are warmer with the mark on.");

        renderer.Isolate([], [materials[1]]);
        var alone = Render(renderer, camera);
        renderer.Highlight([], []);
        Assert.Equal(alone, Render(renderer, camera));

        renderer.Isolate([], []);
        Assert.Equal(whole, Render(renderer, camera));
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

        Assert.Equal(before, same);
        Assert.NotEqual(before, after);
    }

    /// <summary>
    ///     An OBJ draws in the colours of the material library beside it (#231): red on the left, blue on the right.
    ///     Read from its bytes alone, both quads were Assimp's default grey. <c>PATINA_PICTURES</c> keeps the frame.
    /// </summary>
    [Fact]
    public void An_obj_draws_in_the_colours_of_its_material_library() {
        using var renderer = Open();
        Directory.CreateDirectory(Path.Combine(cache.FullName, "looks"));
        File.WriteAllText(
            Path.Combine(cache.FullName, "looks", "paint.mtl"),
            "newmtl red\nKd 0.9 0.05 0.05\nnewmtl blue\nKd 0.05 0.1 0.9\n"
        );
        var mesh = Path.Combine(cache.FullName, "quads.obj");
        File.WriteAllLines(
            mesh,
            [
                "mtllib looks/paint.mtl",
                "v -1.1 -0.5 0", "v -0.1 -0.5 0", "v -0.1 0.5 0", "v -1.1 0.5 0",
                "v 0.1 -0.5 0", "v 1.1 -0.5 0", "v 1.1 0.5 0", "v 0.1 0.5 0",
                "vt 0 0", "vt 1 0", "vt 1 1", "vt 0 1", "vn 0 0 1",
                "usemtl red", "f 1/1/1 2/2/1 3/3/1 4/4/1",
                "usemtl blue", "f 5/1/1 6/2/1 7/3/1 8/4/1"
            ]
        );
        renderer.OpenMesh(mesh);

        var pixels = Render(renderer, OrbitCamera.Default with { Yaw = 0f, Pitch = 0f });
        if (Environment.GetEnvironmentVariable("PATINA_PICTURES") is { Length: > 0 } pictures) {
            File.WriteAllBytes(
                Path.Combine(pictures, "obj-material-library.png"),
                Vixen.Core.Imaging.PngCodec.Encode8(Width, Height, 4, pixels)
            );
        }

        var (left, right) = (At(pixels, Width / 2 - 40, Height / 2), At(pixels, Width / 2 + 40, Height / 2));
        Assert.True(left.R > left.B + 40, $"The left quad is {left}, not red.");
        Assert.True(right.B > right.R + 40, $"The right quad is {right}, not blue.");
    }

    [Fact]
    public void Rendering_with_no_mesh_is_refused() {
        using var renderer = Open();

        Assert.False(renderer.HasMesh);
        Assert.Throws<InvalidOperationException>(() => Render(renderer, OrbitCamera.Default));
    }

    /// <summary>
    ///     At one framing the bounding sphere's silhouette meets the narrower side of the picture: its half-angle, the
    ///     arcsine of the radius over the distance, is half the vertical field of view in a wide picture and the
    ///     horizontal one in a tall picture.
    /// </summary>
    [Theory]
    [InlineData(16f / 9f)]
    [InlineData(9f / 16f)]
    [InlineData(1f / 3f)]
    [InlineData(4f)]
    public void One_framing_fits_the_bounding_sphere_in_the_narrower_side(float aspectRatio) {
        var bounds = new BoundingBox(new(-1f, -2f, 0f), new(1f, 2f, 4f));
        var camera = new OrbitCamera(0.3f, 0.2f, 1f);
        var radius = bounds.Extent.Length();

        var reach = (camera.Eye(bounds, aspectRatio) - bounds.Center).Length();

        var half = camera.FieldOfView * 0.5f;
        var narrower = MathF.Min(half, MathF.Atan(aspectRatio * MathF.Tan(half)));
        Assert.Equal(narrower, MathF.Asin(radius / reach), 4);
    }
}
