namespace Patina.Server;

/// <summary>The Server's own log lines.</summary>
static partial class ServerLog {
    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {Version} (pid {ProcessId}) connected")]
    public static partial void WorkerConnected(ILogger logger, string version, int processId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Worker link lost: {Reason}")]
    public static partial void WorkerLost(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Library {Library} already has a Server (pid {ProcessId})")]
    public static partial void LibraryTaken(ILogger logger, string library, int processId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not update the discovery file: {Reason}")]
    public static partial void DiscoveryFailed(ILogger logger, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Library indexed: {Files} files, {Changed} changed, {Seconds:F2} s"
    )]
    public static partial void LibraryIndexed(ILogger logger, int files, int changed, double seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "The library cannot be opened: {Reason}")]
    public static partial void LibraryUnreadable(ILogger logger, string reason);
}
