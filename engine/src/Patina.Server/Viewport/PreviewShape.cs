using Patina.Contracts;
using Patina.Library;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;

namespace Patina.Server.Viewport;

/// <summary>
///     What a material or a graph with no mesh is seen on: a sphere, a plane, a cube or a cylinder
///     (<see cref="ViewportShapes" />), each written once into the cache. Deleting the cache loses nothing but the time
///     to write them again, and they are generated so that no asset has to ship for them. Every shape fits the box from
///     -1 to 1, and its front faces +z, as the viewport's Front view does.
/// </summary>
public static class PreviewShape {
    const int Rings = 48;
    const int Segments = 96;
    const uint JsonChunk = 0x4E4F534A;
    const uint BinaryChunk = 0x004E4942;
    const uint GlbMagic = 0x46546C67;
    const int FloatComponent = 5126;
    const int IndexComponent = 5125;
    const int Stride = 32;

    /// <summary>Where the shapes are written: the library's cache, or a temporary folder with no library.</summary>
    public static string Folder(string? library) =>
        library is null
            ? Path.Combine(Path.GetTempPath(), "patina-preview")
            : Path.Combine(library, LibraryLayout.CacheFolder, "preview");

    /// <summary>A shape's file, written if it is not there.</summary>
    /// <param name="library">The library whose cache holds it, or null.</param>
    /// <param name="shape">One of <see cref="ViewportShapes.All" />.</param>
    public static string File(string? library, string shape = ViewportShapes.Sphere) {
        var folder = Folder(library);
        var file = Path.Combine(folder, FileName(shape));
        if (!System.IO.File.Exists(file)) {
            Directory.CreateDirectory(folder);
            // A name of its own, so two requests writing the same shape at once never share a half-written file.
            var part = $"{file}.{Guid.NewGuid():N}.part";
            System.IO.File.WriteAllBytes(part, Glb(shape));
            System.IO.File.Move(part, file, true);
        }

        return file;
    }

    /// <summary>Whether a mesh is one of these shapes, which a choice of shape may swap for another.</summary>
    public static bool Holds(string? library, string? mesh) =>
        mesh is not null
        && string.Equals(
            Path.GetDirectoryName(Path.GetFullPath(mesh)),
            Path.GetFullPath(Folder(library)),
            StringComparison.Ordinal
        )
        && ViewportShapes.All.Any(shape => Path.GetFileName(mesh) == FileName(shape));

    /// <summary>A shape as a binary glTF: positions, normals and texture coordinates, and its triangles.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is none of <see cref="ViewportShapes.All" />.</exception>
    public static byte[] Glb(string shape) {
        var mesh = new MeshBuilder();
        switch (shape) {
            case ViewportShapes.Sphere:
                Sphere(mesh);
                break;
            case ViewportShapes.Plane:
                mesh.Face(new(-1, 1, 0), new(2, 0, 0), new(0, -2, 0), Vector3.UnitZ);
                break;
            case ViewportShapes.Cube:
                Cube(mesh);
                break;
            case ViewportShapes.Cylinder:
                Cylinder(mesh);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "No such shape.");
        }

