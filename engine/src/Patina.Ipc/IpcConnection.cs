using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;

namespace Patina.Ipc;

/// <summary>Answers one request. A handler that throws sends the peer an error reply; the link carries on.</summary>
public delegate ValueTask<IpcMessage> IpcHandler(IpcMessage request, CancellationToken cancellationToken);

/// <summary>
///     One end of a connected link: correlates replies with requests and hands the peer's requests to a handler, one at
///     a time, in order. The same on both sides, since the Worker says hello and the Server asks for health.
/// </summary>
/// <param name="stream">The connected stream, which this connection then owns.</param>
/// <param name="handler">Answers the peer's requests.</param>
[SuppressMessage("Skala", "SK3502", Justification = "False positive: DisposeAsync disposes both.")]
public sealed class IpcConnection(Stream stream, IpcHandler handler) : IAsyncDisposable {
    static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(30);

    readonly Stream stream = stream;
    readonly IpcHandler handler = handler;
    readonly ConcurrentDictionary<long, TaskCompletionSource<IpcMessage>> pending = new();
    readonly SemaphoreSlim writeLock = new(1, 1);
    readonly CancellationTokenSource stop = new();
    readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Channel<IpcMessage> queued = Channel.CreateUnbounded<IpcMessage>(new() { SingleReader = true });
    readonly Channel<IpcMessage> ahead = Channel.CreateUnbounded<IpcMessage>(new() { SingleReader = true });
    Task? readLoop;
    Task? queueLoop;
    long nextId;

    /// <summary>
    ///     Which requests are answered as they are read. Set, the rest wait their turn on a queue of their own, so a
    ///     long one cannot keep a health check from being answered. Unset, every request is answered as it is read.
    /// </summary>
    public Func<string, bool>? Immediate { get; init; }

    /// <summary>
    ///     Which requests are answered beside the rest, each on its own as soon as it is read: neither the read nor the
    ///     queue waits for them, nor they for the queue. For work that touches nothing the queue's work does.
    /// </summary>
    public Func<string, bool>? Aside { get; init; }

    /// <summary>
    ///     Which queued requests go ahead of the rest of the queue, in their own order: they still wait for the one
    ///     being answered, never for everything sent before them. For what a person watches, such as the viewport's
    ///     frames.
    /// </summary>
    public Func<string, bool>? Urgent { get; init; }

    /// <summary>
    ///     Told the type of each request as it joins the queue, on the reading thread, while the one being answered
    ///     may still be running: what is answering can give way to it.
    /// </summary>
    public Action<string>? Queued { get; init; }

    /// <summary>Completes when the peer has gone or this connection was disposed.</summary>
    public Task Closed => closed.Task;

    /// <summary>Starts reading. Call it once.</summary>
    public void Start() {
        queueLoop = Immediate is null ? null : Task.Run(() => QueueLoopAsync(stop.Token), CancellationToken.None);
        readLoop = Task.Run(() => ReadLoopAsync(stop.Token), CancellationToken.None);
    }

    /// <summary>Sends a request and returns its reply; throws <see cref="IpcException" /> on an error reply.</summary>
    public async Task<IpcMessage> SendAsync(IpcMessage request, CancellationToken cancellationToken = default) {
        request.Id = Interlocked.Increment(ref nextId);
        var reply = new TaskCompletionSource<IpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[request.Id] = reply;
        try {
            if (closed.Task.IsCompleted) {
                throw new IpcDisconnectedException();
            }

            await WriteAsync(request, cancellationToken).ConfigureAwait(false);
            var message = await reply.Task.WaitAsync(PatienceFor(request.Type), cancellationToken)
                .ConfigureAwait(false);
            return message.Type == IpcTypes.Error
                ? throw new IpcException(message.Error ?? "The IPC peer reported an error.")
                : message;
        } finally {
            pending.TryRemove(request.Id, out _);
        }
    }

    public async ValueTask DisposeAsync() {
        await stop.CancelAsync().ConfigureAwait(false);
        await stream.DisposeAsync().ConfigureAwait(false);
        if (readLoop is not null) {
            await readLoop.ConfigureAwait(false);
        }

        if (queueLoop is not null) {
            await queueLoop.ConfigureAwait(false);
        }

        stop.Dispose();
        writeLock.Dispose();
    }

