namespace Patina.Contracts;

/// <summary>One baked map, as a file.</summary>
/// <param name="Map">What it measures.</param>
/// <param name="Path">The file.</param>
/// <param name="Resolution">Its edge in pixels.</param>
/// <param name="Scale">
///     What a stored unit value is multiplied by to get the measurement back, for a signed map in an integer format;
///     zero when there is nothing to undo.
/// </param>
public sealed record BakedMapFile(string Map, string Path, int Resolution, double Scale);

/// <summary>What a finished bake made.</summary>
/// <param name="Maps">The files.</param>
/// <param name="Covered">How many texels held chart content.</param>
/// <param name="Missed">How many found no source and took the closest point instead.</param>
/// <param name="Warnings">What could not be baked as asked.</param>
/// <param name="Cached">Whether this was found in the cache and nothing ran.</param>
public sealed record BakeResult(
    IReadOnlyList<BakedMapFile> Maps,
    long Covered,
    long Missed,
    IReadOnlyList<string> Warnings,
    bool Cached
) {
    /// <summary>What the bake read and found, line by line: the meshes, each part's match, the time it took.</summary>
    public IReadOnlyList<string> Log { get; init; } = [];
}

/// <summary>Where a job stands.</summary>
public static class JobStates {
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>A piece of work the Server owns, which outlives the Worker running it and the Server itself.</summary>
public sealed record JobState {
    /// <summary>Its identity.</summary>
    public required string Id { get; init; }

    /// <summary>The build key of what it makes.</summary>
    public required string Key { get; init; }

    /// <summary>One of <see cref="JobStates" />.</summary>
    public required string State { get; init; }

    /// <summary>How far through it is, 0 to 1.</summary>
    public double Progress { get; init; }

    /// <summary>How many Workers have started it.</summary>
    public int Attempts { get; init; }

    /// <summary>When it was asked for, in UTC.</summary>
    public required DateTimeOffset Created { get; init; }

    /// <summary>The bake to make, when this job is a bake.</summary>
    public BakeRequest? Bake { get; init; }

    /// <summary>The build to run, when this job is a build.</summary>
    public BuildRequest? Build { get; init; }

    /// <summary>What a build has done so far, or did.</summary>
    public LibraryReport? Report { get; init; }

    /// <summary>What it made, once done.</summary>
    public BakeResult? Result { get; init; }

    /// <summary>Why it failed.</summary>
    public string? Error { get; init; }

    /// <summary>Whether a client has asked for it to stop.</summary>
    public bool CancelRequested { get; init; }
}

/// <summary>A job changing. Numbered, so a client that dropped its socket can ask what it missed.</summary>
/// <param name="Sequence">The number, rising with each event.</param>
/// <param name="Job">The job as it now stands.</param>
public sealed record JobEvent(long Sequence, JobState Job);

/// <summary>Every job the Server remembers, and the sequence a client can follow from.</summary>
/// <param name="Jobs">Newest first.</param>
/// <param name="Sequence">The newest event's number.</param>
public sealed record JobPage(IReadOnlyList<JobState> Jobs, long Sequence);

/// <summary>The file a finished bake leaves beside its maps, which is how a reader undoes their encoding.</summary>
/// <param name="Name">The stem of the files' names.</param>
/// <param name="Format">The format they are in.</param>
/// <param name="Result">The maps, with paths relative to this file's folder.</param>
/// <param name="Mesh">The digest of the mesh baked onto, which says when these maps went out of date.</param>
public sealed record BakeManifest(string Name, string Format, BakeResult Result, string? Mesh = null);

/// <summary>Says which folder of baked maps a graph's Mesh Map nodes read.</summary>
/// <param name="Folder">A folder holding a bake's files, or null to read none.</param>
public sealed record GraphMeshMapsRequest(string? Folder);
