using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Text.Json;

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

    /// <summary>
    ///     Maps baked in another program become the set's bake (#144): copied where its own bake goes, replacing the
    ///     map of their name in the bake there, and described as of the set's UV set, so the set reads them as fitting.
    /// </summary>
    [Fact]
    public async Task Maps_baked_elsewhere_become_the_sets_bake_and_replace_the_maps_of_their_name() {
        var folder = Path.Combine(server.Library, "projects", "crate");
        await File.WriteAllTextAsync(
            Path.Combine(folder, "box.vxlayers"),
            "name: Box\nbaseWidth: 512\nbaseHeight: 256\nsets:\n  - name: Box\n    uvSet: 1\n",
            Token
        );
        var maps = Directory.CreateDirectory(Path.Combine(folder, "bakes", "maps", "Box")).FullName;
        BakeManifest ours = new(
            "crate_low",
            "png16",
            new(
                [new("normal", "crate_low_normal.png", 256, 1, 128), new("ao", "crate_low_ao.png", 256, 1, 128)],
                0,
                0,
                [],
                false
            ),
            UvSet: 1
        );
        await File.WriteAllTextAsync(Path.Combine(maps, "crate_low_normal.png"), "ours", Token);
        await File.WriteAllTextAsync(Path.Combine(maps, "crate_low_ao.png"), "ours", Token);
        await File.WriteAllBytesAsync(
            Path.Combine(maps, "crate_low.bake.json"),
            JsonSerializer.SerializeToUtf8Bytes(ours, ContractsJsonContext.Default.BakeManifest),
            Token
        );
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
        var theirs = Path.Combine(server.Library, "elsewhere_normal.png");
        await File.WriteAllBytesAsync(theirs, png, Token);
        await File.WriteAllTextAsync(Path.Combine(server.Library, "notes.png"), "not a picture", Token);
        var box = ProjectBakes.Read(folder).Documents.Single(static d => d.Material == "Box").Id.Value;

        var imported = await client.PostAsJsonAsync(
            "/api/v1/library/project/bake/import",
            new BakeImportRequest("crate", box, [new("normal", "elsewhere_normal.png")]),
            Token
        );
        var notPng = await client.PostAsJsonAsync(
            "/api/v1/library/project/bake/import",
            new BakeImportRequest("crate", box, [new("ao", "notes.png")]),
            Token
        );
        var unknown = await client.PostAsJsonAsync(
            "/api/v1/library/project/bake/import",
            new BakeImportRequest("crate", box, [new("gloss", "elsewhere_normal.png")]),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(
            "projects/crate/bakes/maps/Box",
            (await imported.Content.ReadFromJsonAsync<BakeImported>(Token))!.Folder
        );
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (notPng.StatusCode, unknown.StatusCode));
        Assert.Equal(png, await File.ReadAllBytesAsync(Path.Combine(maps, "imported_normal.png"), Token));
        Assert.False(File.Exists(Path.Combine(maps, "crate_low_normal.png")));
        Assert.True(File.Exists(Path.Combine(maps, "crate_low_ao.png")));
        var kept = BakeManifestOf(Path.Combine(maps, "crate_low.bake.json"));
        Assert.Equal(["ao"], kept.Result.Maps.Select(static m => m.Map));
        var made = BakeManifestOf(Path.Combine(maps, "imported.bake.json"));
        Assert.Equal(
            ("Box", 1, "tangent", "normal"),
            (made.Material, made.UvSet, made.Space, Assert.Single(made.Result.Maps).Map)
        );
        Assert.Null(BakeFit.Stale(maps, null, new(1, 512, 256)));
    }

    static BakeManifest BakeManifestOf(string file) =>
        JsonSerializer.Deserialize(File.ReadAllBytes(file), ContractsJsonContext.Default.BakeManifest)!;
}
