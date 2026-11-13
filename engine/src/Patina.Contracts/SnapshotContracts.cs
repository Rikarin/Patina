namespace Patina.Contracts;

/// <summary>A named checkpoint of a document: its file and the paint it names, as they were when it was taken (#209).</summary>
/// <param name="Id">Which snapshot, for a restore or a delete.</param>
/// <param name="Name">What the person called it.</param>
/// <param name="Taken">When it was taken.</param>
/// <param name="Files">How many files it holds: the document and its paint.</param>
/// <param name="Bytes">How large those files are together, before any is shared with another snapshot.</param>
public sealed record DocumentSnapshot(Guid Id, string Name, DateTimeOffset Taken, int Files, long Bytes);

/// <summary>A document's snapshots, newest first.</summary>
/// <param name="Path">The document, below the library.</param>
/// <param name="Items">Its snapshots, newest first.</param>
public sealed record DocumentSnapshotList(string Path, IReadOnlyList<DocumentSnapshot> Items);

/// <summary>Takes a snapshot of a document as its file stands.</summary>
/// <param name="Path">The document, below the library.</param>
/// <param name="Name">What to call it. Empty gives it a name by number.</param>
public sealed record SnapshotTakeRequest(string Path, string? Name);

/// <summary>Puts a document back as a snapshot holds it, or forgets a snapshot.</summary>
/// <param name="Path">The document, below the library.</param>
/// <param name="Id">The snapshot.</param>
public sealed record SnapshotRequest(string Path, Guid Id);
