using Patina.Contracts;

namespace Patina.Library.Tests;

/// <summary>
///     Looking after a library: what is deleted goes to the bin and comes back with its identity, and tags, names and
///     pins are written to <c>project.yaml</c>, the truth the index follows.
/// </summary>
public sealed class LibraryCareTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-care-");
    readonly string root;
    readonly OpenLibrary library;

    public LibraryCareTests() {
        root = Path.Combine(folder.FullName, "Game.ptlib");
        LibraryFiles.CreateDefaultLibrary(root, "Game");
        library = OpenLibrary.Open(root);
    }

    public void Dispose() {
        library.Dispose();
        folder.Delete(true);
    }

    [Fact]
    public void A_deleted_project_waits_in_the_bin_and_comes_back_where_it_was() {
        var props = library.CreateFolder(string.Empty, "Props", null);
        var made = library.CreateProject(new("Crate", props.Path, ["wood"]));

        var binned = library.TrashProject(made.Path);

        Assert.False(Directory.Exists(Path.Combine(root, "projects", "Props", "crate")));
        var item = Assert.Single(library.Bin().Items);
        Assert.Equal((binned.Id, "project", "Crate", "projects/Props/crate"), (item.Id, item.Kind, item.Name, item.From));
        Assert.True(library.Bin().Bytes > 0);

        Assert.Equal("projects/Props/crate", library.Restore(item.Id));

        Assert.Empty(library.Bin().Items);
        Assert.Equal(made.Id, library.Contents("Props/crate").Id);
    }

    [Fact]
    public void A_project_whose_place_was_taken_comes_back_beside_it() {
        var made = library.CreateProject(new("Crate"));
        var binned = library.TrashProject(made.Path);
        library.CreateProject(new("Crate"));

        Assert.Equal("projects/crate-2", library.Restore(binned.Id));
        Assert.Equal(made.Id, library.Contents("crate-2").Id);
    }

    [Fact]
    public void A_deleted_document_takes_its_paint_and_comes_back_with_its_id() {
        var made = library.CreateProject(new("Crate"));
        var project = Path.Combine(root, "projects", made.Path);
        File.WriteAllText(Path.Combine(project, "worn.vxlayers"), "layers:\n  - paint: worn.a.vxpaint\n");
        File.WriteAllText(Path.Combine(project, "worn.a.vxpaint"), "pixels");
        var document = library.AddDocument(made.Path, DocumentKinds.LayeredMaterial, "worn.vxlayers", "Worn", null);
        library.SetDocumentTags(made.Path, document.Id, ["rust", "metal"]);

        var binned = library.TrashDocument(made.Path, document.Id);

        Assert.Equal(("document", "Worn", "projects/crate/worn.vxlayers"), (binned.Kind, binned.Name, binned.From));
        Assert.False(File.Exists(Path.Combine(project, "worn.vxlayers")));
        Assert.False(File.Exists(Path.Combine(project, "worn.a.vxpaint")));
        Assert.Empty(library.Contents(made.Path).Documents);

        Assert.Equal("projects/crate/worn.vxlayers", library.Restore(binned.Id));

        var back = Assert.Single(library.Contents(made.Path).Documents);
        Assert.Equal((document.Id.Value, "Worn"), (back.Id, back.Name));
        Assert.Equal(["metal", "rust"], back.Tags);
        Assert.Equal("pixels", File.ReadAllText(Path.Combine(project, "worn.a.vxpaint")));
    }

    [Fact]
    public void A_document_of_a_project_that_is_gone_waits_for_its_project() {
        var made = library.CreateProject(new("Crate"));
        File.WriteAllText(Path.Combine(root, "projects", made.Path, "wood.vxtexgraph"), "graph");
        var document = library.AddDocument(made.Path, DocumentKinds.Graph, "wood.vxtexgraph", "Wood", null);
        var lone = library.TrashDocument(made.Path, document.Id);
        var whole = library.TrashProject(made.Path);

        Assert.Throws<ProjectRefusedException>(() => library.Restore(lone.Id));

        library.Restore(whole.Id);
        library.Restore(lone.Id);
        Assert.Equal("Wood", Assert.Single(library.Contents(made.Path).Documents).Name);
    }

    [Fact]
    public void Emptying_the_bin_deletes_for_good() {
        var first = library.TrashProject(library.CreateProject(new("Crate")).Path);
        library.TrashProject(library.CreateProject(new("Barrel")).Path);
        library.DeleteFromBin(first.Id);
        Assert.Single(library.Bin().Items);

        Assert.Equal(1, library.EmptyBin());

        Assert.Empty(library.Bin().Items);
        Assert.Throws<FileNotFoundException>(() => library.Restore(first.Id));
        Assert.Contains(".trash/", File.ReadAllText(Path.Combine(root, ".gitignore")), StringComparison.Ordinal);
    }

    [Fact]
    public void Tags_and_names_are_kept_in_the_project_file_and_the_index_follows() {
        var made = library.CreateProject(new("Crate"));
        File.WriteAllText(Path.Combine(root, "projects", made.Path, "wood.vxtexgraph"), "graph");
        var document = library.AddDocument(made.Path, DocumentKinds.Graph, "wood.vxtexgraph", "Wood", null);

        library.SetProjectTags(made.Path, [" Hero ", "prop", "hero", ""]);
        library.SetDocumentTags(made.Path, document.Id, ["oak"]);
        library.RenameDocument(made.Path, document.Id, "Oak Planks");

        var yaml = File.ReadAllText(Path.Combine(root, "projects", made.Path, "project.yaml"));
        Assert.Contains("Oak Planks", yaml, StringComparison.Ordinal);
        Assert.Equal(["Hero", "prop"], library.Contents(made.Path).Tags);
        library.Rescan(TestContext.Current.CancellationToken);
        Assert.Equal("Crate", Assert.Single(library.Projects(new(Tags: ["prop"])).Items).Name);
        Assert.Equal("Oak Planks", Assert.Single(library.Assets(new(Tags: ["oak"])).Items).Name);
    }

    [Fact]
    public void A_pin_holds_a_reference_at_the_bytes_the_asset_has_now() {
        var shared = library.CreateProject(new("Shared"));
        var file = Path.Combine(root, "projects", shared.Path, "wood.vxtexgraph");
        File.WriteAllText(file, "first");
        var wood = library.AddDocument(shared.Path, DocumentKinds.Graph, "wood.vxtexgraph", "Wood", null);
        var user = library.CreateProject(new("Crate"));
        library.AddReference(user.Path, wood.Id);
        library.Rescan(TestContext.Current.CancellationToken);

        var pinned = library.SetPin(user.Path, wood.Id, true);

        Assert.Equal(ReferenceMode.Pinned, pinned.Mode);
        Assert.Equal(Revision.Of("first"u8).Text, pinned.Revision?.Text);
        Assert.True(File.Exists(LibraryLayout.RevisionPath(root, pinned.Revision!.Value)));
        library.Rescan(TestContext.Current.CancellationToken);
        Assert.Equal(1, library.Impact(wood.Id)!.Pinned);

        Assert.Equal(ReferenceMode.Floating, library.SetPin(user.Path, wood.Id, false).Mode);
        library.Rescan(TestContext.Current.CancellationToken);
        Assert.Equal(0, library.Impact(wood.Id)!.Pinned);

        // A project that does not refer to it has nothing to pin.
        Assert.Throws<ProjectRefusedException>(() => library.SetPin(shared.Path, wood.Id, true));
    }
}
