// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Export;
using System.Globalization;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;

namespace Patina.Bench;

/// <summary>
///     Shades a low mesh with its baked maps on the CPU, so that a bake can be judged as a lit surface: the viewport
///     does
///     not read normal maps yet. It undoes the baker's tangent frame triangle by triangle, flat across each chart, and
///     what it draws is for looking at, not a renderer anyone else should use.
/// </summary>
static class BakePreview {
    const int Width = 1280;
    const int Height = 960;

    public static int Run(string low, string normalMap, string? occlusionMap, string output, float yaw, float pitch) {
        var mesh = MeshMapBakeRun.Read(low).Mesh;
        var scene = new Scene(
            PngCodec.Load(normalMap),
            occlusionMap is null ? null : PngCodec.Load(occlusionMap),
            yaw,
            pitch
        );
        var triangles = Triangles(mesh);

        foreach (var triangle in triangles) {
            scene.Draw(triangle);
        }

        PngCodec.Save(output, new(Width, Height, scene.Pixels));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{output}: {triangles.Count} triangles"));
        return 0;
    }

    /// <summary>What is being drawn into, and the camera and lights it is seen with.</summary>
    sealed class Scene {
        readonly Bitmap normals;
        readonly Bitmap? occlusion;
        readonly float[] depth = new float[Width * Height];
        readonly (float Sin, float Cos) turn;
        readonly (float Sin, float Cos) tilt;

        readonly Vector3 light = Vector3.Normalize(new(-0.4f, 0.75f, 0.55f));
        readonly Vector3 fill = Vector3.Normalize(new(0.8f, 0.1f, 0.5f));

        readonly Vector3 halfway =
            Vector3.Normalize(Vector3.Normalize(new(-0.4f, 0.75f, 0.55f)) + new Vector3(0f, 0f, 1f));

        public Scene(Bitmap normals, Bitmap? occlusion, float yaw, float pitch) {
            this.normals = normals;
            this.occlusion = occlusion;
            turn = (MathF.Sin(yaw), MathF.Cos(yaw));
            tilt = (MathF.Sin(pitch), MathF.Cos(pitch));
            Array.Fill(depth, float.NegativeInfinity);
            for (var at = 0; at < Pixels.Length; at += 4) {
                (Pixels[at], Pixels[at + 1], Pixels[at + 2], Pixels[at + 3]) = (38, 40, 44, 255);
            }
        }

        public byte[] Pixels { get; } = new byte[Width * Height * 4];

        public void Draw(Triangle triangle) {
            var (a, b, c) = (View(triangle.Points.A), View(triangle.Points.B), View(triangle.Points.C));
            var (sa, sb, sc) = (Screen(a), Screen(b), Screen(c));
            var area = (sb.X - sa.X) * (sc.Y - sa.Y) - (sb.Y - sa.Y) * (sc.X - sa.X);
            if (area >= 0f) {
                return; // facing away, on a y-down screen
            }

            var low = Vector2.Min(sa, Vector2.Min(sb, sc));
            var high = Vector2.Max(sa, Vector2.Max(sb, sc));
            for (var y = Math.Max((int)low.Y, 0); y <= Math.Min((int)high.Y + 1, Height - 1); y++) {
                for (var x = Math.Max((int)low.X, 0); x <= Math.Min((int)high.X + 1, Width - 1); x++) {
                    var (px, py) = (x + 0.5f, y + 0.5f);
                    var w0 = ((sb.X - px) * (sc.Y - py) - (sb.Y - py) * (sc.X - px)) / area;
                    var w1 = ((sc.X - px) * (sa.Y - py) - (sc.Y - py) * (sa.X - px)) / area;
                    var w2 = 1f - w0 - w1;
                    var near = a.Z * w0 + b.Z * w1 + c.Z * w2;
                    var index = y * Width + x;
                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f && near > depth[index]) {
                        depth[index] = near;
                        Shade(index, triangle, triangle.Uv.A * w0 + triangle.Uv.B * w1 + triangle.Uv.C * w2);
                    }
                }
            }
        }

