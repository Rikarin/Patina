using Patina.Contracts;
using Patina.Export;
using Patina.Export.Presets;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>What the library costs, over the API a client uses (#207).</summary>
public sealed partial class LibraryTreeApiTests {
    /// <summary>
    ///     Once the index has caught up with the Knot sample, the library costs its two documents' maps, by the project
    ///     they are in, and the costlier one is the one over a budget between them.
    /// </summary>
    [Fact]
    public async Task The_library_costs_its_documents_maps_by_project_and_names_those_over_a_budget() {
        using var made = await client.PostAsJsonAsync("/api/v1/library/samples", new SampleRequest("Knot"), Token);
        Assert.Equal(HttpStatusCode.OK, made.StatusCode);
        const long Gold = 1024L * 1024 * 3 * 4;
        const long Red = 1024L * 512 * 3 * 4;

        LibraryCosts? costs = null;
        for (var tries = 0; tries < 200 && costs?.Documents != 2; tries++) {
            costs = await client.GetFromJsonAsync<LibraryCosts>("/api/v1/library/costs", Token);
            await Task.Delay(50, Token);
        }

        var over = await client.GetFromJsonAsync<DocumentCostPage>(
            $"/api/v1/library/costs/documents?over={Red}",
            Token
        );
        using var negative = await client.GetAsync("/api/v1/library/costs/documents?over=-1", Token);

        Assert.Equal([("knot", 2, Gold + Red)], costs!.Items.Select(static i => (i.Name, i.Documents, i.Bytes)));
        Assert.Equal("projects/knot/Gold.vxlayers", Assert.Single(over!.Items).Path);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
    }

    /// <summary>
    ///     On a platform the library costs what its preset writes (#207): each of the preset's files for each set, at
    ///     its compression and with its mip chain, as the checks measure a file; a preset nobody has is refused.
    /// </summary>
    [Fact]
    public async Task On_a_platform_the_library_costs_what_its_preset_writes() {
        using var made = await client.PostAsJsonAsync("/api/v1/library/samples", new SampleRequest("Knot"), Token);
        Assert.Equal(HttpStatusCode.OK, made.StatusCode);
        var preset = ExportPresetCatalog.Read(server.Library).Find("gltf")!;
        var expected = (long)Math.Ceiling(Written(preset, 1024, 1024)) + (long)Math.Ceiling(Written(preset, 1024, 512));

        LibraryCosts? costs = null;
        for (var tries = 0; tries < 200 && costs?.Documents != 2; tries++) {
            costs = await client.GetFromJsonAsync<LibraryCosts>("/api/v1/library/costs?preset=gltf", Token);
            await Task.Delay(50, Token);
        }

        var over = await client.GetFromJsonAsync<DocumentCostPage>(
            "/api/v1/library/costs/documents?over=0&preset=gltf",
            Token
        );
        using var missing = await client.GetAsync("/api/v1/library/costs?preset=nobody", Token);

        Assert.Equal(expected, costs!.Bytes);
        Assert.NotEqual((1024L * 1024 + 1024L * 512) * 3 * 4, costs.Bytes);
        Assert.Equal(expected, over!.Items.Sum(static item => item.Bytes));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("preset-missing", await missing.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    static double Written(ExportPreset preset, int width, int height) =>
        preset.Textures.Sum(texture => {
                var (w, h) = ExportMemory.Sized(preset, texture, width, height);
                return ExportMemory.Bytes(texture, w, h);
            }
        );
}
