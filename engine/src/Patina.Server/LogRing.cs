using Patina.Contracts;

namespace Patina.Server;

/// <summary>
///     The latest of what the Server said, kept in memory for any client to read (#192): the editor's Log, and
///     Blender's as much. Patina's own lines from information up, and the framework's from warnings up, so a request
///     served is not a line. Nothing is written anywhere: the console log stays the record, and this is a window on it.
/// </summary>
/// <param name="clock">The time each line is said at, or null for the system's.</param>
public sealed class LogRing(TimeProvider? clock = null) : ILoggerProvider {
    const string Framework = "Microsoft.";

    readonly Lock gate = new();
    readonly Queue<ServerLogEntry> kept = new();
    readonly TimeProvider time = clock ?? TimeProvider.System;
    long sequence;

    public ILogger CreateLogger(string categoryName) => new Writer(this, categoryName);

    public void Dispose() { }

    /// <summary>
    ///     The lines after <paramref name="after" />, the oldest first. A number past the last is one a Server before a
    ///     restart gave, so the lines start again from the first kept, and the page says lines are gone.
    /// </summary>
    public ServerLogPage After(long after) {
        lock (gate) {
            var restarted = after > sequence;
            if (restarted) {
                after = 0;
            }

            var entries = kept.Where(entry => entry.Sequence > after).Take(ServerLogPage.Most).ToList();
            var dropped = restarted || (kept.TryPeek(out var oldest) && oldest.Sequence > after + 1);
            return new(entries, entries.Count > 0 ? entries[^1].Sequence : after, dropped);
        }
    }

    /// <summary>Whether a line of a level from a part of the Server is kept.</summary>
    static bool Kept(string category, LogLevel level) => level is not LogLevel.None && level >= Least(category);

    /// <summary>The least level kept: warnings from the framework, information from Patina.</summary>
    static LogLevel Least(string category) =>
        category.StartsWith(Framework, StringComparison.Ordinal) ? LogLevel.Warning : LogLevel.Information;

    void Add(string category, LogLevel level, string text) {
        lock (gate) {
            kept.Enqueue(new(++sequence, time.GetUtcNow(), Name(level), category, text));
            while (kept.Count > ServerLogPage.Most) {
                kept.Dequeue();
            }
        }
    }

    static string Name(LogLevel level) =>
        level switch {
            LogLevel.Critical => "critical",
            LogLevel.Error => "error",
            LogLevel.Warning => "warning",
            _ => "information"
        };

    sealed class Writer(LogRing ring, string category) : ILogger {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => Kept(category, logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) {
            if (!Kept(category, logLevel)) {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);
            var text = formatter(state, exception);
            ring.Add(category, logLevel, exception is null ? text : $"{text} {exception.Message}");
        }
    }
}
