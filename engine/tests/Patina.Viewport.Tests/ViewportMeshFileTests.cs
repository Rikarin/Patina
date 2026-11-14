namespace Patina.Viewport.Tests;

/// <summary>A model the viewport draws reads the files it names beside itself (#231).</summary>
public sealed class ViewportMeshFileTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-mesh-files-");

    public void Dispose() => folder.Delete(true);

    /// <summary>An OBJ's parts are drawn in the colours of its material library, not Assimp's default.</summary>
    [Fact]
    public void An_objs_parts_take_their_colours_from_its_material_library() {
        File.WriteAllText(Path.Combine(folder.FullName, "paint.mtl"), "newmtl Red\nKd 1 0 0\n");
        var path = Path.Combine(folder.FullName, "quad.obj");
        File.WriteAllLines(
            path,
            [
                "mtllib paint.mtl", "v 0 0 0", "v 1 0 0", "v 1 1 0", "vt 0 0", "vt 1 0", "vt 1 1", "usemtl Red",
                "f 1/1 2/2 3/3"
            ]
        );

        var mesh = ViewportMesh.Read(path);

        var red = Assert.Single(mesh.Materials, static material => material.Name == "Red");
        Assert.Equal(1f, red.Factors.BaseColor.X);
        Assert.Equal(0f, red.Factors.BaseColor.Y);
    }

    /// <summary>
    ///     A file's vertex colours reach every vertex the viewport draws (#146), tangents made or not, and a file with
    ///     none draws white.
    /// </summary>
    [Fact]
    public void A_meshs_vertex_colours_reach_each_vertex_and_a_mesh_without_them_is_white() {
        var path = Path.Combine(folder.FullName, "painted.ply");
        File.WriteAllText(
            path,
            "ply\nformat ascii 1.0\nelement vertex 3\nproperty float x\nproperty float y\nproperty float z\n"
            + "property float s\nproperty float t\nproperty uchar red\nproperty uchar green\nproperty uchar blue\n"
            + "element face 1\nproperty list uchar int vertex_indices\nend_header\n"
            + "0 0 0 0 0 255 0 0\n1 0 0 1 0 0 255 0\n0 1 0 0 1 0 0 255\n3 0 1 2\n"
        );
        var bare = Path.Combine(folder.FullName, "bare.obj");
        File.WriteAllLines(bare, ["v 0 0 0", "v 1 0 0", "v 0 1 0", "f 1 2 3"]);

        static HashSet<(float, float, float)> Colours(ViewportMesh mesh) => [
            ..Enumerable.Range(0, mesh.Vertices.Length / ViewportMesh.FloatsPerVertex)
                .Select(vertex => vertex * ViewportMesh.FloatsPerVertex + 12)
                .Select(at => (MathF.Round(mesh.Vertices[at], 2), MathF.Round(mesh.Vertices[at + 1], 2),
                    MathF.Round(mesh.Vertices[at + 2], 2)))
        ];

        var painted = ViewportMesh.Read(path);

        HashSet<(float, float, float)> primaries = [(1f, 0f, 0f), (0f, 1f, 0f), (0f, 0f, 1f)];
        Assert.Equal(primaries, Colours(painted));
        Assert.Equal(primaries, Colours(painted.WithTangents()));
        Assert.Equal([(1f, 1f, 1f)], Colours(ViewportMesh.Read(bare)));
    }
}
