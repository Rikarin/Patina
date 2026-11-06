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
        Assert.Equal("projects/crate/Low Poly.glb", contents!.Mesh);
        Assert.Equal("projects/crate/crate.vxlayers", contents.Stack);
        var opened = await client.PostAsJsonAsync("/api/v1/stacks", new { path = contents.Stack }, Token);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Contains("Low Poly.glb", await opened.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
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
    public async Task A_project_with_nothing_in_it_is_furnished_when_asked() {
        await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Crate"), Token);
        File.Delete(Path.Combine(server.Library, "projects", "crate", "crate.vxlayers"));

        var put = await client.PutAsJsonAsync("/api/v1/library/project", new ProjectContentsRequest("crate"), Token);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.True(File.Exists(Path.Combine(server.Library, "projects", "crate", "crate.vxlayers")));
    }

    [Fact]
    public async Task What_is_open_cannot_move() {
        await client.PostAsJsonAsync("/api/v1/library/folders", new FolderCreateRequest("Props"), Token);
        await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Crate"), Token);
        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        await client.PostAsJsonAsync("/api/v1/stacks", new { path = contents!.Stack }, Token);

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
