namespace Patina.Library;

/// <summary>A library's documents as its index has them, not read from each project's folder.</summary>
public static class LibraryIndexReads {
    /// <summary>
    ///     Every project's documents but its texturing ones, as the last <see cref="OpenLibrary.Rescan" /> found them:
    ///     one query, where reading each project's contents would read its file and walk its folder (#311).
    /// </summary>
    /// <param name="library">The open library.</param>
    public static IReadOnlyList<IndexedDocument> Documents(this OpenLibrary library) {
        ArgumentNullException.ThrowIfNull(library);
        return library.Queries.Documents();
    }
}
