using Patina.Contracts;
using Patina.Library;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>
///     <c>GET /library/project/bake/plan</c> (#186): the bakes a project's settings ask for, which a script sends to
///     the bake queue as they are, and which are the ones <c>patina bake-maps --project</c> runs.
/// </summary>
public sealed class ProjectBakePlanApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;
    ProjectDocument crate = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
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
        var project = Directory.CreateDirectory(Path.Combine(server.Library, "projects", "crate")).FullName;
        var mesh = AssetId.New();
        ProjectDocument document = new(ProjectId.New(), "Crate", [], [], []) {
            Meshes = [new(mesh, "crate_low.fbx", "crate_high.fbx")],
            Documents = [
                new(AssetId.New(), DocumentKinds.Texturing, "lid.vxlayers", "Lid", mesh, Material: "Lid"),
                new(AssetId.New(), DocumentKinds.Texturing, "box.vxlayers", "Box", mesh, Material: "Box")
            ],
            Bake = new() { Options = new() { Resolution = 256 }, Sets = ["Box"] }
        };
        crate = document;
        File.WriteAllText(Path.Combine(project, LibraryLayout.ProjectFile), DocumentCodec.Write(document));
        client = server.AuthorisedClient();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task A_project_says_what_its_bake_sends_and_a_document_it_does_not_list_is_refused() {
        var plan = await client.GetFromJsonAsync<ProjectBakePlan>(
            "/api/v1/library/project/bake/plan?project=crate&document=projects/crate/box.vxlayers",
            Token
        );

        var folder = Path.Combine(server.Library, "projects", "crate");
        var request = Assert.Single(plan!.Requests);
        Assert.Equal(
            (Path.Combine(folder, "crate_low.fbx"), Path.Combine(folder, "crate_high.fbx"), "Box", 256),
            (request.Low, request.High[0], request.Material, request.Options.Resolution)
        );
        Assert.Equal(Path.Combine(folder, "bakes", "maps", "Box"), request.Output);

        var refused = await client.GetAsync(
            "/api/v1/library/project/bake/plan?project=crate&document=projects/crate/none.vxlayers",
            Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var missing = await client.GetAsync("/api/v1/library/project/bake/plan?project=nothing", Token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task A_chosen_output_folder_and_named_sets_outrank_the_projects_own_and_an_unknown_set_is_refused() {
        var elsewhere = Path.Combine(server.Library, "elsewhere");
        var plan = await client.GetFromJsonAsync<ProjectBakePlan>(
            $"/api/v1/library/project/bake/plan?project=crate&output={Uri.EscapeDataString(elsewhere)}&set=Lid&set=Box",
            Token
        );

        Assert.Equal(
            [("Lid", Path.Combine(elsewhere, "Lid")), ("Box", Path.Combine(elsewhere, "Box"))],
            plan!.Requests.Select(static request => (request.Material, request.Output))
        );

        var refused = await client.GetAsync("/api/v1/library/project/bake/plan?project=crate&set=Strap", Token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    Task<HttpResponseMessage> PreviewAsync(ViewportBakePreview preview) =>
        client.PutAsJsonAsync(
            "/api/v1/viewport/bake-preview",
            preview,
            ContractsJsonContext.Default.ViewportBakePreview,
            Token
        );

    /// <summary>
    ///     A preview that names a project shows what that project's bake plan would run (#296): its meshes and options
    ///     come from the project's file, not from the client, so a client that sends none still shows the right bake.
    /// </summary>
    [Fact]
    public async Task A_preview_of_a_project_takes_its_meshes_and_options_from_the_projects_file() {
        var folder = Path.Combine(server.Library, "projects", "crate");
        using var noLow = await PreviewAsync(new() { Project = "crate", Document = "projects/crate/box.vxlayers" });
        Assert.Equal(HttpStatusCode.NotFound, noLow.StatusCode);
        Assert.Contains("crate_low.fbx", await noLow.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        // The meshes are there, and the project's own extra high mesh is not: it was read from the file.
        File.WriteAllBytes(Path.Combine(folder, "crate_low.fbx"), []);
        File.WriteAllBytes(Path.Combine(folder, "crate_high.fbx"), []);
        var extra = crate with { Bake = crate.Bake! with { High = ["extra_high.fbx"] } };
        File.WriteAllText(Path.Combine(folder, LibraryLayout.ProjectFile), DocumentCodec.Write(extra));
        using var noExtra = await PreviewAsync(new() { Project = "crate", High = ["ignored.fbx"] });
        Assert.Equal(HttpStatusCode.NotFound, noExtra.StatusCode);
        Assert.Contains("extra_high.fbx", await noExtra.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(folder, LibraryLayout.ProjectFile), "schema: 1\nbake: [");
        using var unread = await PreviewAsync(new() { Project = "crate" });
        Assert.Equal(HttpStatusCode.BadRequest, unread.StatusCode);
        File.WriteAllText(Path.Combine(folder, LibraryLayout.ProjectFile), DocumentCodec.Write(crate));

        using var noDocument = await PreviewAsync(new() { Project = "crate", Document = "projects/crate/no.vxlayers" });
        Assert.Equal(HttpStatusCode.BadRequest, noDocument.StatusCode);
        using var noProject = await PreviewAsync(new() { Project = "nothing" });
        Assert.Equal(HttpStatusCode.NotFound, noProject.StatusCode);
    }
}
