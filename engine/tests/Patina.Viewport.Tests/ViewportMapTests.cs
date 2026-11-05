using Patina.Contracts;
using Patina.Export;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Viewport.Tests;

/// <summary>
///     Textures on a real device, judged by pictures with an answer. Skipped where there is no Vulkan. Each test builds
///     its pictures from closed forms: the same surface two ways must come out the same.
/// </summary>
public sealed class ViewportMapTests : IDisposable {
    const int Width = 256;
    const int Height = 256;

    /// <summary>Straight down +Z onto the origin, with +Y up the frame.</summary>
    static readonly OrbitCamera Front = OrbitCamera.Default with { Yaw = 0f, Pitch = 0f };

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-maps-");

    public void Dispose() => folder.Delete(true);

    string In(string name) => Path.Combine(folder.FullName, name);

    ViewportRenderer Open() {
        if (!ViewportRenderer.TryCreate(new(In("shaders")), out var renderer, out var refusal)) {
            Assert.Skip($"No Vulkan device: {refusal}");
        }

        return renderer!;
    }

    static byte[] Render(ViewportRenderer renderer, OrbitCamera? camera = null) {
        var pixels = new byte[ViewportRenderer.FrameBytes(Width, Height)];
        renderer.Render(camera ?? Front, Width, Height, pixels);
        return pixels;
    }

    /// <summary>The largest difference in any channel of any pixel.</summary>
    static int Largest(byte[] one, byte[] two) {
        var worst = 0;
        for (var at = 0; at < one.Length; at++) {
            worst = Math.Max(worst, Math.Abs(one[at] - two[at]));
        }

        return worst;
    }

    /// <summary>A square facing +Z of side two, with u along +X and v down it, as glTF has them.</summary>
    string Square(string name, Vector3? normal = null) {
        var n = normal ?? Vector3.UnitZ;
        var path = In(name);
        ModelWriter.Write(
            path,
            [
                new MeshData {
                    Name = "square",
                    Positions = [new(-1f, -1f, 0f), new(1f, -1f, 0f), new(1f, 1f, 0f), new(-1f, 1f, 0f)],
                    Normals = [n, n, n, n],
                    TexCoords = [new(0f, 1f), new(1f, 1f), new(1f, 0f), new(0f, 0f)],
                    Indices = [0, 1, 2, 0, 2, 3],
                    Bounds = new(new(-1f, -1f, 0f), new(1f, 1f, 0f))
                }
            ],
            "square"
        );
        return path;
    }

    string Picture(string name, byte red, byte green, byte blue) {
        var pixels = new byte[4 * 4 * 4];
        for (var at = 0; at < 16; at++) {
            (pixels[at * 4], pixels[at * 4 + 1], pixels[at * 4 + 2], pixels[at * 4 + 3]) = (red, green, blue, 255);
        }

        File.WriteAllBytes(In(name), PngCodec.Encode(new(4, 4, pixels)));
        return In(name);
    }

    string Normal(string name, Vector3 direction) {
        var value = direction * 0.5f + new Vector3(0.5f);
        var samples = new ushort[3 * 3 * 3];
        for (var at = 0; at < 9; at++) {
            samples[at * 3] = (ushort)MathF.Round(value.X * ushort.MaxValue);
            samples[at * 3 + 1] = (ushort)MathF.Round(value.Y * ushort.MaxValue);
            samples[at * 3 + 2] = (ushort)MathF.Round(value.Z * ushort.MaxValue);
        }

        File.WriteAllBytes(In(name), PngCodec.Encode16(3, 3, 3, samples));
        return In(name);
    }

    static ViewportMapFile File_(string path) =>
        new(path, Convert.ToHexStringLower(File.ReadAllBytes(path)[^32..]) + path);

