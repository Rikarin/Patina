using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>Looking after a library over the API: the bin, names, tags, pins and the person's preferences.</summary>
public sealed class LibraryCareApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() {
        LibraryFiles.CreateDefaultLibrary(server.Library, "Game");
        client = server.AuthorisedClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    async Task<ProjectContents> MakeAsync(string name, string kind = "graph") {
        var made = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest(name, Kind: kind),
            Token
        );
        made.EnsureSuccessStatusCode();
        var path = (await made.Content.ReadFromJsonAsync<ProjectSummary>(Token))!.Path;
        return (await client.GetFromJsonAsync<ProjectContents>($"/api/v1/library/project?path={path}", Token))!;
    }

    [Fact]
    public async Task A_deleted_document_goes_to_the_bin_unless_it_is_open_and_comes_back() {
        var crate = await MakeAsync("Crate");
        var document = Assert.Single(crate.Documents);
        await client.PostAsJsonAsync("/api/v1/graphs", new { path = document.Path }, Token);

        var refused = await client.DeleteAsync(
            $"/api/v1/library/project/document?project=crate&document={document.Id}",
            Token
        );
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var graphs = await client.GetFromJsonAsync<JsonElement>("/api/v1/graphs", Token);
        foreach (var open in graphs.EnumerateArray()) {
            await client.DeleteAsync($"/api/v1/graphs/{open.GetProperty("id").GetString()}?discard=true", Token);
        }

        // An autosave left from before goes with it, through the autosave gate (#239).
        var file = Path.GetFullPath(document.Path, server.Library);
        Recovery.Keep(server.Library, file, "autosaved");
        Assert.Equal(1, Recovery.Count(server.Library, file));

        var deleted = await client.DeleteAsync(
            $"/api/v1/library/project/document?project=crate&document={document.Id}",
            Token
        );
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(0, Recovery.Count(server.Library, file));
        var binned = (await deleted.Content.ReadFromJsonAsync<BinItem>(Token))!;
        var bin = await client.GetFromJsonAsync<BinList>("/api/v1/library/bin", Token);
        Assert.Equal(binned.Id, Assert.Single(bin!.Items).Id);

        var restored = await client.PostAsJsonAsync(
            "/api/v1/library/bin/restore",
            new BinRestoreRequest(binned.Id),
            Token
        );
        Assert.Equal(document.Path, (await restored.Content.ReadFromJsonAsync<BinRestored>(Token))!.Path);
        var back = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        Assert.Equal(document.Id, Assert.Single(back!.Documents).Id);
    }

    [Fact]
    public async Task The_bin_empties_on_request_and_a_missing_entry_is_not_found() {
        await MakeAsync("Crate");
        (await client.DeleteAsync("/api/v1/library/project?path=crate", Token)).EnsureSuccessStatusCode();

        var gone = await client.DeleteAsync($"/api/v1/library/bin/{Guid.NewGuid():N}", Token);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        var emptied = await client.DeleteAsync("/api/v1/library/bin", Token);

        Assert.Equal(1, (await emptied.Content.ReadFromJsonAsync<BinEmptied>(Token))!.Count);
        Assert.Empty((await client.GetFromJsonAsync<BinList>("/api/v1/library/bin", Token))!.Items);
    }

    [Fact]
    public async Task Tags_and_names_are_written_to_the_project_file() {
        var crate = await MakeAsync("Crate");
        var document = Assert.Single(crate.Documents);

        var tagged = await client.PatchAsJsonAsync(
            "/api/v1/library/project/tags",
            new ProjectTagsRequest("crate", ["prop", "hero"]),
            Token
        );
        tagged.EnsureSuccessStatusCode();
        var renamed = await client.PatchAsJsonAsync(
            "/api/v1/library/project/document/name",
            new ProjectDocumentRenameRequest("crate", document.Id!.Value, "Oak"),
            Token
        );
        renamed.EnsureSuccessStatusCode();
        var documentTags = await client.PatchAsJsonAsync(
            "/api/v1/library/project/document/tags",
            new ProjectDocumentTagsRequest("crate", document.Id!.Value, ["wood"]),
            Token
        );
        var contents = (await documentTags.Content.ReadFromJsonAsync<ProjectContents>(Token))!;

        Assert.Equal(["hero", "prop"], contents.Tags);
        Assert.Equal("Oak", contents.Documents[0].Name);
        Assert.Equal(["wood"], contents.Documents[0].Tags);
        var yaml = await File.ReadAllTextAsync(
            Path.Combine(server.Library, "projects", "crate", "project.yaml"),
            Token
        );
        Assert.Contains("wood", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pin_needs_a_reference_of_the_project_itself() {
        var crate = await MakeAsync("Crate");
        var pinned = await client.PatchAsJsonAsync(
            "/api/v1/library/project/reference",
            new ProjectReferenceRequest("crate", crate.Documents[0].Id!.Value, false),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, pinned.StatusCode);
    }

    [Fact]
    public async Task Preferences_are_kept_in_a_plain_file_and_refused_out_of_range() {
        var held = await client.GetFromJsonAsync<ServerPreferences>("/api/v1/preferences", Token);
        Assert.Equal(new(), held!.Autosave);

        var set = await client.PutAsJsonAsync("/api/v1/preferences", new ServerPreferences(new(120, 10, 5)), Token);
        set.EnsureSuccessStatusCode();
        var bad = await client.PutAsJsonAsync("/api/v1/preferences", new ServerPreferences(new(60, 5, 0)), Token);

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var file = await File.ReadAllTextAsync(server.PreferencesFile, Token);
        Assert.Contains("\"intervalSeconds\": 120", file, StringComparison.Ordinal);
        Assert.Equal(
            5,
            (await client.GetFromJsonAsync<ServerPreferences>("/api/v1/preferences", Token))!.Autosave.Keep
        );
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task Autosave_keeps_a_document_once_its_edits_stop_without_waiting_the_interval(int idle, bool kept) {
        (await client.PutAsJsonAsync("/api/v1/preferences", new ServerPreferences(new(3600, idle, 3)), Token))
            .EnsureSuccessStatusCode();
        var state = await client.OpenAsync(GraphTesting.Write(server.Library, GraphTesting.Sample()), Token);

        await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.5f]));

        var file = Path.GetFullPath(state.Path, server.Library);
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (Recovery.Count(server.Library, file) == 0 && DateTime.UtcNow < deadline) {
            await Task.Delay(100, Token);
        }

        // An hour's interval has not passed, so only the idle wait can have kept it.
        Assert.Equal(kept, Recovery.Count(server.Library, file) > 0);
    }

    [Fact]
    public async Task The_cache_says_where_it_is_and_how_large() {
        var cache = await client.GetFromJsonAsync<CacheInfo>("/api/v1/library/cache", Token);

        Assert.Equal(Path.Combine(server.Library, ".ptcache"), cache!.Path);
        Assert.True(cache.Bytes > 0); // the index is in it
    }
}
