namespace Patina.Library.Tests;

/// <summary>
///     Clean and the uploads nothing names (#162, #297): what a file names, however it writes it, keeps; what nothing
///     names goes to the bin and comes back; and the index remembers what each file named, so an unchanged file is not
///     read again.
/// </summary>
public sealed class LibraryCleanTests : IDisposable {
    static readonly Dictionary<string, string> NothingOpen = [];

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-clean-");
    readonly string root;
    readonly OpenLibrary library;

    public LibraryCleanTests() {
        root = Path.Combine(folder.FullName, "Game.ptlib");
        LibraryFiles.CreateDefaultLibrary(root, "Game");
        library = OpenLibrary.Open(root);
    }

    public void Dispose() {
        library.Dispose();
        folder.Delete(true);
    }

    [Theory]
    [InlineData("""{"image":"pictures\\Rust Old.png"}""", true)]
    [InlineData("image: pictures/rust old.PNG", true)]
    [InlineData("image: pictures/Rust%20Old.png", true)]
    [InlineData("image: pictures/Rust Old.jpg", false)]
    [InlineData("image: pictures/Rust.png", false)]
    public void A_name_is_found_however_a_file_writes_it(string text, bool found) {
        var names = LibraryNameReader.Read(text);

        Assert.Equal(found, names.Any(static name => name.Name == "pictures/rust old.png"));
    }

    [Fact]
    public void Another_project_names_a_picture_by_its_folder_and_clean_takes_the_rest() {
        var crate = Project("crate");
        var barrel = Project("barrel");
        Picture(crate, "rust.png");
        Picture(crate, "moss.png");
        Picture(barrel, "rust.png");
        File.WriteAllText(Path.Combine(barrel, "barrel.vxlayers"), """{"image":"../crate/pictures/moss.png"}""");

        var cleaned = library.Care.Clean(NothingOpen);

        Assert.Equal(2, cleaned.Pictures);
        Assert.True(File.Exists(Path.Combine(crate, "pictures", "moss.png")));
        Assert.False(File.Exists(Path.Combine(crate, "pictures", "rust.png")));
        Assert.False(File.Exists(Path.Combine(barrel, "pictures", "rust.png")));
        library.Care.Restore(cleaned.Bin!);
        Assert.True(File.Exists(Path.Combine(barrel, "pictures", "rust.png")));
    }

    /// <summary>
    ///     A file the index has seen, unchanged since by its size and time, is taken at what it named then: a Clean
    ///     reads only what changed (#297). A file changed under the same size and time is the price, as for a rescan.
    /// </summary>
    [Fact]
    public void Clean_reads_again_only_the_files_that_changed() {
        var crate = Project("crate");
        Picture(crate, "rust.png");
        var stack = Path.Combine(crate, "crate.vxlayers");
        File.WriteAllText(stack, """{"image":"pictures/rust.png"}""");
        var earlier = DateTime.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(stack, earlier);
        Assert.Equal(0, library.Care.Clean(NothingOpen).Pictures);

        File.WriteAllText(stack, """{"image":"pictures/xxxx.png"}""");
        File.SetLastWriteTimeUtc(stack, earlier);
        Assert.Equal(0, library.Care.Clean(NothingOpen).Pictures);

        File.SetLastWriteTimeUtc(stack, DateTime.UtcNow.AddMinutes(-30));
        Assert.Equal(1, library.Care.Clean(NothingOpen).Pictures);
    }

    /// <summary>
    ///     The rescan reads every document anyway, so it records what each names (#301): a Clean after it reads none
    ///     of them again. The file changed under the same size and time shows the row came from the scan.
    /// </summary>
    [Fact]
    public void The_rescan_records_what_each_document_names() {
        var crate = Project("crate");
        Picture(crate, "rust.png");
        var stack = Path.Combine(crate, "crate.vxlayers");
        File.WriteAllText(stack, """{"image":"pictures/rust.png"}""");
        var earlier = DateTime.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(stack, earlier);
        library.Rescan(TestContext.Current.CancellationToken);

        File.WriteAllText(stack, """{"image":"pictures/xxxx.png"}""");
        File.SetLastWriteTimeUtc(stack, earlier);

        Assert.Equal(0, library.Care.Clean(NothingOpen).Pictures);
    }

    [Fact]
    public void Uploads_nothing_names_go_to_the_bin_and_come_back() {
        var used = Upload("aaaa", "crate.glb", DateTime.UtcNow.AddDays(-1));
        var unused = Upload("bbbb", "scan.png", DateTime.UtcNow.AddDays(-1));
        var fresh = Upload("cccc", "new.png", DateTime.UtcNow);
        var openOnly = Upload("dddd", "high.fbx", DateTime.UtcNow.AddDays(-1));
        var crate = Project("crate");
        File.AppendAllText(Path.Combine(crate, LibraryLayout.ProjectFile), $"\nsource: '{used}'\n");
        var escaped = openOnly.Replace("""\""", """\\""", StringComparison.Ordinal);
        Dictionary<string, string> open = new() {
            [Path.Combine(crate, "crate.vxlayers")] = $$"""{"high":"{{escaped}}"}"""
        };

        var listed = library.Care.UnusedUploads(open);

        Assert.Equal(["uploads/bbbb/scan.png"], listed.Files.Select(static file => file.Path));
        Assert.Equal(3, listed.Bytes);
        var cleared = library.Care.ClearUploads(open);
        Assert.Equal(1, cleared.Count);
        Assert.False(File.Exists(unused));
        Assert.False(Directory.Exists(Path.GetDirectoryName(unused)));
        Assert.True(File.Exists(used) && File.Exists(fresh) && File.Exists(openOnly));
        Assert.Equal("uploads", library.Care.Restore(cleared.Bin!));
        Assert.True(File.Exists(unused));
    }

    string Project(string name) {
        var made = Path.Combine(root, LibraryLayout.ProjectsFolder, name);
        Directory.CreateDirectory(made);
        File.WriteAllText(Path.Combine(made, LibraryLayout.ProjectFile), $"name: {name}");
        return made;
    }

    static void Picture(string project, string name) {
        Directory.CreateDirectory(Path.Combine(project, "pictures"));
        File.WriteAllBytes(Path.Combine(project, "pictures", name), [1, 2, 3]);
    }

    string Upload(string digest, string name, DateTime sent) {
        var file = Path.Combine(root, LibraryUploads.Folder, digest, name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, [1, 2, 3]);
        File.SetLastWriteTimeUtc(file, sent);
        return file;
    }
}
