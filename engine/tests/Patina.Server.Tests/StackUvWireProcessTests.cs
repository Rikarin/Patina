using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Patina.Server.Tests;

/// <summary>A texture set's UV layout as lines for the 2D view (#137), drawn by a real Worker.</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A stack on no mesh has no layout to draw. On the low housing it is an SVG over the unit square: every end is
    ///     between 0 and 1, an edge two triangles of a chart share is one line, and asking again draws the same.
    /// </summary>
    [Fact]
    public async Task The_uv_layout_comes_as_lines_over_the_unit_square() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("Layout.vxlayers", deadline.Token);
        using (var none = await http.GetAsync($"/api/v1/stacks/{state.Id}/uv-wire", deadline.Token)) {
            Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        }

        await BindMeshAsync(state.Id, deadline.Token);
        using var response = await http.GetAsync($"/api/v1/stacks/{state.Id}/uv-wire", deadline.Token);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(deadline.Token));
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        var svg = await response.Content.ReadAsStringAsync(deadline.Token);

        var lines = Lines().Matches(svg);
        var ends = lines.SelectMany(static line => line.Groups.Values.Skip(1))
            .Select(static number => double.Parse(number.Value, CultureInfo.InvariantCulture))
            .ToList();
        // Ten charts of four sides or more, each side once: a quad split in two shares its diagonal.
        Assert.InRange(lines.Count, 40, 400);
        Assert.All(ends, static end => Assert.InRange(end, 0, 1));
        Assert.Equal(
            lines.Count,
            lines.Select(static line => Sorted(line.Value)).Distinct(StringComparer.Ordinal).Count()
        );

        using var again = await http.GetAsync($"/api/v1/stacks/{state.Id}/uv-wire", deadline.Token);
        Assert.Equal(svg, await again.Content.ReadAsStringAsync(deadline.Token));
    }

    /// <summary>A line's two ends in one order, so a line drawn twice either way round is seen as one.</summary>
    static string Sorted(string line) {
        var ends = line[1..].Split('L');
        return string.CompareOrdinal(ends[0], ends[1]) < 0 ? $"{ends[0]}L{ends[1]}" : $"{ends[1]}L{ends[0]}";
    }

    [GeneratedRegex(@"M(-?[\d.E-]+) (-?[\d.E-]+)L(-?[\d.E-]+) (-?[\d.E-]+)")]
    private static partial Regex Lines();
}
