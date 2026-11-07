using Patina.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>The folders and contents of a library, over the API a client uses.</summary>
public sealed class LibraryTreeApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    readonly DirectoryInfo files = Directory.CreateTempSubdirectory("patina-tree-");
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
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
        files.Delete(true);
    }

    const string Library = """
                           schema: 1
                           id: 0190a000-0000-7000-8000-00000000abcd
                           name: Game
                           texelDensity: 1024
                           channels: []
                           defaultShader: pbr
                           exportTargets: []
                           """;

    [Fact]
    public async Task A_project_made_from_a_mesh_owns_it_and_a_stack() {
        var mesh = Path.Combine(files.FullName, "Low Poly.glb");
        File.WriteAllText(mesh, "mesh");

        var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Mesh: mesh, Resolution: 1024),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        Assert.Equal("projects/crate/Low Poly.glb", Assert.Single(contents!.Meshes).Path);
        var document = Assert.Single(contents.Documents);
        Assert.Equal(("texturing", "projects/crate/crate.vxlayers"), (document.Kind, document.Path));
        var opened = await client.PostAsJsonAsync("/api/v1/stacks", new { path = document.Path }, Token);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Contains("Low Poly.glb", await opened.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("layered-material", ".vxlayers")]
    [InlineData("graph", ".vxtexgraph")]
    public async Task A_project_starts_as_the_kind_asked_for(string kind, string extension) {
        var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Rust", Kind: kind),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=rust", Token);

        var document = Assert.Single(contents!.Documents);
        Assert.Equal((kind, $"projects/rust/rust{extension}"), (document.Kind, document.Path));
    }

    [Fact]
    public async Task A_duplicated_project_has_its_own_identity_and_a_deleted_one_can_be_got_back() {
        var made = await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Crate"), Token);
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var added = await client.PostAsJsonAsync(
            "/api/v1/library/project/documents",
            new ProjectDocumentRequest("crate", "graph", "Wood"),
            Token
        );
        added.EnsureSuccessStatusCode();
        var original = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);

        var duplicated = await client.PostAsJsonAsync(
            "/api/v1/library/project/duplicate",
            new ProjectDuplicateRequest("crate", "Crate Two"),
            Token
        );
        duplicated.EnsureSuccessStatusCode();
        var copy = await duplicated.Content.ReadFromJsonAsync<ProjectContents>(Token);
        Assert.Equal("Crate Two", copy!.Name);
        Assert.Equal(original!.Documents.Count, copy.Documents.Count);
        Assert.Empty(copy.Documents.Select(static d => d.Id).Intersect(original.Documents.Select(static d => d.Id)));

        var taken = await client.PostAsJsonAsync(
            "/api/v1/library/project/duplicate",
            new ProjectDuplicateRequest("crate", "Crate"),
            Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);

        var deleted = await client.DeleteAsync("/api/v1/library/project?path=crate", Token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "projects", "crate")));
        Assert.Single(Directory.GetDirectories(Path.Combine(server.Library, ".trash")));
    }

    [Fact]
    public async Task A_texturing_project_needs_a_mesh() {
        var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Kind: "texturing"),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "projects", "crate")));
    }

    /// <summary>The index catches up after a project is made, and a material elsewhere appears when it has.</summary>
    async Task<IReadOnlyList<MaterialInfo>> ElsewhereAsync(string project) {
        for (var attempt = 0;; attempt++) {
            var list = await client.GetFromJsonAsync<MaterialList>(
                $"/api/v1/library/materials?project={project}",
                Token
            );
            var found = list!.Items.Where(static material => material.Origin == "library").ToList();
            if (found.Count > 0 || attempt > 100) {
                return found;
            }

            await Task.Delay(50, Token);
        }
    }

    [Fact]
    public async Task A_document_is_added_to_a_project_and_moves_to_another_mesh() {
        var (first, second) = (Path.Combine(files.FullName, "a.glb"), Path.Combine(files.FullName, "b.glb"));
        File.WriteAllText(first, "mesh");
        File.WriteAllText(second, "mesh");
        await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Crate", Mesh: first), Token);

        var added = await client.PostAsJsonAsync(
            "/api/v1/library/project/meshes",
            new ProjectMeshRequest("crate", second),
            Token
        );
        var contents = await added.Content.ReadFromJsonAsync<ProjectContents>(Token);
        var other = contents!.Meshes.First(static m => m.Path.EndsWith("b.glb", StringComparison.Ordinal));
        var graph = await client.PostAsJsonAsync(
            "/api/v1/library/project/documents",
            new ProjectDocumentRequest("crate", "graph", "Rust"),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, graph.StatusCode);
        var made = (await graph.Content.ReadFromJsonAsync<ProjectContents>(Token))!.Documents.First(static d =>
            d.Kind == "graph"
        );
        var filter = await client.PatchAsJsonAsync(
            "/api/v1/library/project/document/role",
            new ProjectDocumentRoleRequest("crate", made.Id!.Value, "filter"),
            Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, filter.StatusCode);
        var elsewhere = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Barrel", Kind: "layered-material"),
            Token
        );
        Assert.True(elsewhere.IsSuccessStatusCode, await elsewhere.Content.ReadAsStringAsync(Token));
        var shelf = await client.GetFromJsonAsync<MaterialList>("/api/v1/library/materials?project=barrel", Token);
        var starter = Assert.Single(shelf!.Items, static material => material.Name == "Rough Stone");
        Assert.Contains("normal", starter.Channels);
        var brought = await client.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new MaterialUseRequest("barrel", starter.Source),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, brought.StatusCode);
        Assert.True(File.Exists(Path.Combine(server.Library, "projects", "barrel", "rough-stone.vxtexgraph")));

        var rust = Assert.Single(
            await ElsewhereAsync("barrel"),
            static material => material is { Name: "Rust", Project: "Crate" }
        );
        await File.WriteAllTextAsync(
            Path.Combine(server.Library, rust.Source),
            """
            name: Rust
            nodes:
              - { id: 1, type: Source/Uniform }
              - { id: 2, type: Output/Output, texts: { Usage: baseColor } }
            edges:
              - { fromNode: 1, fromPort: Out, toNode: 2, toPort: Input }
            groups: []
            comments: []
            interface: []
            settings: { baseWidth: '64', baseHeight: '64', seed: '1' }
            parameters: []
            """,
            Token
        );
        var used = await client.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new MaterialUseRequest("barrel", rust.Source),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, used.StatusCode);

        // Using it is what lets an edit to it say this project is affected.
        Assert.Contains(
            made.Id!.Value.ToString(),
            await File.ReadAllTextAsync(Path.Combine(server.Library, "projects", "barrel", "project.yaml"), Token),
            StringComparison.Ordinal
        );
        var moved = await client.PatchAsJsonAsync(
            "/api/v1/library/project/document",
            new ProjectDocumentMeshRequest("crate", contents.Documents[0].Id!.Value, other.Id!.Value),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var stack = await File.ReadAllTextAsync(
            Path.Combine(server.Library, "projects", "crate", "crate.vxlayers"),
            Token
        );
        Assert.Contains("b.glb", stack, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_mesh_that_is_not_there_makes_no_project() {
        var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Mesh: "/nowhere/low.glb"),
            Token
        );

        Assert.Equal(HttpStatusCode.NotFound, created.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "projects", "crate")));
    }

    [Fact]
    public async Task What_is_open_cannot_move() {
        await client.PostAsJsonAsync("/api/v1/library/folders", new FolderCreateRequest("Props"), Token);
        await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Kind: "layered-material"),
            Token
        );
        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        await client.PostAsJsonAsync("/api/v1/stacks", new { path = contents!.Documents[0].Path }, Token);

        var moved = await client.PostAsJsonAsync(
            "/api/v1/library/move",
            new LibraryMoveRequest(["crate"], "Props"),
            Token
        );

        Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        Assert.True(Directory.Exists(Path.Combine(server.Library, "projects", "crate")));
    }

    [Fact]
    public async Task A_folder_is_named_coloured_and_deleted() {
        await client.PostAsJsonAsync("/api/v1/library/folders", new FolderCreateRequest("Props"), Token);

        var renamed = await client.PatchAsJsonAsync(
            "/api/v1/library/folders",
            new FolderUpdateRequest("Props", "Scenery", "#2f80ed"),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var listed = await client.GetFromJsonAsync<FolderList>("/api/v1/library/folders", Token);
        var folder = Assert.Single(listed!.Items);
        Assert.Equal(("Scenery", "#2f80ed"), (folder.Name, folder.Color));

        var deleted = await client.DeleteAsync(
            new Uri("/api/v1/library/folders?path=Scenery", UriKind.Relative),
            Token
        );
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task A_path_outside_the_library_is_refused() {
        var response = await client.GetAsync(new Uri("/api/v1/library/folders?parent=../..", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
