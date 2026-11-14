namespace Patina.Contracts;

/// <summary>One line of what the Server said, as <c>GET /api/v1/log</c> gives it (#192).</summary>
/// <param name="Sequence">Its number, rising by one from the Server's start.</param>
/// <param name="At">When it was said.</param>
/// <param name="Level"><c>information</c>, <c>warning</c>, <c>error</c> or <c>critical</c>.</param>
/// <param name="Source">The part of the Server that said it, such as <c>Patina.Server.Autosave</c>.</param>
/// <param name="Text">What it said, and the exception's message after it when there was one.</param>
public sealed record ServerLogEntry(long Sequence, DateTimeOffset At, string Level, string Source, string Text);

/// <summary>The lines of the Server's log after a number, the oldest first (#192).</summary>
/// <param name="Entries">The lines, at most <see cref="Most" />.</param>
/// <param name="Next">The number to ask after next: the last line's, or the one asked after when none came.</param>
/// <param name="Dropped">Whether lines after the one asked after are gone, as only the latest are kept.</param>
public sealed record ServerLogPage(IReadOnlyList<ServerLogEntry> Entries, long Next, bool Dropped) {
    /// <summary>The most lines one page gives, which is also how many the Server keeps.</summary>
    public const int Most = 1000;
}
