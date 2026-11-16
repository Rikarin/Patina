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
            new() { Environment = sky },
            ContractsJsonContext.Default.ViewportDisplay,
            deadline.Token
        );

        await LitAsync(reply, deadline.Token);
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
                   new() { Environment = sky },
                   ContractsJsonContext.Default.ViewportDisplay,
                   deadline.Token
               )) {
            await LitAsync(lit, deadline.Token);
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

    /// <summary>
    ///     A sky being made followed saves only, so each try at the sun needed a save (#300). An edit the graph has
    ///     evaluated now lights the model, and the file is left as it was.
    /// </summary>
    [Fact]
    public async Task Editing_the_graph_that_lights_the_viewport_draws_it_again_before_a_save() {
        using var deadline = Deadline(120);
        await StartServerAsync(deadline.Token);
        StartWorker();
        var sky = GraphTesting.Write(Library, Sky(256, 128), "sky.vxtexgraph");
        var saved = await File.ReadAllTextAsync(sky, deadline.Token);
        using (var lit = await http.PutAsJsonAsync(
                   "/api/v1/viewport/display",
                   new() { Environment = sky },
                   ContractsJsonContext.Default.ViewportDisplay,
                   deadline.Token
               )) {
            await LitAsync(lit, deadline.Token);
        }

        var state = await http.OpenAsync(sky, deadline.Token);
        await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(1, "Sun Elevation", [0.2f]));

        var previews = LibraryLayout.PreviewsPath(Library);
        while (Directory.GetFiles(previews, "*.exr", SearchOption.AllDirectories).Length < 2) {
            await Task.Delay(100, deadline.Token);
        }

        Assert.Equal(saved, await File.ReadAllTextAsync(sky, deadline.Token));
    }

    [Fact]
    public async Task A_graph_with_no_output_is_refused_as_an_environment() {
        using var deadline = Deadline(60);
        await StartServerAsync(deadline.Token);
        var empty = Sky(256, 128) with { Nodes = [Sky(256, 128).Nodes[0]], Edges = [] };
        var file = GraphTesting.Write(Library, empty, "empty.vxtexgraph");

        using var reply = await http.PutAsJsonAsync(
            "/api/v1/viewport/display",
            new() { Environment = file },
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

    [Fact]
    public async Task A_graph_that_cannot_be_drawn_is_refused_with_the_Workers_reason() {
        using var deadline = Deadline(120);
        await StartServerAsync(deadline.Token);
        StartWorker();
        var sky = Sky(256, 128);
        var broken = sky with { Nodes = [sky.Nodes[0] with { Type = "Environment/Nowhere" }, sky.Nodes[1]] };
        var file = GraphTesting.Write(Library, broken, "broken.vxtexgraph");

        using var reply = await http.PutAsJsonAsync(
            "/api/v1/viewport/display",
            new() { Environment = file },
            ContractsJsonContext.Default.ViewportDisplay,
            deadline.Token
        );

        var problem = await reply.Content.ReadAsStringAsync(deadline.Token);
        if (problem.Contains("No Vulkan", StringComparison.Ordinal)) {
            Gpu.Skip(problem);
        }

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reply.StatusCode);
        Assert.Contains("engine-refused", problem, StringComparison.Ordinal);
        Assert.Contains("'broken.vxtexgraph' cannot be drawn", problem, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Passes a display the graph lit, skips on a machine with no GPU, and fails with the Server's reply for
    ///     anything else, so a refusal names its reason in the log.
    /// </summary>
    static async Task LitAsync(HttpResponseMessage reply, CancellationToken cancellationToken) {
        if (reply.StatusCode == HttpStatusCode.OK) {
            return;
        }

        var problem = await reply.Content.ReadAsStringAsync(cancellationToken);
        if (problem.Contains("No Vulkan", StringComparison.Ordinal)) {
            Gpu.Skip(problem);
        }

        Assert.Fail($"{(int)reply.StatusCode}: {problem}");
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
