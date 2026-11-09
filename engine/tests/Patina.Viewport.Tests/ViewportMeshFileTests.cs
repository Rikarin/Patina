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
}
