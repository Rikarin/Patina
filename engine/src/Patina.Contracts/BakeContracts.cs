namespace Patina.Contracts;

/// <summary>
///     A high-to-low bake. Everything is a path on the Server's machine, and the results are files there: a client
///     sends paths and gets paths, whichever client it is (plan §8).
/// </summary>
public sealed record BakeRequest {
    /// <summary>The detailed mesh.</summary>
    public required string High { get; init; }

    /// <summary>The mesh baked onto, which must have texture coordinates.</summary>
    public required string Low { get; init; }

    /// <summary>The folder the maps are written to.</summary>
    public required string Output { get; init; }

    /// <summary>A mesh with the low mesh's faces, moved outward, which the rays start from.</summary>
    public string? Cage { get; init; }

    /// <summary>The atlas's edge, in texels.</summary>
    public int Resolution { get; init; } = 2048;

    /// <summary>
    ///     What to bake beyond <c>normal</c> and <c>height</c>, which are always baked: <c>ao</c>, <c>bent</c>,
    ///     <c>curvature</c>, <c>thickness</c>, <c>position</c>, <c>world</c>, <c>id</c>.
    /// </summary>
    public IReadOnlyList<string>? Maps { get; init; }

    /// <summary><c>png8</c>, <c>png16</c>, <c>exr16</c> or <c>exr32</c>.</summary>
    public string Format { get; init; } = "png16";

    /// <summary>How many samples along each side of a texel.</summary>
    public int Supersampling { get; init; } = 1;

    /// <summary>How many rays a texel's occlusion is estimated with.</summary>
    public int OcclusionRays { get; init; } = 64;

    /// <summary>How far a ray looks, as a fraction of the high mesh's diagonal.</summary>
    public double SearchRadius { get; init; } = 0.05;

    /// <summary>How far an occluder counts, as a fraction of the diagonal.</summary>
    public double OcclusionRadius { get; init; } = 0.5;

    /// <summary>How far rays lean from the smoothed direction towards the low mesh's own normal, 0 to 1.</summary>
    public double Skew { get; init; }

    /// <summary>A grey image saying where to apply the skew, or null for everywhere.</summary>
    public string? SkewMap { get; init; }

    /// <summary>Whether a part of the low mesh only strikes the high part of the same name.</summary>
    public bool MatchByName { get; init; }

    /// <summary>How many texels of dilation surround each chart.</summary>
    public int Gutter { get; init; } = 4;

    /// <summary><c>tangent</c> or <c>object</c>.</summary>
    public string Space { get; init; } = "tangent";

    /// <summary>The stem of each file's name, or null for the low mesh's.</summary>
    public string? Name { get; init; }
}

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
);

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

    /// <summary>What to do.</summary>
    public required BakeRequest Bake { get; init; }

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
public sealed record BakeManifest(string Name, string Format, BakeResult Result);

/// <summary>Says which folder of baked maps a graph's Mesh Map nodes read.</summary>
/// <param name="Folder">A folder holding a bake's files, or null to read none.</param>
public sealed record GraphMeshMapsRequest(string? Folder);
