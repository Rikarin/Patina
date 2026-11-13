using Patina.Contracts;
using Patina.Server.Stacks;
using System.Net;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>The sample projects Patina ships, made over the API a client uses (#190).</summary>
public sealed partial class LibraryTreeApiTests {
    /// <summary>
    ///     The Knot sample is a project with its mesh and a texturing document for each of its two materials, each on
    ///     the mesh and named for its material; making it again gives a second project rather than writing over the
    ///     first.
    /// </summary>
    [Fact]
    public async Task A_sample_becomes_a_project_with_a_document_for_each_material_of_its_mesh() {
        var samples = await client.GetFromJsonAsync<SampleList>("/api/v1/library/samples", Token);
        using var made = await client.PostAsJsonAsync("/api/v1/library/samples", new SampleRequest("Knot"), Token);
        using var again = await client.PostAsJsonAsync("/api/v1/library/samples", new SampleRequest("Knot"), Token);
        var first = await made.Content.ReadFromJsonAsync<ProjectSummary>(Token);
        var second = await again.Content.ReadFromJsonAsync<ProjectSummary>(Token);
        var contents = await client.GetFromJsonAsync<ProjectContents>(
            $"/api/v1/library/project?path={first!.Path}",
            Token
        );

        Assert.Contains("Knot", samples!.Names);
        Assert.Equal(("knot", "knot-2"), (first.Path, second!.Path));
        Assert.Equal(["sample"], first.Tags);
        var mesh = Assert.Single(contents!.Meshes);
        Assert.Equal(
            [("Gold", "Gold"), ("Red", "Red")],
            contents.Documents.Select(static d => (d.Name, d.Material)).Order()
        );
        Assert.All(contents.Documents, d => Assert.Equal(mesh.Id, d.Mesh));
    }

    /// <summary>
    ///     Each of the sample's documents reads as a stack on the sample's mesh, painted at its own shape, and every
    ///     mask it uses is a compound Patina ships, so it opens with nothing missing.
    /// </summary>
    [Fact]
    public async Task The_samples_documents_are_stacks_on_its_mesh_whose_masks_are_shipped_compounds() {
        using var made = await client.PostAsJsonAsync("/api/v1/library/samples", new SampleRequest("Knot"), Token);
        var project = await made.Content.ReadFromJsonAsync<ProjectSummary>(Token);
        var folder = Path.Combine(server.Library, "projects", project!.Path);

        foreach (var (file, set, width, height) in new[] { ("Gold", "Gold", 2048, 2048), ("Red", "Red", 2048, 1024) }) {
            var stack = StackDocuments.Parse(
                await File.ReadAllTextAsync(Path.Combine(folder, $"{file}.vxlayers"), Token)
            );
            Assert.Equal(("knot.glb", width, height), (stack.Model, stack.BaseWidth, stack.BaseHeight));
            var only = Assert.Single(stack.Sets);
            Assert.Equal(set, only.Name);
            Assert.All(
                only.Layers.Where(static layer => layer.Mask.Source == LayerMaskSource.Generator),
                static layer => Assert.Contains(layer.Mask.Generator, Patina.Content.FactoryCompounds.Paths)
            );
        }

        Assert.True(File.Exists(Path.Combine(folder, "knot.glb")));
    }

    /// <summary>A sample Patina does not ship is refused, and makes no project.</summary>
    [Fact]
    public async Task An_unknown_sample_is_refused() {
        using var refused = await client.PostAsJsonAsync(
            "/api/v1/library/samples",
            new SampleRequest("Teapot"),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "projects", "teapot")));
    }
}
