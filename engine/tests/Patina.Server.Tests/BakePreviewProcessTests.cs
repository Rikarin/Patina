using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Buffers.Binary;
using System.Net;
using System.Net.WebSockets;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>A bake's setup shown in the viewport before it runs (#149), by the processes the bakes run on.</summary>
public sealed partial class BakeProcessTests {
    Task<HttpResponseMessage> PreviewAsync(ViewportBakePreview preview, CancellationToken cancellationToken) =>
        http.PutAsJsonAsync(
            "/api/v1/viewport/bake-preview",
            preview,
            ContractsJsonContext.Default.ViewportBakePreview,
            cancellationToken
        );

    async Task IsolateAsync(ViewportIsolation isolation, CancellationToken cancellationToken) {
        using var response = await http.PutAsJsonAsync(
            "/api/v1/viewport/isolation",
            isolation,
            ContractsJsonContext.Default.ViewportIsolation,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    /// <summary>The first frame that carries an input.</summary>
    async Task FrameAfterAsync(ClientWebSocket socket, uint input, CancellationToken token) {
        var frame = await FrameAsync(socket, token);
        while (BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(16)) < input) {
            frame = await FrameAsync(socket, token);
        }
    }

    /// <summary>
    ///     Where a picture of a frame goes, for a person to look at, when the environment names a
    ///     folder.
    /// </summary>
    static void Keep(byte[] frame, string name) {
        if (Environment.GetEnvironmentVariable("PATINA_KEEP_FRAMES") is not { Length: > 0 } folder) {
            return;
        }

        var width = (int)BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8));
        var height = (int)BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(
            Path.Combine(folder, $"{name}.png"),
            PngCodec.Encode(new(width, height, frame[ViewportFrameHeader..(ViewportFrameHeader + width * height * 4)]))
        );
    }

    /// <summary>
    ///     A bake's setup lies over the low mesh before it runs (#149): the reach, the high mesh and the parts matched
    ///     by
    ///     name, with the key in the answer. The Server holds it, so a new Worker draws it again, and an empty low mesh
    ///     takes it off.
    /// </summary>
    [Fact]
    public async Task A_bake_s_setup_lies_over_the_mesh_and_outlives_the_worker() {
        using var deadline = Deadline(180);
        StartServer();
        await DiscoverAsync(deadline.Token);
        var worker = StartWorker();
        var request = Request();

        using var socket = await ViewportAsync(deadline.Token);
        await SendAsync(socket, new(new(0.6d, 0.35d, 1.6d), 320, 200, 1), deadline.Token);
        while ((await ViewportStateAsync(deadline.Token)).Width != 320) {
            await Task.Delay(10, deadline.Token);
        }

        using var opened = await http.PutAsJsonAsync(
            "/api/v1/viewport/mesh",
            new(request.Low),
            ContractsJsonContext.Default.ViewportMeshRequest,
            deadline.Token
        );
        opened.EnsureSuccessStatusCode();
        var bare = await FrameAsync(socket, deadline.Token);
        Keep(bare, "preview-off");

        var preview = new ViewportBakePreview {
            High = request.High,
            Low = request.Low,
            Options = request.Options with { Rays = new() { Front = 0.05, Rear = 0.03, MatchByName = true } }
        };
        using var shown = await PreviewAsync(preview, deadline.Token);
        if (shown.StatusCode == HttpStatusCode.UnprocessableEntity
            && await shown.Content.ReadAsStringAsync(deadline.Token) is var refusal
            && refusal.Contains("No Vulkan", StringComparison.Ordinal)) {
            Gpu.Skip(refusal);
        }

        shown.EnsureSuccessStatusCode();
        var state = (await shown.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.BakePreviewState,
            deadline.Token
        ))!;
        Assert.Equal(request.Low, state.Preview!.Low);
        Assert.Contains(state.Parts, static part => part is { LowFaces: > 0, HighFaces: > 0 });

        await SendAsync(socket, new(Input: 2), deadline.Token);
        var laid = await FrameAsync(socket, deadline.Token);
        while (BinaryPrimitives.ReadUInt32LittleEndian(laid.AsSpan(16)) != 2) {
            laid = await FrameAsync(socket, deadline.Token);
        }

        Keep(laid, "preview-on");
        Assert.False(SamePicture(bare, laid), "The preview drew nothing.");

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        await SendAsync(socket, new(Input: 9), deadline.Token);
        await Task.Delay(100, deadline.Token);
        StartWorker();
        var again = await FrameAsync(socket, deadline.Token);
        while (BinaryPrimitives.ReadUInt32LittleEndian(again.AsSpan(16)) != 9) {
            again = await FrameAsync(socket, deadline.Token);
        }

        Assert.True(SamePicture(laid, again), "A new Worker drew something else.");

        // The preview keeps to what the viewport draws alone (#136): with no part of the mesh shown, nothing of it.
        await IsolateAsync(new(["no such part"], []), deadline.Token);
        await SendAsync(socket, new(Input: 10), deadline.Token);
        await FrameAfterAsync(socket, 10, deadline.Token);
        var alone = (await http.GetFromJsonAsync(
            "/api/v1/viewport/bake-preview",
            ContractsJsonContext.Default.BakePreviewState,
            deadline.Token
        ))!;
        Assert.Contains(alone.Warnings, static warning => warning.Contains("shown alone", StringComparison.Ordinal));
        await IsolateAsync(new([], []), deadline.Token);

        using var off = await PreviewAsync(new(), deadline.Token);
        off.EnsureSuccessStatusCode();
        await SendAsync(socket, new(Input: 12), deadline.Token);
        var cleared = await FrameAsync(socket, deadline.Token);
        while (BinaryPrimitives.ReadUInt32LittleEndian(cleared.AsSpan(16)) != 12) {
            cleared = await FrameAsync(socket, deadline.Token);
        }

        Assert.True(SamePicture(bare, cleared), "Taking the preview off left something behind.");
    }

    /// <summary>
    ///     A preview of a project follows its <c>project.yaml</c> (#300): written by hand or by another client through
    ///     <c>PUT /library/project/bake</c>, the next frame reads the setup from it again, with no client asking.
    /// </summary>
    [Fact]
    public async Task A_preview_of_a_project_follows_its_file_when_another_client_changes_it() {
        using var deadline = Deadline(180);
        var (shown, crate, file, _) = await ShowCrateAsync(deadline.Token);
        using var socket = shown;
        Assert.Equal(0.05, await FrontAsync(socket, 2, deadline.Token));

        // By hand, as an editor of the file or another program would.
        File.WriteAllText(file, DocumentCodec.Write(crate with { Bake = Front(crate, 0.2) }));
        Assert.Equal(0.2, await FrontAsync(socket, 3, deadline.Token));

        // Through the API, as another client would.
        using var kept = await http.PutAsJsonAsync(
            "/api/v1/library/project/bake",
            new ProjectBakeRequest("crate", Front(crate, 0.1)),
            deadline.Token
        );
        kept.EnsureSuccessStatusCode();
        Assert.Equal(0.1, await FrontAsync(socket, 4, deadline.Token));
    }

    /// <summary>
    ///     What the preview shows is read again when asked, before any frame is drawn (#305): a project file changed
    ///     by hand, and a mesh file rewritten in place, as Reload Mesh reads one.
    /// </summary>
    [Fact]
    public async Task A_preview_answers_with_its_files_as_they_are_before_a_frame_is_drawn() {
        using var deadline = Deadline(180);
        var (shown, crate, file, project) = await ShowCrateAsync(deadline.Token);
        using var socket = shown;
        var before = await PreviewStateAsync(deadline.Token);
        Assert.Equal(0.05, before.Preview!.Options.Rays.Front);

        File.WriteAllText(file, DocumentCodec.Write(crate with { Bake = Front(crate, 0.3) }));
        Assert.Equal(0.3, (await PreviewStateAsync(deadline.Token)).Preview!.Options.Rays.Front);

        var faces = before.Parts.Sum(static part => part.HighFaces);
        Assert.True(faces > 0, "The preview counts the high mesh's faces.");
        BakeFixture.Write(project, 0.04f); // a coarser high mesh, written over the one shown
        var after = await PreviewStateAsync(deadline.Token);
        Assert.Empty(after.Warnings);
        Assert.NotEqual(faces, after.Parts.Sum(static part => part.HighFaces));
    }

    /// <summary>
    ///     A library with the crate project, a Server and a Worker, the crate's low mesh in the viewport, and the
    ///     project's bake setup shown over it.
    /// </summary>
    async Task<(ClientWebSocket Socket, ProjectDocument Crate, string File, string Folder)> ShowCrateAsync(
        CancellationToken token
    ) {
        File.WriteAllText(
            Path.Combine(Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abd0
            name: Game
            texelDensity: 1024
            channels: []
            defaultShader: pbr
            exportTargets: []
            """
        );
        var project = Directory.CreateDirectory(Path.Combine(Library, "projects", "crate")).FullName;
        BakeFixture.Write(project, 0.02f);
        var mesh = AssetId.New();
        ProjectDocument crate = new(ProjectId.New(), "Crate", [], [], []) {
            Meshes = [new(mesh, BakeFixture.LowFile, BakeFixture.HighFile)],
            Bake = new() { Options = new() { Resolution = 64, Rays = new() { Front = 0.05, MatchByName = true } } }
        };
        var file = Path.Combine(project, LibraryLayout.ProjectFile);
        File.WriteAllText(file, DocumentCodec.Write(crate));
        StartServer();
        await DiscoverAsync(token);
        StartWorker();

        var socket = await ViewportAsync(token);
        await SendAsync(socket, new(new(0.6d, 0.35d, 1.6d), 320, 200, 1), token);
        while ((await ViewportStateAsync(token)).Width != 320) {
            await Task.Delay(10, token);
        }

        using var opened = await http.PutAsJsonAsync(
            "/api/v1/viewport/mesh",
            new(Path.Combine(project, BakeFixture.LowFile)),
            ContractsJsonContext.Default.ViewportMeshRequest,
            token
        );
        opened.EnsureSuccessStatusCode();
        await FrameAsync(socket, token);
        using var shown = await PreviewAsync(new() { Project = "crate" }, token);
        if (shown.StatusCode == HttpStatusCode.UnprocessableEntity
            && await shown.Content.ReadAsStringAsync(token) is var refusal
            && refusal.Contains("No Vulkan", StringComparison.Ordinal)) {
            socket.Dispose();
            Gpu.Skip(refusal);
        }

        Assert.True(shown.IsSuccessStatusCode, await shown.Content.ReadAsStringAsync(token));
        return (socket, crate, file, project);
    }

    async Task<BakePreviewState> PreviewStateAsync(CancellationToken token) =>
        (await http.GetFromJsonAsync(
            "/api/v1/viewport/bake-preview",
            ContractsJsonContext.Default.BakePreviewState,
            token
        ))!;

    static ProjectBakeSettings Front(ProjectDocument project, double front) =>
        project.Bake! with {
            Options = project.Bake.Options with { Rays = project.Bake.Options.Rays with { Front = front } }
        };

    /// <summary>The front distance the preview shows once a frame carrying an input is drawn.</summary>
    async Task<double> FrontAsync(ClientWebSocket socket, uint input, CancellationToken token) {
        await SendAsync(socket, new(Input: input), token);
        await FrameAfterAsync(socket, input, token);
        var state = await PreviewStateAsync(token);
        Assert.DoesNotContain(
            state.Warnings,
            static warning => warning.Contains("no longer", StringComparison.Ordinal)
        );
        return state.Preview!.Options.Rays.Front;
    }
}