    [Fact]
    public void A_colour_is_the_same_colour_in_every_format_it_arrives_in() {
        using var renderer = Open();
        renderer.OpenMesh(Square("square.glb"));

        renderer.SetMaps(new(File_(Picture("eight.png", 188, 188, 188))));
        var eight = Render(renderer);

        const ushort Encoded = 188 * 257;
        File.WriteAllBytes(In("sixteen.png"), PngCodec.Encode16(1, 1, 3, [Encoded, Encoded, Encoded]));
        renderer.SetMaps(new(File_(In("sixteen.png"))));
        var sixteen = Render(renderer);

        // An EXR holds linear light, and sRGB 188 is about half of it. Read as linear, the 8-bit picture would be 0.74.
        var linear = 0.5029f;
        File.WriteAllBytes(In("float.exr"), ExrCodec.Encode(new(1, 1, 3, [linear, linear, linear])));
        renderer.SetMaps(new(File_(In("float.exr"))));
        var floating = Render(renderer);

        Assert.InRange(Largest(eight, sixteen), 0, 2);
        Assert.InRange(Largest(eight, floating), 0, 2);
    }

    [Fact]
    public void A_normal_map_turns_the_surface_as_the_same_normals_would() {
        var tilted = Vector3.Normalize(new(0.35f, 0.25f, 0.9f));
        using var renderer = Open();

        renderer.OpenMesh(Square("tilted.glb", tilted));
        var geometry = Render(renderer);

        renderer.OpenMesh(Square("flat.glb"));
        var plain = Render(renderer);
        renderer.SetMaps(new(Normal: File_(Normal("tilted.png", tilted))));
        var mapped = Render(renderer);

        // Green up the image, which is +Y here: the other way is a surface lit from the wrong side.
        renderer.SetMaps(new(Normal: File_(Normal("green-down.png", new(tilted.X, -tilted.Y, tilted.Z)))));
        var inverted = Render(renderer);

        Assert.InRange(Largest(geometry, mapped), 0, 2);
        Assert.InRange(Largest(plain, mapped), 8, 255);
        Assert.InRange(Largest(inverted, mapped), 8, 255);
    }

    [Fact]
    public void Roughness_metallic_and_occlusion_are_read_from_their_own_channels() {
        using var renderer = Open();
        renderer.OpenMesh(Square("square.glb"));
        var plain = Render(renderer);

        // A packed picture: only one of its channels counts for each slot.
        var packed = File_(Picture("packed.png", 255, 90, 0));
        renderer.SetMaps(new(Roughness: packed));
        var rough = Render(renderer);
        renderer.SetMaps(new(Roughness: File_(Picture("grey.png", 90, 90, 90))));
        var grey = Render(renderer);
        renderer.SetMaps(new(Occlusion: packed));
        var open = Render(renderer);
        renderer.SetMaps(new(Occlusion: File_(Picture("dark.png", 30, 255, 255))));
        var dark = Render(renderer);
        renderer.SetMaps(new(Metallic: packed));
        var none = Render(renderer);
        renderer.SetMaps(new(Metallic: File_(Picture("metal.png", 0, 0, 255))));
        var metal = Render(renderer);

        Assert.Equal(grey, rough);
        Assert.Equal(plain, open);
        Assert.NotEqual(plain, dark);
        Assert.NotEqual(none, metal);
    }

    [Fact]
    public void Taking_the_maps_off_puts_the_picture_back() {
        using var renderer = Open();
        renderer.OpenMesh(Square("square.glb"));
        var plain = Render(renderer);

        renderer.SetMaps(new(File_(Picture("red.png", 200, 10, 10))));
        Assert.NotEqual(plain, Render(renderer));

        renderer.SetMaps(ViewportMaps.None);
        Assert.Equal(plain, Render(renderer));
    }

    [Fact]
    public void A_picture_already_read_is_not_read_again() {
        using var renderer = Open();
        renderer.OpenMesh(Square("square.glb"));
        var red = File_(Picture("red.png", 200, 10, 10));
        renderer.SetMaps(new(red));
        var first = Render(renderer);

        File.Delete(red.Path);
        renderer.SetMaps(new(red));

        Assert.Equal(first, Render(renderer));
    }

    [Fact]
    public void A_picture_that_cannot_be_read_is_refused_and_the_view_goes_on() {
        using var renderer = Open();
        renderer.OpenMesh(Square("square.glb"));
        File.WriteAllBytes(In("broken.png"), [1, 2, 3]);

        Assert.ThrowsAny<Exception>(() => renderer.SetMaps(new(File_(In("broken.png")))));

        Assert.True(renderer.HasMesh);
        Render(renderer);
    }

