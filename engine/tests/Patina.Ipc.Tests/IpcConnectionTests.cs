namespace Patina.Ipc.Tests;

public sealed class IpcConnectionTests : IDisposable {
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => File.Delete(socketPath);

    static ValueTask<IpcMessage> Reply(IpcMessage request, CancellationToken _) =>
        ValueTask.FromResult(new IpcMessage { Type = IpcTypes.Ok, Health = new(request.Id, 1) });

    async Task<(IpcConnection Left, IpcConnection Right)> ConnectAsync(
        IpcHandler left,
        IpcHandler right,
        Func<string, bool>? immediate = null,
        Func<string, bool>? aside = null,
        Func<string, bool>? urgent = null
    ) {
        using var listener = IpcSocket.Listen(socketPath);
        var accept = listener.AcceptAsync(Token).AsTask();
        var client = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(5), Token);
        var leftEnd = new IpcConnection(client, left);
        var rightEnd = new IpcConnection(new System.Net.Sockets.NetworkStream(await accept, true), right) {
            Immediate = immediate, Aside = aside, Urgent = urgent
        };
        leftEnd.Start();
        rightEnd.Start();
        return (leftEnd, rightEnd);
    }

    [Fact]
    public async Task Requests_go_both_ways_on_one_connection() {
        var (left, right) = await ConnectAsync(Reply, Reply);
        await using (left) {
            await using (right) {
                var fromLeft = await left.SendAsync(new() { Type = IpcTypes.Health }, Token);
                var fromRight = await right.SendAsync(new() { Type = IpcTypes.Health }, Token);

                Assert.Equal(IpcTypes.Ok, fromLeft.Type);
                Assert.Equal(fromLeft.Id, fromLeft.Health?.UptimeSeconds);
                Assert.Equal(IpcTypes.Ok, fromRight.Type);
            }
        }
    }

    [Fact]
    public async Task Each_reply_reaches_the_request_that_asked() {
        var (left, right) = await ConnectAsync(Reply, Reply);
        await using (left) {
            await using (right) {
                var replies = await Task.WhenAll(
                    Enumerable.Range(0, 50).Select(_ => left.SendAsync(new() { Type = IpcTypes.Health }, Token))
                );

                Assert.All(replies, static reply => Assert.Equal(reply.Id, reply.Health?.UptimeSeconds));
                Assert.Equal(50, replies.Select(static reply => reply.Id).Distinct().Count());
            }
        }
    }

    [Fact]
    public async Task An_error_reply_throws_at_the_caller() {
        var (left, right) = await ConnectAsync(
            Reply,
            static (request, _) =>
                ValueTask.FromResult(IpcMessage.Fail(request.Id, "no such thing"))
        );
        await using (left) {
            await using (right) {
                var error = await Assert.ThrowsAsync<IpcException>(() =>
                    left.SendAsync(new() { Type = IpcTypes.Health }, Token)
                );

                Assert.Equal("no such thing", error.Message);
            }
        }
    }

    [Fact]
    public async Task A_handler_that_throws_answers_an_error_and_the_link_carries_on() {
        var calls = 0;
        var (left, right) = await ConnectAsync(
            Reply,
            (request, cancellationToken) =>
                ++calls == 1 ? throw new InvalidOperationException("broke") : Reply(request, cancellationToken)
        );
        await using (left) {
            await using (right) {
                await Assert.ThrowsAsync<IpcException>(() => left.SendAsync(new() { Type = IpcTypes.Health }, Token));

                Assert.Equal(IpcTypes.Ok, (await left.SendAsync(new() { Type = IpcTypes.Health }, Token)).Type);
            }
        }
    }

    /// <summary>A long request used to hold the read loop, so a healthy Worker failed its health check.</summary>
    [Fact]
    public async Task A_health_check_is_answered_while_a_long_request_runs() {
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        var (left, right) = await ConnectAsync(
            Reply,
            async (request, _) => {
                if (request.Type == IpcTypes.StackEvaluate) {
                    started.TrySetResult();
                    await release.Task;
                }

                return IpcMessage.Ok(request.Id);
            },
            static type => type == IpcTypes.Health
        );
        await using (left) {
            await using (right) {
                var slow = left.SendAsync(new() { Type = IpcTypes.StackEvaluate }, Token);
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);

                var health = await left.SendAsync(new() { Type = IpcTypes.Health }, Token)
                    .WaitAsync(TimeSpan.FromSeconds(5), Token);

                Assert.Equal(IpcTypes.Ok, health.Type);
                Assert.False(slow.IsCompleted);
                release.SetResult();
                Assert.Equal(IpcTypes.Ok, (await slow.WaitAsync(TimeSpan.FromSeconds(5), Token)).Type);
            }
        }
    }

    /// <summary>
    ///     A mesh read the Server waits on is answered beside the queue (#285): neither an evaluation ahead of it nor
    ///     another read keeps it waiting.
    /// </summary>
    [Fact]
    public async Task A_request_aside_is_answered_while_the_queue_and_another_aside_wait() {
        var release = new TaskCompletionSource();
        var (left, right) = await ConnectAsync(
            Reply,
            async (request, _) => {
                if (request.MeshFile is "held") {
                    await release.Task;
                }

                return IpcMessage.Ok(request.Id);
            },
            static type => type == IpcTypes.Health,
            static type => type == IpcTypes.MeshSets
        );
        await using (left) {
            await using (right) {
                var queued = left.SendAsync(new() { Type = IpcTypes.StackEvaluate, MeshFile = "held" }, Token);
                var held = left.SendAsync(new() { Type = IpcTypes.MeshSets, MeshFile = "held" }, Token);

                var read = await left.SendAsync(new() { Type = IpcTypes.MeshSets, MeshFile = "free" }, Token)
                    .WaitAsync(TimeSpan.FromSeconds(5), Token);

                Assert.Equal(IpcTypes.Ok, read.Type);
                Assert.False(queued.IsCompleted || held.IsCompleted);
                release.SetResult();
                await Task.WhenAll(queued, held).WaitAsync(TimeSpan.FromSeconds(5), Token);
            }
        }
    }

    /// <summary>
    ///     A viewport frame waits for the request being answered and goes ahead of the rest of the queue (#274): on a
    ///     software GPU the requests queued before it took longer than anyone watching would wait.
    /// </summary>
    [Fact]
    public async Task An_urgent_request_goes_ahead_of_the_queue_but_not_of_the_one_being_answered() {
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        List<string> answered = [];
        var (left, right) = await ConnectAsync(
            Reply,
            async (request, _) => {
                if (request.MeshFile is "first") {
                    started.TrySetResult();
                    await release.Task;
                }

                lock (answered) {
                    answered.Add(request.MeshFile ?? string.Empty);
                }

                return IpcMessage.Ok(request.Id);
            },
            static type => type == IpcTypes.Health,
            urgent: static type => type == IpcTypes.ViewportRender
        );
        await using (left) {
            await using (right) {
                var first = left.SendAsync(new() { Type = IpcTypes.StackEvaluate, MeshFile = "first" }, Token);
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
                var later = left.SendAsync(new() { Type = IpcTypes.StackEvaluate, MeshFile = "later" }, Token);
                var frame = left.SendAsync(new() { Type = IpcTypes.ViewportRender, MeshFile = "frame" }, Token);
                await Task.Delay(100, Token);

                Assert.False(frame.IsCompleted);
                release.SetResult();
                await Task.WhenAll(first, later, frame).WaitAsync(TimeSpan.FromSeconds(5), Token);
                Assert.Equal(["first", "frame", "later"], answered);
            }
        }
    }

    [Fact]
    public void Device_work_that_stops_moving_is_hung_and_a_beat_or_idle_clears_it() {
        var now = TimeSpan.FromHours(1);
        var watch = new DeviceWatch(TimeSpan.FromSeconds(10), () => now);

        Assert.False(watch.Hung);
        watch.Beat();
        now += TimeSpan.FromSeconds(9);
        Assert.False(watch.Hung);
        now += TimeSpan.FromSeconds(2);
        Assert.True(watch.Hung);

        watch.Beat();
        Assert.False(watch.Hung);
        now += TimeSpan.FromMinutes(1);
        watch.Idle();
        Assert.False(watch.Hung);
    }

    [Fact]
    public async Task A_peer_that_goes_away_closes_the_link_and_fails_what_is_pending() {
        var unanswered = new TaskCompletionSource();
        var (left, right) = await ConnectAsync(
            Reply,
            async (request, cancellationToken) => {
                unanswered.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return IpcMessage.Ok(request.Id);
            }
        );
        await using (left) {
            var pending = left.SendAsync(new() { Type = IpcTypes.Health }, Token);
            await unanswered.Task.WaitAsync(Token);

            await right.DisposeAsync();

            await Assert.ThrowsAsync<IOException>(() => pending);
            await left.Closed.WaitAsync(TimeSpan.FromSeconds(5), Token);
            await Assert.ThrowsAsync<IOException>(() => left.SendAsync(new() { Type = IpcTypes.Health }, Token));
        }
    }

    /// <summary>A Server killed its own host this way: the link went while the render loop was mid-write.</summary>
    [Fact]
    public async Task A_write_under_way_when_the_link_is_disposed_is_a_lost_peer() {
        var stream = new StalledStream();
        var connection = new IpcConnection(stream, Reply);
        connection.Start();
        var send = connection.SendAsync(new() { Type = IpcTypes.Health }, Token);
        await stream.Writing.WaitAsync(TimeSpan.FromSeconds(5), Token);

        await connection.DisposeAsync();
        stream.BreakPipe();

        await Assert.ThrowsAsync<IOException>(() => send);
    }

    [Fact]
    public async Task A_send_to_a_disposed_link_is_a_lost_peer() {
        var connection = new IpcConnection(new StalledStream(), Reply);
        await connection.DisposeAsync();

        await Assert.ThrowsAsync<IOException>(() => connection.SendAsync(new() { Type = IpcTypes.Health }, Token));
    }

    [Fact]
    public async Task Connect_waits_for_a_listener_that_is_still_starting() {
        var connect = IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(10), Token);
        await Task.Delay(300, Token);
        using var listener = IpcSocket.Listen(socketPath);

        await using var stream = await connect;

        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Connect_gives_up_after_its_timeout() =>
        await Assert.ThrowsAnyAsync<System.Net.Sockets.SocketException>(() =>
            IpcSocket.ConnectAsync(socketPath, TimeSpan.FromMilliseconds(200), Token)
        );

    [Fact]
    public void Listening_replaces_a_stale_socket_file() {
        File.WriteAllText(socketPath, string.Empty);

        using var listener = IpcSocket.Listen(socketPath);

        Assert.True(listener.IsBound);
    }

    [Fact]
    public async Task The_stdin_watch_fires_when_its_pipe_ends() {
        var closed = new TaskCompletionSource();

        StdinWatch.Start(new MemoryStream([1, 2, 3]), closed.SetResult);

        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
    }

    /// <summary>Reads nothing until disposed, and holds a write until the test breaks the pipe under it.</summary>
    sealed class StalledStream : Stream {
        readonly TaskCompletionSource<int> disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource writing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource broken = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Writing => writing.Task;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void BreakPipe() => broken.TrySetException(new IOException("Broken pipe"));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(disposed.Task);

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        ) {
            writing.TrySetResult();
            await broken.Task;
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            disposed.TrySetResult(0);
            base.Dispose(disposing);
        }
    }
}
