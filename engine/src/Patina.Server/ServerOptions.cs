namespace Patina.Server;

/// <summary>Configuration section <c>Patina</c>.</summary>
public sealed class ServerOptions {
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
    /// <summary>The Unix domain socket the Server listens on for its Worker.</summary>
    public string SocketPath { get; set; } = Path.Combine(Path.GetTempPath(), $"patina-{Environment.ProcessId}.sock");
}
