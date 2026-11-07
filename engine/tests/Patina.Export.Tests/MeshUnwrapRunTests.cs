using System.Globalization;
using System.Text;
using Vixen.Core.Mathematics;
using Vixen.Interchange;

namespace Patina.Export.Tests;

public sealed class MeshUnwrapRunTests : IDisposable {
    const string Cube = """
                        v -1 -1 -1
                        v 1 -1 -1
                        v 1 1 -1
                        v -1 1 -1
                        v -1 -1 1
                        v 1 -1 1
                        v 1 1 1
                        v -1 1 1
                        f 5 6 7 8
                        f 4 3 2 1
                        f 2 3 7 6
                        f 1 5 8 4
                        f 4 8 7 3
                        f 1 2 6 5
                        """;

    readonly string folder = Directory.CreateTempSubdirectory("patina-unwrap-").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public void A_mesh_with_no_layout_gets_one_in_a_new_file() {
        var source = Path.Combine(folder, "cube.obj");
        var output = Path.Combine(folder, "cube_unwrapped.glb");
        File.WriteAllText(source, Cube);
        Assert.Equal(0, MeshMapBakeRun.Read(source).Mesh.TexCoords.Length);

        Assert.NotNull(MeshUnwrapRun.Run(source, output));

        var mesh = MeshMapBakeRun.Read(output).Mesh;
        Assert.True(mesh.FaceCount > 0);
        Assert.Equal(mesh.CornerCount, mesh.TexCoords.Length);
        Assert.Contains(mesh.TexCoords.ToArray(), static uv => uv.X > 0 || uv.Y > 0);
        Assert.Equal(Cube, File.ReadAllText(source));
        Assert.Null(MeshUnwrapRun.Run(output, Path.Combine(folder, "again.glb")));
        Assert.Equal(
            ["cube.obj", "cube_unwrapped.glb"],
            Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal)
        );
    }

    /// <summary>A smooth ball in two named halves, with a material and a vertex colour on each and no layout.</summary>
    static string Ball() {
        const int Rings = 12;
        const int Sides = 16;
        var text = new StringBuilder();
        for (var ring = 0; ring <= Rings; ring++) {
            for (var side = 0; side < Sides; side++) {
                var polar = MathF.PI * ring / Rings;
                var around = MathF.Tau * side / Sides;
                var (x, y, z) = (MathF.Sin(polar) * MathF.Cos(around), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(around));
                var colour = y >= 0 ? "1 0 0" : "0 0 1";
                text.AppendLine(CultureInfo.InvariantCulture, $"v {x:R} {y:R} {z:R} {colour}");
                text.AppendLine(CultureInfo.InvariantCulture, $"vn {x:R} {y:R} {z:R}");
            }
        }

        void Half(string name, string material, int from, int to) {
            text.AppendLine(CultureInfo.InvariantCulture, $"o {name}");
            text.AppendLine(CultureInfo.InvariantCulture, $"usemtl {material}");
            for (var ring = from; ring < to; ring++) {
                for (var side = 0; side < Sides; side++) {
                    var a = (ring * Sides) + side + 1;
                    var b = (ring * Sides) + ((side + 1) % Sides) + 1;
                    var (c, d) = (a + Sides, b + Sides);
                    text.AppendLine(CultureInfo.InvariantCulture, $"f {a}//{a} {b}//{b} {d}//{d} {c}//{c}");
                }
            }
        }

        Half("Top", "red", 0, Rings / 2);
        Half("Bottom", "blue", Rings / 2, Rings);
        return text.ToString();
    }

    string Unwrapped(string text) {
        var source = Path.Combine(folder, "ball.obj");
        var output = Path.Combine(folder, "ball_unwrapped.glb");
        File.WriteAllText(source, text);
        Assert.NotNull(MeshUnwrapRun.Run(source, output));
        return output;
    }

    static ReadModel Model(string path) =>
        ModelReader.Read(
            File.ReadAllBytes(path),
            Path.GetExtension(path),
            "unwrapped",
            new ModelReadOptions {
                GenerateTangents = false, ImportAnimations = false, ImportBlendShapes = false, ImportVertexColours = true
            }
        );

    [Fact]
    public void A_smooth_mesh_stays_smooth() {
        var mesh = MeshMapBakeRun.Read(Unwrapped(Ball())).Mesh;

        Assert.Equal(mesh.CornerCount, mesh.Normals.Length);
        for (var face = 0; face < mesh.FaceCount; face++) {
            var corners = mesh.CornersOf(face);
            for (var corner = 0; corner < corners.Length; corner++) {
                var radial = Vector3.Normalize(mesh.Positions[corners[corner]]);
                var normal = mesh.Normals[mesh.Faces[face].Start + corner];
                Assert.True(Vector3.Dot(radial, normal) > 0.999f, $"Face {face} is lit flat.");
            }
        }
    }

    [Fact]
    public void Each_material_stays_and_has_a_layout_of_its_own() {
        var output = Unwrapped(Ball());
        var read = Model(output);

        Assert.Equal(["blue", "red"], read.Model.Parts.Select(part => read.Model.Materials[part.Material]).Distinct().Order());
        foreach (var material in new[] { "red", "blue" }) {
            var mesh = MeshMapBakeRun.Read(output, material).Mesh;
            var coordinates = mesh.TexCoords.ToArray();
            Assert.Equal(mesh.CornerCount, coordinates.Length);
            Assert.All(coordinates, static uv => Assert.InRange(uv.X, 0f, 1f));
            Assert.All(coordinates, static uv => Assert.InRange(uv.Y, 0f, 1f));

            // ⚠ A set paints a whole atlas of its own, so one atlas shared by the two halves would waste half of each.
            Assert.True(coordinates.Max(static uv => uv.X) > 0.6f || coordinates.Max(static uv => uv.Y) > 0.6f);
        }
    }

    [Fact]
    public void Vertex_colours_stay_so_a_bake_can_read_them_as_ids() {
        var output = Unwrapped(Ball());

        var flat = MeshMapBakeRun.Read(output);

        Assert.True(flat.ColouredGroups);
        var red = ModelGeometry.PackedColour(new(1f, 0f, 0f, 1f));
        var blue = ModelGeometry.PackedColour(new(0f, 0f, 1f, 1f));
        Assert.Equal(
            new[] { red, blue }.Order(),
            Enumerable.Range(0, flat.Mesh.FaceCount).Select(face => flat.Mesh.Faces[face].Group).Distinct().Order()
        );
    }

    [Fact]
    public void Names_stay_so_a_match_by_name_and_a_mesh_mask_can_find_them() {
        var flat = MeshMapBakeRun.Read(Unwrapped(Ball()));

        Assert.Equal(["Bottom", "Top"], flat.Parts.Select(static part => part.Name).Distinct().Order());
    }

    [Fact]
    public void A_material_with_a_layout_keeps_it_while_one_without_is_given_one() {
        const string Mixed = """
                             v 0 0 0
                             v 1 0 0
                             v 1 1 0
                             v 0 1 0
                             v 0 0 1
                             v 1 0 1
                             v 1 1 1
                             v 0 1 1
                             vt 0.25 0.25
                             vt 0.5 0.25
                             vt 0.5 0.5
                             vt 0.25 0.5
                             o Laid
                             usemtl laid
                             f 1/1 2/2 3/3 4/4
                             o Bare
                             usemtl bare
                             f 5 6 7 8
                             """;
        var output = Unwrapped(Mixed);

        var laid = MeshMapBakeRun.Read(output, "laid").Mesh.TexCoords.ToArray();
        var bare = MeshMapBakeRun.Read(output, "bare").Mesh.TexCoords.ToArray();

        Assert.All(laid, static uv => Assert.InRange(uv.X, 0.25f, 0.5f));
        Assert.True(bare.Max(static uv => uv.X) > 0.5f);
    }
}
