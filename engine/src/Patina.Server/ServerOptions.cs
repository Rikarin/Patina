namespace Patina.Server;

/// <summary>Configuration section <c>Patina</c>.</summary>
public sealed class ServerOptions {
    /// <summary>How often what is open and unsaved is kept.</summary>
    public int AutosaveSeconds { get; set; } = 60;

    /// <summary>
    ///     Access token every request must present (<c>Authorization: Bearer</c>). Without one, a Server with a library
    ///     makes its own, since the discovery file must offer one; a Server with no library is a development Server and
    ///     needs none on loopback.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>The discovery file, or null for the per-user default.</summary>
    public string? DiscoveryFile { get; set; }

    public WorkerOptions Worker { get; set; } = new();
}

/// <summary>The Worker is a separate process, which connects to this socket. The Server starts nothing.</summary>
public sealed class WorkerOptions {
    /// <summary>What every Server's frames file is called, so a later one finds what a killed one left.</summary>
    public const string FramesPattern = "patina-*.frames";

    /// <summary>The Unix domain socket the Server listens on for its Worker.</summary>
    public string SocketPath { get; set; } = Path.Combine(Path.GetTempPath(), $"patina-{Environment.ProcessId}.sock");

    /// <summary>
    ///     The file frames pass through on their way from the Worker: it is created at start and removed at stop.
    /// </summary>
    public string FramesPath { get; set; } =
        Path.Combine(Path.GetTempPath(), $"patina-{Environment.ProcessId}-{Guid.NewGuid():N}.frames");
}
