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
