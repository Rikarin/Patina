using Patina.Contracts;
using Patina.Library;
using System.Net.Http.Json;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     A material brush laid where a fill would lay it (#125), and a material picker reading every channel at a place
///     (#127), with a real Worker.
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A material brush that repeats its maps twice lays what a fill of the material repeated twice lays there, and
    ///     not what it lays once: the placement is the fill's, so a material is the same size painted as filled. The
    ///     picker then reads back, as numbers, the colour the export wrote at that place.
    /// </summary>
    /// <remarks>
    ///     ⚠ A gradient and not a noise: a noise's picture follows the id of the layer it is in (Vixen #875), and the
    ///     stack a material brush reads has a layer of its own, so its noise is another of the same noise.
    /// </remarks>
    [Fact]
    public async Task A_material_brush_lays_its_maps_where_a_fill_with_its_placement_would() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Material.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Grain", kind = "layered-material" },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        await File.WriteAllTextAsync(
            Path.Combine(Library, "projects", "grain", "noise.vxtexgraph"),
            CellGraph("{ id: 1, type: Source/Gradient }"),
            deadline.Token
        );
        var used = await http.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new { project = "grain", source = "projects/grain/noise.vxtexgraph" },
            deadline.Token
        );
        var reply = await used.Content.ReadAsStringAsync(deadline.Token);
        Assert.True(used.IsSuccessStatusCode, reply);
        var graph = JsonDocument.Parse(reply).RootElement.GetProperty("graph").GetString()!;
        var state = await OpenAsync("projects/grain/grain.vxlayers", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("paint", "Paint"));
        var paint = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var size = (await PixelsAsync(state.Id, false, deadline.Token)).First(static map => map.Usage == "baseColor")
            .Width;
        var at = size / 4;
        StackPlacement twice = new(Scale: 2f);

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackStroke(
                "material",
                paint,
                "baseColor",
                new(size / 16f, Falloff: 0f),
                Red,
                [new(at, at)],
                Source: new("material", graph, Placement: twice)
            )
        );
        var painted = await TexelAsync(state.Id, "painted", at, at, deadline.Token);
        var sampled = await SampleAsync(state.Id, (at + 0.5f) / size, (at + 0.5f) / size, deadline.Token);
        await ActAsync(state.Id, "undo", deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Grain"));
        var layer = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(layer, Fill: "Graph", Graph: graph));
        var once = await TexelAsync(state.Id, "once", at, at, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(layer, Placement: twice));
        var filled = await TexelAsync(state.Id, "twice", at, at, deadline.Token);

        Assert.NotEqual(once, filled);
        Assert.Equal(filled, painted);

        // The export writes eight bits of each, little end first: red is the low byte.
        var colour = sampled.Channels.Single(static channel => channel.Usage == "baseColor").Value;
        Assert.Equal(4, colour.Count);
        Assert.InRange(colour[0] * 255f, (painted & 0xFF) - 1.5f, (painted & 0xFF) + 1.5f);
        Assert.InRange(colour[1] * 255f, ((painted >> 8) & 0xFF) - 1.5f, ((painted >> 8) & 0xFF) + 1.5f);
        Assert.Equal(
            (await StateAsync(state.Id, deadline.Token)).Channels.Select(static channel => channel.Usage).Order(),
            sampled.Channels.Select(static channel => channel.Usage).Order()
        );
    }

    async Task<StackSample> SampleAsync(string id, float u, float v, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/stacks/{id}/sample",
            new(u, v),
            ContractsJsonContext.Default.StackFindRequest,
            token
        );
        await SucceededAsync(response, token);
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackSample, token))!;
    }
}
