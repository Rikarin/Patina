using Patina.Contracts;
using Patina.Library;
using System.Net;
using Vixen.Core.Imaging;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>A graph lights the viewport (#180), through the real Server and Worker.</summary>
public sealed partial class GraphProcessTests {
    [Fact]
    public async Task A_graph_lights_the_viewport_as_a_panorama_with_its_sun_past_one() {
        using var deadline = Deadline(120);
        await StartServerAsync(deadline.Token);
        StartWorker();
        var sky = GraphTesting.Write(Library, Sky(256, 128), "sky.vxtexgraph");

        using var reply = await http.PutAsJsonAsync(
            "/api/v1/viewport/display",
            new ViewportDisplay { Environment = sky },
            ContractsJsonContext.Default.ViewportDisplay,
            deadline.Token
        );

        if (reply.StatusCode == HttpStatusCode.ServiceUnavailable) {
            Assert.Fail(await reply.Content.ReadAsStringAsync(deadline.Token));
        }

        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        var state = await reply.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ViewportState, deadline.Token);
        Assert.Equal(sky, state!.Display.Environment);
        var drawn = Directory.GetFiles(LibraryLayout.PreviewsPath(Library), "*.exr", SearchOption.AllDirectories);
        var panorama = ExrCodec.Decode(await File.ReadAllBytesAsync(Assert.Single(drawn), deadline.Token));

        // Computed at the panorama's side along its longer axis, keeping the graph's two to one, and not clamped.
        Assert.Equal((1024, 512), (panorama.Width, panorama.Height));
        Assert.True(panorama.Samples.Max() > 1f, "the sun is brighter than one");
    }

    [Fact]
    public async Task Saving_the_graph_that_lights_the_viewport_draws_it_again() {
        using var deadline = Deadline(120);
        await StartServerAsync(deadline.Token);
        StartWorker();
        var sky = GraphTesting.Write(Library, Sky(256, 128), "sky.vxtexgraph");
        using (var lit = await http.PutAsJsonAsync(
                   "/api/v1/viewport/display",
                   new ViewportDisplay { Environment = sky },
                   ContractsJsonContext.Default.ViewportDisplay,
                   deadline.Token
               )) {
            Assert.Equal(HttpStatusCode.OK, lit.StatusCode);
        }

        var state = await http.OpenAsync(sky, deadline.Token);
        await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(1, "Sun Elevation", [0.2f]));
        await http.ActAsync(state.Id, "save", deadline.Token);

        // The sun is lower, so the panorama is another picture, drawn beside the first.
        var previews = LibraryLayout.PreviewsPath(Library);
        while (Directory.GetFiles(previews, "*.exr", SearchOption.AllDirectories).Length < 2) {
            await Task.Delay(100, deadline.Token);
        }
    }

    [Fact]
    public async Task A_graph_with_no_output_is_refused_as_an_environment() {
        using var deadline = Deadline(60);
        await StartServerAsync(deadline.Token);
        var empty = Sky(256, 128) with { Nodes = [Sky(256, 128).Nodes[0]], Edges = [] };
        var file = GraphTesting.Write(Library, empty, "empty.vxtexgraph");

        using var reply = await http.PutAsJsonAsync(
            "/api/v1/viewport/display",
            new ViewportDisplay { Environment = file },
            ContractsJsonContext.Default.ViewportDisplay,
            deadline.Token
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reply.StatusCode);
        Assert.Contains(
            "panorama-empty",
            await reply.Content.ReadAsStringAsync(deadline.Token),
            StringComparison.Ordinal
        );
    }

    /// <summary>A sky (1) into an Output (2), at a size two to one.</summary>
    static NodeGraphAsset Sky(int width, int height) =>
        new() {
            Name = "Sky",
            Nodes = [
                new() { Id = 1, Type = "Environment/Sky" },
                new() { Id = 2, Type = "Output/Output", X = 300, Texts = new() { ["Usage"] = "baseColor" } }
            ],
            Edges = [new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" }],
            Settings = new() { ["seed"] = "7", ["baseWidth"] = $"{width}", ["baseHeight"] = $"{height}" }
        };
}
