using Patina.Ipc;
using System.Text;

namespace Patina.Worker;

/// <summary>
///     The Worker's log (#297). Every line goes to its console, which the desktop app's supervision reads; warnings and
///     errors also go to the Server, whose log every client reads, so a person sees what the Worker said wherever the
///     Server runs. Lines said with no Server linked wait for the next one, a few at most: they are a person's clue,
///     not state, and the Worker holds nothing a Server needs (plan §3.1).
/// </summary>
/// <param name="console">Where every line goes first.</param>
sealed class WorkerReport(TextWriter console) {
    /// <summary>How many lines wait for a Server at most; the oldest go first.</summary>
    const int MostWaiting = 32;

    readonly Lock gate = new();
    readonly Queue<WorkerLogLine> waiting = new();
    IpcConnection? link;
    Task sending = Task.CompletedTask;

    /// <summary>A line for the console alone: how the link is going.</summary>
    public Task SayAsync(string text, CancellationToken cancellationToken) =>
        console.WriteLineAsync(text.AsMemory(), cancellationToken);

    /// <summary>A warning, for the console and the Server.</summary>
    public Task WarnAsync(string text, CancellationToken cancellationToken = default) =>
        TellAsync(new(false, text), cancellationToken);

    /// <summary>An error, for the console and the Server.</summary>
    public Task FailAsync(string text, CancellationToken cancellationToken = default) =>
        TellAsync(new(true, text), cancellationToken);

    /// <summary>A writer whose every line is an error, for what takes a writer.</summary>
    public TextWriter Failures => new FailureWriter(this);

    /// <summary>A Server is linked: what waited goes to it, and so does what is said from now.</summary>
    public void Linked(IpcConnection connection) {
        lock (gate) {
            link = connection;
            while (waiting.TryDequeue(out var line)) {
                Send(connection, line);
            }
        }
    }

    /// <summary>The Server is gone: what is said waits for the next.</summary>
    public void Unlinked() {
        lock (gate) {
            link = null;
        }
    }

    /// <summary>Waits a little for what was sent to arrive: the Worker is about to end.</summary>
    public async Task SentAsync(TimeSpan patience, CancellationToken cancellationToken = default) {
        Task last;
        lock (gate) {
            last = sending;
        }

        try {
            await last.WaitAsync(patience, cancellationToken).ConfigureAwait(false);
        } catch (TimeoutException) {
            // Not there in time: the console has it.
        }
    }

    async Task TellAsync(WorkerLogLine line, CancellationToken cancellationToken) {
        await console.WriteLineAsync(line.Text.AsMemory(), cancellationToken).ConfigureAwait(false);
        lock (gate) {
            if (link is { Closed.IsCompleted: false } connection) {
                Send(connection, line);
                return;
            }

            waiting.Enqueue(line);
            while (waiting.Count > MostWaiting) {
                waiting.Dequeue();
            }
        }
    }

    /// <summary>Sends a line after those sent before it. Called under the gate.</summary>
    void Send(IpcConnection connection, WorkerLogLine line) {
        var before = sending;
        sending = SendAsync(before, connection, line);
    }

    static async Task SendAsync(Task before, IpcConnection connection, WorkerLogLine line) {
        await before.ConfigureAwait(false);
        try {
            await connection.SendAsync(new() { Type = IpcTypes.WorkerLog, Log = line }, CancellationToken.None)
                .ConfigureAwait(false);
        } catch (Exception e) when (e is IOException
                                        or IpcException
                                        or ObjectDisposedException
                                        or OperationCanceledException) {
            // The link went as it was said; the console has it.
        }
    }

    /// <summary>Every line written is an error; flushing waits a little for the Server to have them.</summary>
    sealed class FailureWriter(WorkerReport report) : TextWriter {
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value) => _ = report.FailAsync(value ?? string.Empty);

        public override Task WriteLineAsync(string? value) => report.FailAsync(value ?? string.Empty);

        public override Task WriteLineAsync(
            ReadOnlyMemory<char> buffer,
            CancellationToken cancellationToken = default
        ) =>
            report.FailAsync(buffer.ToString(), cancellationToken);

        public override async Task FlushAsync(CancellationToken cancellationToken) =>
            await report.SentAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
    }
}
