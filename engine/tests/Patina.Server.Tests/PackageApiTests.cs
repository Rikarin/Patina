using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Text.Json;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>
///     Publishing a graph as a package (ADR 0027) over the API any client uses, and every project's shelf offering it,
///     with no GPU: what publishing brings, what it writes, and that a project using it gets what it reads.
/// </summary>
public sealed class PackageApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    const string Library = """
                           schema: 1
                           id: 0190a000-0000-7000-8000-00000000abcd
                           name: Game
                           texelDensity: 1024
                           channels: []
                           defaultShader: pbr
                           exportTargets: []
                           """;

    /// <summary>
    ///     A picture through a compound of the project's, a shipped grunge over it, and a knob on the blend:
    ///     something of each kind a package brings or leaves.
    /// </summary>
    const string Rust = """
                        name: Rust
                        nodes:
                          - { id: 1, type: Source/Bitmap, texts: { Source: mask.png } }
                          - { id: 2, type: Wear }
                          - { id: 3, type: Grunges/Speckle, texts: { amount: '0.8' } }
                          - { id: 4, type: Colour/Blend, texts: { Mode: Multiply, '=Opacity': 'tint' } }
                          - { id: 5, type: Output/Output, texts: { Usage: baseColor } }
                        edges:
                          - { fromNode: 1, fromPort: Out, toNode: 2, toPort: In }
                          - { fromNode: 2, fromPort: Out, toNode: 4, toPort: Background }
                          - { fromNode: 3, fromPort: Out, toNode: 4, toPort: Foreground }
                          - { fromNode: 4, fromPort: Out, toNode: 5, toPort: Input }
                        settings: { baseWidth: '1024', baseHeight: '1024', seed: '1', size: '64' }
                        parameters:
                          - { name: tint, default: '0.5', kind: Float, minimum: 0, maximum: 1, label: Tint }
                        presets:
                          - { name: Heavy, values: { tint: '0.9' } }
                        description: Rust over a picture.
                        author: Ines
                        tags: [metal]
                        """;

    const string Wear = """
                        name: Wear
                        nodes:
                          - { id: 1, type: Sub-graph/Input }
                          - { id: 2, type: Colour/Levels, values: { 'Input White': [0.5] } }
                          - { id: 3, type: Sub-graph/Output }
                        edges:
                          - { fromNode: 1, fromPort: In, toNode: 2, toPort: Input }
                          - { fromNode: 2, fromPort: Out, toNode: 3, toPort: Out }
                        interface:
                          - { name: In, direction: Input, kind: Image }
                          - { name: Out, direction: Output, kind: Image }
                        """;

    public async ValueTask InitializeAsync() {
        File.WriteAllText(Path.Combine(server.Library, "library.yaml"), Library);
        foreach (var folder in new[] { "shared", "projects" }) {
            Directory.CreateDirectory(Path.Combine(server.Library, folder));
        }

        client = server.AuthorisedClient();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    /// <summary>A project holding the Rust graph, its compound and its picture, tagged by the project.</summary>
    async Task<string> RustProjectAsync() {
        await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Rusty Kit"), Token);
        var made = await client.PostAsJsonAsync(
            "/api/v1/library/project/documents",
            new ProjectDocumentRequest("rusty-kit", "graph", "Rust"),
            Token
        );
        var document = (await made.Content.ReadFromJsonAsync<ProjectContents>(Token))!.Documents.First(static d =>
            d.Kind == "graph"
        );
        var project = Path.Combine(server.Library, "projects", "rusty-kit");
        await File.WriteAllTextAsync(Path.Combine(server.Library, document.Path), Rust, Token);
        Directory.CreateDirectory(Path.Combine(project, "Compounds"));
        await File.WriteAllTextAsync(Path.Combine(project, "Compounds", "Wear.vxtexgraph"), Wear, Token);
        await File.WriteAllBytesAsync(Path.Combine(project, "mask.png"), [1, 2, 3, 4], Token);
        var tagged = await client.PatchAsJsonAsync(
            "/api/v1/library/project/document/tags",
            new ProjectDocumentTagsRequest("rusty-kit", document.Id!.Value, ["weathered"]),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, tagged.StatusCode);
        return document.Path;
    }

    async Task<PackagePlan> PlanAsync(string graph) =>
        (await client.GetFromJsonAsync<PackagePlan>(
            $"/api/v1/library/packages/plan?graph={Uri.EscapeDataString(graph)}",
            Token
        ))!;

    [Fact]
    public async Task A_graph_publishes_as_a_package_with_what_it_reads_and_every_shelf_offers_it() {
        var graph = await RustProjectAsync();

        var plan = await PlanAsync(graph);
        Assert.Equal(
            ("Rust", "material", "packages/Rust.ptpkg", false),
            (plan.Name, plan.Role, plan.Path, plan.Replaces)
        );
        Assert.Empty(plan.Diagnostics);
        Assert.All(plan.Dependencies, static d => Assert.Null(d.Problem));
        Assert.Equal(
            [
                ("compound", "Wear", "projects/rusty-kit/Compounds/Wear.vxtexgraph"),
                ("picture", "mask.png", "projects/rusty-kit/mask.png"),
                ("shipped", "Grunges/Speckle", null)
            ],
            plan.Dependencies.Select(static d => (d.Kind, d.Name, d.File))
        );

        var published = await client.PostAsJsonAsync("/api/v1/library/packages", new PackageRequest(graph), Token);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var package = (await published.Content.ReadFromJsonAsync<PackagePublished>(Token))!;
        Assert.Equal("packages/Rust.ptpkg", package.Path);
        Assert.Equal([new("Packages/Rust/Wear", "Wear")], package.Manifest.Compounds);
        Assert.Equal(["Grunges/Speckle"], package.Manifest.Shipped);
        var file = Assert.Single(package.Manifest.Files);
        Assert.Equal("mask.png", file.From);
        Assert.Matches("""^files/[0-9a-f]{16}\.png$""", file.Path);

        var folder = Path.Combine(server.Library, "packages", "Rust.ptpkg");
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(Path.Combine(folder, file.Path), Token));
        Assert.True(File.Exists(Path.Combine(folder, "Compounds", "Packages", "Rust", "Wear.vxtexgraph")));
        var inside = YamlSerializer.Parse<NodeGraphAsset>(
            await File.ReadAllTextAsync(Path.Combine(folder, "Rust.vxtexgraph"), Token)
        );
        Assert.Equal("Packages/Rust/Wear", inside.Nodes.Single(static n => n.Id == 2).Type);
        Assert.Equal(file.Path, inside.Nodes.Single(static n => n.Id == 1).Texts["Source"]);
        Assert.Equal(["metal", "weathered"], inside.Tags);
        Assert.Equal("Rust over a picture.", inside.Description);
        Assert.Equal(PackageManifests.Text(package.Manifest), PackageManifests.Text(PackageManifests.Read(folder)));
        Assert.True((await client.OpenAsync($"{package.Path}/Rust.vxtexgraph", Token)).ReadOnly);

        // The same graph publishes to the same bytes, so republishing an unchanged material is no diff.
        var manifest = await File.ReadAllTextAsync(Path.Combine(folder, "package.yaml"), Token);
        Assert.True((await PlanAsync(graph)).Replaces);
        (await client.PostAsJsonAsync(
            "/api/v1/library/packages",
            new PackageRequest(graph),
            Token
        )).EnsureSuccessStatusCode();
        Assert.Equal(manifest, await File.ReadAllTextAsync(Path.Combine(folder, "package.yaml"), Token));

        await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Alpha"), Token);

        async Task<MaterialInfo[]> Shelf(string search) => [
            ..(await client.GetFromJsonAsync<MaterialList>(
                $"/api/v1/library/materials?project=alpha&search={Uri.EscapeDataString(search)}",
                Token
            ))!.Items.Where(static m => m.Origin == "package")
        ];

        var offered = Assert.Single(await Shelf(string.Empty));
        Assert.Equal(
            ("Rust", "graph", "material", "packages/Rust.ptpkg/Rust.vxtexgraph"),
            (offered.Name, offered.Kind, offered.Role, offered.Source)
        );
        Assert.Equal(["baseColor"], offered.Channels);
        Assert.Equal("tint", Assert.Single(offered.Parameters!).Name);
        Assert.Equal("Rust over a picture.", offered.Description);
        foreach (var search in new[] { "weathered", "metal", "packages", "rust material" }) {
            Assert.Single(await Shelf(search));
        }

        Assert.Empty(await Shelf("rust brush"));
        var terms = await client.GetFromJsonAsync<MaterialList>("/api/v1/library/materials?project=alpha", Token);
        Assert.Contains(new("package", "Packages"), terms!.Origins);

        var used = await client.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new MaterialUseRequest("alpha", offered.Source),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.Equal("Materials/Rust", (await used.Content.ReadFromJsonAsync<MaterialUse>(Token))!.Graph);
        var alpha = Path.Combine(server.Library, "projects", "alpha", "Compounds");
        var material = YamlSerializer.Parse<NodeGraphAsset>(
            await File.ReadAllTextAsync(Path.Combine(alpha, "Materials", "Rust.vxtexgraph"), Token)
        );
        Assert.Equal($"packages/Rust.ptpkg/{file.Path}", material.Nodes.Single(static n => n.Id == 1).Texts["Source"]);
        Assert.Equal("Packages/Rust/Wear", material.Nodes.Single(static n => n.Id == 2).Type);
        Assert.True(File.Exists(Path.Combine(alpha, "Packages", "Rust", "Wear.vxtexgraph")));
    }

    [Fact]
    public async Task A_graph_that_reads_a_picture_nobody_has_is_refused_with_a_code_and_writes_nothing() {
        var graph = await RustProjectAsync();
        File.Delete(Path.Combine(server.Library, "projects", "rusty-kit", "mask.png"));

        var plan = await PlanAsync(graph);
        var missing = Assert.Single(plan.Dependencies, static d => d.Problem is not null);
        Assert.Equal(("picture", "mask.png", null), (missing.Kind, missing.Name, missing.File));

        var refused = await client.PostAsJsonAsync("/api/v1/library/packages", new PackageRequest(graph), Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
        Assert.Equal("package-refused", problem.RootElement.GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(server.Library, "packages", "Rust.ptpkg")));

        var role = await client.PostAsJsonAsync("/api/v1/library/packages", new PackageRequest(graph, "decal"), Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, role.StatusCode);
    }

    [Fact]
    public async Task What_a_graph_says_about_itself_is_one_undo_step_that_reaches_its_file() {
        var graph = await RustProjectAsync();
        var state = await client.OpenAsync(graph, Token);
        Assert.Equal(
            ("Rust over a picture.", "Ines", null),
            (state.Info.Description, state.Info.Author, state.Info.PhysicalWidth)
        );
        Assert.Equal(["metal"], state.Info.Tags);

        await client.EditAsync(
            state.Id,
            Token,
            new GraphSetInfo(
                "Iron left out in the rain.",
                Tags: ["metal", " Metal ", string.Empty, "iron"],
                PhysicalWidth: 200,
                PhysicalHeight: 150
            )
        );
        var described = await client.StateAsync(state.Id, Token);
        Assert.Equal("Iron left out in the rain.", described.Info.Description);
        Assert.Equal("Ines", described.Info.Author);
        Assert.Equal(["metal", "iron"], described.Info.Tags);
        Assert.Equal((200f, 150f), (described.Info.PhysicalWidth, described.Info.PhysicalHeight));
        Assert.Equal("Describe Graph", described.History.UndoLabel);

        var bad = await client.SendAsync(
            state.Id,
            new([new GraphSetInfo(PhysicalWidth: -1, PhysicalHeight: 5)]),
            Token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);

        await client.ActAsync(state.Id, "undo", Token);
        Assert.Equal("Rust over a picture.", (await client.StateAsync(state.Id, Token)).Info.Description);
        await client.ActAsync(state.Id, "redo", Token);
        await client.ActAsync(state.Id, "save", Token);
        var saved = YamlSerializer.Parse<NodeGraphAsset>(
            await File.ReadAllTextAsync(Path.Combine(server.Library, graph), Token)
        );
        Assert.Equal("Iron left out in the rain.", saved.Description);
        Assert.Equal([200f, 150f], saved.PhysicalSize);
    }
}
