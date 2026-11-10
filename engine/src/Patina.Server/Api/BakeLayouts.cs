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

        var own = library is not null && request.Material is { } material ? Of(library, request.Low, material) : null;
        return request with { UvSet = own ?? 0 };
    }

    /// <summary>The UV set of the texture set on this material of the project that holds the mesh, or null.</summary>
    static int? Of(OpenLibrary library, string low, string material) {
        var root = Path.GetFullPath(Path.Combine(library.RootPath, LibraryLayout.ProjectsFolder));
        for (var folder = Path.GetDirectoryName(Path.GetFullPath(low));
             folder is not null && folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
             folder = Path.GetDirectoryName(folder)) {
            if (!File.Exists(Path.Combine(folder, LibraryLayout.ProjectFile))) {
                continue;
            }

            var project = LibraryLayout.Relative(root, folder);
            var set = library.Contents(project).Documents.FirstOrDefault(d => d.Material == material);
            if (set is null) {
                return null;
            }

            var stack = StackDocuments.Parse(SharedFile.ReadAllText(LibraryLayout.Full(library.RootPath, set.Path)));
            return (stack.SetNamed(material) ?? stack.Sets.FirstOrDefault())?.UvSet;
        }

        return null;
    }
}
