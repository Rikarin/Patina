using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>Material presets as documents of a project, over the API a client uses (#124).</summary>
public sealed class MaterialPresetApiTests : IAsyncLifetime {
    const string Rust = """
                        name: Rust
                        nodes:
                          - { id: 1, type: Source/Uniform, values: { Colour: [1, 0, 0, 1] } }
                          - { id: 2, type: Source/Uniform, values: { Colour: [0, 0, 1, 1] } }
                          - { id: 3, type: Colour/Blend, texts: { '=Opacity': 'redness' } }
                          - { id: 4, type: Output/Output, texts: { Usage: baseColor } }
                        edges:
                          - { fromNode: 2, fromPort: Out, toNode: 3, toPort: Background }
                          - { fromNode: 1, fromPort: Out, toNode: 3, toPort: Foreground }
                          - { fromNode: 3, fromPort: Out, toNode: 4, toPort: Input }
                        settings: { baseWidth: '64', baseHeight: '64', seed: '1' }
                        parameters:
                          - { name: redness, default: '0.25', kind: Float, minimum: 0, maximum: 1 }
                        """;

    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abcf
            name: Game
            texelDensity: 1024
            channels: []
            defaultShader: pbr
            exportTargets: []
            """
        );
        client = server.AuthorisedClient();
        foreach (var name in (string[])["Crate", "Barrel"]) {
            using var created = await client.PostAsJsonAsync(
                "/api/v1/library/projects",
                new ProjectCreateRequest(name, Kind: "layered-material"),
                Token
            );
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    /// <summary>
    ///     A graph fill's material and values become a preset, a <c>.ptpreset</c> document of the project that names
    ///     the material as the shelf has it. The shelf lists it among the materials, in its own project and in another,
    ///     where a search for the material's name finds it, and using it in the other project publishes the material
    ///     there and hands back the values for the fill.
    /// </summary>
    [Fact]
    public async Task A_fill_kept_as_a_preset_fills_a_layer_of_another_project_at_its_values() {
        var source = await RustAsync();
        var graph = await UseAsync("crate", source);

        using var kept = await client.PostAsJsonAsync(
            "/api/v1/library/materials/presets",
            new MaterialPresetCreateRequest(
                "crate",
                "Red Rust",
                graph.Graph,
                new Dictionary<string, string> { ["redness"] = "0.9" }
            ),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        var preset = (await kept.Content.ReadFromJsonAsync<MaterialInfo>(Token))!;
        Assert.Equal(("Red Rust", DocumentKinds.MaterialPreset, "material"), (preset.Name, preset.Kind, preset.Role));
        var file = MaterialPresets.Read(Path.Combine(server.Library, preset.Source));
        Assert.Equal(source, file.Material);
        Assert.Equal([new MaterialPresetValue("redness", "0.9")], file.Values);

        var own = await ShelfAsync("crate", null);
        Assert.Contains(own, material => material.Source == preset.Source && material.Origin == "project");
        // Another project's documents are found through the index, which catches up with the write behind the API.
        await ServerFixture.IndexedAsync(client, Token);
        var found = Assert.Single(
            await ShelfAsync("barrel", "rust"),
            static material => material.Kind == DocumentKinds.MaterialPreset
        );
        Assert.Equal(("library", "Crate"), (found.Origin, found.Project));

        var used = await UseAsync("barrel", preset.Source);
        Assert.Equal("0.9", used.Parameters!["redness"]);
        Assert.True(
            File.Exists(Path.Combine(server.Library, "projects", "barrel", "Compounds", used.Graph + ".vxtexgraph"))
        );
    }

    /// <summary>
    ///     A fill whose graph the shelf did not give has nothing a preset could find again, so keeping it is refused
    ///     with a code, and nothing is written.
    /// </summary>
    [Fact]
    public async Task A_graph_the_shelf_did_not_give_is_refused() {
        using var refused = await client.PostAsJsonAsync(
            "/api/v1/library/materials/presets",
            new MaterialPresetCreateRequest("crate", "Lost", "Materials/Nothing", new Dictionary<string, string>()),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
        Assert.Equal("library-refused", problem.RootElement.GetProperty("code").GetString());
        Assert.Empty(
            Directory.EnumerateFiles(Path.Combine(server.Library, "projects", "crate"), "*" + MaterialPresets.Extension)
        );
    }

    /// <summary>A material graph of the first project, through the API, as the shelf names it.</summary>
    async Task<string> RustAsync() {
        using var made = await client.PostAsJsonAsync(
            "/api/v1/library/project/documents",
            new ProjectDocumentRequest("crate", "graph", "Rust"),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, made.StatusCode);
        var document = (await made.Content.ReadFromJsonAsync<ProjectContents>(Token))!.Documents.First(static d =>
            d.Kind == "graph"
        );
        await File.WriteAllTextAsync(Path.Combine(server.Library, document.Path), Rust, Token);
        return document.Path;
    }

    async Task<MaterialUse> UseAsync(string project, string source) {
        using var used = await client.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new MaterialUseRequest(project, source),
            Token
        );
        Assert.True(used.IsSuccessStatusCode, await used.Content.ReadAsStringAsync(Token));
        return (await used.Content.ReadFromJsonAsync<MaterialUse>(Token))!;
    }

    async Task<IReadOnlyList<MaterialInfo>> ShelfAsync(string project, string? search) {
        var query = search is null ? string.Empty : $"&search={search}";
        return (await client.GetFromJsonAsync<MaterialList>(
            $"/api/v1/library/materials?project={project}{query}",
            Token
        ))!.Items;
    }
}
