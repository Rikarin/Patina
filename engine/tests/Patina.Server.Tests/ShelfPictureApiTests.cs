using Microsoft.Extensions.DependencyInjection;
using Patina.Contracts;
using Patina.Server.Graphs;
using System.Buffers.Binary;
using System.Net;
using Vixen.NodeGraph;

namespace Patina.Server.Tests;

/// <summary>
///     The pictures of the library's other projects are on the shelf beside the open project's (#203), from Other
///     projects, and one brought into its project after the shelf was first read is listed the next time.
/// </summary>
public sealed class ShelfPictureApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    const string Library = """
                           schema: 1
                           id: 0190a000-0000-7000-8000-00000000abcf
                           name: Game
                           texelDensity: 1024
                           channels: []
                           defaultShader: pbr
                           exportTargets: []
                           """;

    public async ValueTask InitializeAsync() {
        File.WriteAllText(Path.Combine(server.Library, "library.yaml"), Library);
        foreach (var folder in new[] { "shared", "projects" }) {
            Directory.CreateDirectory(Path.Combine(server.Library, folder));
        }

        client = server.AuthorisedClient();
        foreach (var name in new[] { "Alpha", "Wear Kit" }) {
            (await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest(name), Token))
                .EnsureSuccessStatusCode();
        }
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task Another_projects_pictures_are_on_the_shelf_and_a_search_finds_them() {
        var pictures = Path.Combine(server.Library, "projects", "wear-kit", "pictures");
        Directory.CreateDirectory(pictures);
        await File.WriteAllBytesAsync(Path.Combine(pictures, "Rust.png"), [1, 2, 3], Token);
        await File.WriteAllBytesAsync(Path.Combine(server.Library, "projects", "alpha", "Own.png"), [4], Token);
        server.Services.GetRequiredService<LibraryHost>().RequestRescan();

        var rust = Assert.Single(await PicturesAsync("alpha", "Rust", static found => found.Count > 0));
        Assert.Equal(
            ("Rust", "picture", "projects/wear-kit/pictures/Rust.png", "library", "Wear Kit"),
            (rust.Name, rust.Role, rust.Source, rust.Origin, rust.Project)
        );
        Assert.Equal(["PNG"], rust.Tags);
        var own = Assert.Single(await PicturesAsync("alpha", "Own", static found => found.Count > 0));
        Assert.Equal(("project", "Alpha"), (own.Origin, own.Project));
        Assert.Single(await PicturesAsync("alpha", "rust other png", static found => found.Count > 0));
        Assert.Empty(await PicturesAsync("alpha", "rust brush", static _ => true));

        // Copied in after the shelf read the project, two folders deep: the next scan lists it, though the project's
        // file did not change (#306).
        var deep = Directory.CreateDirectory(Path.Combine(pictures, "worn", "edges")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(deep, "Moss.png"), Png(2048, 512), Token);
        server.Services.GetRequiredService<LibraryHost>().RequestRescan();
        var moss = Assert.Single(await PicturesAsync("alpha", "Moss", static found => found.Count > 0));
        Assert.Equal(("library", "projects/wear-kit/pictures/worn/edges/Moss.png"), (moss.Origin, moss.Source));
        Assert.Equal(["PNG", "2048 × 512"], moss.Tags);
        Assert.Single(await PicturesAsync("alpha", "2048", static found => found.Count > 0));
    }

    /// <summary>
    ///     A panorama of any project is an environment on the shelf too (#203), told apart from the picture it is by
    ///     its source, and the brush alphas are there to choose from.
    /// </summary>
    [Fact]
    public async Task Panoramas_are_environments_and_the_brush_alphas_are_on_the_shelf() {
        var pictures = Directory.CreateDirectory(Path.Combine(server.Library, "projects", "wear-kit", "skies"))
            .FullName;
        await File.WriteAllBytesAsync(Path.Combine(pictures, "Dusk.hdr"), [1], Token);
        await File.WriteAllBytesAsync(Path.Combine(server.Library, "projects", "alpha", "Noon.exr"), [2], Token);
        server.Services.GetRequiredService<LibraryHost>().RequestRescan();

        var skies = await PicturesAsync("alpha", string.Empty, static found => found.Count == 2, "environment");
        Assert.Equal(
            [
                ("Noon", "environment:projects/alpha/Noon.exr", "project"),
                ("Dusk", "environment:projects/wear-kit/skies/Dusk.hdr", "library")
            ],
            skies.Select(static sky => (sky.Name, sky.Source, sky.Origin))
        );
        Assert.Equal(2, (await PicturesAsync("alpha", "dusk", static found => found.Count == 2, null)).Count);
        var alphas = await PicturesAsync("alpha", string.Empty, static _ => true, "alpha");
        Assert.Equal(["Round", "Square", "Chisel", "Cross"], alphas.Take(4).Select(static alpha => alpha.Name));
        Assert.Equal("alpha:Square", alphas[1].Source);
        Assert.Contains(alphas, static alpha => alpha is { Name: "Splatter", Source: "alpha-picture:Splatter" });
    }

    /// <summary>
    ///     A picture Patina ships as an alpha is on the shelf with the shapes (#34), and taking it up copies it into
    ///     the project's brushes, as a brush names its alpha from the project. Taken up again it is the same file; a
    ///     file of that name that is another picture is left, and the copy takes the next name.
    /// </summary>
    [Fact]
    public async Task A_shipped_picture_alpha_is_brought_into_the_projects_brushes() {
        var alphas = await PicturesAsync("alpha", string.Empty, static _ => true, "alpha");
        Assert.Equal(
            ["Bristles", "Cloud", "Crackle", "Leaf", "Ring", "Scratches", "Speckle", "Splatter", "Sponge", "Star"],
            alphas.Where(static alpha => alpha.Source.StartsWith("alpha-picture:", StringComparison.Ordinal))
                .Select(static alpha => alpha.Name)
        );
        Assert.All(alphas, static alpha => Assert.Equal("shipped", alpha.Origin));

        var brushes = Path.Combine(server.Library, "projects", "alpha", "brushes");
        Assert.Equal("brushes/Splatter.png", await BrushAsync("alpha-picture:Splatter"));
        var shipped = await File.ReadAllBytesAsync(Path.Combine(brushes, "Splatter.png"), Token);
        Assert.Equal(Png(256, 256)[..24], shipped[..24]);
        Assert.Equal("brushes/Splatter.png", await BrushAsync("alpha-picture:Splatter"));

        await File.WriteAllBytesAsync(Path.Combine(brushes, "Star.png"), [1, 2, 3], Token);
        Assert.Equal("brushes/Star 2.png", await BrushAsync("alpha-picture:Star"));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(brushes, "Star.png"), Token));

        var none = await client.PostAsJsonAsync(
            "/api/v1/library/materials/brush",
            new MaterialUseRequest("alpha", "alpha-picture:Nothing"),
            Token
        );
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }

    /// <summary>Takes up an alpha from the shelf for the project Alpha, and says the path its brush names.</summary>
    async Task<string> BrushAsync(string source) {
        var reply = await client.PostAsJsonAsync(
            "/api/v1/library/materials/brush",
            new MaterialUseRequest("alpha", source),
            Token
        );
        Assert.True(reply.IsSuccessStatusCode, await reply.Content.ReadAsStringAsync(Token));
        return (await reply.Content.ReadFromJsonAsync<MaterialBrush>(Token))!.Alpha;
    }

    /// <summary>
    ///     A graph that makes a sky is on the shelf as an environment too (#203), in the open project and from any
    ///     other, whose source is the graph's path behind the environment prefix; a graph that makes none is not, and
    ///     every other project's documents come from the index (#311).
    /// </summary>
    [Fact]
    public async Task A_graph_that_makes_a_sky_is_an_environment_from_any_project() {
        var library = server.Services.GetRequiredService<LibraryHost>().Library!;
        var dusk = library.CreateGraph(new("Dusk", "wear-kit"), GraphDocuments.Text(Sky("Dusk", "Environment/Sky")));
        var noon = library.CreateGraph(new("Noon", "alpha"), GraphDocuments.Text(Sky("Noon", "Environment/Sky")));
        library.CreateGraph(new("Rust", "wear-kit"), GraphDocuments.Text(Sky("Rust", "Colour/Dissolve")));
        server.Services.GetRequiredService<LibraryHost>().RequestRescan();

        var skies = await PicturesAsync("alpha", string.Empty, static found => found.Count == 2, "environment");
        Assert.Equal(
            [("Noon", $"environment:{noon}", "project"), ("Dusk", $"environment:{dusk}", "library")],
            skies.Select(static sky => (sky.Name, sky.Source, sky.Origin))
        );
        Assert.Equal("Wear Kit", skies[1].Project);
        var found = await PicturesAsync("alpha", "environment dusk", static found => found.Count > 0, null);
        Assert.Equal(["environment"], found.Select(static item => item.Role));
        var materials = await PicturesAsync("alpha", "wear", static found => found.Count == 2, "material");
        Assert.Equal(["Dusk", "Rust"], materials.Select(static item => item.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>A node (1) into an Output (2).</summary>
    static NodeGraphAsset Sky(string name, string node) =>
        new() {
            Name = name,
            Nodes = [
                new() { Id = 1, Type = node },
                new() { Id = 2, Type = "Output/Output", X = 300, Texts = new() { ["Usage"] = "baseColor" } }
            ],
            Edges = [new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" }]
        };

    /// <summary>The start of a PNG as wide and high as asked: its signature and its header chunk.</summary>
    static byte[] Png(int width, int height) {
        byte[] head = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, .."IHDR"u8, ..new byte[8]];
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(20), height);
        return head;
    }

    /// <summary>
    ///     The pictures a project's shelf finds for a search, once the index lists every project: it catches up after a
    ///     project is made. Of one type, or of any for none.
    /// </summary>
    async Task<IReadOnlyList<MaterialInfo>> PicturesAsync(
        string project,
        string search,
        Func<IReadOnlyList<MaterialInfo>, bool> done,
        string? role = "picture"
    ) {
        for (var attempt = 0;; attempt++) {
            var list = await client.GetFromJsonAsync<MaterialList>(
                $"/api/v1/library/materials?project={project}&search={Uri.EscapeDataString(search)}",
                Token
            );
            var found = list!.Items.Where(material => role is null || material.Role == role).ToList();
            if (done(found) || attempt > 100) {
                return found;
            }

            await Task.Delay(50, Token);
        }
    }
}
