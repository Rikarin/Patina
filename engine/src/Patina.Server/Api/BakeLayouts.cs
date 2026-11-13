using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     The UV set a bake of a texture set lands in, when the request leaves it to the set (#210), and the shape it
///     bakes at, when the request follows the set's (#253): a client that bakes a project's sets need not open each one
///     to ask. An open set is read as its last edit left it, saved or not, as the client that baked it shows it.
/// </summary>
static class BakeLayouts {
    /// <summary>
    ///     The request with its UV set said: the one asked for, the texture set's own, or the first; and, when it
    ///     follows the set's shape and there is a set, with the height that keeps that shape at its width.
    /// </summary>
    public static BakeRequest Resolved(OpenLibrary? library, BakeRequest request, StackSessions? stacks = null) {
        var follow = request.Options.FollowSetShape ?? true;
        var file = library is not null && (request.UvSet is null || follow)
            ? DocumentOf(library, request.Low, request.Material)
            : null;
        var set = file is null ? null : LayoutOf(file, request.Material, stacks);
        var options = request.Options;
        if (follow && set is { } found) {
            options = options with { Rows = BakeFit.RowsAt(options.Resolution, found.Width, found.Height) };
        }

        // Followed or not, the choice is spent: the key holds the height it made, so one shape is one bake.
        return request with {
            UvSet = request.UvSet ?? set?.UvSet ?? 0, Options = options with { FollowSetShape = null }
        };
    }

    /// <summary>
    ///     The file of the texture set on this material of the project that holds the mesh, or null. A mesh of one
    ///     material has one document, which names no material, and a bake of it names none either: that document is
    ///     found by the mesh it is on.
    /// </summary>
    static string? DocumentOf(OpenLibrary library, string low, string? material) {
        var root = Path.GetFullPath(Path.Combine(library.RootPath, LibraryLayout.ProjectsFolder));
        for (var folder = Path.GetDirectoryName(Path.GetFullPath(low));
             folder is not null && folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
             folder = Path.GetDirectoryName(folder)) {
            if (!File.Exists(Path.Combine(folder, LibraryLayout.ProjectFile))) {
                continue;
            }

            return FileOf(library, library.Contents(LibraryLayout.Relative(root, folder)), low, material);
        }

        return null;
    }

    /// <summary>The file of the project's texture set on the material, or on the mesh when none is named.</summary>
    static string? FileOf(OpenLibrary library, ProjectContents contents, string low, string? material) {
        var below = LibraryLayout.Relative(Path.GetFullPath(library.RootPath), Path.GetFullPath(low));
        var mesh = contents.Meshes.FirstOrDefault(m => m.Path == below)?.Id;
        var set = (material is null ? null : contents.Documents.FirstOrDefault(d => d.Material == material))
            ?? contents.Documents.FirstOrDefault(d =>
                d.Kind == DocumentKinds.Texturing && d.Material is null && mesh is not null && d.Mesh == mesh
            );
        return set is null ? null : Path.GetFullPath(LibraryLayout.Full(library.RootPath, set.Path));
    }

    /// <summary>
    ///     What a bake reads of a texture set: its UV set and the shape it is authored at, from its open session when
    ///     it has one, so a Height changed and not yet saved bakes at the shape on screen, and from its file otherwise.
    /// </summary>
    static BakeFit.SetLayout? LayoutOf(string file, string? material, StackSessions? stacks) {
        if (stacks?.All.FirstOrDefault(open => string.Equals(open.Path, file, StringComparison.Ordinal)) is { } open) {
            return open.Layout;
        }

        var stack = StackDocuments.Parse(SharedFile.ReadAllText(file));
        var own = (material is null ? null : stack.SetNamed(material)) ?? stack.Sets.FirstOrDefault();
        return own is null ? null : new BakeFit.SetLayout(own.UvSet, stack.BaseWidth, stack.BaseHeight);
    }
}
