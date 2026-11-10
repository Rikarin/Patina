using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     The UV set a bake of a texture set lands in, when the request leaves it to the set (#210): a client that bakes a
///     project's sets need not open each one to ask.
/// </summary>
static class BakeLayouts {
    /// <summary>The request with its UV set said: the one asked for, the texture set's own, or the first.</summary>
    public static BakeRequest Resolved(OpenLibrary? library, BakeRequest request) {
        if (request.UvSet is not null) {
            return request;
        }

        var own = library is not null ? Of(library, request.Low, request.Material) : null;
        return request with { UvSet = own ?? 0 };
    }

    /// <summary>
    ///     The UV set of the texture set on this material of the project that holds the mesh, or null. A mesh of one
    ///     material has one document, which names no material, and a bake of it names none either: that document is
    ///     found by the mesh it is on.
    /// </summary>
    static int? Of(OpenLibrary library, string low, string? material) {
        var root = Path.GetFullPath(Path.Combine(library.RootPath, LibraryLayout.ProjectsFolder));
        for (var folder = Path.GetDirectoryName(Path.GetFullPath(low));
             folder is not null && folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
             folder = Path.GetDirectoryName(folder)) {
            if (!File.Exists(Path.Combine(folder, LibraryLayout.ProjectFile))) {
                continue;
            }

            return SetOf(library, library.Contents(LibraryLayout.Relative(root, folder)), low, material);
        }

        return null;
    }

    /// <summary>The UV set of the project's texture set on the material, or on the mesh when none is named.</summary>
    static int? SetOf(OpenLibrary library, ProjectContents contents, string low, string? material) {
        var below = LibraryLayout.Relative(Path.GetFullPath(library.RootPath), Path.GetFullPath(low));
        var mesh = contents.Meshes.FirstOrDefault(m => m.Path == below)?.Id;
        var set = (material is null ? null : contents.Documents.FirstOrDefault(d => d.Material == material))
            ?? contents.Documents.FirstOrDefault(d =>
                d.Kind == DocumentKinds.Texturing && d.Material is null && mesh is not null && d.Mesh == mesh
            );
        if (set is null) {
            return null;
        }

        var stack = StackDocuments.Parse(SharedFile.ReadAllText(LibraryLayout.Full(library.RootPath, set.Path)));
        return ((material is null ? null : stack.SetNamed(material)) ?? stack.Sets.FirstOrDefault())?.UvSet;
    }
}
