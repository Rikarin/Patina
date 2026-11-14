using Patina.Contracts;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     The presets a build of the library writes when its request names none (#207): kept in <c>library.yaml</c>, each
///     once, read back by <c>GET /library</c>, and refused with nothing written when one is no preset.
/// </summary>
public sealed class ExportTargetsApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() {
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
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        client = server.AuthorisedClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task The_library_keeps_the_presets_a_build_writes() {
        using var kept = await client.PutAsJsonAsync(
            "/api/v1/library/export/targets",
            new LibraryTargetsRequest(["godot", "gltf", "godot"]),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        Assert.Equal(["godot", "gltf"], (await kept.Content.ReadFromJsonAsync<LibraryInfo>(Token))!.ExportTargets);
        var info = await client.GetFromJsonAsync<LibraryInfo>("/api/v1/library", Token);
        Assert.Equal(["godot", "gltf"], info!.ExportTargets);
        var yaml = await File.ReadAllTextAsync(Path.Combine(server.Library, "library.yaml"), Token);
        Assert.Contains("godot", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_target_that_is_no_preset_is_refused_and_nothing_is_written() {
        using var refused = await client.PutAsJsonAsync(
            "/api/v1/library/export/targets",
            new LibraryTargetsRequest(["gltf", "nothing"]),
            Token
        );

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
        Assert.Equal("preset-missing", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("nothing", problem.RootElement.GetProperty("values").GetProperty("preset").GetString());
        Assert.Empty((await client.GetFromJsonAsync<LibraryInfo>("/api/v1/library", Token))!.ExportTargets);
    }
}
