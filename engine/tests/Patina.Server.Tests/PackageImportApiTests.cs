using Patina.Contracts;
using Patina.Library;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>
///     Bringing a package someone sent into a library, zipped or as its folder, and relinking a picture from the
///     dependency view of Publish as Package (#185), over the API any client uses and with no GPU.
/// </summary>
public sealed class PackageImportApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    readonly DirectoryInfo outside = Directory.CreateTempSubdirectory("patina-sent-");
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    const string Library = """
                           schema: 1
                           id: 0190a000-0000-7000-8000-00000000abce
                           name: Game
                           texelDensity: 1024
                           channels: []
                           defaultShader: pbr
                           exportTargets: []
                           """;

    /// <summary>A graph that reads a picture beside it, which the relink tests move.</summary>
    const string Moss = """
                        name: Moss
                        nodes:
                          - { id: 1, type: Source/Bitmap, texts: { Source: moss.png } }
                          - { id: 2, type: Source/Bitmap, texts: { Source: moss.png } }
                          - { id: 3, type: Source/Bitmap, texts: { Source: other.png } }
                          - { id: 4, type: Output/Output, texts: { Usage: baseColor } }
                        edges:
                          - { fromNode: 1, fromPort: Out, toNode: 4, toPort: Input }
                        settings: { baseWidth: '64', baseHeight: '64', seed: '1' }
                        description: Moss on stone.
                        """;

    public async ValueTask InitializeAsync() {
        File.WriteAllText(Path.Combine(server.Library, "library.yaml"), Library);
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        Directory.CreateDirectory(Path.Combine(server.Library, "loose", "pictures"));
        client = server.AuthorisedClient();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
        outside.Delete(true);
    }

    /// <summary>A package folder as someone else's Patina wrote it, outside the library.</summary>
    string SentPackage(int format = PackageManifests.Format) {
        var package = Path.Combine(outside.FullName, "Moss.ptpkg");
        Directory.CreateDirectory(Path.Combine(package, "files"));
        File.WriteAllText(Path.Combine(package, "Moss.vxtexgraph"), Moss.Replace("moss.png", "files/a.png"));
        File.WriteAllBytes(Path.Combine(package, "files", "a.png"), [7, 7, 7]);
        File.WriteAllText(
            Path.Combine(package, "package.yaml"),
            $"""
             format: {format}
             name: Moss
             graph: Moss.vxtexgraph
             role: material
             writer: Patina 9.9.9
             compounds: []
             shipped: []
             files:
               - path: files/a.png
                 from: moss.png
             """
        );
        return package;
    }

    Task<HttpResponseMessage> ImportAsync(string source) =>
        client.PostAsJsonAsync("/api/v1/library/packages/import", new PackageImportRequest(source), Token);

    /// <summary>A zip of these entries, written as they are named, however they are named.</summary>
    static void Zip(string path, params (string Name, string Text)[] entries) {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in entries) {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(text);
        }
    }

    static async Task<string> CodeOf(HttpResponseMessage response) {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task A_zipped_package_comes_in_whole_and_the_shelf_offers_it_and_a_second_replaces_it() {
        var package = SentPackage();
        var zip = Path.Combine(outside.FullName, "Moss.zip");
        PackageArchives.Zip(package, zip);
        var again = Path.Combine(outside.FullName, "again.zip");
        PackageArchives.Zip(package, again);
        Assert.Equal(await File.ReadAllBytesAsync(zip, Token), await File.ReadAllBytesAsync(again, Token));

        var imported = await ImportAsync(zip);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var first = (await imported.Content.ReadFromJsonAsync<PackageImported>(Token))!;
        Assert.Equal(
            ("packages/Moss.ptpkg", "Moss", "Patina 9.9.9", false),
            (first.Path, first.Manifest.Name, first.Manifest.Writer, first.Replaced)
        );
        var folder = Path.Combine(server.Library, "packages", "Moss.ptpkg");
        Assert.Equal([7, 7, 7], await File.ReadAllBytesAsync(Path.Combine(folder, "files", "a.png"), Token));
        var packages = Directory.GetDirectories(Path.GetDirectoryName(folder)!);
        Assert.Equal(["Moss.ptpkg"], packages.Select(Path.GetFileName));

        await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Alpha"), Token);
        var shelf = await client.GetFromJsonAsync<MaterialList>("/api/v1/library/materials?project=alpha", Token);
        var offered = Assert.Single(shelf!.Items, static m => m.Origin == "package");
        Assert.Equal(("Moss", "Moss on stone."), (offered.Name, offered.Description));

        // The folder itself comes in the same way, and replaces the package of its name.
        var second = (await (await ImportAsync(package)).Content.ReadFromJsonAsync<PackageImported>(Token))!;
        Assert.True(second.Replaced);
        Assert.True(File.Exists(Path.Combine(folder, "package.yaml")));
    }

    [Fact]
    public async Task A_zip_that_climbs_out_of_its_package_or_is_newer_or_holds_no_package_writes_nothing() {
        var climbing = Path.Combine(outside.FullName, "climbing.zip");
        Zip(
            climbing,
            ("Moss.ptpkg/package.yaml", "format: 1\nname: Moss\ngraph: Moss.vxtexgraph\n"),
            ("Moss.ptpkg/Moss.vxtexgraph", Moss),
            ("Moss.ptpkg/../../escaped.txt", "out")
        );

        var newer = Path.Combine(outside.FullName, "newer.zip");
        ZipFile.CreateFromDirectory(SentPackage(PackageManifests.Format + 1), newer, CompressionLevel.Fastest, true);
        var empty = Path.Combine(outside.FullName, "empty.zip");
        Zip(empty, ("readme.txt", string.Empty));

        foreach (var source in new[] { climbing, newer, empty, Path.Combine(outside.FullName, "Moss.vxtexgraph") }) {
            await File.WriteAllTextAsync(Path.Combine(outside.FullName, "Moss.vxtexgraph"), Moss, Token);
            var refused = await ImportAsync(source);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Equal("package-refused", await CodeOf(refused));
        }

        Assert.False(File.Exists(Path.Combine(server.Library, "escaped.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(server.Library, "packages")));
        var nothing = await ImportAsync(Path.Combine(outside.FullName, "no.zip"));
        Assert.Equal(HttpStatusCode.NotFound, nothing.StatusCode);
    }

    [Fact]
    public async Task A_missing_picture_is_relinked_on_every_bitmap_that_reads_it_as_one_undo_step() {
        await File.WriteAllTextAsync(Path.Combine(server.Library, "loose", "Moss.vxtexgraph"), Moss, Token);
        await File.WriteAllBytesAsync(Path.Combine(server.Library, "loose", "other.png"), [1], Token);
        var found = Path.Combine(server.Library, "loose", "pictures", "moss-new.png");
        await File.WriteAllBytesAsync(found, [2], Token);
        var planned = await client.GetAsync("/api/v1/library/packages/plan?graph=loose/Moss.vxtexgraph", Token);
        Assert.True(planned.IsSuccessStatusCode, await planned.Content.ReadAsStringAsync(Token));
        var plan = await planned.Content.ReadFromJsonAsync<PackagePlan>(Token);
        var missing = Assert.Single(plan!.Dependencies, static d => d.Problem is not null);
        Assert.Equal(("moss.png", null), (missing.Name, missing.In));

        var state = await client.OpenAsync("loose/Moss.vxtexgraph", Token);
        var relinked = await client.PostAsJsonAsync(
            $"/api/v1/graphs/{state.Id}/relink",
            new GraphRelinkRequest("moss.png", found),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, relinked.StatusCode);
        var after = await client.StateAsync(state.Id, Token);
        Assert.Equal(
            ["pictures/moss-new.png", "pictures/moss-new.png", "other.png"],
            after.Nodes.Where(static n => n.Type == "Source/Bitmap")
                .OrderBy(static n => n.Id)
                .Select(static n => n.Texts["Source"])
        );
        Assert.Equal("Relink Picture", after.History.UndoLabel);

        await client.ActAsync(state.Id, "save", Token);
        var replanned = await client.GetFromJsonAsync<PackagePlan>(
            "/api/v1/library/packages/plan?graph=loose/Moss.vxtexgraph",
            Token
        );
        Assert.All(replanned!.Dependencies, static d => Assert.Null(d.Problem));

        foreach (var (from, to) in new[] { ("moss.png", found), ("pictures/moss-new.png", "loose/nothing.png") }) {
            var refused = await client.PostAsJsonAsync(
                $"/api/v1/graphs/{state.Id}/relink",
                new GraphRelinkRequest(from, to),
                Token
            );
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        }

        await client.ActAsync(state.Id, "undo", Token);
        var saved = YamlSerializer.Parse<NodeGraphAsset>(
            await File.ReadAllTextAsync(Path.Combine(server.Library, "loose", "Moss.vxtexgraph"), Token)
        );
        Assert.Equal("pictures/moss-new.png", saved.Nodes[0].Texts["Source"]);
    }
}
