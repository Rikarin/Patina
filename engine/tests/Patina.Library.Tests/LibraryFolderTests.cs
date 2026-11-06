namespace Patina.Library.Tests;

/// <summary>Folders are folders on disk, and a move keeps a project's id.</summary>
public sealed class LibraryFolderTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-folders-");
    readonly string root;
    readonly OpenLibrary library;

    public LibraryFolderTests() {
        root = Path.Combine(folder.FullName, "Game.ptlib");
        LibraryFiles.CreateDefaultLibrary(root, "Game");
        library = OpenLibrary.Open(root);
    }

    public void Dispose() {
        library.Dispose();
        folder.Delete(true);
    }

    [Fact]
    public void A_new_folder_survives_on_disk_with_its_colour() {
        var made = library.CreateFolder(string.Empty, "Hero Props", "#D05A3C");
        library.CreateFolder(made.Path, "Doors", null);

        Assert.True(File.Exists(Path.Combine(root, "projects", "Hero Props", LibraryLayout.FolderFile)));
        var top = Assert.Single(library.Folders(string.Empty));
        Assert.Equal(("Hero Props", "#d05a3c"), (top.Name, top.Color));
        Assert.Equal("Hero Props/Doors", Assert.Single(library.Folders("Hero Props")).Path);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/../..")]
    [InlineData("/etc")]
    public void A_path_cannot_leave_the_library(string path) {
        Assert.ThrowsAny<Exception>(() => library.CreateFolder(path, "Escape", null));

        Assert.False(Directory.Exists(Path.Combine(root, "..", "Escape")));
    }

    [Fact]
    public void A_colour_has_to_be_one() =>
        Assert.Throws<ProjectRefusedException>(() => library.CreateFolder(string.Empty, "Red", "red"));

    [Fact]
    public void A_folder_cannot_share_a_name() {
        library.CreateFolder(string.Empty, "Props", null);

        Assert.Throws<ProjectExistsException>(() => library.CreateFolder(string.Empty, "Props", null));
    }

    [Fact]
    public void A_project_keeps_its_id_and_contents_when_it_moves() {
        library.CreateFolder(string.Empty, "Props", null);
        var made = library.CreateProject(new("Crate"));
        File.WriteAllText(Path.Combine(root, "projects", made.Path, "crate.vxlayers"), "x");
        library.Rescan();

        var moved = library.Move([made.Path], "Props");
        library.Rescan();

        Assert.Equal("Props/crate", Assert.Single(moved));
        var found = Assert.Single(library.Projects(new(Folder: "Props")).Items);
        Assert.Equal((made.Id, "Props/crate"), (found.Id, found.Path));
        Assert.Empty(library.Projects(new(Folder: string.Empty)).Items);
        Assert.Equal(1, Assert.Single(library.Folders(string.Empty)).ProjectCount);
        Assert.Contains(library.Contents("Props/crate").Files, static f => f.Kind == "stack");
    }

    [Fact]
    public void A_listing_of_a_folder_holds_only_what_is_directly_in_it() {
        library.CreateFolder(string.Empty, "a", null);
        library.CreateFolder("a", "b", null);
        library.CreateProject(new("Top"));
        library.CreateProject(new("Mid", "a"));
        library.CreateProject(new("Deep", "a/b"));
        library.Rescan();

        Assert.Equal("Top", Assert.Single(library.Projects(new(Folder: string.Empty)).Items).Name);
        Assert.Equal("Mid", Assert.Single(library.Projects(new(Folder: "a")).Items).Name);
        Assert.Equal(3, library.Projects(new()).Total);
        Assert.Equal(2, Assert.Single(library.Folders(string.Empty)).ProjectCount);
    }

    [Fact]
    public void A_folder_cannot_go_inside_itself() {
        library.CreateFolder(string.Empty, "a", null);
        library.CreateFolder("a", "b", null);

        Assert.Throws<ProjectRefusedException>(() => library.Move(["a"], "a/b"));
        Assert.True(Directory.Exists(Path.Combine(root, "projects", "a", "b")));
    }

    [Fact]
    public void Nothing_goes_inside_a_project() {
        library.CreateFolder(string.Empty, "a", null);
        library.CreateProject(new("Crate"));

        Assert.Throws<ProjectRefusedException>(() => library.Move(["a"], "crate"));
        Assert.Throws<ProjectRefusedException>(() => library.CreateFolder("crate", "b", null));
    }

    [Fact]
    public void A_move_with_one_clash_moves_nothing() {
        library.CreateFolder(string.Empty, "into", null);
        library.CreateFolder("into", "taken", null);
        library.CreateFolder(string.Empty, "free", null);
        library.CreateFolder(string.Empty, "taken", null);

        Assert.Throws<ProjectExistsException>(() => library.Move(["free", "taken"], "into"));

        Assert.True(Directory.Exists(Path.Combine(root, "projects", "free")));
        Assert.True(Directory.Exists(Path.Combine(root, "projects", "taken")));
    }

    [Fact]
    public void A_folder_renames_and_changes_only_its_case() {
        library.CreateFolder(string.Empty, "props", null);

        Assert.Equal("Props", library.RenameFolder("props", "Props"));
        Assert.Equal("Props", Assert.Single(library.Folders(string.Empty)).Name);
        Assert.Equal("Scenery", library.RenameFolder("Props", "Scenery"));
    }

    [Fact]
    public void A_folder_with_something_in_it_is_not_deleted() {
        library.CreateFolder(string.Empty, "a", null);
        var made = library.CreateProject(new("Crate", "a"));

        Assert.Throws<ProjectExistsException>(() => library.DeleteFolder("a", false));

        Assert.True(File.Exists(LibraryFiles.ProjectFile(root, made.Path)));
    }

    [Fact]
    public void Lifting_keeps_every_project_and_removes_the_folder() {
        library.CreateFolder(string.Empty, "a", null);
        library.CreateProject(new("Crate", "a"));

        library.DeleteFolder("a", true);

        Assert.False(Directory.Exists(Path.Combine(root, "projects", "a")));
        Assert.True(File.Exists(LibraryFiles.ProjectFile(root, "crate")));
    }

    [Fact]
    public void A_project_remembers_its_mesh_and_stack_across_a_rewrite() {
        var made = library.CreateProject(new("Crate"));
        var mesh = Path.Combine(folder.FullName, "low.glb");
        File.WriteAllText(mesh, "mesh");

        Assert.Equal("low.glb", library.UseMesh(made.Path, mesh));
        library.UseStack(made.Path, "crate.vxlayers");

        var contents = library.Contents(made.Path);
        Assert.Equal("projects/crate/low.glb", contents.Mesh);
        Assert.Equal("projects/crate/crate.vxlayers", contents.Stack);
        Assert.Equal("projects/crate/bakes", contents.Bakes);
    }

    [Fact]
    public void A_project_written_before_contents_still_reads() {
        const string Text = """
                            schema: 1
                            id: 0190b3a0-0000-7000-8000-000000000001
                            name: Old
                            tags: []
                            references: []
                            outputs: []
                            """;

        var read = DocumentCodec.ReadProject(Text, "project.yaml");

        Assert.Equal((1, null, null), (read.FileSchema, read.Document.Mesh, read.Document.Stack));
    }

    [Fact]
    public void A_file_that_is_no_mesh_is_refused() {
        var file = Path.Combine(folder.FullName, "notes.txt");
        File.WriteAllText(file, "x");
        var made = library.CreateProject(new("Crate"));

        Assert.Throws<ProjectRefusedException>(() => library.UseMesh(made.Path, file));
    }
}
