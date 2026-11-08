using Patina.Contracts;

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
    public void A_mesh_from_outside_the_library_leaves_no_path_in_what_is_committed() {
        var elsewhere = Directory.CreateTempSubdirectory("patina-art-");
        try {
            var made = library.CreateProject(new("Crate"));
            var outside = Path.Combine(elsewhere.FullName, "low.glb");
            File.WriteAllBytes(outside, [1, 2, 3]);

            library.AddMesh(made.Path, outside, null);

            var yaml = File.ReadAllText(Path.Combine(root, "projects", made.Path, "project.yaml"));
            Assert.DoesNotContain("source", yaml, StringComparison.Ordinal);
            Assert.DoesNotContain(elsewhere.Name, yaml, StringComparison.Ordinal);
            Assert.Equal(outside, Assert.Single(library.Contents(made.Path).Meshes).Source);

            // Another machine has the project and none of this.
            Directory.Delete(Path.Combine(root, LibraryLayout.CacheFolder, "mesh-sources"), true);
            Assert.Null(Assert.Single(library.Contents(made.Path).Meshes).Source);
        } finally {
            elsewhere.Delete(true);
        }
    }

    [Fact]
    public void A_mesh_from_inside_the_library_is_remembered_relative_to_it() {
        var made = library.CreateProject(new("Crate"));
        var art = Directory.CreateDirectory(Path.Combine(root, "art"));
        var inside = Path.Combine(art.FullName, "low.glb");
        File.WriteAllBytes(inside, [1, 2, 3]);

        library.AddMesh(made.Path, inside, null);

        var yaml = File.ReadAllText(Path.Combine(root, "projects", made.Path, "project.yaml"));
        Assert.Contains("source: art/low.glb", yaml, StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath(inside), Assert.Single(library.Contents(made.Path).Meshes).Source);
    }

    [Fact]
    public void A_mesh_brought_in_again_from_inside_the_project_keeps_where_it_came_from() {
        var elsewhere = Directory.CreateTempSubdirectory("patina-art-");
        try {
            var made = library.CreateProject(new("Crate"));
            var outside = Path.Combine(elsewhere.FullName, "low.glb");
            File.WriteAllBytes(outside, [1, 2, 3]);
            library.AddMesh(made.Path, outside, null);

            library.AddMesh(made.Path, Path.Combine(root, "projects", made.Path, "low.glb"), null);

            Assert.Equal(outside, Assert.Single(library.Contents(made.Path).Meshes).Source);
        } finally {
            elsewhere.Delete(true);
        }
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
    public void A_copy_of_a_stack_paints_on_its_own_file() {
        var made = library.CreateProject(new("Crate"));
        var folder = Path.Combine(root, "projects", made.Path);
        var stack = new ProjectItem(AssetId.New(), DocumentKinds.LayeredMaterial, "crate.vxlayers", "Crate");
        LibraryFiles.WriteDocument(root, made.Path, "sets:\n  - layers:\n    - paint: crate.a.vxpaint\n"u8, stack);
        File.WriteAllText(Path.Combine(folder, "crate.a.vxpaint"), "paint");

        var copy = library.CopyDocument(made.Path, stack.Id, "Crate Worn");

        Assert.Equal("crate-worn.vxlayers", copy.File);
        Assert.Contains(
            "crate-worn.a.vxpaint",
            File.ReadAllText(Path.Combine(folder, copy.File)),
            StringComparison.Ordinal
        );
        Assert.Equal("paint", File.ReadAllText(Path.Combine(folder, "crate-worn.a.vxpaint")));
        Assert.Contains(library.Contents(made.Path).Documents, static d => d.Name == "Crate Worn");
    }

    [Fact]
    public void Autosaved_work_is_offered_until_it_is_restored_or_saved_over() {
        var file = Path.Combine(root, "crate.vxlayers");
        File.WriteAllText(file, "saved");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-5));
        Assert.Null(Recovery.Newer(root, file));

        File.WriteAllText(Directory.CreateDirectory(Recovery.Folder(root, file)).FullName + "/a.vxpaint", "pixels");
        Recovery.Keep(root, file, "newer");
        Assert.NotNull(Recovery.Newer(root, file));

        Recovery.Restore(root, file);

        Assert.Equal("newer", File.ReadAllText(file));
        Assert.Equal("pixels", File.ReadAllText(Path.Combine(root, "a.vxpaint")));
        Assert.Null(Recovery.Newer(root, file));
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
    public void One_mesh_goes_under_every_texturing_document_and_auto_unwrap_is_remembered() {
        var made = library.CreateProject(new("Crate"));
        var folder = Directory.CreateTempSubdirectory("patina-mesh-");
        var first = Path.Combine(folder.FullName, "first.glb");
        var second = Path.Combine(folder.FullName, "second.glb");
        File.WriteAllText(first, "mesh");
        File.WriteAllText(second, "mesh");
        var before = library.AddMesh(made.Path, first, null);
        library.AddDocument(made.Path, DocumentKinds.Texturing, "a.vxlayers", "A", before.Id);
        library.AddDocument(made.Path, DocumentKinds.Texturing, "b.vxlayers", "B", before.Id);
        var after = library.AddMesh(made.Path, second, null);

        var moved = library.UseMesh(made.Path, after.Id);
        library.SetMeshHandling(made.Path, false, false);

        Assert.Equal(2, moved.Count);
        var contents = library.Contents(made.Path);
        Assert.False(contents.AutoUnwrap);
        Assert.False(contents.PreservePaint);
        Assert.All(contents.Documents, document => Assert.Equal(after.Id.Value, document.Mesh));
        Assert.Equal(after.Id.Value, contents.Meshes.Single(static mesh => mesh.Id is not null).Id);
        Assert.True(File.Exists(library.ProjectFile(made.Path, before.File)));
    }

    /// <summary>
    ///     A project keeps how its viewport was left, every setting of it, through its file: a project that never set
    ///     one has none, and its file says nothing about the viewport.
    /// </summary>
    [Fact]
    public void A_project_remembers_how_its_viewport_was_left() {
        var made = library.CreateProject(new("Lamp"));
        Assert.Null(library.Contents(made.Path).Viewport);
        Assert.DoesNotContain("viewport", File.ReadAllText(LibraryFiles.ProjectFile(root, made.Path)));
        var left = new ViewportDisplay {
            Environment = "/hdri/forest path.exr",
            EnvironmentRotation = 2.25,
            BackgroundBlur = 0.1,
            BackgroundOpacity = 0.5,
            LightRotation = -1.5,
            Exposure = -0.75,
            Shadows = 0.4,
            ToneMapping = "agx",
            Look = "/looks/warm.cube",
            View = "picture",
            Picture = "/bakes/curvature.png",
            Wireframe = true,
            Orthographic = true,
            FieldOfView = 1.2,
            Contrast = 1.1,
            Saturation = 0.9,
            Vignette = 0.2,
            Grain = 0.3,
            Aberration = 0.4,
            Sharpen = 0.5
        };

        library.SetViewport(made.Path, left);

        Assert.Equal(left, library.Contents(made.Path).Viewport);
        library.SetViewport(made.Path, new());
        Assert.Equal(new ViewportDisplay(), library.Contents(made.Path).Viewport);
    }

    [Fact]
    public void A_project_remembers_how_it_unwraps_and_what_an_unwrap_came_to() {
        var made = library.CreateProject(new("Ball"));
        var scratch = Directory.CreateTempSubdirectory("patina-unwrap-");
        var plain = Path.Combine(scratch.FullName, "ball.obj");
        var unwrapped = Path.Combine(scratch.FullName, "ball_unwrapped.glb");
        File.WriteAllText(plain, "mesh");
        File.WriteAllText(unwrapped, "mesh");
        UnwrapOptions options = new() {
            Resolution = 1024,
            Margin = 8,
            Quality = "boxes",
            Rotations = 2,
            TexelDensity = 512
        };
        UnwrapReport report = new(
            "ball.obj",
            [new("red", 3, 0, 1.04, 1.4, 0.61, ["A chart was split."]), new("blue", 2, 1, 1.1, 2, 0.5, [])]
        );

        library.SetMeshHandling(made.Path, null, null, options);
        library.AddMesh(made.Path, plain, null);
        library.AddMesh(made.Path, unwrapped, null, null, report);

        var contents = library.Contents(made.Path);
        Assert.Equal(options, contents.Unwrap);
        var listed = contents.Meshes.Single(static mesh => mesh.Path.EndsWith(
                "ball_unwrapped.glb",
                StringComparison.Ordinal
            )
        );
        Assert.Equal(report.From, listed.Unwrapped!.From);
        Assert.Equal(
            report.Sets.Select(static set => set with { Warnings = [] }),
            listed.Unwrapped.Sets.Select(static set => set with { Warnings = [] })
        );
        Assert.Equal(["A chart was split."], listed.Unwrapped.Sets[0].Warnings);
        Assert.Null(
            contents.Meshes.Single(static mesh => mesh.Path.EndsWith("ball.obj", StringComparison.Ordinal)).Unwrapped
        );
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
