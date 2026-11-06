using Patina.Library;
using System.Text;
using System.Text.Json.Nodes;

namespace Patina.Server.Viewport;

/// <summary>
///     What a material with no mesh is seen on: a sphere, written once into the cache. Deleting the cache loses nothing
///     but the time to write it again, and it is generated so that no asset has to ship for it.
/// </summary>
static class PreviewShape {
    const int Rings = 48;
    const int Segments = 96;
    const uint JsonChunk = 0x4E4F534A;
    const uint BinaryChunk = 0x004E4942;
    const uint GlbMagic = 0x46546C67;
    const int FloatComponent = 5126;
    const int IndexComponent = 5125;

    /// <summary>The shape's file, written if it is not there.</summary>
    public static string File(string? library) {
        var folder = library is null
            ? Path.Combine(Path.GetTempPath(), "patina-preview")
            : Path.Combine(library, LibraryLayout.CacheFolder, "preview");
        var file = Path.Combine(folder, "Sphere.glb");
        if (!System.IO.File.Exists(file)) {
            Directory.CreateDirectory(folder);
            var part = file + ".part";
            System.IO.File.WriteAllBytes(part, Sphere());
            System.IO.File.Move(part, file, true);
        }

        return file;
    }

    static byte[] Sphere() {
        var vertices = (Rings + 1) * (Segments + 1);
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data);
        for (var ring = 0; ring <= Rings; ring++) {
            for (var segment = 0; segment <= Segments; segment++) {
                var (x, y, z) = Point(ring, segment);
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                // Two turns of the map around and one up, so a tiling material is seen at a sensible size.
                writer.Write(segment / (float)Segments * 2f);
                writer.Write(ring / (float)Rings);
            }
        }

        var indexOffset = data.Length;
        for (var ring = 0; ring < Rings; ring++) {
            for (var segment = 0; segment < Segments; segment++) {
                var corner = (uint)(ring * (Segments + 1) + segment);
                var next = corner + Segments + 1;
                foreach (var index in new[] { corner, next, corner + 1, corner + 1, next, next + 1 }) {
                    writer.Write(index);
                }
            }
        }

        writer.Flush();
        return Glb(Document(vertices, indexOffset, data.Length), data.ToArray());
    }

    static (float X, float Y, float Z) Point(int ring, int segment) {
        var theta = MathF.PI * ring / Rings;
        var phi = MathF.Tau * segment / Segments;
        return (MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi));
    }

    static string Document(int vertices, long indexOffset, long length) {
        const int Stride = 32;
        var indices = (length - indexOffset) / sizeof(uint);
        JsonArray accessors = [
            Accessor(0, 0, vertices, "VEC3", [1, 1, 1], [-1, -1, -1]),
            Accessor(0, 12, vertices, "VEC3", null, null),
            Accessor(0, 24, vertices, "VEC2", null, null),
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

    static JsonObject Accessor(int view, int offset, int count, string type, float[]? max, float[]? min) {
        var accessor = new JsonObject {
            ["bufferView"] = view,
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

    static byte[] Glb(string json, byte[] binary) {
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
