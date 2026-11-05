// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;

namespace Patina.Bench;

/// <summary>
///     Writes <c>content/fixtures/viewport-test.glb</c>: a brushed-gold torus knot around a rough red sphere, which is
///     the smallest scene where a metal, a dielectric and their reflections can all be told apart in a screenshot.
///     Vixen's model writer stores no materials, so this writes the glTF container itself.
/// </summary>
static class MeshFixture {
    const int ComponentFloat = 5126;
    const int ComponentUnsignedInt = 5125;
    const uint GlbMagic = 0x46546C67;
    const uint JsonChunk = 0x4E4F534A;
    const uint BinaryChunk = 0x004E4942;

    public static int Write(string path) {
        var parts = new[] {
            Grid(180, 24, static (u, v) => TorusKnot(u, v)), Grid(48, 32, static (u, v) => Sphere(u, v) * 0.3f)
        };
        var binary = new MemoryStream();
        var bufferViews = new JsonArray();
        var accessors = new JsonArray();
        var meshes = new JsonArray();
        var nodes = new JsonArray();

        for (var i = 0; i < parts.Length; i++) {
            var (positions, normals, indices) = parts[i];
            var first = accessors.Count;
            accessors.Add(Vectors(bufferViews, binary, positions, true));
            accessors.Add(Vectors(bufferViews, binary, normals, false));
            accessors.Add(Scalars(bufferViews, binary, indices));
            meshes.Add(
                new JsonObject {
                    ["primitives"] = new JsonArray(
                        new JsonObject {
                            ["attributes"] = new JsonObject { ["POSITION"] = first, ["NORMAL"] = first + 1 },
                            ["indices"] = first + 2,
                            ["material"] = i
                        }
                    )
                }
            );
            nodes.Add(new JsonObject { ["mesh"] = i });
        }

        var document = new JsonObject {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "patina-bench" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0, 1) }),
            ["nodes"] = nodes,
            ["meshes"] = meshes,
            ["materials"] = new JsonArray(
                Material("Gold", 1f, 0.72f, 0.3f, 1f, 0.28f),
                Material("Red", 0.7f, 0.05f, 0.04f, 0f, 0.55f)
            ),
            ["accessors"] = accessors,
            ["bufferViews"] = bufferViews,
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = binary.Length })
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, Glb(document.ToJsonString(), binary.ToArray()));
        Console.WriteLine($"{path}: {new FileInfo(path).Length} bytes");
        return 0;
    }

    static JsonObject Material(string name, float r, float g, float b, float metallic, float roughness) =>
        new() {
            ["name"] = name,
            ["pbrMetallicRoughness"] = new JsonObject {
                ["baseColorFactor"] = new JsonArray(r, g, b, 1f),
                ["metallicFactor"] = metallic,
                ["roughnessFactor"] = roughness
            }
        };

    static Vector3 Sphere(float u, float v) {
        var (longitude, latitude) = (u * MathF.Tau, (v - 0.5f) * MathF.PI);
        return new(
            MathF.Cos(latitude) * MathF.Cos(longitude),
            MathF.Sin(latitude),
            MathF.Cos(latitude) * MathF.Sin(longitude)
        );
    }

    /// <summary>A (2, 3) knot, wrapped in a tube around its own frame.</summary>
    public static Vector3 TorusKnot(float u, float v, float tube = 0.11f) {
        static Vector3 Centre(float t) {
            var radius = 2f + MathF.Cos(3f * t);
            return new Vector3(radius * MathF.Cos(2f * t), MathF.Sin(3f * t), radius * MathF.Sin(2f * t)) * 0.4f;
        }

        var t = u * MathF.Tau;
        var centre = Centre(t);
        var tangent = Vector3.Normalize(Centre(t + 0.001f) - centre);
        var side = Vector3.Normalize(Vector3.Cross(tangent, Vector3.Normalize(centre)));
        var lift = Vector3.Cross(side, tangent);
        // Against the frame's own turn, so that the grid's winding faces outwards.
        var angle = -v * MathF.Tau;
        return centre + (side * MathF.Cos(angle) + lift * MathF.Sin(angle)) * tube;
    }

    /// <summary>A closed grid over a surface, with normals taken from its own differences.</summary>
    static (Vector3[] Positions, Vector3[] Normals, uint[] Indices) Grid(
        int columns,
        int rows,
        Func<float, float, Vector3> surface
    ) {
        var positions = new Vector3[(columns + 1) * (rows + 1)];
        var normals = new Vector3[positions.Length];
        var indices = new List<uint>(columns * rows * 6);
        const float Step = 1e-3f;
        for (var y = 0; y <= rows; y++) {
            for (var x = 0; x <= columns; x++) {
                var (u, v) = ((float)x / columns, (float)y / rows);
                var at = y * (columns + 1) + x;
                positions[at] = surface(u, v);
                var across = surface(u + Step, v) - surface(u - Step, v);
                var along = surface(u, Math.Min(v + Step, 1f)) - surface(u, Math.Max(v - Step, 0f));
                var normal = Vector3.Cross(along, across);
                normals[at] = normal.LengthSquared() > 1e-12f
                    ? Vector3.Normalize(normal)
                    : Vector3.Normalize(positions[at]);
                if (x < columns && y < rows) {
                    var (a, b, c, d) = ((uint)at, (uint)at + 1, (uint)(at + columns + 1), (uint)(at + columns + 2));
                    indices.AddRange([a, c, b, b, c, d]);
                }
            }
        }

        return (positions, normals, [..indices]);
    }

    static JsonObject Vectors(JsonArray views, MemoryStream binary, Vector3[] values, bool bounds) {
        var offset = binary.Length;
        Span<byte> bytes = stackalloc byte[4];
        foreach (var value in values) {
            foreach (var component in new[] { value.X, value.Y, value.Z }) {
                BinaryPrimitives.WriteSingleLittleEndian(bytes, component);
                binary.Write(bytes);
            }
        }

        var accessor = new JsonObject {
            ["bufferView"] = View(views, offset, binary.Length - offset, 34962),
            ["componentType"] = ComponentFloat,
            ["count"] = values.Length,
            ["type"] = "VEC3"
        };
        if (bounds) {
            accessor["min"] = new JsonArray(
                values.Min(static v => v.X),
                values.Min(static v => v.Y),
                values.Min(static v => v.Z)
            );
            accessor["max"] = new JsonArray(
                values.Max(static v => v.X),
                values.Max(static v => v.Y),
                values.Max(static v => v.Z)
            );
        }

        return accessor;
    }

    static JsonObject Scalars(JsonArray views, MemoryStream binary, uint[] values) {
        var offset = binary.Length;
        Span<byte> bytes = stackalloc byte[4];
        foreach (var value in values) {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            binary.Write(bytes);
        }

        return new() {
            ["bufferView"] = View(views, offset, binary.Length - offset, 34963),
            ["componentType"] = ComponentUnsignedInt,
            ["count"] = values.Length,
            ["type"] = "SCALAR"
        };
    }

    static int View(JsonArray views, long offset, long length, int target) {
        views.Add(
            new JsonObject { ["buffer"] = 0, ["byteOffset"] = offset, ["byteLength"] = length, ["target"] = target }
        );
        return views.Count - 1;
    }

    static byte[] Glb(string json, byte[] binary) {
        var text = Encoding.UTF8.GetBytes(json + new string(' ', (4 - json.Length % 4) % 4));
        var file = new MemoryStream();
        var writer = new BinaryWriter(file);
        writer.Write(GlbMagic);
        writer.Write(2u);
        writer.Write(12 + 8 + text.Length + 8 + binary.Length);
        writer.Write(text.Length);
        writer.Write(JsonChunk);
        writer.Write(text);
        writer.Write(binary.Length);
        writer.Write(BinaryChunk);
        writer.Write(binary);
        return file.ToArray();
    }
}
