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
