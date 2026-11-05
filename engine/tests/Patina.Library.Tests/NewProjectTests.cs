using CsCheck;

namespace Patina.Library.Tests;

public sealed class NewProjectTests : IDisposable {
    readonly TestLibrary library = new();

    public void Dispose() => library.Dispose();

    [Theory]
    [InlineData("Crate 01", "crate-01")]
    [InlineData("  Rusty   Oak_Barrel! ", "rusty-oak-barrel")]
    [InlineData("Šípka", "šípka")]
    public void A_name_makes_a_folder(string name, string slug) => Assert.Equal(slug, NewProjects.Slug(name));

    [Fact]
    public void A_new_project_reads_back_as_written() {
        using var open = library.Open();

        var created = open.CreateProject(new("""Crate: "one" #1""", "props/small", ["wood", "hero", "wood"]));

        var text = File.ReadAllText(LibraryFiles.ProjectFile(library.Root, created.Path));
        var read = DocumentCodec.ReadProject(text, created.Path).Document;
        Assert.Equal("props/small/crate-one-1", created.Path);
        Assert.Equal(created.Id, read.Id.Value);
        Assert.Equal("""Crate: "one" #1""", read.Name);
        Assert.Equal(["hero", "wood"], read.Tags);
    }

    [Fact]
    public void A_new_shared_graph_is_a_file_with_a_sidecar_that_a_scan_indexes() {
        using var open = library.Open();

        var path = open.CreateGraph(new("Rust: Heavy"), "name: Rust");
        open.Rescan(TestContext.Current.CancellationToken);

        Assert.Equal("shared/graphs/rust-heavy.vxtexgraph", path);
        Assert.Equal("name: Rust", File.ReadAllText(Path.Combine(library.Root, path)));
        var asset = Assert.Single(open.Assets(new(null, null, 0, 50)).Items);
        Assert.Equal(("Rust: Heavy", LibraryLayout.GraphKind), (asset.Name, asset.Kind));
        Assert.Throws<ProjectExistsException>(() => open.CreateGraph(new("rust heavy"), "other"));
        Assert.Equal("name: Rust", File.ReadAllText(Path.Combine(library.Root, path)));
    }

    [Fact]
    public void A_new_project_graph_lands_in_its_project_and_a_missing_project_is_refused() {
        using var open = library.Open();
        var project = open.CreateProject(new("Crate", "props"));

        var path = open.CreateGraph(new("Rust", project.Path), "name: Rust");

        Assert.Equal("projects/props/crate/rust.vxtexgraph", path);
        Assert.True(File.Exists(Path.Combine(library.Root, path)));
        Assert.Throws<ProjectRefusedException>(() => open.CreateGraph(new("Rust", "props/nothing"), "text"));
        Assert.Throws<ProjectRefusedException>(() => open.CreateGraph(new("!!!"), "text"));
    }

    [Fact]
    public void A_graph_reads_the_compounds_folder_beside_it_and_a_compound_reads_its_own() {
        var graph = Path.Combine(library.Root, "shared/graphs/rust.vxtexgraph");
        var compounds = Path.Combine(library.Root, "shared/graphs", LibraryLayout.CompoundsFolder);
        var nested = Directory.CreateDirectory(Path.Combine(compounds, "Grunge")).FullName;

        Assert.Equal(compounds, CompoundFolders.Of(graph));
        Assert.False(CompoundFolders.Holds(graph));
        Assert.Equal(compounds, CompoundFolders.Of(Path.Combine(nested, "dirt.vxtexgraph")));
        Assert.True(CompoundFolders.Holds(Path.Combine(nested, "dirt.vxtexgraph")));
    }

    [Fact]
    public void The_index_lags_a_write_until_a_scan_sees_it() {
        using var open = library.Open();
        open.Rescan();

        open.CreateProject(new("Crate"));

        Assert.True(open.Indexing);
        Assert.Equal(0, open.Projects(new()).Total);
        open.Rescan();
        Assert.False(open.Indexing);
        Assert.Equal("Crate", open.Projects(new()).Items.Single().Name);
    }

    [Fact]
    public void The_same_folder_twice_is_refused() {
        using var open = library.Open();
        open.CreateProject(new("Crate"));

        Assert.Throws<ProjectExistsException>(() => open.CreateProject(new("crate")));
    }

    [Fact]
    public void A_project_cannot_go_inside_a_project() {
        using var open = library.Open();
        open.CreateProject(new("Crate", "props"));

        Assert.Throws<ProjectExistsException>(() => open.CreateProject(new("Lid", "props/crate")));
    }

    [Fact]
    public void A_project_cannot_go_over_a_folder_of_projects() {
        using var open = library.Open();
        open.CreateProject(new("Crate", "props"));

        Assert.Throws<ProjectExistsException>(() => open.CreateProject(new("Props")));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/../..")]
    [InlineData(".git")]
    [InlineData("a//b")]
    [InlineData("a /b")]
    [InlineData("""back\slash""")]
    [InlineData("con")]
    [InlineData("nul.txt")]
    [InlineData("trailing.")]
    public void A_folder_a_library_cannot_hold_is_refused(string folder) {
        using var open = library.Open();

        Assert.Throws<ProjectRefusedException>(() => open.CreateProject(new("Crate", folder)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("a\nb")]
    public void A_name_that_makes_no_folder_is_refused(string name) {
        using var open = library.Open();

        Assert.Throws<ProjectRefusedException>(() => open.CreateProject(new(name)));
    }

    [Fact]
    public void A_control_character_in_a_tag_is_refused() {
        using var open = library.Open();

        Assert.Throws<ProjectRefusedException>(() => open.CreateProject(new("Crate", null, ["a\tb"])));
    }

    [Fact]
    public void Tags_of_projects_and_of_assets_are_counted_apart() {
        library.Write("a", TestLibrary.Project("A") with { Tags = ["hero", "wood"] });
        library.Write("b", TestLibrary.Project("B") with { Tags = ["hero"] });
        library.Write("graphs/g.vxtexgraph", TestLibrary.Asset("G") with { Tags = ["metal"] });
        using var open = library.Open();
        open.Rescan();

        var projects = open.ProjectTags();
        var page = open.ProjectTags(1, 1);

        Assert.Equal([new("hero", 2), new("wood", 1)], projects.Items);
        Assert.Equal(new("wood", 1), page.Items.Single());
        Assert.Equal(2, page.Total);
        Assert.Equal("metal", open.AssetTags().Items.Single().Tag);
    }

    [Fact]
    [Trait("Category", "Property")]
    public void Any_slug_is_one_clean_folder() =>
        Gen.String.Sample(static name => {
                var slug = NewProjects.Slug(name);
                return slug.Length == 0
                    || (slug.Length <= 64 && slug[0] != '-' && slug[^1] != '-' && !slug.Contains('/'));
            }
        );
}
