using Patina.Contracts;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>Project templates: a project's settings kept by name, which a new project starts from (#144).</summary>
public sealed partial class LibraryTreeApiTests {
    /// <summary>
    ///     A project's settings kept as a template come back on a project made from it, with its own mesh and size: the
    ///     viewport, checks and Auto Unwrap the template holds, and the bake it holds in place of the project's size.
    ///     A template nobody kept refuses the new project before anything is made, and one thrown away is gone.
    /// </summary>
    [Fact]
    public async Task A_project_made_from_a_template_starts_with_its_settings() {
        await MakeCrateAsync();
        var display = new ViewportDisplay(Exposure: 1.5, View: "clay");
        var checks = new ProjectCheckSettings { MemoryBudget = 16, MaxSize = 2048 };
        var bake = new ProjectBakeSettings { Options = new() { Resolution = 512, Maps = ["normal"] } };
        await KeptAsync("/api/v1/library/project/viewport", new ProjectViewportRequest("crate", display));
        await KeptAsync("/api/v1/library/project/checks", new ProjectChecksRequest("crate", checks));
        await KeptAsync("/api/v1/library/project/bake", new ProjectBakeRequest("crate", bake));
        using (var unwrap = await client.PatchAsJsonAsync(
                   "/api/v1/library/project/settings",
                   new ProjectSettingsRequest("crate", null, null, false),
                   Token
               )) {
            Assert.True(unwrap.IsSuccessStatusCode, await unwrap.Content.ReadAsStringAsync(Token));
        }

        using var saved = await client.PostAsJsonAsync(
            "/api/v1/library/project-templates",
            new ProjectTemplateRequest("crate", "  Hero Prop "),
            Token
        );
        var listed = await client.GetFromJsonAsync<ProjectTemplateList>("/api/v1/library/project-templates", Token);
        var mesh = Path.Combine(files.FullName, "barrel.glb");
        File.WriteAllText(mesh, "mesh");
        using var made = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Barrel", Mesh: mesh, Resolution: 1024) { Template = "Hero Prop" },
            Token
        );
        using var typo = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Stool") { Template = "Hero Porp" },
            Token
        );
        using var deleted = await client.DeleteAsync("/api/v1/library/project-templates?name=Hero%20Prop", Token);
        using var again = await client.DeleteAsync("/api/v1/library/project-templates?name=Hero%20Prop", Token);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(["Hero Prop"], listed!.Names);
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var barrel = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=barrel", Token);
        Assert.Equal(display, barrel!.Viewport);
        Assert.Equal(checks, barrel.Checks);
        Assert.Equal(512, barrel.Bake.Options.Resolution);
        Assert.Equal(["normal"], barrel.Bake.Options.Maps);
        Assert.False(barrel.AutoUnwrap);
        Assert.Single(barrel.Meshes);
        Assert.Equal(HttpStatusCode.NotFound, typo.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "projects", "stool")));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Empty((await deleted.Content.ReadFromJsonAsync<ProjectTemplateList>(Token))!.Names);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    /// <summary>
    ///     A template that says nothing about baking leaves a new project baking at the size it was made at, and a name
    ///     that cannot name a file is refused.
    /// </summary>
    [Fact]
    public async Task A_template_with_no_bake_leaves_the_size_a_project_was_made_at() {
        await MakeCrateAsync();
        using var saved = await client.PostAsJsonAsync(
            "/api/v1/library/project-templates",
            new ProjectTemplateRequest("crate", "Plain"),
            Token
        );
        using var nameless = await client.PostAsJsonAsync(
            "/api/v1/library/project-templates",
            new ProjectTemplateRequest("crate", "--"),
            Token
        );
        var mesh = Path.Combine(files.FullName, "small.glb");
        File.WriteAllText(mesh, "mesh");
        using var made = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Small", Mesh: mesh, Resolution: 1024) { Template = "plain" },
            Token
        );

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nameless.StatusCode);
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var small = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=small", Token);
        Assert.Equal(1024, small!.Bake.Options.Resolution);
    }

    async Task KeptAsync<T>(string route, T request) {
        using var kept = await client.PutAsJsonAsync(route, request, Token);
        Assert.True(kept.IsSuccessStatusCode, await kept.Content.ReadAsStringAsync(Token));
    }
}
