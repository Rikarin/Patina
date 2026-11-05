namespace Patina.Contracts;

/// <summary>
///     A high-to-low bake. Everything is a path on the Server's machine, and the results are files there: a client
///     sends paths and gets paths, whichever client it is (plan §8).
/// </summary>
/// <param name="High">The detailed mesh.</param>
/// <param name="Low">The mesh baked onto, which must have texture coordinates.</param>
/// <param name="Output">The folder the maps are written to.</param>
/// <param name="Cage">A mesh with the low mesh's faces, moved outward, which the rays start from.</param>
/// <param name="Resolution">The atlas's edge, in texels.</param>
/// <param name="Maps">
///     What to bake beyond <c>normal</c> and <c>height</c>, which are always baked: <c>ao</c>, <c>bent</c>,
///     <c>curvature</c>, <c>thickness</c>, <c>position</c>, <c>world</c>, <c>id</c>.
/// </param>
/// <param name="Format"><c>png8</c>, <c>png16</c>, <c>exr16</c> or <c>exr32</c>.</param>
/// <param name="Supersampling">How many samples along each side of a texel.</param>
/// <param name="OcclusionRays">How many rays a texel's occlusion is estimated with.</param>
/// <param name="SearchRadius">How far a ray looks, as a fraction of the high mesh's diagonal.</param>
/// <param name="OcclusionRadius">How far an occluder counts, as a fraction of the diagonal.</param>
/// <param name="Skew">How far rays lean from the smoothed direction towards the low mesh's own normal, 0 to 1.</param>
/// <param name="SkewMap">A grey image saying where to apply the skew, or null for everywhere.</param>
/// <param name="MatchByName">Whether a part of the low mesh only strikes the high part of the same name.</param>
/// <param name="Gutter">How many texels of dilation surround each chart.</param>
/// <param name="Space"><c>tangent</c> or <c>object</c>.</param>
/// <param name="Name">The stem of each file's name, or null for the low mesh's.</param>
public sealed record BakeRequest(
    string High,
    string Low,
    string Output,
    string? Cage = null,
    int Resolution = 2048,
    IReadOnlyList<string>? Maps = null,
    string Format = "png16",
    int Supersampling = 1,
    int OcclusionRays = 64,
    double SearchRadius = 0.05,
    double OcclusionRadius = 0.5,
    double Skew = 0,
    string? SkewMap = null,
    bool MatchByName = false,
    int Gutter = 4,
    string Space = "tangent",
    string? Name = null
);

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
/// <param name="Id">Its identity.</param>
/// <param name="Key">The build key of what it makes.</param>
/// <param name="State">One of <see cref="JobStates" />.</param>
/// <param name="Progress">How far through it is, 0 to 1.</param>
/// <param name="Attempts">How many Workers have started it.</param>
/// <param name="Created">When it was asked for, in UTC.</param>
/// <param name="Bake">What to do.</param>
/// <param name="Result">What it made, once done.</param>
/// <param name="Error">Why it failed.</param>
/// <param name="CancelRequested">Whether a client has asked for it to stop.</param>
public sealed record JobState(
    string Id,
    string Key,
    string State,
    double Progress,
    int Attempts,
    DateTimeOffset Created,
    BakeRequest Bake,
    BakeResult? Result = null,
    string? Error = null,
    bool CancelRequested = false
);

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
