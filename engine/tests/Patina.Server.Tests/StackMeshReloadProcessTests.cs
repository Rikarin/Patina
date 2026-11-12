using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>Reloading a project's mesh while its documents are open (#89).</summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A mesh reloaded on the same layout moves nothing: the open document stays open with its undo, no paint is
    ///     backed up, and the project's copy is the new file. A layout or a material that changed is refused before
    ///     anything is touched, so that a client can ask what reloading costs and close the documents first.
    /// </summary>
    [Fact]
    public async Task A_mesh_reloaded_on_the_same_layout_keeps_its_open_document_and_its_undo() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = Path.Combine(folder.FullName, "two.obj");
        BakeFixture.WriteTwoMaterials(mesh);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        using (var made = await http.PatchAsJsonAsync(
                   "/api/v1/library/project/settings",
                   new { project, autoUnwrap = false },
                   deadline.Token
               )) {
            Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        }

        var projectFolder = Path.Combine(Library, "projects", project);
        var held = Path.Combine(projectFolder, "two.obj");
        var contents = await http.GetFromJsonAsync<ProjectContents>(
            $"/api/v1/library/project?path={project}",
            deadline.Token
        );
        var strap = contents!.Documents.Single(static d => d.Material == "Strap");
        var state = await OpenAsync(strap.Path, deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Kept", Tint));
        var yaml = await File.ReadAllTextAsync(Path.Combine(projectFolder, "project.yaml"), deadline.Token);
        var digest = LibraryFiles.Digest(held);

        var relaid = Variant(
            "relaid",
            static text => text.Replace("vt 1 1", "vt 0.9 1", StringComparison.Ordinal)
        );
        await RefusedReloadAsync(project, relaid, "'Strap' is laid out differently", deadline.Token);
        var renamed = Variant("renamed", static text => text.Replace("Strap", "Belt", StringComparison.Ordinal));
        await RefusedReloadAsync(project, renamed, "materials", deadline.Token);

        // Asked to move paint under an open document, it refuses before the mesh is overwritten.
        using (var moving = await http.PatchAsJsonAsync(
                   "/api/v1/library/project/settings",
                   new ProjectSettingsRequest(project, relaid, null, null),
                   deadline.Token
               )) {
            Assert.Equal(HttpStatusCode.BadRequest, moving.StatusCode);
            Assert.Contains("Close 'Strap' first", await moving.Content.ReadAsStringAsync(deadline.Token));
        }

        Assert.Equal(yaml, await File.ReadAllTextAsync(Path.Combine(projectFolder, "project.yaml"), deadline.Token));
        Assert.Equal(digest, LibraryFiles.Digest(held));

        var lifted = Path.Combine(
            Directory.CreateDirectory(Path.Combine(folder.FullName, "lifted")).FullName,
            "two.obj"
        );
        BakeFixture.WriteTwoMaterials(lifted, 0.5f);
        using (var reloaded = await ReloadAsync(project, lifted, deadline.Token)) {
            Assert.True(reloaded.IsSuccessStatusCode, await reloaded.Content.ReadAsStringAsync(deadline.Token));
        }

        Assert.Equal(LibraryFiles.Digest(lifted), LibraryFiles.Digest(held));
        Assert.False(Directory.Exists(Path.Combine(projectFolder, LibraryLayout.PaintBackupFolder)));
        Assert.Contains((await StateAsync(state.Id, deadline.Token)).Layers, static layer => layer.Name == "Kept");
        await ActAsync(state.Id, "undo", deadline.Token);
        Assert.DoesNotContain(
            (await StateAsync(state.Id, deadline.Token)).Layers,
            static layer => layer.Name == "Kept"
        );

        string Variant(string name, Func<string, string> change) {
            var into = Directory.CreateDirectory(Path.Combine(folder.FullName, name)).FullName;
            foreach (var file in Directory.EnumerateFiles(folder.FullName, "two.*")) {
                File.WriteAllText(Path.Combine(into, Path.GetFileName(file)), change(File.ReadAllText(file)));
            }

            return Path.Combine(into, "two.obj");
        }
    }

    Task<HttpResponseMessage> ReloadAsync(string project, string mesh, CancellationToken token) =>
        http.PatchAsJsonAsync(
            "/api/v1/library/project/settings",
            new ProjectSettingsRequest(project, mesh, null, null, SameLayout: true),
            token
        );

    async Task RefusedReloadAsync(string project, string mesh, string why, CancellationToken token) {
        using var refused = await ReloadAsync(project, mesh, token);
        var body = await refused.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("mesh-layout-changed", body, StringComparison.Ordinal);
        Assert.Contains(why, body, StringComparison.Ordinal);
    }
}
