namespace Patina.Server;

/// <summary>The Server's own log lines.</summary>
static partial class ServerLog {
    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {Version} (pid {ProcessId}) connected")]
    public static partial void WorkerConnected(ILogger logger, string version, int processId);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Service} failed and starts again: {Reason}")]
    public static partial void ServiceFailed(ILogger logger, string service, string reason, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {Job} failed: {Reason}")]
    public static partial void JobFailed(ILogger logger, string job, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not autosave {Path}: {Reason}")]
    public static partial void AutosaveFailed(ILogger logger, string path, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Worker link lost: {Reason}")]
    public static partial void WorkerLost(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Worker went away mid-frame; the next one will take over")]
    public static partial void ViewportWorkerLost(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Viewport: {Reason}")]
    public static partial void ViewportFailed(ILogger logger, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The Worker went away evaluating {Path}; the next one will take over"
    )]
    public static partial void GraphWorkerLost(ILogger logger, string path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The Worker went away with job {Job} on attempt {Attempt}; it goes back in the queue"
    )]
    public static partial void JobWorkerLost(ILogger logger, string job, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Graph {Path}: {Reason}")]
    public static partial void GraphFailed(ILogger logger, string path, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Library {Library} already has a Server (pid {ProcessId})")]
    public static partial void LibraryTaken(ILogger logger, string library, int processId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not update the discovery file: {Reason}")]
    public static partial void DiscoveryFailed(ILogger logger, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Library indexed: {Files} files, {Changed} changed, {Seconds:F2} s"
    )]
    public static partial void LibraryIndexed(ILogger logger, int files, int changed, double seconds);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Preferences in {Path} cannot be used, so the defaults are: {Reason}"
    )]
    public static partial void PreferencesUnreadable(ILogger logger, string path, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "The library cannot be opened: {Reason}")]
    public static partial void LibraryUnreadable(ILogger logger, string reason);
}
