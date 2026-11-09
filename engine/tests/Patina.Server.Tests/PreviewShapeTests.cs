using Patina.Contracts;
using Patina.Server.Viewport;
using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>The shapes a material or a graph with no mesh is seen on (#184).</summary>
public sealed class PreviewShapeTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-shapes-");

    public void Dispose() => folder.Delete(true);

    public static TheoryData<string> Shapes => [..ViewportShapes.All];

    /// <summary>
    ///     Every triangle turns anticlockwise seen from outside, glTF's front, which is the side the viewport draws: the
    ///     sphere turned the other way and was drawn inside out. Its normals point away from the middle, and the picture
    ///     on it reads the right way round, as a tangent-space normal map needs: the sphere's was mirrored.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void Every_face_turns_the_same_way_and_looks_outwards(string shape) {
        var (positions, normals, uvs, indices) = Read(PreviewShape.Glb(shape));
        Assert.NotEmpty(indices);
        for (var at = 0; at < indices.Count; at += 3) {
            var (a, b, c) = (positions[(int)indices[at]], positions[(int)indices[at + 1]],
                positions[(int)indices[at + 2]]);
            var turned = Vector3.Cross(b - a, c - a);
            if (turned.LengthSquared() < 1e-12f) {
                continue; // a sliver at a pole has no direction
            }

            var normal = normals[(int)indices[at]];
            Assert.True(Vector3.Dot(turned, normal) > 0, $"Triangle {at / 3} of the {shape} turns the other way.");
            Assert.True(Vector3.Dot(normal, (a + b + c) / 3) >= 0, $"Triangle {at / 3} of the {shape} faces in.");

            // Down the picture is down the texture, so a triangle seen from the front turns the other way in it.
            var (u, v, w) = (uvs[(int)indices[at]], uvs[(int)indices[at + 1]], uvs[(int)indices[at + 2]]);
            var mirrored = (v.X - u.X) * (w.Y - u.Y) - (v.Y - u.Y) * (w.X - u.X);
            Assert.True(mirrored <= 1e-9f, $"Triangle {at / 3} of the {shape} shows the picture mirrored.");
        }

        Assert.All(positions, static point => Assert.True(MathF.Abs(point.X) <= 1 && MathF.Abs(point.Y) <= 1));
    }

    [Fact]
    public void A_shape_is_written_once_and_known_as_one() {
        var library = folder.FullName;
        var plane = PreviewShape.File(library, ViewportShapes.Plane);
        Assert.EndsWith("Plane.glb", plane, StringComparison.Ordinal);
        Assert.Equal(plane, PreviewShape.File(library, ViewportShapes.Plane));
        Assert.True(PreviewShape.Holds(library, plane));
        Assert.False(PreviewShape.Holds(library, Path.Combine(library, "Plane.glb")));
        Assert.False(PreviewShape.Holds(library, null));
        Assert.Single(Directory.GetFiles(PreviewShape.Folder(library)));
    }

    [Fact]
    public void A_shape_that_is_none_is_refused() {
        Assert.Contains("no shape", new ViewportDisplay(Shape: "teapot").Problem(), StringComparison.Ordinal);
        Assert.Null(new ViewportDisplay(Shape: ViewportShapes.Cylinder).Problem());
    }

    /// <summary>Positions, normals and indices from a GLB as <see cref="PreviewShape" /> lays it out.</summary>
    static (List<Vector3> Positions, List<Vector3> Normals, List<Vector2> Uvs, List<uint> Indices) Read(byte[] glb) {
        var span = glb.AsSpan();
        Assert.Equal(0x46546C67u, BinaryPrimitives.ReadUInt32LittleEndian(span));
        var jsonLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(span[12..]);
        using var json = JsonDocument.Parse(glb.AsMemory(20, jsonLength));
        var binary = span[(20 + jsonLength + 8)..];
        var accessors = json.RootElement.GetProperty("accessors");
        var count = accessors[0].GetProperty("count").GetInt32();
        var indexCount = accessors[3].GetProperty("count").GetInt32();
        var indexOffset = json.RootElement.GetProperty("bufferViews")[1].GetProperty("byteOffset").GetInt32();

        List<Vector3> positions = [];
        List<Vector3> normals = [];
        List<Vector2> uvs = [];
        for (var vertex = 0; vertex < count; vertex++) {
            var at = binary[(vertex * 32)..];
            positions.Add(new(Float(at), Float(at[4..]), Float(at[8..])));
            normals.Add(new(Float(at[12..]), Float(at[16..]), Float(at[20..])));
            uvs.Add(new(Float(at[24..]), Float(at[28..])));
        }

        List<uint> indices = [];
        for (var index = 0; index < indexCount; index++) {
            indices.Add(BinaryPrimitives.ReadUInt32LittleEndian(binary[(indexOffset + index * 4)..]));
        }

        return (positions, normals, uvs, indices);
    }

    static float Float(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadSingleLittleEndian(bytes);
}
