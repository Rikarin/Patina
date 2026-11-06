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
    public void A_picture_is_copied_into_the_project_unless_it_is_there() {
        var made = library.CreateProject(new("Crate"));
        var outside = Path.Combine(root, "rust.png");
        File.WriteAllBytes(outside, [1, 2, 3]);

        var path = library.AddPicture(made.Path, outside);

        Assert.Equal($"projects/{made.Path}/pictures/rust.png", path);
        Assert.True(File.Exists(Path.Combine(root, path)));
        Assert.Equal(path, library.AddPicture(made.Path, Path.Combine(root, path)));
        Assert.Throws<FileNotFoundException>(() => library.AddPicture(made.Path, Path.Combine(root, "none.png")));
    }

    [Theory]
    [InlineData("../other.vxpaint")]
    [InlineData("a/../../other.vxpaint")]
    [InlineData("/etc/passwd")]
    public void A_name_in_a_library_file_cannot_leave_its_folder(string name) =>
        Assert.Throws<InvalidDataException>(() => LibraryLayout.Beside(root, name));

    [Fact]
    public void A_name_beside_its_file_is_allowed() =>
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "a", "b.vxpaint"), LibraryLayout.Beside(root, "a/b.vxpaint"));

    [Fact]
    public void One_process_at_a_time_holds_a_library() {
        using var first = LibraryLock.Take(root);
        Assert.NotNull(first);

        Assert.Null(LibraryLock.Take(root));
        first.Dispose();
        using var second = LibraryLock.Take(root);
        Assert.NotNull(second);
    }

    [Fact]
    public void A_crash_leaves_nothing_behind_after_the_next_start() {
        var cache = Path.Combine(root, LibraryLayout.CacheFolder);
        Directory.CreateDirectory(Path.Combine(cache, "objects", "a.part"));
        File.WriteAllBytes(Path.Combine(cache, "objects", "keep.bin"), [1]);
        Directory.CreateDirectory(Path.Combine(LibraryLayout.PaintPath(root), "session"));
        Directory.CreateDirectory(Path.Combine(root, "exports", "unity"));
        File.WriteAllBytes(Path.Combine(root, "exports", "unity", "a.png.123.part"), [1]);

        Assert.Equal(3, CacheSweep.Leftovers(root));

        Assert.True(File.Exists(Path.Combine(cache, "objects", "keep.bin")));
        Assert.Empty(Directory.EnumerateDirectories(LibraryLayout.PaintPath(root)));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "exports", "unity")));
    }

    [Fact]
    public void Pruning_removes_only_what_has_not_been_touched() {
        var objects = LibraryLayout.ObjectsPath(root);
        Directory.CreateDirectory(objects);
        var old = Path.Combine(objects, "old.bin");
        var fresh = Path.Combine(objects, "fresh.bin");
        File.WriteAllBytes(old, [1]);
        File.WriteAllBytes(fresh, [1]);
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-40));

        Assert.Equal(1, CacheSweep.Prune(root, TimeSpan.FromDays(30)));

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
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
        Assert.Single(library.Contents("Props/crate").Documents);
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
    public void A_project_remembers_its_meshes_and_documents_across_a_rewrite() {
        var made = library.CreateProject(new("Crate"));
        var low = Path.Combine(folder.FullName, "low.glb");
        var high = Path.Combine(folder.FullName, "high.glb");
        File.WriteAllText(low, "mesh");
        File.WriteAllText(high, "mesh");

        var mesh = library.AddMesh(made.Path, low, high);
        library.AddDocument(made.Path, DocumentKinds.Texturing, "crate.vxlayers", "Crate", mesh.Id);

        var contents = library.Contents(made.Path);
        var listed = Assert.Single(contents.Meshes);
        Assert.Equal(("projects/crate/low.glb", "projects/crate/high.glb"), (listed.Path, listed.High));
        var document = Assert.Single(contents.Documents);
        Assert.Equal(
            ("texturing", "projects/crate/crate.vxlayers", mesh.Id.Value),
            (document.Kind, document.Path, document.Mesh)
        );
        Assert.Equal("projects/crate/bakes", contents.BakeFolder);
    }

    [Fact]
    public void Adding_a_mesh_again_keeps_what_is_on_it() {
        var made = library.CreateProject(new("Crate"));
        var low = Path.Combine(folder.FullName, "low.glb");
        var high = Path.Combine(folder.FullName, "high.glb");
        File.WriteAllText(low, "mesh");
        File.WriteAllText(high, "mesh");
        var mesh = library.AddMesh(made.Path, low, null);
        library.AddDocument(made.Path, DocumentKinds.Texturing, "crate.vxlayers", "Crate", mesh.Id);

        var again = library.AddMesh(made.Path, low, high);

        Assert.Equal(mesh.Id, again.Id);
        Assert.Equal(again.Id.Value, Assert.Single(library.Contents(made.Path).Documents).Mesh);
    }

    [Fact]
    public void A_texturing_document_needs_a_mesh_and_no_other_kind_has_one() {
        var made = library.CreateProject(new("Crate"));

        Assert.Throws<ProjectRefusedException>(() =>
            library.AddDocument(made.Path, DocumentKinds.Texturing, "a.vxlayers", "A", null)
        );
        Assert.Throws<ProjectRefusedException>(() =>
            library.AddDocument(made.Path, DocumentKinds.Graph, "b.vxtexgraph", "B", AssetId.New())
        );
    }

    [Fact]
    public void A_file_the_project_does_not_list_is_still_shown() {
        var made = library.CreateProject(new("Crate"));
        File.WriteAllText(Path.Combine(root, "projects", made.Path, "old.vxlayers"), "x");
        File.WriteAllText(Path.Combine(root, "projects", made.Path, "old.glb"), "x");

        var contents = library.Contents(made.Path);

        Assert.Null(Assert.Single(contents.Documents).Id);
        Assert.Null(Assert.Single(contents.Meshes).Id);
    }

    [Fact]
    public void A_graph_made_in_a_project_is_listed_with_a_kind() {
        var made = library.CreateProject(new("Crate"));

        library.CreateGraph(new("Rust", made.Path), "graph");

        var listed = Assert.Single(library.Contents(made.Path).Documents);
        Assert.Equal((DocumentKinds.Graph, "Rust"), (listed.Kind, listed.Name));
        Assert.NotNull(listed.Id);
    }

    [Theory]
    [InlineData("kind: painting\n    name: A\n    file: a.vxlayers", "no kind")]
    [InlineData("kind: graph\n    name: A\n    file: ../a.vxtexgraph", "outside")]
    [InlineData("kind: texturing\n    name: A\n    file: a.vxlayers", "on a mesh")]
    public void A_project_that_lists_nonsense_is_refused(string fields, string expected) {
        var text = $"""
                    schema: 2
                    id: 0190b3a0-0000-7000-8000-000000000001
                    name: Bad
                    tags: []
                    documents:
                      - id: 0190b3a0-0000-7000-8000-000000000002
                        {fields}
                    references: []
                    outputs: []
                    """;

        var problem = Assert.Throws<LibraryFormatException>(() => DocumentCodec.ReadProject(text, "project.yaml"));

        Assert.Contains(expected, problem.Message, StringComparison.Ordinal);
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

        Assert.Equal(1, read.FileSchema);
        Assert.Empty(read.Document.Documents);
    }

    [Fact]
    public void A_file_that_is_no_mesh_is_refused() {
        var file = Path.Combine(folder.FullName, "notes.txt");
        File.WriteAllText(file, "x");
        var made = library.CreateProject(new("Crate"));

        Assert.Throws<ProjectRefusedException>(() => library.AddMesh(made.Path, file, null));
    }
}
