using Patina.Contracts;
using Patina.Library;
using System.Net.Http.Json;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>Graphs reaching a stack as a client sees it: their knobs, their roles and a save.</summary>
public sealed partial class StackProcessTests {
    /// <summary>A saved graph reaches a stack that is open on it, and a layer sets the graph's knobs.</summary>
    [Fact]
    public async Task A_saved_graph_and_a_layers_knob_both_change_the_picture() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Crate", kind = "layered-material" },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        var file = Path.Combine(Library, "projects", "crate", "cells.vxtexgraph");
        await File.WriteAllTextAsync(
            file,
            CellGraph("{ id: 1, type: Source/Noise, texts: { '=Scale': Cells } }"),
            deadline.Token
        );
        var used = await http.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new { project = "crate", source = "projects/crate/cells.vxtexgraph" },
            deadline.Token
        );
        var reply = await used.Content.ReadAsStringAsync(deadline.Token);
        Assert.True(used.IsSuccessStatusCode, reply);
        var graph = JsonDocument.Parse(reply).RootElement.GetProperty("graph").GetString();

        var state = await OpenAsync("projects/crate/crate.vxlayers", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Cells"));
        var layer = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(layer, Fill: "Graph", Graph: graph));
        var first = Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor");

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(layer, Parameters: new Dictionary<string, string> { ["Cells"] = "32" })
        );
        var knob = Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor");
        Assert.NotEqual(first, knob);

        var thumbnails = await http.GetFromJsonAsync<JsonElement>(
            $"/api/v1/stacks/{state.Id}/thumbnails",
            deadline.Token
        );
        var hash = Assert.Single(thumbnails.GetProperty("items").EnumerateArray()).GetProperty("hash").GetString();
        var picture = await http.GetAsync($"/api/v1/previews/{hash}", deadline.Token);
        Assert.True(picture.IsSuccessStatusCode);

        await File.WriteAllTextAsync(file, CellGraph("{ id: 1, type: Source/Uniform }"), deadline.Token);
        var refreshed = await http.PostAsync("/api/v1/library/materials/refresh?project=crate", null, deadline.Token);
        Assert.True(refreshed.IsSuccessStatusCode);

        // A file saved behind the stack is picked up without opening it again.
        for (var attempt = 0; attempt < 100; attempt++) {
            if (Digest(await PixelsAsync(state.Id, false, deadline.Token), "baseColor") != knob) {
                return;
            }

            await Task.Delay(100, deadline.Token);
        }

        Assert.Fail("The stack still shows the graph as it was before the save.");
    }

    /// <summary>A graph can be a brush, a mask or a filter, and a mask runs effects.</summary>
    [Fact]
    public async Task A_graph_serves_each_role_and_a_mask_runs_its_effects() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Crate", kind = "layered-material" },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        var file = Path.Combine(Library, "projects", "crate", "spots.vxtexgraph");
        await File.WriteAllTextAsync(
            file,
            CellGraph("{ id: 1, type: Source/Noise, texts: { '=Scale': Cells } }"),
            deadline.Token
        );

        var brush = await http.PostAsJsonAsync(
            "/api/v1/library/materials/brush",
            new { project = "crate", source = "projects/crate/spots.vxtexgraph" },
            deadline.Token
        );
        var reply = await brush.Content.ReadAsStringAsync(deadline.Token);
        if (!brush.IsSuccessStatusCode) {
            Gpu.Skip(reply);
        }

        Assert.Equal("brushes/spots.png", JsonDocument.Parse(reply).RootElement.GetProperty("alpha").GetString());
        Assert.True(File.Exists(Path.Combine(Library, "projects", "crate", "brushes", "spots.png")));

        var used = await http.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new { project = "crate", source = "projects/crate/spots.vxtexgraph" },
            deadline.Token
        );
        var graph = JsonDocument.Parse(await used.Content.ReadAsStringAsync(deadline.Token))
            .RootElement.GetProperty("graph")
            .GetString()!;
        var state = await OpenAsync("projects/crate/crate.vxlayers", deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer(
                "fill",
                "Red",
                new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = [1f, 0f, 0f, 1f] }
            )
        );
        var layer = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(layer, Mask: new("Generator", Generator: graph)));
        var masked = Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor");

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(layer, Mask: new("Generator", Generator: graph, Effects: [new("Colour/Invert")]))
        );
        var inverted = Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor");

        Assert.NotEqual(masked, inverted);
        var shown = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Mask.Effects;
        Assert.Equal("Colour/Invert", Assert.Single(shown!).Node);
    }

    static string CellGraph(string source) =>
        $$"""
          name: Cells
          nodes:
            - {{source}}
            - { id: 2, type: Output/Output, texts: { Usage: baseColor } }
          edges:
            - { fromNode: 1, fromPort: Out, toNode: 2, toPort: Input }
          groups: []
          comments: []
          interface: []
          settings: { baseWidth: '256', baseHeight: '256', seed: '1' }
          parameters:
            - name: Cells
              default: '4'
              kind: Float
          """;
}
