namespace Patina.Contracts;

/// <summary>The reply to <c>GET /api/v1/system</c>.</summary>
/// <param name="Version">The Server's release version.</param>
/// <param name="ApiVersion">The major version of the REST API; it is the <c>v1</c> in the URL.</param>
/// <param name="Library">The library this Server owns, or null when it was started without one.</param>
/// <param name="Host">The machine and runtime the Server runs on.</param>
/// <param name="Worker">The Worker process as the Server sees it.</param>
/// <param name="TextureSides">The texture sides a client offers, smallest first.</param>
/// <param name="DefaultTextureSide">The side a new document gets when nobody says.</param>
public sealed record SystemInfo(
    string Version,
    int ApiVersion,
    string? Library,
    HostInfo Host,
    WorkerStatus Worker,
    IReadOnlyList<int> TextureSides,
    int DefaultTextureSide
);

/// <summary>The machine and runtime a process runs on.</summary>
/// <param name="Os">The operating system's description.</param>
/// <param name="Architecture">The process architecture, such as <c>Arm64</c>.</param>
/// <param name="Runtime">The .NET runtime's description.</param>
public sealed record HostInfo(string Os, string Architecture, string Runtime);

/// <summary>The Worker as the Server sees it. All but <c>Connected</c> are null while there is none.</summary>
/// <param name="Connected">Whether a Worker is connected and answering.</param>
/// <param name="Version">The Worker's release version, from its hello.</param>
/// <param name="ProcessId">The Worker's process id, from its hello.</param>
/// <param name="UptimeSeconds">How long the Worker has run, from its last health reply.</param>
/// <param name="WorkingSetBytes">The Worker's memory, from its last health reply.</param>
/// <param name="BusySeconds">How long its current device work has gone without progress, from its last health.</param>
public sealed record WorkerStatus(
    bool Connected,
    string? Version = null,
    int? ProcessId = null,
    double? UptimeSeconds = null,
    long? WorkingSetBytes = null,
    double? BusySeconds = null
);
