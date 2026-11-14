using Patina.Contracts;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     A project's sets checked at once, and for one platform it ships to (#159), over the API a client uses. No set is
///     open, so the Server judges each on what its file says and no Worker is asked.
/// </summary>
public sealed class ProjectChecksApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() {
        foreach (var folder in new[] { "shared", "projects" }) {
            Directory.CreateDirectory(Path.Combine(server.Library, folder));
        }

        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abcd
            name: Game
            texelDensity: 1024
            channels: []
            defaultShader: pbr
            exportTargets: []
            """
        );
        client = server.AuthorisedClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    /// <summary>
    ///     Every set is listed with its file, unmeasured since nobody has it open, and the project's memory is theirs
    ///     together. A platform's own size limit holds only when the check is for that platform's preset, and a zero
    ///     in it leaves the project's.
    /// </summary>
    [Fact]
    public async Task Every_set_is_checked_at_once_and_a_platform_holds_them_to_its_own_limits() {
        await MakeAsync();
        var unreal = new PlatformCheckLimits("unreal") { MaxSize = 1024 };
        using var kept = await client.PutAsJsonAsync(
            "/api/v1/library/project/checks",
            new ProjectChecksRequest("crate", new() { MaxSize = 4096, Platforms = [unreal] }),
            Token
        );
        kept.EnsureSuccessStatusCode();

        var own = await ChecksAsync(null);
        var forUnreal = await ChecksAsync("unreal");

        Assert.Equal(
            ["projects/crate/crate.vxlayers", "projects/crate/trim.vxlayers"],
            own.Sets.Select(static set => set.Document)
        );
        Assert.All(own.Sets, static set => Assert.False(set.Measured));
        Assert.All(own.Sets, static set => Assert.Equal(string.Empty, set.Stack));
        Assert.Equal(own.Sets.Sum(static set => set.MemoryBytes), own.MemoryBytes);
        Assert.DoesNotContain(own.Sets.SelectMany(static set => set.Problems), Large);
        Assert.Equal([true, false], forUnreal.Sets.Select(static set => set.Problems.Any(Large)));
        Assert.Equal("1024", forUnreal.Sets[0].Problems.First(Large).Values["limit"]);
    }

    /// <summary>A platform no library or project has a preset for is refused with the code a client words.</summary>
    [Fact]
    public async Task A_check_for_a_preset_nobody_has_is_refused() {
        await MakeAsync();

        using var refused = await client.GetAsync("/api/v1/library/project/checks?project=crate&preset=nowhere", Token);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
        Assert.Equal("preset-missing", problem.RootElement.GetProperty("code").GetString());
    }

    /// <summary>A project keeps its platforms' limits in its file, and refuses two for one platform.</summary>
    [Fact]
    public async Task A_project_keeps_one_set_of_limits_for_each_platform() {
        await MakeAsync();
        var checks = new ProjectCheckSettings {
            MemoryBudget = 64, Platforms = [new("unreal") { MemoryBudget = 16 }, new("gltf") { TexelDensity = 256 }]
        };

        using var kept = await client.PutAsJsonAsync(
            "/api/v1/library/project/checks",
            new ProjectChecksRequest("crate", checks),
            Token
        );
        using var twice = await client.PutAsJsonAsync(
            "/api/v1/library/project/checks",
            new ProjectChecksRequest("crate", checks with { Platforms = [new("unreal"), new("unreal")] }),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        Assert.Equal(checks, contents!.Checks);
    }

    static bool Large(CheckProblem problem) => problem.Code == CheckCodes.TooLarge;

    async Task<ProjectChecks> ChecksAsync(string? preset) {
        var query = preset is null ? string.Empty : $"&preset={preset}";
        var checks = await client.GetFromJsonAsync<ProjectChecks>(
            $"/api/v1/library/project/checks?project=crate{query}",
            Token
        );
        return checks!;
    }

    /// <summary>A project of two layered materials, one of 2048 texels a side and one of 1024.</summary>
    async Task MakeAsync() {
        using var made = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Resolution: 2048, Kind: "layered-material"),
            Token
        );
        made.EnsureSuccessStatusCode();
        using var added = await client.PostAsJsonAsync(
            "/api/v1/library/project/documents",
            new ProjectDocumentRequest("crate", "layered-material", "Trim", Resolution: 1024),
            Token
        );
        added.EnsureSuccessStatusCode();
    }
}
