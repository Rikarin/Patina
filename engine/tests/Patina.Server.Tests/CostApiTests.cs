using Patina.Contracts;
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
}
