using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

public sealed class LibraryApiTests : IDisposable {
    readonly ServerFixture server = new();

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => server.Dispose();

    /// <summary>A library with one material used by a floating and a pinned project.</summary>
    AssetId CreateLibrary() {
        LibraryFiles.CreateLibrary(
            server.Library,
            new(LibraryId.New(), "Ember", 1024, ["baseColor"], "pbr", ["default"])
        );
        var asset = new AssetDocument(AssetId.New(), "smart-material", "Rust", ["metal"], []);
        var content = "rust"u8.ToArray();
        LibraryFiles.WriteAsset(server.Library, "smart-materials/rust.vxsmart", content, asset);
        var revision = Revision.Of(content);
        LibraryFiles.WriteProject(
            server.Library,
            "props/crate",
            new(ProjectId.New(), "Crate", ["hero"], [AssetReference.Floating(asset.Id)], [])
        );
        LibraryFiles.WriteProject(
            server.Library,
            "props/door",
            new(ProjectId.New(), "Door", [], [AssetReference.Pinned(asset.Id, revision)], [])
        );
        return asset.Id;
    }

    /// <summary>The Server scans behind the API, so a test waits for the scan it depends on.</summary>
    async Task<LibraryInfo> WaitForIndexAsync(HttpClient client) {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (true) {
            var info = await client.GetFromJsonAsync(
                "/api/v1/library",
                ContractsJsonContext.Default.LibraryInfo,
                deadline.Token
            );
            if (info is { Indexing: false, ProjectCount: > 0 }) {
                return info;
            }

            await Task.Delay(20, deadline.Token);
        }
    }

    [Fact]
    public async Task The_library_says_what_it_is() {
        CreateLibrary();
        using var client = server.AuthorisedClient();

        var info = await WaitForIndexAsync(client);

        Assert.Equal("Ember", info.Name);
        Assert.Equal(1, info.SchemaVersion);
        Assert.Equal((2, 1), (info.ProjectCount, info.AssetCount));
        Assert.Equal(["baseColor"], info.Channels);
    }

    [Fact]
    public async Task A_folder_with_no_library_is_a_404_and_the_rest_still_works() {
        using var client = server.AuthorisedClient();

        var response = await client.GetAsync("/api/v1/library", Token);
        var projects = await client.GetAsync("/api/v1/library/projects", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, projects.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/system", Token)).StatusCode);
    }

    [Fact]
    public async Task Projects_come_in_pages() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        var page = await client.GetFromJsonAsync(
            "/api/v1/library/projects?limit=1&offset=1",
            ContractsJsonContext.Default.ProjectPage,
            Token
        );

        Assert.Equal(2, page?.Total);
        Assert.Equal("Door", page?.Items.Single().Name);
        Assert.Equal(1, page?.Items.Single().ReferenceCount);
    }

    [Fact]
    public async Task Projects_can_be_searched_and_filtered() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        var found = await client.GetFromJsonAsync(
            "/api/v1/library/projects?search=cra",
            ContractsJsonContext.Default.ProjectPage,
            Token
        );
        var tagged = await client.GetFromJsonAsync(
            "/api/v1/library/projects?tag=hero",
            ContractsJsonContext.Default.ProjectPage,
            Token
        );

        Assert.Equal("Crate", found?.Items.Single().Name);
        Assert.Equal("Crate", tagged?.Items.Single().Name);
    }

    [Fact]
    public async Task Assets_carry_their_usage() {
        var id = CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        var page = await client.GetFromJsonAsync(
            "/api/v1/library/assets",
            ContractsJsonContext.Default.AssetPage,
            Token
        );

        var asset = page?.Items.Single();
        Assert.Equal(id.Value, asset?.Id);
        Assert.Equal(2, asset?.UsageCount);
        Assert.Equal("smart-material", asset?.Kind);
    }

    [Fact]
    public async Task Impact_counts_the_projects_and_the_pins() {
        var id = CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        var impact = await client.GetFromJsonAsync(
            $"/api/v1/library/assets/{id}/impact",
            ContractsJsonContext.Default.AssetImpact,
            Token
        );

        Assert.Equal((2, 1), (impact?.Projects, impact?.Pinned));
        Assert.True(impact?.Items.Single(static p => p.Name == "Door").Pinned);
        Assert.False(impact?.Items.Single(static p => p.Name == "Crate").Pinned);
    }

    [Fact]
    public async Task Impact_of_an_unknown_asset_is_a_404() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        var response = await client.GetAsync($"/api/v1/library/assets/{AssetId.New()}/impact", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=501")]
    [InlineData("offset=-1")]
    public async Task A_page_out_of_range_is_refused(string query) {
        CreateLibrary();
        using var client = server.AuthorisedClient();

        var response = await client.GetAsync($"/api/v1/library/projects?{query}", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_library_needs_the_token() {
        CreateLibrary();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/v1/library", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
