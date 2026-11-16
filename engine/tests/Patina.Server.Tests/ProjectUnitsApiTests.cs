using Patina.Contracts;
using Patina.Library;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>
///     A project says how long its mesh's unit is (#146, #307), for a file that says nothing or says it wrongly: kept
///     in its file, given back with the project, and turned back to the file's own by zero.
/// </summary>
public sealed class ProjectUnitsApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    string ProjectFile => Path.Combine(server.Library, "projects", "crate", LibraryLayout.ProjectFile);

    public ValueTask InitializeAsync() {
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abd0
            name: Game
            texelDensity: 1024
            channels: []
            defaultShader: pbr
            exportTargets: []
            """
        );
        var project = Directory.CreateDirectory(Path.Combine(server.Library, "projects", "crate")).FullName;
        ProjectDocument document = new(ProjectId.New(), "Crate", [], [], []);
        File.WriteAllText(Path.Combine(project, LibraryLayout.ProjectFile), DocumentCodec.Write(document));
        client = server.AuthorisedClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    Task<HttpResponseMessage> SetAsync(double unit) =>
        client.PatchAsJsonAsync(
            "/api/v1/library/project/settings",
            new ProjectSettingsRequest("crate", null, null, null) { MetresPerUnit = unit },
            Token
        );

    [Fact]
    public async Task A_project_keeps_its_meshs_unit_and_zero_goes_back_to_the_files() {
        using var centimetres = await SetAsync(0.01);

        Assert.Equal(HttpStatusCode.OK, centimetres.StatusCode);
        Assert.Equal(0.01, (await centimetres.Content.ReadFromJsonAsync<ProjectContents>(Token))!.MetresPerUnit);
        Assert.Contains(
            "metresPerUnit: 0.01",
            await File.ReadAllTextAsync(ProjectFile, Token),
            StringComparison.Ordinal
        );

        using var file = await SetAsync(0);
        Assert.Null((await file.Content.ReadFromJsonAsync<ProjectContents>(Token))!.MetresPerUnit);
        Assert.DoesNotContain(
            "metresPerUnit",
            await File.ReadAllTextAsync(ProjectFile, Token),
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1e-9)]
    [InlineData(5000)]
    public async Task A_unit_no_mesh_is_in_is_refused(double unit) {
        using var refused = await SetAsync(unit);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.DoesNotContain(
            "metresPerUnit",
            await File.ReadAllTextAsync(ProjectFile, Token),
            StringComparison.Ordinal
        );
    }
}
