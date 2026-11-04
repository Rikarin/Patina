using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Patina.Ipc;

/// <summary>Answers one request. A handler that throws sends the peer an error reply; the link carries on.</summary>
public delegate ValueTask<IpcMessage> IpcHandler(IpcMessage request, CancellationToken cancellationToken);

/// <summary>
///     One end of a connected link: correlates replies with requests and hands the peer's requests to a handler, one at
///     a time, in order. The same on both sides, since the Worker says hello and the Server asks for health.
/// </summary>
[SuppressMessage("Skala", "SK3502", Justification = "False positive: DisposeAsync disposes both.")]
public sealed class IpcConnection : IAsyncDisposable {
    static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(30);

    readonly Stream stream;
    readonly IpcHandler handler;
    readonly ConcurrentDictionary<long, TaskCompletionSource<IpcMessage>> pending = new();
    readonly SemaphoreSlim writeLock = new(1, 1);
    readonly CancellationTokenSource stop = new();
    readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Task? readLoop;
    long nextId;

    /// <param name="stream">The connected stream, which this connection then owns.</param>
    /// <param name="handler">Answers the peer's requests.</param>
    public IpcConnection(Stream stream, IpcHandler handler) {
        this.stream = stream;
        this.handler = handler;
    }

    /// <summary>Completes when the peer has gone or this connection was disposed.</summary>
    public Task Closed => closed.Task;

    /// <summary>Starts reading. Call it once.</summary>
    public void Start() => readLoop = Task.Run(() => ReadLoopAsync(stop.Token), CancellationToken.None);

    /// <summary>Sends a request and returns its reply; throws <see cref="IpcException" /> on an error reply.</summary>
    public async Task<IpcMessage> SendAsync(IpcMessage request, CancellationToken cancellationToken = default) {
        request.Id = Interlocked.Increment(ref nextId);
        var reply = new TaskCompletionSource<IpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[request.Id] = reply;
        try {
            if (closed.Task.IsCompleted) {
                throw new IOException("The IPC peer has disconnected.");
            }

            await WriteAsync(request, cancellationToken).ConfigureAwait(false);
            var message = await reply.Task.WaitAsync(ReplyTimeout, cancellationToken).ConfigureAwait(false);
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

        stop.Dispose();
        writeLock.Dispose();
    }

    async Task WriteAsync(IpcMessage message, CancellationToken cancellationToken) {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            await IpcFraming.WriteAsync(stream, message, cancellationToken).ConfigureAwait(false);
        } finally {
            writeLock.Release();
        }
    }

    async Task ReadLoopAsync(CancellationToken cancellationToken) {
        try {
            while (await IpcFraming.ReadAsync(stream, cancellationToken).ConfigureAwait(false) is { } message) {
                if (message.Type is IpcTypes.Ok or IpcTypes.Error) {
                    if (pending.TryGetValue(message.Id, out var reply)) {
                        reply.TrySetResult(message);
                    }
                } else {
                    await WriteAsync(
                            await AnswerAsync(message, cancellationToken).ConfigureAwait(false),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
            }
        } catch (Exception e) when (e is IOException
                                        or SocketException
                                        or ObjectDisposedException
                                        or InvalidDataException
                                        or OperationCanceledException) {
            // A peer that went away, or one that broke the framing: the link is over either way.
        } finally {
            closed.TrySetResult();
            foreach (var reply in pending.Values) {
                reply.TrySetException(new IOException("The IPC peer has disconnected."));
            }
        }
    }

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
}
