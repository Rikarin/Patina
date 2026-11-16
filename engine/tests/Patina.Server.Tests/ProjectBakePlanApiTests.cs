using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Text.Json;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     <c>GET /library/project/bake/plan</c> (#186): the bakes a project's settings ask for, which a script sends to
///     the bake queue as they are, and which are the ones <c>patina bake-maps --project</c> runs.
/// </summary>
public sealed class ProjectBakePlanApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;
    PictureWorker pictures = null!;
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
        pictures = await PictureWorker.ConnectAsync(server.SocketPath, Token);
    }

    public async ValueTask DisposeAsync() {
        await pictures.DisposeAsync();
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
        await File.WriteAllTextAsync(Path.Combine(server.Library, "notes.txt"), "not a picture", Token);
        var box = ProjectBakes.Read(folder).Documents.Single(static d => d.Material == "Box").Id.Value;

        var imported = await client.PostAsJsonAsync(
            "/api/v1/library/project/bake/import",
            new BakeImportRequest("crate", box, [new("normal", "elsewhere_normal.png")]),
            Token
        );
        var notPng = await client.PostAsJsonAsync(
            "/api/v1/library/project/bake/import",
            new BakeImportRequest("crate", box, [new("ao", "notes.txt")]),
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

    /// <summary>
    ///     A map in any picture Patina reads comes in as a 16-bit PNG, and a normal whose green runs down comes in with
    ///     it turned up (#300). The GPU engine writes them, since the Server decodes no picture; green turned on a map
    ///     with none is refused, as is a picture that does not read, and nothing is left in the cache.
    /// </summary>
    [Fact]
    public async Task Any_picture_comes_in_as_a_png_and_a_green_that_ran_down_runs_up() {
        var folder = Path.Combine(server.Library, "projects", "crate");
        await File.WriteAllTextAsync(Path.Combine(folder, "box.vxlayers"), "name: Box\n", Token);
        var box = ProjectBakes.Read(folder).Documents.Single(static d => d.Material == "Box").Id.Value;
        var exr = Path.Combine(server.Library, "ao.exr");
        await File.WriteAllBytesAsync(exr, ExrCodec.Encode(new(2, 1, 1, [0.25f, 2f])), Token);
        var directX = Path.Combine(server.Library, "normal_dx.png");
        await File.WriteAllBytesAsync(directX, PngCodec.Encode8(1, 1, 3, [128, 64, 255]), Token);
        await File.WriteAllTextAsync(Path.Combine(server.Library, "broken.tga"), "not a picture", Token);

        Task<HttpResponseMessage> ImportAsync(params BakeImportMap[] maps) =>
            client.PostAsJsonAsync(
                "/api/v1/library/project/bake/import",
                new BakeImportRequest("crate", box, maps),
                Token
            );

        using var imported = await ImportAsync(new("ao", "ao.exr"), new("normal", "normal_dx.png", true));
        using var aoTurned = await ImportAsync(new BakeImportMap("ao", "ao.exr", true));
        using var broken = await ImportAsync(new BakeImportMap("ao", "broken.tga"));

        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var maps = Path.Combine(folder, "bakes", "maps", "Box");
        var ao = PngCodec.Decode16(await File.ReadAllBytesAsync(Path.Combine(maps, "imported_ao.png"), Token));
        Assert.Equal((2, 1, 1), (ao.Width, ao.Height, ao.Channels));
        Assert.Equal([(ushort)16384, ushort.MaxValue], ao.Samples);
        var normal = PngCodec.Decode16(await File.ReadAllBytesAsync(Path.Combine(maps, "imported_normal.png"), Token));
        Assert.Equal([(ushort)(128 * 257), (ushort)(191 * 257), ushort.MaxValue], normal.Samples);
        Assert.Equal(HttpStatusCode.BadRequest, aoTurned.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, broken.StatusCode);
        Assert.Contains("picture-unreadable", await broken.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        var cache = Path.Combine(server.Library, ".ptcache", "pictures", "maps");
        Assert.Empty(Directory.Exists(cache) ? Directory.GetFiles(cache) : []);
    }

    /// <summary>
    ///     A project says which way its normal maps' green runs (#144), and a normal brought in that does not say is
    ///     read that way: in a DirectX project it comes in turned up, unless the map says its green already runs up.
    ///     A map with no green is never turned, and a format Patina does not know is refused.
    /// </summary>
    [Fact]
    public async Task A_normal_brought_in_runs_its_green_as_the_project_says_unless_it_says_otherwise() {
        var folder = Path.Combine(server.Library, "projects", "crate");
        await File.WriteAllTextAsync(Path.Combine(folder, "box.vxlayers"), "name: Box\n", Token);
        var box = ProjectBakes.Read(folder).Documents.Single(static d => d.Material == "Box").Id.Value;
        var normal = PngCodec.Encode8(1, 1, 3, [128, 64, 255]);
        var ao = PngCodec.Encode8(1, 1, 3, [10, 64, 30]);
        await File.WriteAllBytesAsync(Path.Combine(server.Library, "normal.png"), normal, Token);
        await File.WriteAllBytesAsync(Path.Combine(server.Library, "ao.png"), ao, Token);

        Task<HttpResponseMessage> SetAsync(string format) =>
            client.PatchAsJsonAsync(
                "/api/v1/library/project/settings",
                new ProjectSettingsRequest("crate", null, null, null) { NormalFormat = format },
                Token
            );

        // Whether the normal came in turned over: copied as it is, or written by the GPU engine with green up.
        async Task<bool> TurnedAsync(bool? greenDown) {
            using var imported = await client.PostAsJsonAsync(
                "/api/v1/library/project/bake/import",
                new BakeImportRequest("crate", box, [new("normal", "normal.png", greenDown), new("ao", "ao.png")]),
                Token
            );
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
            var maps = Path.Combine(folder, "bakes", "maps", "Box");
            Assert.Equal(ao, await File.ReadAllBytesAsync(Path.Combine(maps, "imported_ao.png"), Token));
            var brought = await File.ReadAllBytesAsync(Path.Combine(maps, "imported_normal.png"), Token);
            if (brought.SequenceEqual(normal)) {
                return false;
            }

            Assert.Equal((ushort)(191 * 257), PngCodec.Decode16(brought).Samples[1]);
            return true;
        }

        Assert.False(await TurnedAsync(null));
        using var directX = await SetAsync("y-down");
        Assert.Equal(HttpStatusCode.OK, directX.StatusCode);
        Assert.Equal("y-down", (await directX.Content.ReadFromJsonAsync<ProjectContents>(Token))!.NormalFormat);
        Assert.Contains(
            "normalFormat: y-down",
            await File.ReadAllTextAsync(Path.Combine(folder, LibraryLayout.ProjectFile), Token),
            StringComparison.Ordinal
        );
        Assert.True(await TurnedAsync(null));
        Assert.False(await TurnedAsync(false));

        using var unknown = await SetAsync("y-sideways");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        using var openGl = await SetAsync("y-up");
        Assert.DoesNotContain(
            "normalFormat",
            await File.ReadAllTextAsync(Path.Combine(folder, LibraryLayout.ProjectFile), Token),
            StringComparison.Ordinal
        );
    }

    static BakeManifest BakeManifestOf(string file) =>
        JsonSerializer.Deserialize(File.ReadAllBytes(file), ContractsJsonContext.Default.BakeManifest)!;
}
