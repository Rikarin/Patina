using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

    /// <summary>
    ///     Export settings are the project's, kept in its project.yaml by the Server for any client: what one client
    ///     sets, another reads back from the project's contents. That they outlive the cache is the library's test
    ///     (<c>A_projects_export_bake_viewport_and_paint_settings_outlive_the_cache_together</c>), which closes the
    ///     library before it deletes <c>.ptcache</c>: a running Server holds <c>index.sqlite</c> open, and Windows will
    ///     not delete an open file.
    /// </summary>
    [Fact]
    public async Task A_projects_export_settings_are_kept_in_its_file_and_read_back() {
        await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Crate"), Token);
        var settings = new ProjectExportSettings {
            Preset = "godot",
            Size = 1024,
            Padding = new("transparent", 8, []),
            Files = [new("godot", "{project}_albedo", true, "tga")]
        };

        using var kept = await client.PutAsJsonAsync(
            "/api/v1/library/project/export",
            new ProjectExportRequest("crate", settings),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        var yaml = await File.ReadAllTextAsync(
            Path.Combine(server.Library, "projects", "crate", "project.yaml"),
            Token
        );
        Assert.Contains("export:", yaml, StringComparison.Ordinal);

        var read = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        Assert.Equal(
            ("godot", 1024, "transparent"),
            (read!.Export!.Preset, read.Export.Size, read.Export.Padding!.Mode)
        );
        Assert.Equal("tga", Assert.Single(read.Export.Files).Format);
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
    public async Task Autosaved_work_is_offered_only_while_nothing_newer_is_open() {
        var (document, file, stack) = await OpenStackAsync();
        LeaveAutosave(file);
        Assert.NotNull(await RecoveryOfAsync(document));

        await EditAsync(stack);

        Assert.Null(await RecoveryOfAsync(document));
    }

    [Fact]
    public async Task Autosaved_work_can_only_be_answered_with_the_document_closed() {
        var (document, file, stack) = await OpenStackAsync();
        LeaveAutosave(file);

        using var refused = await client.PostAsJsonAsync(
            "/api/v1/library/recovery",
            new DocumentRecoveryRequest(document, false),
            Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.NotNull(await RecoveryOfAsync(document));

        using var closed = await client.DeleteAsync($"/api/v1/stacks/{stack}", Token);
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
        using var answered = await client.PostAsJsonAsync(
            "/api/v1/library/recovery",
            new DocumentRecoveryRequest(document, false),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Null(await RecoveryOfAsync(document));
    }

    [Fact]
    public async Task Discarding_a_stack_forgets_its_autosave_and_reopens_on_the_saved_file() {
        var (document, file, stack) = await OpenStackAsync();
        var before = await File.ReadAllTextAsync(file, Token);
        await EditAsync(stack);
        LeaveAutosave(file);

        using var refused = await client.DeleteAsync($"/api/v1/stacks/{stack}", Token);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        using var discarded = await client.DeleteAsync($"/api/v1/stacks/{stack}?discard=true", Token);
        Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);

        Assert.Null(await RecoveryOfAsync(document));
        using var reopened = await client.PostAsJsonAsync("/api/v1/stacks", new { path = document }, Token);
        var state = await reopened.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackState, Token);
        Assert.False(state!.History.Dirty);
        Assert.NotEqual(stack, state.Id);
        Assert.Equal(before, await File.ReadAllTextAsync(file, Token));
    }

    [Fact]
    public async Task Discarding_a_graph_forgets_its_autosave() {
        var state = await client.OpenAsync(GraphTesting.Write(server.Library, GraphTesting.Sample()), Token);
        await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.5f]));
        LeaveAutosave(state.Path);

        using var discarded = await client.DeleteAsync($"/api/v1/graphs/{state.Id}?discard=true", Token);

        Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);
        Assert.Null(await RecoveryOfAsync(state.Path));
    }

    [Fact]
    public async Task Discard_on_a_clean_session_has_nothing_to_throw_away() {
        var (document, file, stack) = await OpenStackAsync();
        LeaveAutosave(file);

        using var closed = await client.DeleteAsync($"/api/v1/stacks/{stack}?discard=true", Token);

        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
        Assert.NotNull(await RecoveryOfAsync(document));
    }

    [Fact]
    public async Task An_import_goes_where_its_kind_goes_and_the_project_lists_it() {
        await MakeCrateAsync();

        var withMesh = await ImportAsync("Spare.glb");
        var withPicture = await ImportAsync("Rust.png");

        Assert.Contains(withMesh!.Meshes, static mesh => mesh.Path == "projects/crate/Spare.glb");
        Assert.Equal(
            new("picture", "Rust.png", "projects/crate/pictures/Rust.png"),
            Assert.Single(withPicture!.Pictures)
        );
        Assert.True(File.Exists(Path.Combine(server.Library, "projects/crate/pictures/Rust.png")));
    }

    [Theory]
    [InlineData("model.dae")]
    [InlineData("scene.usd")]
    [InlineData("notes.txt")]
    [InlineData("height.tif")]
    public async Task An_import_nothing_can_read_is_refused_and_copies_nothing(string name) {
        await MakeCrateAsync();
        var file = Path.Combine(files.FullName, name);
        File.WriteAllText(file, "not much");

        using var refused = await client.PostAsJsonAsync(
            "/api/v1/library/project/files",
            new ProjectFileRequest("crate", file),
            Token
        );
        using var picture = await client.PostAsJsonAsync(
            "/api/v1/library/project/pictures",
            new ProjectPictureRequest("crate", file),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(name, await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Contains(".glb", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, picture.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "projects/crate/pictures")));
    }

    [Theory]
    [InlineData("height.exr")]
    [InlineData("sky.hdr")]
    [InlineData("paint.psd")]
    public async Task A_deep_picture_is_a_picture_on_every_route(string name) {
        await MakeCrateAsync();
        var file = Path.Combine(files.FullName, name);
        File.WriteAllText(file, "not much");

        using var imported = await client.PostAsJsonAsync(
            "/api/v1/library/project/files",
            new ProjectFileRequest("crate", file),
            Token
        );
        using var added = await client.PostAsJsonAsync(
            "/api/v1/library/project/pictures",
            new ProjectPictureRequest("crate", file),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var contents = await imported.Content.ReadFromJsonAsync<ProjectContents>(Token);
        Assert.Equal(new("picture", name, $"projects/crate/pictures/{name}"), Assert.Single(contents!.Pictures));
    }

    [Fact]
    public async Task A_picture_the_Server_drew_for_a_mask_is_not_listed_as_the_users() {
        await MakeCrateAsync();
        var pictures = Directory.CreateDirectory(Path.Combine(server.Library, "projects/crate/pictures"));
        File.WriteAllText(Path.Combine(pictures.FullName, "mesh-mask-1234.png"), "drawn");

        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);

        Assert.Empty(contents!.Pictures);
    }

    [Fact]
    public async Task Reloading_a_file_that_did_not_change_has_nothing_to_do() {
        var (source, project) = await MakeProjectOnMeshAsync();
        var yaml = await File.ReadAllTextAsync(Path.Combine(project, "project.yaml"), Token);

        Assert.False(await MeshChangedAsync(source));
        // There is no Worker here, so anything past the check would wait on one and fail.
        using var reloaded = await client.PatchAsJsonAsync(
            "/api/v1/library/project/settings",
            new ProjectSettingsRequest("crate", source, null, null, null),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, reloaded.StatusCode);
        Assert.Equal(yaml, await File.ReadAllTextAsync(Path.Combine(project, "project.yaml"), Token));
        Assert.False(Directory.Exists(Path.Combine(project, LibraryLayout.PaintBackupFolder)));

        await File.AppendAllTextAsync(source, "more", Token);
        Assert.True(await MeshChangedAsync(source));
    }

    /// <summary>
    ///     A project keeps how its viewport was left and gives it back when it opens, without touching the mesh;
    ///     settings that could not be shown are refused before anything is kept.
    /// </summary>
    [Fact]
    public async Task A_project_keeps_its_viewport_and_refuses_one_that_cannot_be_shown() {
        await MakeCrateAsync();
        var left = new ViewportDisplay(Exposure: 1.5, View: "clay", Wireframe: true, ToneMapping: "agx");

        using var kept = await client.PutAsJsonAsync(
            "/api/v1/library/project/viewport",
            new ProjectViewportRequest("crate", left),
            Token
        );
        using var refused = await client.PutAsJsonAsync(
            "/api/v1/library/project/viewport",
            new ProjectViewportRequest("crate", left with { View = "x-ray" }),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        Assert.Equal(left, contents!.Viewport);
    }

    [Fact]
    public async Task Another_file_of_a_different_name_is_a_change_even_with_the_same_bytes() {
        var (source, _) = await MakeProjectOnMeshAsync();
        var other = Path.Combine(files.FullName, "Other.glb");
        File.Copy(source, other);

        Assert.True(await MeshChangedAsync(other));
    }

    [Fact]
    public async Task A_bake_goes_out_of_date_when_the_mesh_it_was_made_on_does() {
        var (_, project) = await MakeProjectOnMeshAsync();
        var bake = Directory.CreateDirectory(Path.Combine(project, "bakes", "first"));
        var manifest = Path.Combine(bake.FullName, "crate.bake.json");

        void Baked(string? mesh) =>
            File.WriteAllBytes(
                manifest,
                JsonSerializer.SerializeToUtf8Bytes(
                    new BakeManifest("crate", "png8", new([], 0, 0, [], false), mesh),
                    ContractsJsonContext.Default.BakeManifest
                )
            );

        async Task<bool> StaleAsync() {
            var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
            return Assert.Single(contents!.Bakes).Stale;
        }

        Baked(LibraryFiles.Digest(Path.Combine(project, "Low Poly.glb")));
        Assert.False(await StaleAsync());

        Baked("some other mesh");
        Assert.True(await StaleAsync());

        Baked(null);
        Assert.False(await StaleAsync());
    }

    async Task<(string Source, string Project)> MakeProjectOnMeshAsync() {
        var source = Path.Combine(files.FullName, "Low Poly.glb");
        File.WriteAllText(source, "mesh");
        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Mesh: source, Resolution: 1024),
            Token
        );
        return (source, Path.Combine(server.Library, "projects", "crate"));
    }

    async Task<bool> MeshChangedAsync(string mesh) {
        var change = await client.GetFromJsonAsync<ProjectMeshChange>(
            $"/api/v1/library/project/mesh-change?project=crate&mesh={Uri.EscapeDataString(mesh)}",
            Token
        );
        return change!.Changed;
    }

    Task<HttpResponseMessage> MakeCrateAsync() =>
        client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Kind: "layered-material"),
            Token
        );

    async Task<ProjectContents?> ImportAsync(string name) {
        var file = Path.Combine(files.FullName, name);
        File.WriteAllText(file, "bytes");
        using var imported = await client.PostAsJsonAsync(
            "/api/v1/library/project/files",
            new ProjectFileRequest("crate", file),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        return await imported.Content.ReadFromJsonAsync<ProjectContents>(Token);
    }

    async Task<(string Document, string File, string Stack)> OpenStackAsync() {
        await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Kind: "layered-material"),
            Token
        );
        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        var document = contents!.Documents[0].Path;
        using var opened = await client.PostAsJsonAsync("/api/v1/stacks", new { path = document }, Token);
        var stack = await opened.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackState, Token);
        return (document, Path.GetFullPath(document, server.Library), stack!.Id);
    }

    /// <summary>What autosave leaves, older than nothing: the file was last saved a while ago.</summary>
    void LeaveAutosave(string file) {
        Recovery.Keep(server.Library, file, "autosaved");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-1));
    }

    async Task EditAsync(string stack) {
        using var edited = await client.PostAsJsonAsync(
            $"/api/v1/stacks/{stack}/commands",
            new([new StackAddChannel("height")]),
            ContractsJsonContext.Default.StackCommandBatch,
            Token
        );
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync(Token));
    }

    async Task<DateTimeOffset?> RecoveryOfAsync(string document) {
        var found = await client.GetFromJsonAsync<DocumentRecovery>(
            $"/api/v1/library/recovery?path={Uri.EscapeDataString(document)}",
            Token
        );
        return found!.Saved;
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
