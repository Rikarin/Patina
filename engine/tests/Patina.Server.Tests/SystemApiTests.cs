using Patina.Contracts;
using Patina.Ipc;
using System.Net;

namespace Patina.Server.Tests;

public sealed class SystemApiTests : IDisposable {
    readonly ServerFixture server = new();

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => server.Dispose();

    Task<SystemInfo?> SystemAsync(HttpClient client) =>
        client.GetFromJsonAsync("/api/v1/system", ContractsJsonContext.Default.SystemInfo, Token);

    async Task<SystemInfo> WaitForWorkerAsync(HttpClient client, bool connected) {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (true) {
            var system = await SystemAsync(client);
            if (system?.Worker.Connected == connected) {
                return system;
            }

            await Task.Delay(50, deadline.Token);
        }
    }

    static ValueTask<IpcMessage> Healthy(IpcMessage request, CancellationToken _) =>
        ValueTask.FromResult(new IpcMessage { Type = IpcTypes.Ok, Health = new(12.5, 4096) });

    async Task<IpcConnection> ConnectWorkerAsync(int protocol = IpcMessage.ProtocolVersion) {
        var connection = new IpcConnection(
            await IpcSocket.ConnectAsync(server.SocketPath, TimeSpan.FromSeconds(10), Token),
            Healthy
        );
        connection.Start();
        await connection.SendAsync(
            new() { Type = IpcTypes.Hello, Worker = new(protocol, "9.9.9", 1234) },
            Token
        );
        return connection;
    }

    /// <summary>About says which Vixen the engine runs on (#190): its version, and its commit abbreviated.</summary>
    [Fact]
    public async Task The_system_says_which_vixen_it_runs_on() {
        using var client = server.AuthorisedClient();

        var system = await SystemAsync(client);

        Assert.Matches(@"^\d+\.\d+\.\d+(-[\w.]+)?(\+[0-9a-f]{1,9})?$", system!.Vixen);
    }

    [Fact]
    public async Task There_is_one_list_of_sizes_and_a_client_reads_it_from_the_server() {
        using var client = server.AuthorisedClient();

        var system = await SystemAsync(client);

        Assert.Equal([256, 512, 1024, 2048, 4096, 8192], system!.TextureSides);
        Assert.Contains(system.DefaultTextureSide, system.TextureSides);
    }

    [Fact]
    public async Task System_says_what_the_server_is() {
        using var client = server.AuthorisedClient();

        var system = await SystemAsync(client);

        Assert.NotNull(system);
        Assert.Equal(ProductInfo.Version, system.Version);
        Assert.Equal(1, system.ApiVersion);
        Assert.Equal(server.Library, system.Library);
        Assert.NotEmpty(system.Host.Runtime);
    }

    [Fact]
    public async Task With_no_worker_the_server_still_answers() {
        using var client = server.AuthorisedClient();

        var system = await SystemAsync(client);

        Assert.Equal(new(false), system?.Worker);
    }

    [Fact]
    public async Task A_request_without_the_token_is_refused() {
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/v1/system", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>With no token, a page on a name that resolves here must not reach the API as this machine.</summary>
    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("evil.example", false)]
    [InlineData("192.168.1.2", false)]
    public void Only_a_loopback_name_passes_with_no_token(string host, bool passes) =>
        Assert.Equal(passes, Api.Access.IsLoopbackHost(host));

    [Fact]
    public async Task A_worker_shows_up_and_goes_away() {
        using var client = server.AuthorisedClient();
        await SystemAsync(client); // starts the Server, and so its socket

        var worker = await ConnectWorkerAsync();
        var connected = await WaitForWorkerAsync(client, true);

        Assert.Equal(new(true, "9.9.9", 1234, 12.5, 4096, 0), connected.Worker);

        await worker.DisposeAsync();

        Assert.Equal(new(false), (await WaitForWorkerAsync(client, false)).Worker);
    }

    [Fact]
    public async Task The_next_worker_takes_over_when_the_first_dies() {
        using var client = server.AuthorisedClient();
        await SystemAsync(client);

        await (await ConnectWorkerAsync()).DisposeAsync();
        await using var second = await ConnectWorkerAsync();

        Assert.True((await WaitForWorkerAsync(client, true)).Worker.Connected);
    }

    [Fact]
    public async Task A_worker_on_another_protocol_is_refused() {
        using var client = server.AuthorisedClient();
        await SystemAsync(client);

        await Assert.ThrowsAsync<IpcException>(() => ConnectWorkerAsync(IpcMessage.ProtocolVersion + 1));
    }

    [Fact]
    public void The_engine_and_the_editor_are_the_same_version() {
        var package = File.ReadAllText(Path.Combine(Engine.Root, "..", "editor", "package.json"));
        var version = System.Text.Json.Nodes.JsonNode.Parse(package)!["version"]!.GetValue<string>();

        // A tag build overrides the engine's version from PATINA_VERSION and the editor's at packaging time.
        Assert.SkipWhen(Environment.GetEnvironmentVariable("PATINA_VERSION") is { Length: > 0 }, "Set by the release.");
        Assert.Equal(version, ProductInfo.Version);
    }
}
