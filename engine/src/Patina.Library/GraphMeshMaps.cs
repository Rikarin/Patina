using Patina.Contracts;

namespace Patina.Library;

/// <summary>Which bake a graph's Mesh Map nodes read: a one-line file beside it, which outlives a session.</summary>
public static class GraphMeshMaps {
    /// <summary>The file beside a graph that says which bake it reads.</summary>
    public static string FileOf(string graph) => $"{graph}.bake";

    /// <summary>The folder of the bake a graph file reads, or null when none has been chosen.</summary>
    public static string? Read(string graph) {
        var file = FileOf(graph);
        return File.Exists(file)
            ? Path.GetFullPath(SharedFile.ReadAllText(file).Trim(), Path.GetDirectoryName(Path.GetFullPath(graph))!)
            : null;
    }
}
