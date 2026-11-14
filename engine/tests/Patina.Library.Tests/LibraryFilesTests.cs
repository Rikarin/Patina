using Patina.Bench;
using System.Security.Cryptography;

namespace Patina.Library.Tests;

public sealed class LibraryFilesTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-files-");

    public void Dispose() => folder.Delete(true);

    string Root(string name) => Path.Combine(folder.FullName, name);

    static string Digest(string root) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal)) {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file)));
            hash.AppendData(File.ReadAllBytes(file));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    [Fact]
    public void An_old_shared_folder_becomes_a_project_and_every_reference_still_resolves() {
        var root = Root("Old.ptlib");
        LibraryFiles.CreateLibrary(root, new(LibraryId.New(), "Old", 1024, [], "pbr", []));
        var asset = new AssetDocument(AssetId.New(), "graph", "Rust", ["metal"], []);
        var shared = Path.Combine(root, "shared");
        Directory.CreateDirectory(Path.Combine(shared, "graphs", "Compounds"));
        Directory.CreateDirectory(Path.Combine(shared, "export-presets"));
        Directory.CreateDirectory(Path.Combine(shared, "brushes"));
        File.WriteAllText(Path.Combine(shared, "graphs", "rust.vxtexgraph"), "name: Rust");
        File.WriteAllText(Path.Combine(shared, "graphs", "rust.vxtexgraph.asset.yaml"), DocumentCodec.Write(asset));
        File.WriteAllText(Path.Combine(shared, "graphs", "Compounds", "c.vxtexgraph"), "name: C");
        File.WriteAllText(Path.Combine(shared, "export-presets", "mine.yaml"), "id: mine");
        File.WriteAllText(Path.Combine(shared, "brushes", "round.png"), "png");

        using var library = OpenLibrary.Open(root);
        library.Rescan(TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(shared));
        var found = Assert.Single(library.Assets(new(null, null, 0, 50)).Items);
        Assert.Equal(
            (asset.Id.Value, "projects/shared-assets/rust.vxtexgraph"),
            (found.Id, found.Path)
        );
        Assert.True(File.Exists(Path.Combine(root, "export-presets", "mine.yaml")));
        Assert.True(File.Exists(Path.Combine(root, "projects", "shared-assets", "brushes", "round.png")));
        Assert.True(File.Exists(Path.Combine(root, "projects", "shared-assets", "Compounds", "c.vxtexgraph")));
    }

    [Fact]
    public void A_file_written_straight_into_shared_is_kept_and_a_loose_graph_is_listed() {
        var root = Root("Loose.ptlib");
        LibraryFiles.CreateLibrary(root, new(LibraryId.New(), "Loose", 1024, [], "pbr", []));
        var shared = Path.Combine(root, "shared");
        Directory.CreateDirectory(Path.Combine(shared, "graphs"));
        File.WriteAllText(Path.Combine(shared, "graphs", "moss.vxtexgraph"), "name: Moss");
        File.WriteAllText(Path.Combine(shared, "top.vxtexgraph"), "name: Top");
        File.WriteAllText(Path.Combine(shared, "notes.txt"), "keep me");

        using var library = OpenLibrary.Open(root);
        library.Rescan(TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(shared));
        var project = Path.Combine(root, "projects", "shared-assets");
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(project, "notes.txt")));
        Assert.Equal(
            ["projects/shared-assets/moss.vxtexgraph", "projects/shared-assets/top.vxtexgraph"],
            library.Assets(new(null, null, 0, 50)).Items.Select(static a => a.Path).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void A_shared_folder_that_comes_back_joins_the_first_move_and_overwrites_nothing() {
        var root = Root("Again.ptlib");
        LibraryFiles.CreateLibrary(root, new(LibraryId.New(), "Again", 1024, [], "pbr", []));
        var shared = Path.Combine(root, "shared", "graphs");
        var first = new AssetDocument(AssetId.New(), "graph", "Rust", [], []);
        var second = new AssetDocument(AssetId.New(), "graph", "Rust Again", [], []);
        Directory.CreateDirectory(shared);
        File.WriteAllText(Path.Combine(shared, "rust.vxtexgraph"), "name: Rust");
        File.WriteAllText(Path.Combine(shared, "rust.vxtexgraph.asset.yaml"), DocumentCodec.Write(first));
        OpenLibrary.Open(root).Dispose();
        Directory.CreateDirectory(shared);
        File.WriteAllText(Path.Combine(shared, "rust.vxtexgraph"), "name: Rust Again");
        File.WriteAllText(Path.Combine(shared, "rust.vxtexgraph.asset.yaml"), DocumentCodec.Write(second));

        using var library = OpenLibrary.Open(root);
        library.Rescan(TestContext.Current.CancellationToken);

        var project = Path.Combine(root, "projects", "shared-assets");
        Assert.Equal("name: Rust", File.ReadAllText(Path.Combine(project, "rust.vxtexgraph")));
        Assert.Equal("name: Rust Again", File.ReadAllText(Path.Combine(project, "rust-2.vxtexgraph")));
        Assert.Equal(
            [
                (second.Id.Value, "projects/shared-assets/rust-2.vxtexgraph"),
                (first.Id.Value, "projects/shared-assets/rust.vxtexgraph")
            ],
            library.Assets(new(null, null, 0, 50))
                .Items.Select(static a => (a.Id, a.Path))
                .OrderBy(static a => a.Path, StringComparer.Ordinal)
        );
    }

    [Fact]
    public void A_new_library_has_the_layout_of_the_plan() {
        var root = Root("New.ptlib");
        var id = LibraryId.New();

        LibraryFiles.CreateLibrary(root, new(id, "New", 2048, ["baseColor"], "pbr", []));

        using var library = OpenLibrary.Open(root);
        Assert.Equal(id, library.Id);
        Assert.Equal(2048, library.Document.TexelDensity);
        Assert.False(Directory.Exists(Path.Combine(root, "shared")));
        Assert.True(Directory.Exists(Path.Combine(root, "projects")));
        Assert.Contains(".ptcache/", File.ReadAllText(Path.Combine(root, ".gitignore")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_library_is_not_created_twice() {
        var root = Root("Twice.ptlib");
        LibraryFiles.CreateLibrary(root, new(LibraryId.New(), "A", 1024, [], string.Empty, []));

        Assert.Throws<IOException>(() =>
            LibraryFiles.CreateLibrary(root, new(LibraryId.New(), "B", 1024, [], string.Empty, []))
        );
    }

    [Fact]
    public void Keeping_a_revision_keeps_the_bytes_under_their_hash() {
        var root = Root("Pins.ptlib");
        LibraryFiles.CreateLibrary(root, new(LibraryId.New(), "A", 1024, [], string.Empty, []));
        LibraryFiles.WriteAsset(root, "graphs/a.vxtexgraph", "old"u8, new(AssetId.New(), "graph", "A", [], []));

        var revision = LibraryFiles.KeepRevision(root, "graphs/a.vxtexgraph");

        Assert.Equal("old", File.ReadAllText(LibraryLayout.RevisionPath(root, revision)));
        Assert.Equal(revision, LibraryFiles.KeepRevision(root, "graphs/a.vxtexgraph"));
    }

    [Fact]
    public void One_seed_generates_one_library() {
        LibraryGenerator.Generate(Root("A"), new(30, 7));
        LibraryGenerator.Generate(Root("B"), new(30, 7));

        Assert.Equal(Digest(Root("A")), Digest(Root("B")));
    }

    [Fact]
    public void Another_seed_generates_another_library() {
        LibraryGenerator.Generate(Root("A"), new(30, 7));
        LibraryGenerator.Generate(Root("B"), new(30, 8));

        Assert.NotEqual(Digest(Root("A")), Digest(Root("B")));
    }

    [Fact]
    public void A_generated_library_is_healthy() {
        var generated = LibraryGenerator.Generate(Root("G"), new(100));
        using var library = OpenLibrary.Open(Root("G"));

        var report = library.Check();

        Assert.False(report.HasErrors, string.Join('\n', report.Findings.Where(static f => f.IsError).Take(5)));
        Assert.Equal(102, report.Projects); // and the two the generator keeps its materials in
        Assert.True(library.Impact(generated.Popular)!.Projects > 10);
    }
}
