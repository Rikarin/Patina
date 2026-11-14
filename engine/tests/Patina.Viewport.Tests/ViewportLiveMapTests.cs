using System.Globalization;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Viewport.Tests;

/// <summary>
///     Live maps on a real device, against a file of the same pixels, which is the viewport's own definition of what a
///     picture should look like. Skipped where there is no Vulkan.
/// </summary>
public sealed class ViewportLiveMapTests : IDisposable {
    const int Width = 320;
    const int Height = 240;
    const int Map = 256;

    // 1.4 and 12 bounding radii away, in framings: the field of view's half is the narrower side's in a wide frame.
    static readonly OrbitCamera Near = new(0f, 0f, 0.48f);
    static readonly OrbitCamera Far = new(0f, 0f, 4.1f);

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-live-");

    public void Dispose() => folder.Delete(true);

    string In(string name) => Path.Combine(folder.FullName, name);

    ViewportRenderer Open() {
        if (!ViewportRenderer.TryCreate(new(In("shaders")), out var renderer, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
        }

        return renderer!;
    }

    string Square(string name, Vector3 centre, float half) {
        var path = In(name);
        ModelWriter.Write(
            path,
            [
                new MeshData {
                    Name = "square",
                    Positions = [
                        centre + new Vector3(-half, -half, 0f), centre + new Vector3(half, -half, 0f),
                        centre + new Vector3(half, half, 0f), centre + new Vector3(-half, half, 0f)
                    ],
                    Normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
                    TexCoords = [new(0f, 1f), new(1f, 1f), new(1f, 0f), new(0f, 0f)],
                    Indices = [0, 1, 2, 0, 2, 3]
                }
            ],
            "square"
        );
        return path;
    }

    static byte[] Picture(Func<int, int, (byte R, byte G, byte B)> colour) {
        var pixels = new byte[Map * Map * 4];
        for (var y = 0; y < Map; y++) {
            for (var x = 0; x < Map; x++) {
                var (r, g, b) = colour(x, y);
                pixels[(y * Map + x) * 4] = r;
                pixels[(y * Map + x) * 4 + 1] = g;
                pixels[(y * Map + x) * 4 + 2] = b;
                pixels[(y * Map + x) * 4 + 3] = byte.MaxValue;
            }
        }

        return pixels;
    }

    static byte[] Render(ViewportRenderer renderer, OrbitCamera camera) {
        var pixels = new byte[ViewportRenderer.FrameBytes(Width, Height)];
        renderer.Render(camera, Width, Height, pixels);
        return pixels;
    }

    static int Largest(byte[] one, byte[] two) {
        var worst = 0;
        for (var at = 0; at < one.Length; at++) {
            worst = Math.Max(worst, Math.Abs(one[at] - two[at]));
        }

        return worst;
    }

    ViewportMapFile File(string name, byte[] pixels) {
        var path = In(name);
        System.IO.File.WriteAllBytes(path, PngCodec.Encode(new(Map, Map, pixels)));
        return new(path, name);
    }

    /// <summary>
    ///     The same bytes draw the same frame as a file does, close to and from far enough to read the mips, before and
    ///     after a repair of one rectangle. That is what makes a stack on the mesh look like its export.
    /// </summary>
    [Fact]
    public void A_live_map_draws_what_a_file_of_the_same_bytes_draws_and_a_repair_keeps_it_so() {
        using var renderer = Open();
        renderer.OpenMesh(Square("square.glb", Vector3.Zero, 1f));
        var before = Picture(static (x, y) => ((byte)(x ^ y), (byte)(x / 2), (byte)(255 - y)));

        var after = (byte[])before.Clone();
        ViewportRect rect = new(64, 32, 96, 120);
        var patch = new byte[rect.Width * rect.Height * 4];
        for (var at = 0; at < patch.Length; at += 4) {
            (patch[at], patch[at + 1], patch[at + 2], patch[at + 3]) = (250, 20, 30, 255);
        }

        for (var row = 0; row < rect.Height; row++) {
            patch.AsSpan(row * rect.Width * 4, rect.Width * 4)
                .CopyTo(after.AsSpan(((rect.Y + row) * Map + rect.X) * 4));
        }

        renderer.SetMaps(new(File("before.png", before)));
        var (fileNear, fileFar) = (Render(renderer, Near), Render(renderer, Far));
        renderer.SetMaps(new(File("after.png", after)));
        var (repairedNear, repairedFar) = (Render(renderer, Near), Render(renderer, Far));
        renderer.SetMaps(ViewportMaps.None);

        renderer.SetLive(ViewportSlot.BaseColor, Map, Map, before);
        Assert.InRange(Largest(fileNear, Render(renderer, Near)), 0, 2);
        Assert.InRange(Largest(fileFar, Render(renderer, Far)), 0, 3);

        renderer.RepairLive(ViewportSlot.BaseColor, [new(rect, patch)]);
        Assert.Equal(after, renderer.Sampled(ViewportSlot.BaseColor));
        Assert.InRange(Largest(repairedNear, Render(renderer, Near)), 0, 2);
        Assert.InRange(Largest(repairedFar, Render(renderer, Far)), 0, 3);

        // The instrument: this frame has to be able to tell the two pictures apart.
        Assert.InRange(Largest(fileFar, repairedFar), 20, 255);
    }

    [Fact]
    public void Taking_the_live_maps_off_leaves_what_was_there() {
        using var renderer = Open();
        renderer.OpenMesh(Square("square.glb", Vector3.Zero, 1f));
        var plain = Render(renderer, Near);

        renderer.SetLive(ViewportSlot.BaseColor, Map, Map, Picture(static (_, _) => (10, 200, 10)));
        Assert.InRange(Largest(plain, Render(renderer, Near)), 20, 255);

        renderer.ClearLive();
        Assert.Null(renderer.Sampled(ViewportSlot.BaseColor));
        Assert.Equal(plain, Render(renderer, Near));
    }

    /// <summary>
    ///     A quick mask is red over the model where it is painted and nowhere else (#127), and taken off leaves the
    ///     picture as it was. On Linux CI the e2e that paints one saw no red (#274); this says whether the renderer
    ///     draws it there. The red is laid over the finished picture after the tone curve (#286), so it is as red on a
    ///     square near white, and with the exposure three stops up, as anywhere: red about 115 levels over the mean of
    ///     green and blue whatever is beneath. Laid on before the curve, as it was, it came out about (227, 209, 208)
    ///     there, under twenty levels over, and pink to the eye.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(3f)]
    public void A_quick_mask_is_red_where_it_is_painted(float exposure) {
        using var renderer = Open();
        renderer.OpenMesh(Square("square.glb", Vector3.Zero, 1f));
        renderer.Shading = new() { Exposure = exposure };
        var plain = Render(renderer, Near);

        // White on the left half of the map, nothing on the right.
        var white = Picture(static (x, _) => x < Map / 2 ? (byte.MaxValue, byte.MaxValue, byte.MaxValue) : default);
        renderer.SetLive(ViewportSlot.QuickMask, Map, Map, white);
        var masked = Render(renderer, Near);

        // Red seventy-five levels over the mean of green and blue, which grey never is and the tint always is.
        static int Red(byte[] pixels, int from, int to) {
            var count = 0;
            for (var y = 0; y < Height; y++) {
                for (var x = from; x < to; x++) {
                    var at = (y * Width + x) * 4;
                    count += pixels[at] * 2 > pixels[at + 1] + pixels[at + 2] + 150 ? 1 : 0;
                }
            }

            return count;
        }

        Assert.Equal(0, Red(plain, 0, Width));
        Assert.True(Red(masked, 0, Width / 2) > 1000, $"Only {Red(masked, 0, Width / 2)} pixels are red on the left.");
        Assert.True(Red(masked, Width / 2 + 8, Width) < 50, "The quick mask is red where it is not painted.");

        renderer.ClearLive(ViewportSlot.QuickMask);
        Assert.Equal(plain, Render(renderer, Near));
    }

    /// <summary>
    ///     A camera's axes are worked out away from the matrix that draws, so they are held to the picture: a small
    ///     square off to one side, above the middle, must be where the arithmetic says. A flip of either axis fails,
    ///     and so does an orthographic view drawn through any lens but the one a stroke is told.
    /// </summary>
    [Fact]
    public void The_axes_of_the_camera_put_a_point_where_the_picture_shows_it() {
        using var renderer = Open();
        renderer.Shading = new() { Shadows = 0f }; // the speck's shadow would pull the centroid away from the speck
        var centre = new Vector3(0.7f, 0.45f, 0.2f);
        var bounds = new BoundingBox(new(-1f, -1f, 0f), new(1f, 1f, 0f));

        OrbitCamera[] cameras = [
            new(0f, 0f, 1.03f), new(0.8f, 0.5f, 1.03f), new(-1.2f, -0.4f, 0.89f),
            new(0.8f, 0.5f, 1.03f, Orthographic: true), new(0.3f, 0.2f, 0.8f, FieldOfView: 1.4f)
        ];
        foreach (var camera in cameras) {
            renderer.OpenMesh(Marked("away.glb", new(0.7f, 0.45f, 0.2f), 0.0001f));
            var plain = Render(renderer, camera);
            renderer.OpenMesh(Marked("marked.glb", centre, 0.03f));
            var marked = Render(renderer, camera);

            var (x, y) = Centroid(plain, marked);
            var (expectedX, expectedY) = Project(camera, bounds, centre);
            Assert.InRange(MathF.Abs(x - expectedX), 0f, 3f);
            Assert.InRange(MathF.Abs(y - expectedY), 0f, 3f);
        }
    }

    /// <summary>The square with a speck floating in front of it, keeping the same bounds in x and y.</summary>
    string Marked(string name, Vector3 at, float half) {
        var path = In(name);
        ModelWriter.Write(
            path,
            [
                new MeshData {
                    Name = "marked",
                    Positions = [
                        new(-1f, -1f, 0f), new(1f, -1f, 0f), new(1f, 1f, 0f), new(-1f, 1f, 0f),
                        at + new Vector3(-half, -half, 0f), at + new Vector3(half, -half, 0f),
                        at + new Vector3(half, half, 0f), at + new Vector3(-half, half, 0f)
                    ],
                    Normals = [
                        ..Enumerable.Repeat(Vector3.UnitZ, 4),
                        ..Enumerable.Repeat(Vector3.Normalize(new(-1f, -1f, 0.2f)), 4)
                    ],
                    TexCoords = [..Enumerable.Repeat(Vector2.Zero, 8)],
                    Indices = [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7]
                }
            ],
            "marked"
        );
        return path;
    }

    static (float X, float Y) Centroid(byte[] without, byte[] with) {
        var (sumX, sumY, count) = (0f, 0f, 0);
        for (var y = 0; y < Height; y++) {
            for (var x = 0; x < Width; x++) {
                var at = (y * Width + x) * 4;
                if (Math.Abs(without[at] - with[at]) + Math.Abs(without[at + 1] - with[at + 1]) > 12) {
                    (sumX, sumY, count) = (sumX + x + 0.5f, sumY + y + 0.5f, count + 1);
                }
            }
        }

        Assert.True(count > 0, string.Create(CultureInfo.InvariantCulture, $"{count} pixels moved."));
        return (sumX / count, sumY / count);
    }

    static (float X, float Y) Project(OrbitCamera camera, BoundingBox bounds, Vector3 point) {
        // The speck stands off the square, so the bounds that frame the picture reach out to it.
        var framed = BoundingBox.Merge(bounds, point);
        var eye = camera.Eye(framed, (float)Width / Height);
        var (right, up, forward) = camera.Axes();
        var tangent = MathF.Tan(camera.Lens * 0.5f);
        var offset = point - eye;
        var depth = Vector3.Dot(offset, forward);
        return (
            (Vector3.Dot(offset, right) / (depth * tangent * Width / Height) + 1f) * 0.5f * Width,
            (1f - Vector3.Dot(offset, up) / (depth * tangent)) * 0.5f * Height
        );
    }
}
