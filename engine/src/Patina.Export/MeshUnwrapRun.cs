using Vixen.Geometry.Uv;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>Gives a mesh a UV layout of its own, in a new file beside it.</summary>
public static class MeshUnwrapRun {
    /// <summary>The atlas side its margins are counted against, which a map of another size only rescales.</summary>
    const int Resolution = 2048;

    /// <summary>Unwraps a mesh that has no layout into one atlas. The mesh it came from is not touched.</summary>
    /// <param name="source">The mesh file.</param>
    /// <param name="output">Where the unwrapped mesh goes, ending in a format a model can be written to.</param>
    /// <returns>Whether it needed one. A mesh that has a layout keeps it, and nothing is written.</returns>
    /// <exception cref="ModelFormatException">It is not a readable model.</exception>
    /// <exception cref="InvalidOperationException">Its islands could not be fitted.</exception>
    public static bool Run(string source, string output) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);

        var mesh = MeshMapBakeRun.Read(source).Mesh;
        if (mesh.TexCoords.Length == mesh.CornerCount) {
            return false;
        }

        UvUnwrap.All(mesh, new(), new() { Resolution = Resolution }, out var coordinates);

        // ⚠ The extension says what to write, and half a file must never look like a mesh a project could list.
        var name = Path.GetFileNameWithoutExtension(output);
        var part = Path.Combine(
            Path.GetDirectoryName(output) ?? string.Empty,
            $"{name}.{Guid.NewGuid():N}{Path.GetExtension(output)}"
        );
        try {
            ModelWriter.Write(part, [new PolygonMesh { Mesh = mesh, Name = name, TexCoords = coordinates }], name);
            File.Move(part, output, true);
        } finally {
            File.Delete(part);
        }

        return true;
    }
}
