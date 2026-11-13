namespace Patina.Library.Tests;

public sealed class SnapshotTests : IDisposable {
    const string Stack = """
                         name: Crate
                         sets:
                           - name: Wood
                             layers:
                               - id: a
                                 paint: paint/a.vxpaint
                               - id: b
                                 paint: paint/b.vxpaint
                         """;

    readonly TestLibrary library = new();

    public void Dispose() => library.Dispose();

    string Document => LibraryLayout.Full(library.Root, "projects/crate/crate.vxlayers");

    string Paint(string name) => LibraryLayout.Full(library.Root, $"projects/crate/paint/{name}.vxpaint");

    static void Write(string file, string text) => LibraryFiles.Replace(file, text);

    [Fact]
    public void A_restore_puts_back_the_document_and_its_paint_and_takes_away_paint_painted_since() {
        Write(Document, Stack);
        Write(Paint("a"), "first strokes");
        var taken = Snapshots.Take(library.Root, Document, "Before rust");

        Write(Document, Stack + "\n# an edit\n");
        Write(Paint("a"), "later strokes");
        Write(Paint("b"), "a layer painted only later");
        Snapshots.Restore(library.Root, Document, taken.Id);

        Assert.Equal(Stack, File.ReadAllText(Document));
        Assert.Equal("first strokes", File.ReadAllText(Paint("a")));
        Assert.False(File.Exists(Paint("b")));
    }

    [Fact]
    public void A_restore_keeps_how_the_document_stood_so_it_can_be_taken_back() {
        Write(Document, Stack);
        var taken = Snapshots.Take(library.Root, Document, "Clean");
        Write(Document, Stack + "\n# kept\n");
        Write(Paint("b"), "later");

        var before = Snapshots.Restore(library.Root, Document, taken.Id);
        Snapshots.Restore(library.Root, Document, before.Id);

        Assert.Equal("Before restoring Clean", before.Name);
        Assert.Equal(Stack + "\n# kept\n", File.ReadAllText(Document));
        Assert.Equal("later", File.ReadAllText(Paint("b")));
    }

    [Fact]
    public void Snapshots_list_newest_first_by_name_and_survive_the_cache_being_deleted() {
        Write(Document, Stack);
        Write(Paint("a"), "strokes");
        Snapshots.Take(library.Root, Document, "  One\n");
        Snapshots.Take(library.Root, Document, null);
        var cache = Path.Combine(library.Root, LibraryLayout.CacheFolder);
        if (Directory.Exists(cache)) {
            Directory.Delete(cache, true);
        }

        var list = Snapshots.List(library.Root, Document);

        Assert.Equal("projects/crate/crate.vxlayers", list.Path);
        Assert.Equal(["Snapshot 2", "One"], list.Items.Select(static item => item.Name));
        Assert.All(list.Items, static item => Assert.Equal(2, item.Files));
        Assert.Equal(Stack.Length + "strokes".Length, list.Items[0].Bytes);
    }

    [Fact]
    public void Forgetting_a_snapshot_leaves_the_document_and_the_others() {
        Write(Document, Stack);
        var first = Snapshots.Take(library.Root, Document, "First");
        Snapshots.Take(library.Root, Document, "Second");

        Snapshots.Forget(Document, first.Id);

        Assert.Equal("Second", Assert.Single(Snapshots.List(library.Root, Document).Items).Name);
        Assert.Throws<FileNotFoundException>(() => Snapshots.Forget(Document, first.Id));
        Assert.Equal(Stack, File.ReadAllText(Document));
    }

    [Fact]
    public void Paint_named_outside_the_documents_folder_is_neither_kept_nor_written() {
        Write(Document, Stack.Replace("paint/b.vxpaint", "../../outside.vxpaint", StringComparison.Ordinal));
        var outside = LibraryLayout.Full(library.Root, "outside.vxpaint");
        Write(outside, "not the document's");

        var taken = Snapshots.Take(library.Root, Document, "Guarded");
        File.Delete(outside);
        Snapshots.Restore(library.Root, Document, taken.Id);

        Assert.Equal(1, taken.Files);
        Assert.False(File.Exists(outside));
    }

    [Fact]
    public void A_snapshot_whose_kept_bytes_are_gone_is_refused_before_anything_is_written() {
        Write(Document, Stack);
        var taken = Snapshots.Take(library.Root, Document, "Lost");
        Directory.Delete(Path.Combine(library.Root, LibraryLayout.RevisionsFolder), true);
        Write(Document, "name: Changed\n");

        Assert.Throws<ProjectRefusedException>(() => Snapshots.Restore(library.Root, Document, taken.Id));
        Assert.Equal("name: Changed\n", File.ReadAllText(Document));
        Assert.Single(Snapshots.List(library.Root, Document).Items);
    }
}
