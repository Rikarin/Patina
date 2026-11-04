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
    public void A_new_library_has_the_layout_of_the_plan() {
        var root = Root("New.ptlib");
        var id = LibraryId.New();

        LibraryFiles.CreateLibrary(root, new(id, "New", 2048, ["baseColor"], "pbr", []));

        using var library = OpenLibrary.Open(root);
        Assert.Equal(id, library.Id);
        Assert.Equal(2048, library.Document.TexelDensity);
        Assert.True(Directory.Exists(Path.Combine(root, "shared", "smart-materials")));
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
        Assert.Equal(100, report.Projects);
        Assert.True(library.Impact(generated.Popular)!.Projects > 10);
    }
}