    /// <summary>A glTF with a material, made by hand: its picture's top half is red and its bottom half blue.</summary>
    string TexturedGltf(bool embedded) {
        var pixels = new byte[2 * 2 * 4];
        for (var row = 0; row < 2; row++) {
            for (var column = 0; column < 2; column++) {
                var at = (row * 2 + column) * 4;
                (pixels[at], pixels[at + 2], pixels[at + 3]) = ((byte)(row == 0 ? 220 : 0), (byte)(row == 0 ? 0 : 220),
                    255);
            }
        }

        var png = PngCodec.Encode(new(2, 2, pixels));
        File.WriteAllBytes(In("picture.png"), png);
        var uri = embedded ? $"data:image/png;base64,{Convert.ToBase64String(png)}" : "picture.png";
        var binary = new MemoryStream();
        foreach (var value in new[] { -1f, -1f, 0f, 1f, -1f, 0f, 1f, 1f, 0f, -1f, 1f, 0f }) {
            binary.Write(BitConverter.GetBytes(value));
        }

        foreach (var value in new[] { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f }) {
            binary.Write(BitConverter.GetBytes(value));
        }

        foreach (var value in new[] { 0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f }) {
            binary.Write(BitConverter.GetBytes(value));
        }

        foreach (var value in new ushort[] { 0, 1, 2, 0, 2, 3 }) {
            binary.Write(BitConverter.GetBytes(value));
        }

        var data = Convert.ToBase64String(binary.ToArray());
        var json = $$"""
                     {
                       "asset": { "version": "2.0" },
                       "scene": 0, "scenes": [{ "nodes": [0] }], "nodes": [{ "mesh": 0 }],
                       "images": [{ "uri": "{{uri}}" }],
                       "textures": [{ "source": 0 }],
                       "materials": [{ "pbrMetallicRoughness": {
                         "baseColorTexture": { "index": 0 }, "metallicFactor": 0.0, "roughnessFactor": 1.0
                       } }],
                       "meshes": [{ "primitives": [{
                         "attributes": { "POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2 }, "indices": 3, "material": 0
                       }] }],
                       "buffers": [{
                         "uri": "data:application/octet-stream;base64,{{data}}",
                         "byteLength": {{binary.Length}}
                       }],
                       "bufferViews": [
                         { "buffer": 0, "byteOffset": 0, "byteLength": 48 },
                         { "buffer": 0, "byteOffset": 48, "byteLength": 48 },
                         { "buffer": 0, "byteOffset": 96, "byteLength": 32 },
                         { "buffer": 0, "byteOffset": 128, "byteLength": 12 }
                       ],
                       "accessors": [
                         { "bufferView": 0, "componentType": 5126, "count": 4, "type": "VEC3",
                           "min": [-1, -1, 0], "max": [1, 1, 0] },
                         { "bufferView": 1, "componentType": 5126, "count": 4, "type": "VEC3" },
                         { "bufferView": 2, "componentType": 5126, "count": 4, "type": "VEC2" },
                         { "bufferView": 3, "componentType": 5123, "count": 6, "type": "SCALAR" }
                       ]
                     }
                     """;
        var path = In(embedded ? "embedded.gltf" : "beside.gltf");
        File.WriteAllText(path, json);
        return path;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_material_s_picture_is_the_right_way_up(bool embedded) {
        using var renderer = Open();
        renderer.OpenMesh(TexturedGltf(embedded));

        var pixels = Render(renderer);

        byte[] At(int x, int y) => pixels[((y * Width + x) * 4)..((y * Width + x) * 4 + 3)];
        var (top, bottom) = (At(Width / 2, Height * 3 / 8), At(Width / 2, Height * 5 / 8));

        Assert.True(top[0] > top[2] + 40, $"the top of the square is {top[0]},{top[1]},{top[2]}");
        Assert.True(bottom[2] > bottom[0] + 40, $"the bottom of the square is {bottom[0]},{bottom[1]},{bottom[2]}");
    }

    /// <summary>A band of a sphere with its atlas: smooth for a low mesh, and rippled for its high.</summary>
    string Band(string name, int sides, int rows, float ripple) {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var coordinates = new List<Vector2>();
        var indices = new List<int>();

        Vector3 Surface(float around, float up) {
            var radius = 1f + ripple * MathF.Sin(around * 24f) * MathF.Sin(up * 24f);
            return new Vector3(MathF.Cos(around) * MathF.Cos(up), MathF.Sin(up), MathF.Sin(around) * MathF.Cos(up))
                * radius;
        }

        for (var side = 0; side <= sides; side++) {
            for (var row = 0; row <= rows; row++) {
                var around = 0.2f + 2.7f * side / sides;
                var up = 1.1f * ((float)row / rows - 0.5f);
                const float Step = 1e-3f;
                var slope = Vector3.Cross(
                    Surface(around, up + Step) - Surface(around, up - Step),
                    Surface(around + Step, up) - Surface(around - Step, up)
                );
                positions.Add(Surface(around, up));
                normals.Add(Vector3.Normalize(slope));
                coordinates.Add(new(0.05f + 0.9f * side / sides, 0.95f - 0.9f * row / rows));
            }
        }

        for (var side = 0; side < sides; side++) {
            for (var row = 0; row < rows; row++) {
                var (at, next) = (side * (rows + 1) + row, (side + 1) * (rows + 1) + row);
                indices.AddRange([at, at + 1, next, next, at + 1, next + 1]);
            }
        }

        ModelWriter.Write(
            In(name),
            [
                new MeshData {
                    Name = name,
                    Positions = [..positions],
                    Normals = [..normals],
                    TexCoords = [..coordinates],
                    Indices = [..indices],
                    Bounds = new(new(-1f), new(1f))
                }
            ],
            name
        );
        return In(name);
    }

    /// <summary>The mean difference over the middle of the frame, where both surfaces cover every pixel.</summary>
    static double Difference(byte[] one, byte[] two) {
        var (sum, count) = (0d, 0);
        for (var y = Height * 3 / 8; y < Height * 5 / 8; y++) {
            for (var x = Width * 3 / 8; x < Width * 5 / 8; x++) {
                for (var lane = 0; lane < 3; lane++) {
                    var at = (y * Width + x) * 4 + lane;
                    sum += Math.Abs(one[at] - two[at]);
                    count++;
                }
            }
        }

        return sum / count;
    }

    [Theory]
    [InlineData(BakeInputsFormats.Png16)]
    [InlineData(BakeInputsFormats.Exr16)]
    public void A_low_mesh_with_its_bake_looks_like_the_high_mesh(string format) {
        var high = Band("high.glb", 800, 300, 0.01f);
        var low = Band("low.glb", 8, 3, 0f);
        var request = new BakeRequest {
            High = high,
            Low = low,
            Output = In("out"),
            Resolution = 512,
            Format = format,
            SearchRadius = 0.15
        };
        var manifest = MeshMapBakeRun.Run(request, In("bake"), 0, null, TestContext.Current.CancellationToken);
        var normal = manifest.Result.Maps.Single(static map => map.Map == "normal");
        using var renderer = Open();
        var camera = OrbitCamera.Default with { Yaw = MathF.PI / 2f - 1.55f, Pitch = 0.1f };

        renderer.OpenMesh(high);
        var truth = Render(renderer, camera);
        renderer.OpenMesh(low);
        var bare = Render(renderer, camera);
        renderer.SetMaps(new(Normal: File_(Path.Combine(In("bake"), normal.Path))));
        var baked = Render(renderer, camera);

        var (before, after) = (Difference(truth, bare), Difference(truth, baked));
        TestContext.Current.TestOutputHelper?.WriteLine($"{format}: low alone {before:F2}, with its bake {after:F2}");
        Assert.InRange(after, 0d, 1.5d);
        Assert.True(after < before / 3d, $"{before:F2} became {after:F2}");
    }

    /// <summary>The bake formats, as constants an attribute can hold.</summary>
    public static class BakeInputsFormats {
        public const string Png16 = "png16";
        public const string Exr16 = "exr16";
    }
}
