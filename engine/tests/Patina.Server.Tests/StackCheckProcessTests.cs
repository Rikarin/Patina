using Patina.Contracts;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>Checks before export with a real Worker (#159): the Worker measures the maps, and the Server judges them.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A fill too dark for a material that is not metal is found over every texel, with its code and a view that
    ///     shows it; half metal is found as partial; and a set with nothing wrong says nothing of its values. The
    ///     memory it costs is the library's first preset's.
    /// </summary>
    [Fact]
    public async Task A_check_finds_values_no_material_has_and_says_where() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("Checked.vxlayers", deadline.Token);

        var clean = await ChecksAsync(state.Id, deadline.Token);
        Assert.DoesNotContain(clean.Problems, static problem => problem.Code.StartsWith("pbr-", StringComparison.Ordinal));
        Assert.Equal((Side, Side), (clean.Width, clean.Height));
        Assert.True(clean.MemoryBytes > 0, "The first preset writes the set's maps.");
        Assert.NotEmpty(clean.Preset);

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("fill", "Soot", new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = [0.01f, 0.01f, 0.01f, 1f] })
        );
        var dark = Assert.Single(
            (await ChecksAsync(state.Id, deadline.Token)).Problems,
            static problem => problem.Code == CheckCodes.AlbedoDark
        );
        Assert.Equal("warning", dark.Severity);
        Assert.Equal("1", dark.Values["share"]);
        Assert.Equal("baseColor", dark.Target.View);
        Assert.NotNull(dark.Target.U);

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("fill", "Half metal", new Dictionary<string, IReadOnlyList<float>> { ["metalness"] = [0.5f, 0.5f, 0.5f, 1f] })
        );
        var codes = (await ChecksAsync(state.Id, deadline.Token)).Problems.Select(static problem => problem.Code);
        Assert.Contains(CheckCodes.MetalPartial, codes);
    }

    async Task<StackChecks> ChecksAsync(string id, CancellationToken token) {
        using var response = await http.GetAsync($"/api/v1/stacks/{id}/checks", token);
        await SucceededAsync(response, token);
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackChecks, token))!;
    }
}
