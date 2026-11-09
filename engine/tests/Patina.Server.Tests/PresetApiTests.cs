using Patina.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>The preset editor's API (#155): presets as files in the library or a project, every write read first.</summary>
public sealed class PresetApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abce
            name: Game
            texelDensity: 1024
            channels: []
            defaultShader: pbr
            exportTargets: []
            """
        );
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        client = server.AuthorisedClient();
        var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Kind: "layered-material"),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    async Task<ExportPresetDocument> Factory(string id) =>
        (await client.GetFromJsonAsync<ExportPresetSource>($"/api/v1/export/presets/{id}", Token))!.Document;

    async Task<string?> CodeOf(HttpResponseMessage response) {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return problem.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>A duplicate kept in a project is that project's: a file in its folder, listed only with it.</summary>
    [Fact]
    public async Task A_preset_kept_in_a_project_is_a_file_there_and_listed_with_it() {
        var unreal = await Factory("unreal");

        var put = await client.PutAsJsonAsync(
            "/api/v1/export/presets/crate-unreal",
            new ExportPresetWrite(unreal with { Name = "Crate's Unreal" }, "crate"),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var file = Path.Combine(server.Library, "projects", "crate", "export-presets", "crate-unreal.yaml");
        Assert.Contains("name: \"Crate's Unreal\"", await File.ReadAllTextAsync(file, Token), StringComparison.Ordinal);
        var withProject = await client.GetFromJsonAsync<ExportPresetList>("/api/v1/export/presets?project=crate", Token);
        var library = await client.GetFromJsonAsync<ExportPresetList>("/api/v1/export/presets", Token);
        Assert.Equal("project", withProject!.Presets.Single(static p => p.Id == "crate-unreal").Origin);
        Assert.DoesNotContain(library!.Presets, static p => p.Id == "crate-unreal");
        Assert.Contains(library.Compressions, static term => term is { Value: "bc7", Label: "BC7" });
        Assert.Contains("udim", library.Tokens);
    }

    [Fact]
    public async Task A_preset_a_file_could_not_hold_is_refused_with_the_readers_reason() {
        var unreal = await Factory("unreal");
        var broken = unreal with {
            Textures = [unreal.Textures[0] with { Format = "jpeg" }]
        };

        var put = await client.PutAsJsonAsync("/api/v1/export/presets/broken", new ExportPresetWrite(broken), Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, put.StatusCode);
        Assert.Equal("preset-invalid", await CodeOf(put));
        Assert.False(File.Exists(Path.Combine(server.Library, "export-presets", "broken.yaml")));
    }

    [Fact]
    public async Task A_rename_moves_the_file_and_a_factory_preset_is_not_removed() {
        var unreal = await Factory("unreal");
        await client.PutAsJsonAsync("/api/v1/export/presets/mine", new ExportPresetWrite(unreal), Token);

        var renamed = await client.PutAsJsonAsync(
            "/api/v1/export/presets/ours",
            new ExportPresetWrite(unreal, null, "mine"),
            Token
        );
        var factory = await client.DeleteAsync("/api/v1/export/presets/godot", Token);
        var removed = await client.DeleteAsync("/api/v1/export/presets/ours", Token);
        var again = await client.DeleteAsync("/api/v1/export/presets/ours", Token);

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal((HttpStatusCode.Conflict, "preset-factory"), (factory.StatusCode, await CodeOf(factory)));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal((HttpStatusCode.NotFound, "preset-missing"), (again.StatusCode, await CodeOf(again)));
        Assert.Empty(Directory.GetFiles(Path.Combine(server.Library, "export-presets")));
    }

    /// <summary>A check keeps nothing and names each file as a build of the project would.</summary>
    [Fact]
    public async Task A_check_names_the_files_and_says_what_is_wrong() {
        var gltf = await Factory("gltf");

        var good = await (await client.PostAsJsonAsync(
            "/api/v1/export/presets/check",
            new ExportPresetCheckRequest(gltf with { Textures = [gltf.Textures[0] with { File = "{mesh}/{set}_{udim}" }] }, "crate"),
            Token
        )).Content.ReadFromJsonAsync<ExportPresetCheck>(Token);
        var bad = await (await client.PostAsJsonAsync(
            "/api/v1/export/presets/check",
            new ExportPresetCheckRequest(gltf with { Normal = "sideways" }),
            Token
        )).Content.ReadFromJsonAsync<ExportPresetCheck>(Token);

        Assert.Null(good!.Problem);
        Assert.Equal("Crate/Crate_1001.png", Assert.Single(good.Files).Name);
        Assert.Contains("sideways", bad!.Problem, StringComparison.Ordinal);
        Assert.Empty(bad.Files);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "export-presets")));
    }

    [Fact]
    public async Task A_preset_file_is_brought_in_after_it_is_read() {
        var source = Path.Combine(server.Library, "..", "shared-preset.yaml");
        await File.WriteAllTextAsync(source, (await client.GetFromJsonAsync<ExportPresetSource>("/api/v1/export/presets/godot", Token))!.Text.Replace("id: godot", "id: studio", StringComparison.Ordinal), Token);

        var imported = await client.PostAsJsonAsync("/api/v1/export/presets/import", new ExportPresetImport(source, "crate"), Token);

        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.True(File.Exists(Path.Combine(server.Library, "projects", "crate", "export-presets", "studio.yaml")));
    }

    /// <summary>A preview of a project nothing has built says so with a code, rather than compressing nothing.</summary>
    [Fact]
    public async Task A_compression_preview_of_unbuilt_maps_is_refused_with_a_code() {
        var response = await client.PostAsJsonAsync(
            "/api/v1/export/compression",
            new CompressionPreviewRequest("crate", "vixen", "{project}_orm"),
            Token
        );

        Assert.Equal((HttpStatusCode.Conflict, "export-unbuilt"), (response.StatusCode, await CodeOf(response)));
        var picture = await client.GetAsync("/api/v1/export/compression/00000000000000000000000000000000/source", Token);
        Assert.Equal(HttpStatusCode.NotFound, picture.StatusCode);
    }
}
