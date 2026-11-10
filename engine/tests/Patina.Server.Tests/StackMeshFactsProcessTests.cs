using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>What a mesh is, read by the Worker and said by the Server (#146).</summary>
public sealed partial class StackProcessTests {
    [Fact]
    public async Task A_mesh_says_its_parts_and_materials_by_path_inside_or_outside_the_library() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = Path.Combine(folder.FullName, "two.obj");
        BakeFixture.WriteTwoMaterials(mesh);
        await StartAsync("Unused.vxlayers", deadline.Token);

        var facts = await http.GetFromJsonAsync<MeshFacts>(
            $"/api/v1/library/mesh?path={Uri.EscapeDataString(mesh)}",
            deadline.Token
        );

        Assert.NotNull(facts);
        Assert.Equal("two.obj", facts.File);
        Assert.Equal(["Body", "Strap"], facts.Materials.Select(static m => m.Name).Order(StringComparer.Ordinal));
        Assert.Equal(1, facts.UvSets);
        Assert.True(facts.Triangles > 0);

        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        var held = await http.GetFromJsonAsync<MeshFacts>(
            $"/api/v1/library/mesh?path=projects/{project}/two.obj",
            deadline.Token
        );
        Assert.Equal(facts.Triangles, held!.Triangles);

        using var picture = await http.GetAsync(
            "/api/v1/library/mesh?path=projects/housing/project.yaml",
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, picture.StatusCode);
    }
}