    /// <summary>
    ///     The owner disposes the link when the peer goes, whoever is writing at that moment, so a write that meets a
    ///     disposed link is one more way of losing the peer. Callers recover from that, and only from that.
    /// </summary>
    async Task WriteAsync(IpcMessage message, CancellationToken cancellationToken) {
        try {
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                await IpcFraming.WriteAsync(stream, message, cancellationToken).ConfigureAwait(false);
            } finally {
                writeLock.Release();
            }
        } catch (ObjectDisposedException e) {
            throw new IpcDisconnectedException(e);
        }
    }

    async Task ReadLoopAsync(CancellationToken cancellationToken) {
        try {
            while (await IpcFraming.ReadAsync(stream, cancellationToken).ConfigureAwait(false) is { } message) {
                await RouteAsync(message, cancellationToken).ConfigureAwait(false);
            }
        } catch (Exception e) when (e is IOException
                                        or SocketException
                                        or ObjectDisposedException
                                        or InvalidDataException
                                        or JsonException
                                        or OperationCanceledException) {
            // A peer that went away, or one that broke the framing: the link is over either way.
        } finally {
            queued.Writer.TryComplete();
            ahead.Writer.TryComplete();
            closed.TrySetResult();
            foreach (var reply in pending.Values) {
                reply.TrySetException(new IpcDisconnectedException());
            }
        }
    }

    /// <summary>
    ///     Where a message read goes: a reply to its caller, a request aside, to the queue (urgent ones ahead), or
    ///     answered here.
    /// </summary>
    async Task RouteAsync(IpcMessage message, CancellationToken cancellationToken) {
        if (message.Type is IpcTypes.Ok or IpcTypes.Error) {
            if (pending.TryGetValue(message.Id, out var reply)) {
                reply.TrySetResult(message);
            }
        } else if (Aside?.Invoke(message.Type) == true) {
            _ = Task.Run(() => AnswerAsideAsync(message, cancellationToken), CancellationToken.None);
        } else if (Immediate is { } immediate && !immediate(message.Type)) {
            (Urgent?.Invoke(message.Type) == true ? ahead : queued).Writer.TryWrite(message);
            Queued?.Invoke(message.Type);
        } else {
            await WriteAsync(await AnswerAsync(message, cancellationToken).ConfigureAwait(false), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    async Task QueueLoopAsync(CancellationToken cancellationToken) {
        try {
            while (await NextAsync(cancellationToken).ConfigureAwait(false) is { } message) {
                await AnswerAsideAsync(message, cancellationToken).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) {
            // The peer has gone, and the read loop says so.
        }
    }

    /// <summary>The next queued request, an urgent one first; null once the link is over and nothing is left.</summary>
    async Task<IpcMessage?> NextAsync(CancellationToken cancellationToken) {
        while (true) {
            if (ahead.Reader.TryRead(out var urgent) || queued.Reader.TryRead(out urgent)) {
                return urgent;
            }

            var first = ahead.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var rest = queued.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var woke = await Task.WhenAny(first, rest).ConfigureAwait(false);
            if (!await woke.ConfigureAwait(false)) {
                // Both close together when the link ends: what is still in either is answered, then nothing.
                return ahead.Reader.TryRead(out var left) || queued.Reader.TryRead(out left) ? left : null;
            }
        }
    }

    /// <summary>Answers one request and writes the answer, unless the peer has gone.</summary>
    async Task AnswerAsideAsync(IpcMessage message, CancellationToken cancellationToken) {
        try {
            await WriteAsync(
                    await AnswerAsync(message, cancellationToken).ConfigureAwait(false),
                    cancellationToken
                )
                .ConfigureAwait(false);
        } catch (Exception e) when (e is IOException
                                        or SocketException
                                        or ObjectDisposedException
                                        or OperationCanceledException) {
            // The peer has gone, and the read loop says so.
        }
    }

    /// <summary>
    ///     A greeting or a health check is answered at once by a live peer. Anything else can rightly take minutes on a
    ///     big stack, so it waits as long as the link lasts, and the health check is what notices the link has died.
    /// </summary>
    static TimeSpan PatienceFor(string type) =>
        type is IpcTypes.Hello or IpcTypes.Health ? ReplyTimeout : Timeout.InfiniteTimeSpan;

    async ValueTask<IpcMessage> AnswerAsync(IpcMessage request, CancellationToken cancellationToken) {
        try {
            var reply = await handler(request, cancellationToken).ConfigureAwait(false);
            reply.Id = request.Id;
            return reply;
        } catch (Exception e) when (e is not (OperationCanceledException or OutOfMemoryException)) {
            return IpcMessage.Fail(request.Id, e.Message);
        }
    }
}

public sealed class IpcException : Exception {
    public IpcException() : this("IPC error.") { }

    public IpcException(string message) : base(message) { }

    public IpcException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>How a refusal about the machine, not the request, begins: the Worker has no GPU to use.</summary>
    public const string NoDevicePrefix = "No Vulkan device";

    /// <summary>
    ///     Whether the peer refused for want of a device rather than for anything in what was asked: the same ask may
    ///     well be answered on another machine, or by the next Worker, so nothing is to be remembered of it (#302).
    /// </summary>
    public bool AboutTheDevice => Message.StartsWith(NoDevicePrefix, StringComparison.Ordinal);

    /// <summary>The refusal of a Worker that could open no device, with the reason the device gave.</summary>
    public static IpcException NoDevice(string? refusal) => new($"{NoDevicePrefix}: {refusal}");
}

/// <summary>
///     The peer went away before it answered: its process ended or the link broke. Nothing in the request was wrong,
///     and the same request may be answered by the next peer (#309). It is an <see cref="IOException" />, as it was,
///     so that what recovers from a lost link still does; what refuses a request tells it apart from a file.
/// </summary>
public sealed class IpcDisconnectedException : IOException {
    const string Said = "The IPC peer has disconnected.";

    public IpcDisconnectedException() : base(Said) { }

    public IpcDisconnectedException(Exception innerException) : base(Said, innerException) { }

    public IpcDisconnectedException(string message) : base(message) { }

    public IpcDisconnectedException(string message, Exception innerException) : base(message, innerException) { }
}
