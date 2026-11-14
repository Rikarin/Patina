using Patina.Contracts;
using Patina.Library;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>A material brush laid in the model's space on a real mesh, with a real Worker (#125).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     ⚠ A material brush projected triplanar lays, on the housing, what a triplanar fill of the material lays
    ///     there, and not what the same brush lays over the UV layout: its size follows the model, not how the UVs are
    ///     laid out.
    /// </summary>
    [Fact]
    public async Task A_triplanar_material_brush_lays_what_a_triplanar_fill_lays_and_not_what_the_layout_does() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Triplanar.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Ramp", kind = "layered-material" },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        await File.WriteAllTextAsync(
            Path.Combine(Library, "projects", "ramp", "ramp.vxtexgraph"),
            CellGraph("{ id: 1, type: Source/Gradient }"),
            deadline.Token
        );
        var used = await http.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new { project = "ramp", source = "projects/ramp/ramp.vxtexgraph" },
            deadline.Token
        );
        var reply = await used.Content.ReadAsStringAsync(deadline.Token);
        Assert.True(used.IsSuccessStatusCode, reply);
        var graph = JsonElement.Parse(reply).GetProperty("graph").GetString()!;
        var state = await OpenAsync("projects/ramp/ramp.vxlayers", deadline.Token);
        await BindMeshAsync(state.Id, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("paint", "Paint"));
        var paint = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var size = (await PixelsAsync(state.Id, false, deadline.Token)).First(static map => map.Usage == "baseColor")
            .Width;

        // A quarter of the way across the housing's first side, which fills the first cell of a four-by-four layout.
        var (x, y) = ((int)(size * 0.3f / 4f), (int)(size * 0.6f / 4f));
        StackPlacement twice = new(2f);

        StackStroke Dab(string? projection) =>
            new(
                "material",
                paint,
                "baseColor",
                new(size / 32f, Falloff: 0f),
                Red,
                [new(x, y)],
                Source: new("material", graph, Projection: projection, Placement: twice)
            );

        await EditAsync(state.Id, deadline.Token, Dab("Triplanar"));
        var painted = await TexelAsync(state.Id, "triplanar", x, y, deadline.Token);
        await ActAsync(state.Id, "undo", deadline.Token);
        await EditAsync(state.Id, deadline.Token, Dab(null));
        var laid = await TexelAsync(state.Id, "layout", x, y, deadline.Token);
        await ActAsync(state.Id, "undo", deadline.Token);

        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Ramp"));
        var layer = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(layer, Fill: "Graph", Graph: graph, Projection: "Triplanar", Placement: twice)
        );
        var filled = await TexelAsync(state.Id, "filled", x, y, deadline.Token);

        Assert.Equal(filled, painted);
        Assert.NotEqual(laid, painted);
    }
}