        return mesh.Glb();
    }

    static string FileName(string shape) => $"{char.ToUpperInvariant(shape[0])}{shape[1..]}.glb";

    /// <summary>
    ///     Two turns of the map around and one up, so a tiling material is seen at a sensible size. Around runs to the
    ///     right as the sphere is seen from outside, so the picture is not mirrored.
    /// </summary>
    static void Sphere(MeshBuilder mesh) =>
        mesh.Grid(
            Segments,
            Rings,
            static (segment, ring) => {
                var theta = MathF.PI * ring / Rings;
                var point = (Around(segment) * MathF.Sin(theta)) with { Y = MathF.Cos(theta) };
                return new(point, point, segment / (float)Segments * 2f, ring / (float)Rings);
            }
        );

    /// <summary>A point on the unit circle round the up axis, turning right as it is seen from outside.</summary>
    static Vector3 Around(int segment) {
        var phi = MathF.Tau * segment / Segments;
        return new(MathF.Cos(phi), 0, -MathF.Sin(phi));
    }

    /// <summary>Each face is the whole picture once, upright as its side is seen from outside.</summary>
    static void Cube(MeshBuilder mesh) {
        mesh.Face(new(-1, 1, 1), new(2, 0, 0), new(0, -2, 0), Vector3.UnitZ);
        mesh.Face(new(1, 1, -1), new(-2, 0, 0), new(0, -2, 0), -Vector3.UnitZ);
        mesh.Face(new(1, 1, 1), new(0, 0, -2), new(0, -2, 0), Vector3.UnitX);
        mesh.Face(new(-1, 1, -1), new(0, 0, 2), new(0, -2, 0), -Vector3.UnitX);
        mesh.Face(new(-1, 1, -1), new(2, 0, 0), new(0, 0, 2), Vector3.UnitY);
        mesh.Face(new(-1, -1, 1), new(2, 0, 0), new(0, 0, -2), -Vector3.UnitY);
    }

    /// <summary>The side has the sphere's two turns of the map around, and each cap the picture once.</summary>
    static void Cylinder(MeshBuilder mesh) {
        mesh.Grid(
            Segments,
            1,
            static (segment, row) => {
                var normal = Around(segment);
                return new(normal with { Y = 1 - 2 * row }, normal, segment / (float)Segments * 2f, row);
            }
        );
        mesh.Cap(1);
        mesh.Cap(-1);
    }

    readonly record struct Corner(Vector3 Position, Vector3 Normal, float U, float V);

    /// <summary>
    ///     Vertices and triangles. A triangle turns anticlockwise as its front is seen, which is glTF's front and what
    ///     the viewport draws: the sphere this began with turned the other way and was drawn inside out.
    /// </summary>
    sealed class MeshBuilder {
        readonly List<Corner> corners = [];
        readonly List<uint> indices = [];

        /// <summary>
        ///     A grid of points, <paramref name="across" /> by <paramref name="down" /> cells, across running to the
        ///     right of a viewer outside and down running down.
        /// </summary>
        public void Grid(int across, int down, Func<int, int, Corner> point) {
            var first = (uint)corners.Count;
            for (var row = 0; row <= down; row++) {
                for (var column = 0; column <= across; column++) {
                    corners.Add(point(column, row));
                }
            }

            for (var row = 0; row < down; row++) {
                for (var column = 0; column < across; column++) {
                    var corner = first + (uint)(row * (across + 1) + column);
                    var below = corner + (uint)across + 1;
                    uint[] cell = [corner, below, corner + 1, corner + 1, below, below + 1];
                    indices.AddRange(cell);
                }
            }
        }

        /// <summary>A flat square from its top-left corner as seen from outside, the picture upright on it.</summary>
        public void Face(Vector3 topLeft, Vector3 right, Vector3 down, Vector3 normal) =>
            Grid(1, 1, (column, row) => new(topLeft + right * column + down * row, normal, column, row));

        /// <summary>A round end of the cylinder at height <paramref name="side" />, one or minus one.</summary>
        public void Cap(int side) {
            var normal = Vector3.UnitY * side;
            var centre = (uint)corners.Count;
            corners.Add(new(normal, normal, 0.5f, 0.5f));
            for (var segment = 0; segment <= Segments; segment++) {
                var around = Around(segment);
                var (x, z) = (around.X, around.Z);
                // Seen from outside, down the picture is towards the front on the top and away from it underneath.
                corners.Add(new(new(x, side, z), normal, (x + 1) / 2, (1 + side * z) / 2));
            }

            for (var segment = 0; segment < Segments; segment++) {
                var at = centre + 1 + (uint)segment;
                uint[] fan = side > 0 ? [centre, at, at + 1] : [centre, at + 1, at];
                indices.AddRange(fan);
            }
        }

        public byte[] Glb() {
            using var data = new MemoryStream();
            using var writer = new BinaryWriter(data);
            var (least, most) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
            foreach (var (position, normal, u, v) in corners) {
                least = Vector3.Min(least, position);
                most = Vector3.Max(most, position);
                float[] vertex = [position.X, position.Y, position.Z, normal.X, normal.Y, normal.Z, u, v];
                foreach (var value in vertex) {
                    writer.Write(value);
                }
            }

            var indexOffset = data.Length;
            foreach (var index in indices) {
                writer.Write(index);
            }

            writer.Flush();
            return Pack(Document(corners.Count, indices.Count, least, most, indexOffset, data.Length), data.ToArray());
        }
    }

    static string Document(int vertices, int indices, Vector3 least, Vector3 most, long indexOffset, long length) {
        JsonArray accessors = [
            Accessor(0, vertices, "VEC3", [most.X, most.Y, most.Z], [least.X, least.Y, least.Z]),
            Accessor(12, vertices, "VEC3", null, null),
            Accessor(24, vertices, "VEC2", null, null),
            new JsonObject {
                ["bufferView"] = 1, ["componentType"] = IndexComponent, ["count"] = indices, ["type"] = "SCALAR"
            }
        ];
        var document = new JsonObject {
            ["asset"] = new JsonObject { ["version"] = "2.0" },
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = length }),
            ["bufferViews"] = new JsonArray(
                new JsonObject {
                    ["buffer"] = 0, ["byteLength"] = indexOffset, ["byteStride"] = Stride, ["target"] = 34962
                },
                new JsonObject {
                    ["buffer"] = 0,
                    ["byteOffset"] = indexOffset,
                    ["byteLength"] = length - indexOffset,
                    ["target"] = 34963
                }
            ),
            ["accessors"] = accessors,
            ["meshes"] = new JsonArray(
                new JsonObject {
                    ["primitives"] = new JsonArray(
                        new JsonObject {
                            ["attributes"] = new JsonObject { ["POSITION"] = 0, ["NORMAL"] = 1, ["TEXCOORD_0"] = 2 },
                            ["indices"] = 3
                        }
                    )
                }
            ),
            ["nodes"] = new JsonArray(new JsonObject { ["mesh"] = 0 }),
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }),
            ["scene"] = 0
        };
        return document.ToJsonString();
    }

    static JsonObject Accessor(int offset, int count, string type, float[]? max, float[]? min) {
        var accessor = new JsonObject {
            ["bufferView"] = 0,
            ["byteOffset"] = offset,
            ["componentType"] = FloatComponent,
            ["count"] = count,
            ["type"] = type
        };
        if (max is not null && min is not null) {
            accessor["max"] = new JsonArray([..max.Select(static v => JsonValue.Create(v))]);
            accessor["min"] = new JsonArray([..min.Select(static v => JsonValue.Create(v))]);
        }

        return accessor;
    }

    static byte[] Pack(string json, byte[] binary) {
        var text = Encoding.UTF8.GetBytes(json);
        var padding = (4 - text.Length % 4) % 4;
        var binaryPadding = (4 - binary.Length % 4) % 4;
        using var file = new MemoryStream();
        using var writer = new BinaryWriter(file);
        writer.Write(GlbMagic);
        writer.Write(2u);
        writer.Write((uint)(12 + 8 + text.Length + padding + 8 + binary.Length + binaryPadding));
        writer.Write((uint)(text.Length + padding));
        writer.Write(JsonChunk);
        writer.Write(text);
        writer.Write(Enumerable.Repeat((byte)' ', padding).ToArray());
        writer.Write((uint)(binary.Length + binaryPadding));
        writer.Write(BinaryChunk);
        writer.Write(binary);
        writer.Write(new byte[binaryPadding]);
        writer.Flush();
        return file.ToArray();
    }
}
