using Patina.Contracts;
using Patina.Server.Api;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>Bake options no bake should run with are refused with a code, before anything is kept or queued (#228).</summary>
public sealed class BakeRefusalApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Occlusion whose minimum occluder distance is the occlusion distance: every ray would start past its reach.</summary>
    static BakeOptions White => new() { Maps = ["normal", "ao"], Occlusion = new() { Distance = 0.2, Minimum = 0.2 } };

    public async ValueTask InitializeAsync() {
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
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
        client = server.AuthorisedClient();
        using var created = await client.PostAsJsonAsync(
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

    [Fact]
    public async Task A_project_does_not_keep_a_minimum_occluder_distance_at_the_occlusion_distance() {
        using var refused = await client.PutAsJsonAsync(
            "/api/v1/library/project/bake",
            new ProjectBakeRequest("crate", new() { Options = White }),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var (code, values) = await RefusalAsync(refused);
        Assert.Equal(Refusals.BakeOccluderMinimum, code);
        Assert.Equal(("0.2", "0.2"), (values.GetProperty("minimum").GetString(), values.GetProperty("distance").GetString()));

        // Without occlusion or the bent normal nothing reads the minimum, so it is kept as it is.
        using var kept = await client.PutAsJsonAsync(
            "/api/v1/library/project/bake",
            new ProjectBakeRequest("crate", new() { Options = White with { Maps = ["normal", "thickness"] } }),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
    }

    [Fact]
    public async Task A_bake_with_a_minimum_occluder_distance_past_the_occlusion_distance_is_refused_before_it_queues() {
        using var refused = await client.PostAsJsonAsync(
            "/api/v1/jobs/bakes",
            new BakeRequest {
                High = ["high.glb"],
                Low = "low.glb",
                Output = "out",
                Options = White with { Maps = ["bent"], Occlusion = White.Occlusion with { Minimum = 0.3 } }
            },
            ContractsJsonContext.Default.BakeRequest,
            Token
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(Refusals.BakeOccluderMinimum, (await RefusalAsync(refused)).Code);
    }

    async Task<(string? Code, JsonElement Values)> RefusalAsync(HttpResponseMessage response) {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return (problem.RootElement.GetProperty("code").GetString(), problem.RootElement.GetProperty("values").Clone());
    }
}
