namespace Patina.Ipc;

/// <summary>
///     A line of the Worker's log that a person may need, a warning or an error, which the Server keeps in its own log
///     for every client to read (#297).
/// </summary>
/// <param name="Error">Whether it is an error, not a warning.</param>
/// <param name="Text">What the Worker said.</param>
public sealed record WorkerLogLine(bool Error, string Text);
