using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     Reads the UDIM tile a texture set's layout sits on, which needs a mesh reader the library does not have.
/// </summary>
/// <param name="mesh">The mesh file, absolute.</param>
/// <param name="material">The set's material, or null for the whole mesh.</param>
/// <param name="uvSet">The UV set it is laid out in.</param>
/// <returns>The tile: 1001 for one on the first, on several, or one that cannot be read.</returns>
public delegate int TileReader(string mesh, string? material, int uvSet);

/// <summary>
///     Each texturing document's UDIM tile in a project's contents (#289), so a client can show every set's tile and
///     not only the open one's.
/// </summary>
public static class ProjectTiles {
    /// <summary>The contents with each texturing document on a mesh given its tile.</summary>
    /// <param name="root">The library's folder.</param>
    /// <param name="contents">The project's contents, with paths below the library.</param>
    /// <param name="read">What reads a set's tile from its mesh.</param>
    public static ProjectContents With(string root, ProjectContents contents, TileReader read) {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(read);
        if (!contents.Documents.Any(static d => d is { Kind: DocumentKinds.Texturing, Mesh: not null })) {
            return contents;
        }

        return contents with { Documents = [..contents.Documents.Select(d => Tiled(root, contents, d, read))] };
    }

    static ProjectDocumentInfo Tiled(
        string root,
        ProjectContents contents,
        ProjectDocumentInfo document,
        TileReader read
    ) {
        if (document is not { Kind: DocumentKinds.Texturing, Mesh: { } id }
            || contents.Meshes.FirstOrDefault(m => m.Id == id) is not { } mesh) {
            return document;
        }

        var file = LibraryLayout.Full(root, document.Path);
        var text = File.Exists(file) ? SharedFile.ReadAllText(file) : string.Empty;
        var uvSet = StackInputs.LayoutOf(text, document.Material).UvSet;
        return document with { Udim = read(LibraryLayout.Full(root, mesh.Path), document.Material, uvSet) };
    }
}
