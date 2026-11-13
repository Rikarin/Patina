using Patina.Contracts;
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
}
