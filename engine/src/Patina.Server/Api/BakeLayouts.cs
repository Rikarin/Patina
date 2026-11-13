using Patina.Contracts;
using Patina.Library;
using Patina.Server.Stacks;

namespace Patina.Server.Api;

/// <summary>
///     The UV set a bake of a texture set lands in, when the request leaves it to the set (#210), and the shape it
///     bakes at, when the request follows the set's (#253): a client that bakes a project's sets need not open each one
///     to ask.
/// </summary>
static class BakeLayouts {
    /// <summary>
    ///     The request with its UV set said: the one asked for, the texture set's own, or the first; and, when it
    ///     follows the set's shape and there is a set, with the height that keeps that shape at its width.
    /// </summary>
    public static BakeRequest Resolved(OpenLibrary? library, BakeRequest request) {
        var follow = request.Options.FollowSetShape ?? true;
        var set = library is not null && (request.UvSet is null || follow)
            ? Of(library, request.Low, request.Material)
            : null;
        var options = request.Options;
        if (follow && set is { } found) {
            options = options with { Rows = RowsOf(options.Resolution, found.Width, found.Height) };
        }

        // Followed or not, the choice is spent: the key holds the height it made, so one shape is one bake.
        return request with {
            UvSet = request.UvSet ?? set?.UvSet ?? 0, Options = options with { FollowSetShape = null }
        };
    }

    /// <summary>
    ///     The height of a bake at a width that keeps a set's shape: none, a square, when the set is square, and
    ///     within what a bake takes otherwise.
    /// </summary>
    internal static int? RowsOf(int resolution, int width, int height) {
        if (width <= 0 || height <= 0 || width == height) {
            return null;
        }

        var rows = Math.Clamp((int)Math.Round((double)resolution * height / width), 16, 16384);
        return rows == resolution ? null : rows;
    }

    /// <summary>What a bake reads of a texture set: its UV set and the shape it is authored at.</summary>
    readonly record struct SetLayout(int UvSet, int Width, int Height);

    /// <summary>
    ///     The layout of the texture set on this material of the project that holds the mesh, or null. A mesh of one
    ///     material has one document, which names no material, and a bake of it names none either: that document is
    ///     found by the mesh it is on.
    /// </summary>
    static SetLayout? Of(OpenLibrary library, string low, string? material) {
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

    /// <summary>The layout of the project's texture set on the material, or on the mesh when none is named.</summary>
    static SetLayout? SetOf(OpenLibrary library, ProjectContents contents, string low, string? material) {
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
        return ((material is null ? null : stack.SetNamed(material)) ?? stack.Sets.FirstOrDefault()) is { } own
            ? new SetLayout(own.UvSet, stack.BaseWidth, stack.BaseHeight)
            : null;
    }
}
