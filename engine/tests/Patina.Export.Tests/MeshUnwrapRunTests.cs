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

        Assert.True(MeshUnwrapRun.Run(source, output));

        var mesh = MeshMapBakeRun.Read(output).Mesh;
        Assert.True(mesh.FaceCount > 0);
        Assert.Equal(mesh.CornerCount, mesh.TexCoords.Length);
        Assert.Contains(mesh.TexCoords.ToArray(), static uv => uv.X > 0 || uv.Y > 0);
        Assert.Equal(Cube, File.ReadAllText(source));
        Assert.False(MeshUnwrapRun.Run(output, Path.Combine(folder, "again.glb")));
        Assert.Equal(
            ["cube.obj", "cube_unwrapped.glb"],
            Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal)
        );
    }
}
