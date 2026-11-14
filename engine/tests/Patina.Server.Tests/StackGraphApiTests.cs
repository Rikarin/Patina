using Patina.Contracts;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     A layer stack written into its project as the graph it stands for (#205), over the API a client uses. Nothing
///     here needs the Worker: the graph is built from the document.
/// </summary>
public sealed class StackGraphApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;
    string stack = string.Empty;
    string stackFile = string.Empty;

    CancellationToken Token => TestContext.Current.CancellationToken;

    static readonly Dictionary<string, IReadOnlyList<float>> Rust = new() { ["baseColor"] = [0.6f, 0.2f, 0.1f, 1f] };

    public async ValueTask InitializeAsync() {
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
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
        client = server.AuthorisedClient();
        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Kind: "layered-material"),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        stackFile = Directory.GetFiles(Path.Combine(server.Library, "projects", "crate"), "*.vxlayers").Single();
        using var opened = await client.PostAsJsonAsync("/api/v1/stacks", new StackOpenRequest(stackFile), Token);
        Assert.True(opened.IsSuccessStatusCode, await opened.Content.ReadAsStringAsync(Token));
        stack = (await opened.Content.ReadFromJsonAsync<StackState>(Token))!.Id;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    Task<HttpResponseMessage> WriteGraphAsync(string name) =>
        client.PostAsJsonAsync($"/api/v1/stacks/{stack}/graph", new StackGraphRequest(name), Token);

    async Task<StackGraphMade> MadeAsync(string name) {
        using var made = await WriteGraphAsync(name);
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(Token));
        return (await made.Content.ReadFromJsonAsync<StackGraphMade>(Token))!;
    }

    /// <summary>
    ///     The graph is a new document of the stack's project, beside it, that opens as a graph: one Output per channel,
    ///     the stack's size and seed, held at its edges as a stack is, and a comment saying it goes one way. The stack
    ///     is not touched, and a second graph of the same name gets a number.
    /// </summary>
    [Fact]
    public async Task A_stack_becomes_a_graph_document_beside_it() {
        var stackBefore = await File.ReadAllTextAsync(stackFile, Token);

        var made = await MadeAsync("Crate Graph");

        Assert.Equal("Crate Graph", made.Name);
        Assert.StartsWith("projects/crate/", made.Path, StringComparison.Ordinal);
        Assert.EndsWith(".vxtexgraph", made.Path, StringComparison.Ordinal);
        var file = Path.Combine(server.Library, made.Path);
        Assert.Equal(Path.GetDirectoryName(stackFile), Path.GetDirectoryName(Path.GetFullPath(file)));
        Assert.Contains("Exploded from a .vxlayers layer stack", await File.ReadAllTextAsync(file, Token));
        Assert.Equal(stackBefore, await File.ReadAllTextAsync(stackFile, Token));

        var contents = (await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token))!;
        var listed = Assert.Single(contents.Documents, document => document.Path == made.Path);
        Assert.Equal("graph", listed.Kind);
        Assert.Equal("Crate Graph", listed.Name);

        var graph = await client.OpenAsync(file, Token);
        Assert.Contains(graph.Nodes, static node => node.Type == "Output/Output");
        Assert.Equal("false", graph.Settings["tilingX"]);
        Assert.Equal("false", graph.Settings["tilingY"]);
        Assert.True(graph.Settings.ContainsKey("baseWidth"));
        Assert.True(graph.Settings.ContainsKey("seed"));

        var again = await MadeAsync("Crate Graph");
        Assert.Equal("Crate Graph 2", again.Name);
        Assert.NotEqual(made.Path, again.Path);
    }

    /// <summary>
    ///     A stack with unsaved edits is refused with a code a client tells apart, since the graph reads painted layers
    ///     from their files and those do not hold the strokes yet; undone back to what is saved, it is not.
    /// </summary>
    [Fact]
    public async Task A_stack_with_unsaved_edits_is_refused_until_it_is_as_saved() {
        using (var added = await client.PostAsJsonAsync(
                   $"/api/v1/stacks/{stack}/commands",
                   new StackCommandBatch([new StackAddLayer("fill", "Rust", Rust)]),
                   Token
               )) {
            Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync(Token));
        }

        using (var refused = await WriteGraphAsync("Crate Graph")) {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
            Assert.Equal("stack-graph-unsaved", problem.RootElement.GetProperty("code").GetString());
        }

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(stackFile)!, "*.vxtexgraph"));

        using (var undone = await client.PostAsync($"/api/v1/stacks/{stack}/undo", null, Token)) {
            Assert.True(undone.IsSuccessStatusCode, await undone.Content.ReadAsStringAsync(Token));
        }

        await MadeAsync("Crate Graph");
    }

    [Fact]
    public async Task A_graph_needs_a_name_and_an_open_stack() {
        using (var nameless = await WriteGraphAsync("  ")) {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, nameless.StatusCode);
        }

        using var missing = await client.PostAsJsonAsync(
            "/api/v1/stacks/nothing/graph",
            new StackGraphRequest("Crate Graph"),
            Token
        );
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
