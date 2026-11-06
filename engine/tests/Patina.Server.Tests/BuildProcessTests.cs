using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>
///     Builds as a client sees them: the real Server and Worker, paths in and paths out. A build of bakes needs no GPU,
///     so the kills here run on every platform. What is killed is the process, part-way through a real step.
/// </summary>
public sealed class BuildProcessTests : IDisposable {
    const int Projects = 3;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-build-");
    readonly string socketPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");
    readonly List<Process> started = [];
    readonly HttpClient http = new();
    readonly HttpClient restarted = new();

    string Library => Path.Combine(folder.FullName, "Game.ptlib");

    string DiscoveryFile => Path.Combine(folder.FullName, "servers.json");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() {
        foreach (var process in started) {
            if (!process.HasExited) {
                process.Kill(true);
                process.WaitForExit();
            }

            process.Dispose();
        }

        http.Dispose();
        restarted.Dispose();
        File.Delete(socketPath);
        try {
            folder.Delete(true);
        } catch (IOException) {
            // A Worker's last write can land while the folder is going.
        }
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

    Process StartWorker() =>
        Start(Engine.Dll("Patina.Worker", "patina-worker"), "--socket", socketPath, "--watch-stdin");

    Process StartServer() =>
        Start(
            Engine.Dll("Patina.Server", "patina-server"),
            "--library",
            Library,
            $"--Patina:DiscoveryFile={DiscoveryFile}",
            $"--Patina:Worker:SocketPath={socketPath}",
            "--Patina:WatchStdin=true"
        );

    async Task DiscoverAsync(HttpClient client, Uri? notThis, CancellationToken cancellationToken) {
        var registry = new InstanceRegistry(DiscoveryFile);
        while (true) {
            if (await registry.FindAsync(Library, cancellationToken) is { } instance) {
                var address = new Uri($"http://127.0.0.1:{instance.Port}");
                if (address != notThis) {
                    client.BaseAddress = address;
                    client.DefaultRequestHeaders.Authorization = new("Bearer", instance.Token);
                    return;
                }
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    static CancellationTokenSource Deadline(int seconds) {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return deadline;
    }

    /// <summary>Projects that each bake a mesh pair of their own, slowly enough to be killed in.</summary>
    void WriteLibrary(string? resolution = null) {
        LibraryFiles.CreateLibrary(
            Library,
            new(LibraryId.From(Guid.NewGuid()), "Game", 1024, [], "pbr", ["unity-urp"])
        );
        for (var project = 0; project < Projects; project++) {
            var path = $"p{project}";
            LibraryFiles.WriteProject(
                Library,
                path,
                new(
                    ProjectId.From(Guid.NewGuid()),
                    $"Prop {project}",
                    [],
                    [],
                    [
                        new OutputDeclaration(
                            "bake",
                            BakeKeys.Tool,
                            [BakeFixture.LowFile, BakeFixture.HighFile],
                            [],
                            new Dictionary<string, string> {
                                ["low"] = BakeFixture.LowFile,
                                ["high"] = BakeFixture.HighFile,
                                ["resolution"] = resolution ?? $"{256 + project}",
                                ["rays"] = "128",
                                ["maps"] = "ao"
                            }
                        )
                    ]
                )
            );
            BakeFixture.Write(Path.Combine(Library, "projects", path), 0.02f);
        }
    }

    static async Task<JobState> OneAsync(HttpClient client, string id, CancellationToken cancellationToken) =>
        (await client.GetFromJsonAsync(
            $"/api/v1/jobs/{id}",
            ContractsJsonContext.Default.JobState,
            cancellationToken
        ))!;

    static async Task<JobState> UntilAsync(
        HttpClient client,
        string id,
        Func<JobState, bool> done,
        CancellationToken cancellationToken
    ) {
        while (true) {
            var job = await OneAsync(client, id, cancellationToken);
            if (done(job)) {
                return job;
            }

            await Task.Delay(25, cancellationToken);
        }
    }

    static bool Over(JobState job) => job.State is JobStates.Done or JobStates.Failed or JobStates.Cancelled;

    static async Task<JobState> BuildAsync(HttpClient client, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync(
            "/api/v1/builds",
            new(),
            ContractsJsonContext.Default.BuildRequest,
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.JobState, cancellationToken))!;
    }

    void AssertFinished(JobState job) {
        Assert.Equal(JobStates.Done, job.State);
        Assert.Null(job.Error);
        Assert.Equal(Projects, job.Report!.Built);
        Assert.Equal(Projects, job.Report.Exported);
        for (var project = 0; project < Projects; project++) {
            var exported = Path.Combine(Library, "exports", "unity-urp", $"p{project}", $"Prop_{project}_Normal.png");
            Assert.True(File.Exists(exported), exported);
        }
    }

    [Fact]
    public async Task A_killed_worker_loses_only_its_step_and_a_second_build_does_nothing() {
        using var deadline = Deadline(300);
        WriteLibrary();
        StartServer();
        await DiscoverAsync(http, null, deadline.Token);
        var worker = StartWorker();

        var preview = await (await http.PostAsJsonAsync(
                "/api/v1/builds/preview",
                new(),
                ContractsJsonContext.Default.BuildRequest,
                deadline.Token
            ))
            .Content.ReadFromJsonAsync(ContractsJsonContext.Default.BuildPreview, deadline.Token);
        Assert.Equal(Projects, preview!.Rebuilds);

        var job = await BuildAsync(http, deadline.Token);
        await UntilAsync(http, job.Id, static j => Over(j) || j.Report is { Built: >= 1 }, deadline.Token);

        worker.Kill(true);
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();

        var finished = await UntilAsync(http, job.Id, Over, deadline.Token);
        AssertFinished(finished);
        Assert.True(finished.Attempts >= 2, $"{finished.Attempts} attempts");

        var again = await BuildAsync(http, deadline.Token);
        var nothing = await UntilAsync(http, again.Id, Over, deadline.Token);
        Assert.Equal(0, nothing.Report!.Projects);
        Assert.Equal(Projects, nothing.Report.Fresh);

        var report = await http.GetFromJsonAsync(
            $"/api/v1/builds/{job.Id}/report/projects",
            ContractsJsonContext.Default.ProjectReportPage,
            deadline.Token
        );
        Assert.Equal(Projects, report!.Total);
        Assert.All(
            report.Items,
            static p => Assert.All(p.Exports.SelectMany(static e => e.Files), static f => Assert.True(File.Exists(f)))
        );
    }

    [Fact]
    public async Task A_killed_server_comes_back_to_a_build_and_its_report_keeps_what_was_done() {
        using var deadline = Deadline(300);
        WriteLibrary();
        var server = StartServer();
        await DiscoverAsync(http, null, deadline.Token);
        StartWorker();

        var job = await BuildAsync(http, deadline.Token);
        await UntilAsync(http, job.Id, static j => Over(j) || j.Report is { Built: >= 1 }, deadline.Token);

        server.Kill(true);
        await server.WaitForExitAsync(deadline.Token);
        http.CancelPendingRequests();
        File.Delete(DiscoveryFile);
        File.Delete(socketPath);

        StartServer();
        await DiscoverAsync(restarted, http.BaseAddress, deadline.Token);

        // The old Worker reconnects by itself and is handed what is left.
        AssertFinished(await UntilAsync(restarted, job.Id, Over, deadline.Token));
    }

    [Fact]
    public async Task A_bad_setting_fails_its_job_and_the_server_carries_on() {
        using var deadline = Deadline(120);
        WriteLibrary("2k");
        var server = StartServer();
        await DiscoverAsync(http, null, deadline.Token);
        StartWorker();

        var job = await BuildAsync(http, deadline.Token);
        var finished = await UntilAsync(http, job.Id, Over, deadline.Token);

        Assert.Equal(JobStates.Done, finished.State);
        Assert.True(finished.Report!.Failed >= Projects);
        Assert.False(server.HasExited);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/v1/system", deadline.Token)).StatusCode);
    }

    [Fact]
    public async Task A_preset_nobody_has_is_refused_before_anything_queues() {
        using var deadline = Deadline(60);
        WriteLibrary();
        StartServer();
        await DiscoverAsync(http, null, deadline.Token);

        var response = await http.PostAsJsonAsync(
            "/api/v1/builds",
            new() { Presets = ["nobody"] },
            ContractsJsonContext.Default.BuildRequest,
            deadline.Token
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
