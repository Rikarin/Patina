using Patina.Contracts;
using System.Buffers.Binary;
using System.Net;
using System.Net.WebSockets;

namespace Patina.Server.Tests;

/// <summary>One button for one map of a bake alone on its low mesh (#149), with a real Worker.</summary>
public sealed partial class BakeProcessTests {
    /// <summary>
    ///     Asked for one map, the viewport shows it alone through the picture view, with nothing else of the display
    ///     touched, and draws something other than the material; a map the bake did not make is not there.
    /// </summary>
    async Task OneMapAloneAsync(
        JobState job,
        ClientWebSocket socket,
        byte[] material,
        CancellationToken cancellationToken
    ) {
        using var alone = await http.PutAsJsonAsync(
            "/api/v1/viewport/bake",
            new(job.Id, "ao"),
            ContractsJsonContext.Default.ViewportBakeRequest,
            cancellationToken
        );
        alone.EnsureSuccessStatusCode();
        var display = (await alone.Content.ReadFromJsonAsync(
            ContractsJsonContext.Default.ViewportState,
            cancellationToken
        ))!.Display;
        Assert.Equal(ViewportViews.Picture, display.View);
        Assert.EndsWith("_ao.png", display.Picture, StringComparison.Ordinal);
        Assert.Equal(new ViewportDisplay().Environment, display.Environment);
        await SendAsync(socket, new(Input: 10), cancellationToken);
        var occlusion = await FrameAsync(socket, cancellationToken);
        while (BinaryPrimitives.ReadUInt32LittleEndian(occlusion.AsSpan(16)) != 10) {
            occlusion = await FrameAsync(socket, cancellationToken);
        }

        Assert.False(SamePicture(material, occlusion), "The map alone drew the material.");

        using var none = await http.PutAsJsonAsync(
            "/api/v1/viewport/bake",
            new(job.Id, "thickness"),
            ContractsJsonContext.Default.ViewportBakeRequest,
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }
}
