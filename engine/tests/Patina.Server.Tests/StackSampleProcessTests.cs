using Patina.Contracts;
using Patina.Library;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Patina.Server.Tests;

/// <summary>The sample Start offers, through a real Worker (#190).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     The Knot sample's mesh is read as two materials laid out in one UV set, and each of its documents evaluates
    ///     on it as it ships, masks and all, so a first look at Patina opens on something that draws.
    /// </summary>
    [Fact]
    public async Task The_knot_sample_reads_as_two_laid_out_materials_and_each_document_draws() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var (instance, _) = await StartAsync("Unused.vxlayers", deadline.Token);

        using var made = await http.PostAsJsonAsync(
            "/api/v1/library/samples",
            new SampleRequest("Knot"),
            deadline.Token
        );
        var project = await made.Content.ReadFromJsonAsync<ProjectSummary>(deadline.Token);
        var facts = await http.GetFromJsonAsync<MeshFacts>(
            $"/api/v1/library/mesh?path=projects/{project!.Path}/knot.glb",
            deadline.Token
        );

        Assert.Equal(["Gold", "Red"], facts!.Materials.Select(static m => m.Name).Order(StringComparer.Ordinal));
        Assert.All(facts.Materials, static material => Assert.Equal(1, material.UvSets));
        foreach (var document in new[] { "Gold", "Red" }) {
            Shrink(Path.Combine(Library, "projects", project.Path, $"{document}.vxlayers"));
        }

        foreach (var document in new[] { "Gold", "Red" }) {
            using var opened = await http.PostAsJsonAsync(
                "/api/v1/stacks",
                new { path = $"projects/{project.Path}/{document}.vxlayers" },
                deadline.Token
            );
            var state = await opened.Content.ReadFromJsonAsync(
                ContractsJsonContext.Default.StackState,
                deadline.Token
            );
            Assert.Equal(256, state!.Width);
            using var socket = await FollowAsync(instance, state, deadline.Token);
            var evaluation = await EvaluatedAsync(socket, state.Revision, deadline.Token);
            Assert.Empty(evaluation.Problems);
            Assert.True(evaluation.Previews.Count > 0, $"{document} drew no maps.");
        }
    }

    /// <summary>
    ///     Draws a sample's document at an eighth of its side, its shape kept. ⚠ The sample ships at 1024 by 1024, and
    ///     its grunges' splatters and slope blur at that size outlast the deadline on CI's software GPU: the plan is
    ///     the same at any size, and only the pixels it covers differ.
    /// </summary>
    static void Shrink(string document) =>
        File.WriteAllText(document, SampleSide().Replace(File.ReadAllText(document), Eighth));

    static string Eighth(Match side) =>
        $"{side.Groups["key"].Value}: {int.Parse(side.Groups["side"].Value, CultureInfo.InvariantCulture) / 8}";

    [GeneratedRegex("^(?<key>baseWidth|baseHeight): (?<side>[0-9]+)", RegexOptions.Multiline)]
    private static partial Regex SampleSide();
}
