namespace Patina.Ipc.Tests;

public sealed class IpcConnectionTests : IDisposable {
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => File.Delete(socketPath);

    static ValueTask<IpcMessage> Reply(IpcMessage request, CancellationToken _) =>
        ValueTask.FromResult(new IpcMessage { Type = IpcTypes.Ok, Health = new(request.Id, 1) });

    async Task<(IpcConnection Left, IpcConnection Right)> ConnectAsync(IpcHandler left, IpcHandler right) {
        using var listener = IpcSocket.Listen(socketPath);
        var accept = listener.AcceptAsync(Token).AsTask();
        var client = await IpcSocket.ConnectAsync(socketPath, TimeSpan.FromSeconds(5), Token);
        var leftEnd = new IpcConnection(client, left);
        var rightEnd = new IpcConnection(new System.Net.Sockets.NetworkStream(await accept, true), right);
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
}
