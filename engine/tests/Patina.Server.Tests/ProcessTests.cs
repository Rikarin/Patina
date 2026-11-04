using Patina.Contracts;
using Patina.Library;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>The real executables, as separate processes: what a client that is not the test host sees.</summary>
public sealed class ProcessTests : IDisposable {
    static readonly HttpClient Http = new();

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-process-");
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
    readonly List<Process> started = [];

    string Library => Directory.CreateDirectory(Path.Combine(folder.FullName, "Game.ptlib")).FullName;

    string DiscoveryFile => Path.Combine(folder.FullName, "servers.json");

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() {
        foreach (var process in started) {
            if (!process.HasExited) {
                process.Kill(true);
                process.WaitForExit();
            }

            process.Dispose();
        }

        File.Delete(socketPath);
        folder.Delete(true);
    }

    Process Start(string dll, params string[] arguments) {
        var info = new ProcessStartInfo("dotnet") {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add(dll);
        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        var process = Process.Start(info)!;
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        started.Add(process);
        return process;
    }

    Process StartServer() =>
        Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Library,
            $"--Patina:DiscoveryFile={DiscoveryFile}",
            $"--Patina:Worker:SocketPath={socketPath}",
            "--Patina:WatchStdin=true"
        );

    async Task<ServerInstance> DiscoverAsync(CancellationToken cancellationToken) {
        var registry = new InstanceRegistry(DiscoveryFile);
        while (true) {
            if (await registry.FindAsync(Library, cancellationToken) is { } instance) {
                return instance;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    static async Task<HttpResponseMessage> GetSystemAsync(
        ServerInstance instance,
        string? token,
        CancellationToken cancellationToken
    ) {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"http://127.0.0.1:{instance.Port}/api/v1/system"
        );
        if (token is not null) {
            request.Headers.Authorization = new("Bearer", token);
        }

        return await Http.SendAsync(request, cancellationToken);
    }

    static async Task<SystemInfo> WaitForWorkerAsync(ServerInstance instance, CancellationToken cancellationToken) {
        while (true) {
            using var response = await GetSystemAsync(instance, instance.Token, cancellationToken);
            var system = await response.Content.ReadFromJsonAsync(
                ContractsJsonContext.Default.SystemInfo,
                cancellationToken
            );
            if (system?.Worker.Connected == true) {
                return system;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    [Fact]
    public async Task A_client_finds_the_server_through_the_discovery_file_and_sees_its_worker() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var server = StartServer();

        var instance = await DiscoverAsync(deadline.Token);
        using var refused = await GetSystemAsync(instance, null, deadline.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        var worker = Start(Engine.Dll("Patina.Worker", "patina-worker"), "--socket", socketPath, "--watch-stdin");
        var system = await WaitForWorkerAsync(instance, deadline.Token);

        Assert.Equal(Library, system.Library);
        Assert.Equal(ProductInfo.Version, system.Worker.Version);

        // Both lifelines close, as they do when the desktop app ends, and both processes leave cleanly.
        server.StandardInput.Close();
        worker.StandardInput.Close();
        await server.WaitForExitAsync(deadline.Token);
        await worker.WaitForExitAsync(deadline.Token);
        Assert.Equal(0, server.ExitCode);
        Assert.Equal(0, worker.ExitCode);
        Assert.Null(await new InstanceRegistry(DiscoveryFile).FindAsync(Library, deadline.Token));
        Assert.False(File.Exists(socketPath));
    }

    [Fact]
    public async Task A_second_server_on_a_library_refuses_to_run() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        StartServer();
        await DiscoverAsync(deadline.Token);

        var second = Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Library,
            $"--Patina:DiscoveryFile={DiscoveryFile}",
            $"--Patina:Worker:SocketPath={socketPath}.2"
        );
        await second.WaitForExitAsync(deadline.Token);

        Assert.Equal(1, second.ExitCode);
    }

    [Fact]
    public async Task A_library_folder_that_does_not_exist_stops_the_server() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));

        var server = Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Path.Combine(folder.FullName, "missing"),
            $"--Patina:DiscoveryFile={DiscoveryFile}"
        );
        await server.WaitForExitAsync(deadline.Token);

        Assert.NotEqual(0, server.ExitCode);
    }

    [Fact]
    public async Task The_cli_prints_the_version() {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true };
        info.ArgumentList.Add(Engine.Dll("Patina.Cli", "patina"));
        info.ArgumentList.Add("version");

        using var cli = Process.Start(info)!;
        var output = await cli.StandardOutput.ReadToEndAsync(deadline.Token);
        await cli.WaitForExitAsync(deadline.Token);

        Assert.Equal(0, cli.ExitCode);
        Assert.Equal(ProductInfo.Version, output.Trim());
    }

    async Task<(int ExitCode, string Output)> PatinaAsync(params string[] arguments) {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Engine.Dll("Patina.Cli", "patina"));
        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        using var cli = Process.Start(info)!;
        var error = cli.StandardError.ReadToEndAsync(deadline.Token);
        var output = await cli.StandardOutput.ReadToEndAsync(deadline.Token);
        await cli.WaitForExitAsync(deadline.Token);
        return (cli.ExitCode, output + await error);
    }

    [Fact]
    public async Task The_cli_creates_and_checks_a_library() {
        var library = Path.Combine(folder.FullName, "Made.ptlib");

        Assert.Equal(0, (await PatinaAsync("library", "new", library, "--name", "Made")).ExitCode);
        Assert.NotEqual(0, (await PatinaAsync("library", "new", library)).ExitCode);

        var clean = await PatinaAsync("library", "check", library);
        Assert.Equal(0, clean.ExitCode);
        Assert.Contains("0 findings", clean.Output, StringComparison.Ordinal);

        Directory.CreateDirectory(Path.Combine(library, "projects", "broken"));
        await File.WriteAllTextAsync(Path.Combine(library, "projects", "broken", "project.yaml"), "<<<<<<<\n", Token);

        var broken = await PatinaAsync("library", "check", library);
        Assert.Equal(1, broken.ExitCode);
        Assert.Contains("Unreadable (1)", broken.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_cli_says_when_a_folder_is_no_library() {
        var (exitCode, output) = await PatinaAsync("library", "check", folder.FullName);

        Assert.Equal(1, exitCode);
        Assert.Contains("is not a library", output, StringComparison.Ordinal);
    }
}
