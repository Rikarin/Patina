using Patina.Contracts;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>
///     The shipped compounds are on the shelf by what each can be (#122): grunges, noises, patterns and generators as
///     masks, the weathering and the adjustments as filters. Using one names its node type and writes nothing into the
///     project, and a compound the shelf does not ship is not used.
/// </summary>
public sealed class ShelfCompoundApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
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

    public async ValueTask InitializeAsync() {
        File.WriteAllText(Path.Combine(server.Library, "library.yaml"), Library);
        foreach (var folder in new[] { "shared", "projects" }) {
            Directory.CreateDirectory(Path.Combine(server.Library, folder));
        }

        client = server.AuthorisedClient();
        (await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest("Alpha"), Token))
            .EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task Shipped_compounds_are_on_the_shelf_by_role_and_a_search_finds_them() {
        var shelf = await ShelfAsync(string.Empty);

        Assert.Equal("mask", Compound(shelf, "Grunges/Grunge Rust").Role);
        Assert.Equal("mask", Compound(shelf, "Generators/Dirt").Role);
        Assert.Equal("mask", Compound(shelf, "Generators/Thin Parts").Role);
        Assert.Equal("filter", Compound(shelf, "Weathering/Rust").Role);
        Assert.Equal("filter", Compound(shelf, "Adjust/Histogram Shift").Role);
        Assert.All(
            shelf.Where(static material => material.Source.StartsWith("compound:", StringComparison.Ordinal)),
            static material => {
                Assert.Equal(("graph", "shipped"), (material.Kind, material.Origin));
                Assert.Contains(material.Role, (string[])["mask", "filter"]);
            }
        );

        var found = await ShelfAsync("grunge rust");
        Assert.Contains(found, static material => material.Source == "compound:Grunges/Grunge Rust");
        Assert.DoesNotContain(found, static material => material.Source == "compound:Patterns/Brick");
    }

    [Fact]
    public async Task Using_a_shipped_compound_names_its_node_and_writes_nothing() {
        var used = await client.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new MaterialUseRequest("alpha", "compound:Generators/Dirt"),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.Equal("Generators/Dirt", (await used.Content.ReadFromJsonAsync<MaterialUse>(Token))!.Graph);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "projects", "alpha", "Compounds")));

        var unknown = await client.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new MaterialUseRequest("alpha", "compound:Nowhere/Nothing"),
            Token
        );
        Assert.False(unknown.IsSuccessStatusCode);
    }

    static MaterialInfo Compound(IEnumerable<MaterialInfo> shelf, string node) =>
        Assert.Single(shelf, material => material.Source == "compound:" + node);

    async Task<MaterialInfo[]> ShelfAsync(string search) => [
        ..(await client.GetFromJsonAsync<MaterialList>(
            $"/api/v1/library/materials?project=alpha&search={Uri.EscapeDataString(search)}",
            Token
        ))!.Items
    ];
}