        void Shade(int index, Triangle triangle, Vector2 uv) {
            var local = Sample(normals, uv) * 2f - new Vector3(1f);
            var world = triangle.Frame.Tangent * local.X
                + triangle.Frame.Bitangent * local.Y
                + triangle.Frame.Normal * local.Z;
            var seen = Vector3.Normalize(View(Vector3.Normalize(world)));

            var diffuse = MathF.Max(Vector3.Dot(seen, light), 0f) * 0.75f
                + MathF.Max(Vector3.Dot(seen, fill), 0f) * 0.25f;
            var specular = MathF.Pow(MathF.Max(Vector3.Dot(seen, halfway), 0f), 40f) * 0.35f;
            var open = occlusion is { } picture ? Sample(picture, uv).X : 1f;
            var shade = (0.16f + diffuse) * (0.35f + 0.65f * open) + specular;

            var at = index * 4;
            Pixels[at] = Byte(shade * 0.72f);
            Pixels[at + 1] = Byte(shade * 0.71f);
            Pixels[at + 2] = Byte(shade * 0.68f);
        }

        Vector3 View(Vector3 point) {
            var x = point.X * turn.Cos + point.Z * turn.Sin;
            var z = point.Z * turn.Cos - point.X * turn.Sin;
            return new(x, point.Y * tilt.Cos - z * tilt.Sin, z * tilt.Cos + point.Y * tilt.Sin);
        }

        static Vector2 Screen(Vector3 view) =>
            new(Width / 2f + view.X * Height * 0.34f, Height / 2f - view.Y * Height * 0.34f);
    }

    static byte Byte(float value) => (byte)Math.Clamp(MathF.Pow(value, 1f / 2.2f) * 255f, 0f, 255f);

    /// <summary>A bilinear read, with the atlas's v running up and the file's rows running down.</summary>
    static Vector3 Sample(in Bitmap image, Vector2 uv) {
        var (x, y) = (uv.X * image.Width - 0.5f, (1f - uv.Y) * image.Height - 0.5f);
        var (ix, iy) = ((int)MathF.Floor(x), (int)MathF.Floor(y));
        var (fx, fy) = (x - ix, y - iy);

        static Vector3 Read(in Bitmap picture, int px, int py) {
            var at = picture.Offset(Math.Clamp(px, 0, picture.Width - 1), Math.Clamp(py, 0, picture.Height - 1));
            return new Vector3(picture.Pixels[at], picture.Pixels[at + 1], picture.Pixels[at + 2]) / 255f;
        }

        var top = Vector3.Lerp(Read(image, ix, iy), Read(image, ix + 1, iy), fx);
        var bottom = Vector3.Lerp(Read(image, ix, iy + 1), Read(image, ix + 1, iy + 1), fx);
        return Vector3.Lerp(top, bottom, fy);
    }

    /// <summary>A triangle's corners, its atlas coordinates, and the frame its chart implies.</summary>
    sealed record Triangle(
        (Vector3 A, Vector3 B, Vector3 C) Points,
        (Vector2 A, Vector2 B, Vector2 C) Uv,
        (Vector3 Tangent, Vector3 Bitangent, Vector3 Normal) Frame
    );

    static List<Triangle> Triangles(Vixen.Geometry.EditMesh mesh) {
        List<Triangle> made = [];
        for (var face = 0; face < mesh.FaceCount; face++) {
            var entry = mesh.Faces[face];
            var loop = mesh.CornersOf(face);
            var (p0, p1, p2) = (mesh.Positions[loop[0]], mesh.Positions[loop[1]], mesh.Positions[loop[2]]);
            var (uv0, uv1, uv2) = (mesh.TexCoords[entry.Start], mesh.TexCoords[entry.Start + 1],
                mesh.TexCoords[entry.Start + 2]);
            var (e1, e2, d1, d2) = (p1 - p0, p2 - p0, uv1 - uv0, uv2 - uv0);
            var normal = mesh.Normals.Length == mesh.CornerCount
                ? mesh.Normals[entry.Start]
                : Vector3.Normalize(Vector3.Cross(e1, e2));
            var inverse = 1f / (d1.X * d2.Y - d2.X * d1.Y);
            var tangent = (e1 * d2.Y - e2 * d1.Y) * inverse;
            var bitangent = (e2 * d1.X - e1 * d2.X) * inverse;
            var straight = Vector3.Normalize(tangent - normal * Vector3.Dot(normal, tangent));
            var crossed = Vector3.Cross(normal, straight);
            made.Add(
                new(
                    (p0, p1, p2),
                    (uv0, uv1, uv2),
                    (straight, Vector3.Dot(crossed, bitangent) < 0f ? -crossed : crossed, normal)
                )
            );
        }

        return made;
    }
}
