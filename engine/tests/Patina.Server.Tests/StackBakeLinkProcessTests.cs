using Patina.Bench;
using Patina.Contracts;
using Patina.Library;

namespace Patina.Server.Tests;

/// <summary>A bake reaches the open set it is of with no client pointing the set at it (#268).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A client that bakes and says nothing more, as a Blender add-on would, finds the open set reading the bake
    ///     once the job is done: one run on the Worker, then one from the cache, each into a folder of its own.
    /// </summary>
    [Fact]
    public async Task A_bake_of_an_open_set_is_what_the_set_reads_when_the_job_is_done() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.05f);
        _ = await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(Path.Combine(meshes, BakeFixture.LowFile), deadline.Token);
        var projectFolder = Path.Combine(Library, "projects", project);
        var state = await OpenAsync($"projects/{project}/housing.vxlayers", deadline.Token);
        Assert.Null(state.MeshMaps);

        BakeRequest request = new() {
            High = [Path.Combine(meshes, BakeFixture.HighFile)],
            Low = Path.Combine(projectFolder, BakeFixture.LowFile),
            Output = Path.Combine(projectFolder, "chosen"),
            Options = new() { Resolution = 64, Maps = ["normal"] }
        };
        var made = await BakeAsync(request, deadline.Token);
        Assert.False(made.Result!.Cached);
        Assert.Equal(request.Output, (await StateAsync(state.Id, deadline.Token)).MeshMaps);

        request.Output = Path.Combine(projectFolder, "again");
        var cached = await BakeAsync(request, deadline.Token);
        Assert.True(cached.Result!.Cached);
        var after = await StateAsync(state.Id, deadline.Token);
        Assert.Equal(request.Output, after.MeshMaps);
        Assert.Null(after.BakeStale);
    }
}
