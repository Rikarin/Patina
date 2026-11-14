using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>Apply Channels to All Sets (#141): one texture set's channels reach the model's other sets.</summary>
public sealed partial class StackProcessTests {
    async Task<StackChannelsApplied> ApplyChannelsAsync(string id, CancellationToken token) {
        using var response = await http.PostAsync($"/api/v1/stacks/{id}/channels/apply-to-all", null, token);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackChannelsApplied, token))!;
    }

    /// <summary>
    ///     A closed set is given the channels in its file, and an open one as an undo step of its own; a set that has
    ///     them already is left alone.
    /// </summary>
    [Fact]
    public async Task A_sets_channels_reach_the_models_other_sets_open_or_closed() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = Path.Combine(folder.FullName, "two.obj");
        BakeFixture.WriteTwoMaterials(mesh);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        await SplitIntoSetsAsync(project, deadline.Token);
        using var contents = JsonDocument.Parse(
            await http.GetStringAsync($"/api/v1/library/project?path={project}", deadline.Token)
        );
        var documents = contents.RootElement.GetProperty("documents")
            .EnumerateArray()
            .ToDictionary(
                static d => d.GetProperty("material").GetString()!,
                static d => d.GetProperty("path").GetString()!
            );

        var body = await OpenAsync(documents["Body"], deadline.Token);
        await EditAsync(body.Id, deadline.Token, new StackAddChannel("height"));

        var closed = Assert.Single((await ApplyChannelsAsync(body.Id, deadline.Token)).Sets);
        Assert.Equal(("Strap", false, null), (closed.Set, closed.Open, closed.Problem));
        Assert.Equal(["height"], closed.Added);

        var strap = await OpenAsync(documents["Strap"], deadline.Token);
        Assert.Contains(strap.Channels, static channel => channel.Usage == "height");
        Assert.Empty(Assert.Single((await ApplyChannelsAsync(body.Id, deadline.Token)).Sets).Added);

        await EditAsync(body.Id, deadline.Token, new StackAddChannel("occlusion"));
        var open = Assert.Single((await ApplyChannelsAsync(body.Id, deadline.Token)).Sets);

        Assert.Equal(("Strap", true), (open.Set, open.Open));
        Assert.Equal(["occlusion"], open.Added);
        var after = await StateAsync(strap.Id, deadline.Token);
        Assert.Contains(after.Channels, static channel => channel.Usage == "occlusion");
        Assert.Equal("Add channels from Body", after.History.UndoLabel);
    }
}
