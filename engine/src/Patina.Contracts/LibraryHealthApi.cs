namespace Patina.Contracts;

/// <summary>What one kind of finding of a library check came to, as <c>patina library check</c> prints it.</summary>
/// <param name="Kind">What was found, such as <c>MissingReference</c> or <c>StaleOutput</c>.</param>
/// <param name="IsError">Whether the library is wrong, as opposed to having work to do or room to tidy.</param>
/// <param name="Count">How many there are in all.</param>
/// <param name="Findings">The first of them, at most <see cref="LibraryHealth.FindingsPerKind" />.</param>
public sealed record LibraryFindingGroup(string Kind, bool IsError, int Count, IReadOnlyList<LibraryFinding> Findings);

/// <summary>One thing a library check found.</summary>
/// <param name="Path">The file or project it concerns, relative to the library.</param>
/// <param name="Detail">The specifics.</param>
public sealed record LibraryFinding(string Path, string Detail);

/// <summary>The health of a library, from <c>POST /api/v1/library/check</c>.</summary>
/// <param name="Projects">The projects checked.</param>
/// <param name="Assets">The shared assets checked.</param>
/// <param name="Outputs">The outputs checked.</param>
/// <param name="Errors">How many findings say the library is wrong.</param>
/// <param name="Groups">The findings by kind, errors first.</param>
public sealed record LibraryHealth(
    int Projects,
    int Assets,
    int Outputs,
    int Errors,
    IReadOnlyList<LibraryFindingGroup> Groups
) {
    /// <summary>Findings of one kind sent in full: a broken library can have ten thousand of each.</summary>
    public const int FindingsPerKind = 50;
}

/// <summary>The body of <c>POST /api/v1/library/cache/prune</c>.</summary>
/// <param name="Days">How long a built object or report may go untouched before it goes, 0 or more.</param>
public sealed record CachePruneRequest(int Days);

/// <summary>What tidying the library took away.</summary>
/// <param name="Removed">How many entries or folders went.</param>
/// <param name="Bytes">The room that freed on disk.</param>
public sealed record LibraryTidied(int Removed, long Bytes);

/// <summary>What <c>POST /api/v1/library/clean</c> took away (#162).</summary>
/// <param name="Removed">How many folders of copies of moved paint were deleted.</param>
/// <param name="Bytes">The room those freed on disk.</param>
/// <param name="Pictures">How many pictures nothing read went to the bin.</param>
/// <param name="PictureBytes">The room those take, which emptying the bin frees.</param>
/// <param name="Bin">The bin's entry that holds those pictures, for a Restore, or null when none went.</param>
public sealed record LibraryCleaned(int Removed, long Bytes, int Pictures, long PictureBytes, string? Bin);

/// <summary>A file of the library and the room it takes.</summary>
/// <param name="Path">Its path below the library, with forward slashes.</param>
/// <param name="Bytes">Its size.</param>
public sealed record UnusedFile(string Path, long Bytes);

/// <summary>What <c>GET /api/v1/library/uploads/unused</c> answers (#297).</summary>
/// <param name="Files">The files sent from another computer that nothing names, in order of their path.</param>
/// <param name="Bytes">The room they take.</param>
public sealed record UnusedUploadList(IReadOnlyList<UnusedFile> Files, long Bytes);

/// <summary>The body of <c>POST /api/v1/library/uploads/clear</c> (#301).</summary>
/// <param name="Held">
///     Paths on the Server's machine, or any text naming them, that the client holds where no file of the library
///     does yet, such as an open dialog's unsaved settings. An upload any of them names counts as used.
/// </param>
public sealed record UploadsClearRequest(IReadOnlyList<string>? Held);

/// <summary>What <c>POST /api/v1/library/uploads/clear</c> took away (#297).</summary>
/// <param name="Count">How many uploads nothing named went to the bin.</param>
/// <param name="Bytes">The room those take, which emptying the bin frees.</param>
/// <param name="Bin">The bin's entry that holds them, for a Restore, or null when none went.</param>
public sealed record UploadsCleared(int Count, long Bytes, string? Bin);
