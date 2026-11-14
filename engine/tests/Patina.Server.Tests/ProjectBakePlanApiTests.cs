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
}
