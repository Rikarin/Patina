using Patina.Contracts;

namespace Patina.Library.Tests;

/// <summary>
///     Every project's documents are in the index with their kinds, roles and tags (#311), so the shelf lists other
///     projects' documents with a query and not a read of every project's file and a walk of its folder.
/// </summary>
public sealed class IndexedDocumentTests : IDisposable {
    readonly TestLibrary library = new();

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => library.Dispose();

    [Fact]
    public void A_rescan_lists_each_projects_documents_as_the_project_lists_them_but_its_texturing_ones() {
        ProjectMesh mesh = new(AssetId.New(), "hero.glb");
        ProjectItem[] items = [
            new(AssetId.New(), DocumentKinds.Graph, "rust.vxtexgraph", "Rust", Role: GraphRoles.Mask) {
                Tags = ["metal", "worn"]
            },
            new(AssetId.New(), DocumentKinds.LayeredMaterial, "edge.vxlayers", "Edge Wear", Role: GraphRoles.Mask),
            new(AssetId.New(), DocumentKinds.LayeredMaterial, "paint.vxlayers", "Car Paint"),
            new(AssetId.New(), DocumentKinds.Graph, "plain.vxtexgraph", "Plain"),
            new(AssetId.New(), DocumentKinds.Texturing, "hero.vxlayers", "Hero", mesh.Id)
        ];
        library.Write("props/crate", TestLibrary.Project("Crate") with { Meshes = [mesh], Documents = items });
        library.Write("hero", TestLibrary.Project("Hero"));
        foreach (var item in items) {
            library.SaveFile($"projects/props/crate/{item.File}", "{}");
        }

        library.SaveFile("projects/hero/loose.vxtexgraph", "{}"); // copied in by hand: listed with no id
        library.SaveFile("projects/hero/notes.vxlayers", "{}"); // a stack nobody listed is no material
        using var open = library.Open();

        open.Rescan(Token);

        var found = open.Documents();
        var indexed = found.ToLookup(static document => document.Project, static document => document.Document);
        foreach (var project in (string[])["props/crate", "hero"]) {
            var listed = open.Contents(project)
                .Documents.Where(static document => document.Kind != DocumentKinds.Texturing)
                .OrderBy(static document => document.Path, StringComparer.Ordinal);
            Assert.Equal(listed.Select(Shelved), indexed[project].Select(Shelved));
        }

        Assert.Equal(["Crate", "Hero"], found.Select(static document => document.ProjectName).Distinct());
        Assert.Equal(5, found.Count);
    }

    [Fact]
    public void A_document_listed_since_or_retagged_is_known_after_the_next_scan() {
        library.Write("crate", TestLibrary.Project("Crate"));
        using var open = library.Open();
        open.Rescan(Token);
        Assert.Empty(open.Documents());

        var path = open.CreateGraph(new("Moss", "crate"), "{}");
        var id = Assert.Single(open.Contents("crate").Documents).Id!.Value;
        open.SetDocumentTags("crate", new(id), ["green"]);
        open.Rescan(Token);

        var document = Assert.Single(open.Documents()).Document;
        Assert.Equal((path, "Moss", "green"), (document.Path, document.Name, string.Join(',', document.Tags)));
    }

    static (Guid? Id, string Kind, string Name, string Path, string? Role, string Tags) Shelved(
        ProjectDocumentInfo document
    ) =>
        (document.Id, document.Kind, document.Name, document.Path, document.Role, string.Join(',', document.Tags));
}
