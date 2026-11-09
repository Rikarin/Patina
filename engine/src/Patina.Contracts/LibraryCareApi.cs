namespace Patina.Contracts;

/// <summary>The body of <c>PATCH /api/v1/library/project/document/name</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Document">The document.</param>
/// <param name="Name">Its new display name. Its file stays.</param>
public sealed record ProjectDocumentRenameRequest(string Project, Guid Document, string Name);

/// <summary>The body of <c>PATCH /api/v1/library/project/tags</c>.</summary>
/// <param name="Path">The project, below <c>projects/</c>.</param>
/// <param name="Tags">Every tag it has from now on. Repeats and empty ones are dropped.</param>
public sealed record ProjectTagsRequest(string Path, IReadOnlyList<string> Tags);

/// <summary>The body of <c>PATCH /api/v1/library/project/document/tags</c>.</summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Document">The document.</param>
/// <param name="Tags">Every tag it has from now on. Repeats and empty ones are dropped.</param>
public sealed record ProjectDocumentTagsRequest(string Project, Guid Document, IReadOnlyList<string> Tags);

/// <summary>The body of <c>PATCH /api/v1/library/project/reference</c>.</summary>
/// <param name="Project">The project that refers to the asset, below <c>projects/</c>.</param>
/// <param name="Asset">The asset it refers to itself.</param>
/// <param name="Pinned">True holds it at the bytes the asset has now; false lets it follow the asset again.</param>
public sealed record ProjectReferenceRequest(string Project, Guid Asset, bool Pinned);

/// <summary>A project's reference to an asset, as it now stands.</summary>
/// <param name="Asset">The asset.</param>
/// <param name="Pinned">Whether a pin holds it.</param>
/// <param name="Revision">The bytes it is pinned at, or null when it follows the asset.</param>
public sealed record ProjectReference(Guid Asset, bool Pinned, string? Revision);

/// <summary>Something in the library's bin, which can be put back or deleted for good.</summary>
/// <param name="Id">The entry, which the bin routes take.</param>
/// <param name="Kind"><c>project</c> or <c>document</c>.</param>
/// <param name="Name">Its display name when it was deleted.</param>
/// <param name="From">Where it lived, below the library, and where restoring puts it.</param>
/// <param name="Deleted">When it was deleted.</param>
public sealed record BinItem(string Id, string Kind, string Name, string From, DateTimeOffset Deleted);

/// <summary>What the bin holds.</summary>
/// <param name="Items">Its entries, the latest first.</param>
/// <param name="Bytes">The room they take on disk.</param>
public sealed record BinList(IReadOnlyList<BinItem> Items, long Bytes);

/// <summary>The body of <c>POST /api/v1/library/bin/restore</c>.</summary>
/// <param name="Id">The entry.</param>
public sealed record BinRestoreRequest(string Id);

/// <summary>Where something put back from the bin now is.</summary>
/// <param name="Path">Below the library: a project's folder, or a document's file.</param>
public sealed record BinRestored(string Path);

/// <summary>What emptying the bin deleted.</summary>
/// <param name="Count">How many entries went for good.</param>
public sealed record BinEmptied(int Count);

/// <summary>The library's cache: what deleting it would cost is time, and nothing else.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Bytes">The room it takes on disk.</param>
public sealed record CacheInfo(string Path, long Bytes);

/// <summary>
///     The body of <c>POST /api/v1/library/thumbnails</c>. A thumbnail service for every client and every list: the
///     Library's grid, the shelf, and anything after them.
/// </summary>
/// <param name="Sources">
///     What to draw, each as its list names it: a document below the library (a graph, an image material, a layered
///     material or a texturing document), a project's folder below the library (<c>projects/…</c>, drawn as the
///     document that best stands for it), or a material the shelf offers from Patina itself.
/// </param>
public sealed record ThumbnailRequest(IReadOnlyList<string> Sources);

/// <summary>A picture in the preview cache. The pixels are at <c>/previews/{hash}</c>, never in JSON.</summary>
/// <param name="Source">What was asked for.</param>
/// <param name="Hash">The picture, or null when there is nothing to show.</param>
public sealed record Thumbnail(string Source, string? Hash);

/// <summary>Pictures of what was asked for.</summary>
/// <param name="Items">Them, in the order they were asked for.</param>
public sealed record ThumbnailList(IReadOnlyList<Thumbnail> Items);

/// <summary>
///     The Server's settings that belong to the person and not to a library or project, kept in a plain file beside
///     the discovery file, which anyone can read and edit.
/// </summary>
/// <param name="Autosave">How unsaved work is kept.</param>
public sealed record ServerPreferences(AutosavePreferences Autosave);

/// <summary>How unsaved work is kept. It goes to the library's cache, never over a document.</summary>
/// <param name="IntervalSeconds">How often open, unsaved documents are kept, at the most. Zero turns autosave off.</param>
/// <param name="IdleSeconds">How long after the last edit a document is kept, without waiting for the interval. Zero waits.</param>
/// <param name="Keep">How many autosaves of a document are kept, the latest first. Restoring takes the latest.</param>
public sealed record AutosavePreferences(int IntervalSeconds = 60, int IdleSeconds = 5, int Keep = 3);
