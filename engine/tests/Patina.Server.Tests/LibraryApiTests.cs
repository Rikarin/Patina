using Patina.Contracts;
using Patina.Library;
using System.Net;

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
        var asset = new AssetDocument(AssetId.New(), "graph", "Rust", ["metal"], []);
        var content = "rust"u8.ToArray();
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
        LibraryFiles.WriteAsset(server.Library, "props/crate/rust.vxtexgraph", content, asset);
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
        Assert.Equal("graph", asset?.Kind);
    }

    [Fact]
    public async Task A_new_graph_opens_empty_and_is_indexed_by_the_scan() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/library/graphs",
            new("Heavy Rust", "props/crate"),
            ContractsJsonContext.Default.GraphCreateRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphCreated, Token);
        Assert.Equal("projects/props/crate/heavy-rust.vxtexgraph", created!.Path);

        var state = await client.OpenAsync(created.Path, Token);
        Assert.Equal("Heavy Rust", state.Name);
        Assert.Empty(state.Nodes);
        Assert.Equal("1024", state.Settings["baseWidth"]);
        Assert.Equal($"{TextureSizes.Default}", state.Settings[TextureSizes.GraphSetting]);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (true) {
            var assets = await client.GetFromJsonAsync(
                "/api/v1/library/assets?search=heavy",
                ContractsJsonContext.Default.AssetPage,
                deadline.Token
            );
            if (assets!.Items.Count > 0) {
                Assert.Equal("Heavy Rust", assets.Items[0].Name);
                return;
            }

            await Task.Delay(50, deadline.Token);
        }
    }

    [Fact]
    public async Task A_new_graph_with_a_taken_name_or_no_project_is_refused() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);
        GraphCreateRequest request = new("Heavy", "props/crate");
        using var first = await client.PostAsJsonAsync(
            "/api/v1/library/graphs",
            request,
            ContractsJsonContext.Default.GraphCreateRequest,
            Token
        );
        first.EnsureSuccessStatusCode();
        using var second = await client.PostAsJsonAsync(
            "/api/v1/library/graphs",
            request,
            ContractsJsonContext.Default.GraphCreateRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using var lost = await client.PostAsJsonAsync(
            "/api/v1/library/graphs",
            new("Rust", "props/nothing"),
            ContractsJsonContext.Default.GraphCreateRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, lost.StatusCode);
    }

    /// <summary>A new graph starts from nothing, an Output node per channel, or a starter preset (#183).</summary>
    [Fact]
    public async Task A_new_graph_starts_from_the_template_asked_for() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);
        var templates = await client.GetFromJsonAsync(
            "/api/v1/library/graph-templates",
            ContractsJsonContext.Default.IReadOnlyListStackTerm,
            Token
        );
        Assert.Equal(
            ["empty", "outputs", "Brushed Steel", "Moss", "Rough Stone", "Terracotta"],
            templates!.Select(static term => term.Value)
        );
        Assert.Equal("Material Outputs", templates![1].Label);

        async Task<GraphState> CreateAsync(string name, string? template, int? side = null) {
            using var response = await client.PostAsJsonAsync(
                "/api/v1/library/graphs",
                new(name, "props/crate", side, template),
                ContractsJsonContext.Default.GraphCreateRequest,
                Token
            );
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphCreated, Token);
            return await client.OpenAsync(created!.Path, Token);
        }

        var outputs = await CreateAsync("Channels", "outputs");
        Assert.Equal(
            ["baseColor", "normal", "roughness", "metalness", "height", "occlusion"],
            outputs.Nodes.Select(static node => node.Texts["Usage"])
        );
        Assert.All(outputs.Nodes, static node => Assert.Equal("Output/Output", node.Type));

        var moss = await CreateAsync("My Moss", "Moss", 512);
        Assert.Equal("My Moss", moss.Name);
        Assert.Contains(moss.Nodes, static node => node.Type == "Surface/Height to Normal");
        Assert.Equal("512", moss.Settings[TextureSizes.GraphSetting]);
        Assert.True(moss.Settings.ContainsKey("seed"));

        Assert.Empty((await CreateAsync("Nothing", "empty")).Nodes);

        using var unknown = await client.PostAsJsonAsync(
            "/api/v1/library/graphs",
            new("Odd", "props/crate", null, "Marble"),
            ContractsJsonContext.Default.GraphCreateRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
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
    public async Task Tags_are_listed_with_their_counts() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        var projects = await client.GetFromJsonAsync(
            "/api/v1/library/projects/tags",
            ContractsJsonContext.Default.TagPage,
            Token
        );
        var assets = await client.GetFromJsonAsync(
            "/api/v1/library/assets/tags",
            ContractsJsonContext.Default.TagPage,
            Token
        );

        Assert.Equal(new("hero", 1), projects?.Items.Single());
        Assert.Equal(new("metal", 1), assets?.Items.Single());
    }

    Task<HttpResponseMessage> CreateProjectAsync(HttpClient client, ProjectCreateRequest request) =>
        client.PostAsJsonAsync(
            "/api/v1/library/projects",
            request,
            ContractsJsonContext.Default.ProjectCreateRequest,
            Token
        );

    [Fact]
    public async Task A_new_project_is_a_file_first_and_reaches_the_index_behind_it() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        var response = await CreateProjectAsync(client, new("Oak Barrel", "props", ["wood", "hero"]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ProjectSummary, Token);
        Assert.Equal("props/oak-barrel", created?.Path);
        Assert.Equal(["hero", "wood"], created?.Tags);
        Assert.True(File.Exists(LibraryFiles.ProjectFile(server.Library, "props/oak-barrel")));

        var indexed = await WaitForIndexAsync(client);
        var found = await client.GetFromJsonAsync(
            "/api/v1/library/projects?search=oak",
            ContractsJsonContext.Default.ProjectPage,
            Token
        );
        var listed = found?.Items.Single();
        Assert.Equal(3, indexed.ProjectCount);
        Assert.Equal(created?.Id, listed?.Id);
        Assert.Equal(created?.Path, listed?.Path);
    }

    [Fact]
    public async Task No_reply_says_the_index_has_caught_up_before_it_has() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);

        await CreateProjectAsync(client, new("Oak Barrel"));

        var info = await client.GetFromJsonAsync("/api/v1/library", ContractsJsonContext.Default.LibraryInfo, Token);
        Assert.True(info is { Indexing: true } or { ProjectCount: 3 });
    }

    [Fact]
    public async Task A_taken_folder_is_a_409_and_writes_nothing() {
        CreateLibrary();
        using var client = server.AuthorisedClient();
        await WaitForIndexAsync(client);
        var file = LibraryFiles.ProjectFile(server.Library, "props/crate");
        var before = await File.ReadAllTextAsync(file, Token);

        var response = await CreateProjectAsync(client, new("Crate", "props"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await File.ReadAllTextAsync(file, Token));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("???", null)]
    [InlineData("Crate", "../outside")]
    [InlineData("Crate", "/../outside")]
    [InlineData("Crate", ".hidden")]
    public async Task A_bad_new_project_is_a_400(string name, string? folder) {
        CreateLibrary();
        using var client = server.AuthorisedClient();

        var response = await CreateProjectAsync(client, new(name, folder));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_new_project_needs_a_library() {
        using var client = server.AuthorisedClient();

        var response = await CreateProjectAsync(client, new("Crate"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_library_needs_the_token() {
        CreateLibrary();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/v1/library", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
