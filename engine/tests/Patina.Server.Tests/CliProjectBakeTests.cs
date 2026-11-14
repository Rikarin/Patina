using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Diagnostics;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     <c>patina bake-maps --project</c> as a build farm drives it (#186): a project's bake with the settings its
///     <c>project.yaml</c> keeps, into the folder its documents read, with no Server, no Worker and no GPU.
/// </summary>
public sealed class CliProjectBakeTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-cli-project-bake-");

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    /// <summary>The housing pair, and a project that paints the low and bakes two maps at 32.</summary>
    string Project() {
        var project = Path.Combine(folder.FullName, "Housing");
        BakeFixture.Write(Path.Combine(project, "meshes"), 0.02f);
        var mesh = AssetId.New();
        var document = TestLibraryProject() with {
            Meshes = [new(mesh, $"meshes/{BakeFixture.LowFile}", $"meshes/{BakeFixture.HighFile}")],
            Documents = [new(AssetId.New(), DocumentKinds.Texturing, "housing.vxlayers", "Housing", mesh)],
            Bake = new() { Options = new() { Resolution = 32, Maps = ["normal", "height"] } }
        };
        File.WriteAllText(Path.Combine(project, LibraryLayout.ProjectFile), DocumentCodec.Write(document));
        return project;
    }

    static ProjectDocument TestLibraryProject() => new(ProjectId.New(), "Housing", [], [], []);

    async Task<(int Exit, string Out, string Error)> PatinaAsync(params string[] arguments) {
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Engine.Dll("Patina.Cli", "patina"));
        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        using var cli = Process.Start(info)!;
        var error = cli.StandardError.ReadToEndAsync(Token);
        var output = await cli.StandardOutput.ReadToEndAsync(Token);
        await cli.WaitForExitAsync(Token);
        return (cli.ExitCode, output, await error);
    }

    [Fact]
    public async Task A_project_bakes_with_its_own_settings_into_the_folder_its_documents_read() {
        var project = Project();

        var dry = await PatinaAsync("bake-maps", "--project", project, "--dry-run");
        Assert.True(dry.Exit == 0, dry.Error);
        var plan = JsonSerializer.Deserialize(dry.Out, PackageJsonContext.Default.ProjectBakePlan)!;
        var request = Assert.Single(plan.Requests);
        Assert.Equal((32, null), (request.Options.Resolution, request.Material));
        Assert.Equal(Path.Combine(project, "bakes", "maps"), request.Output);
        Assert.False(Directory.Exists(Path.Combine(project, "bakes")));

        var baked = await PatinaAsync("bake-maps", "--project", project, "--threads", "2");
        Assert.True(baked.Exit == 0, baked.Error);
        var maps = Path.Combine(project, "bakes", "maps");
        Assert.True(File.Exists(Path.Combine(maps, "housing_low_normal.png")), baked.Out + baked.Error);
        Assert.True(File.Exists(Path.Combine(maps, "housing_low_height.png")));
        Assert.Equal(maps, DocumentBakes.FolderOf(project, ProjectBakes.Read(project).Documents[0]));

        var refused = await PatinaAsync("bake-maps", "--project", project, "--set", "Lid");
        Assert.Equal(2, refused.Exit);
        Assert.Contains("no texture set", refused.Error, StringComparison.Ordinal);
        Assert.Equal(2, (await PatinaAsync("bake-maps", "--project", folder.FullName)).Exit);
        var help = await PatinaAsync("bake-maps", "--help");
        Assert.Contains("--project <folder>", help.Out, StringComparison.Ordinal);
    }
}
