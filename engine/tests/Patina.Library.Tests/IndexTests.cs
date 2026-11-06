using CsCheck;

namespace Patina.Library.Tests;

public sealed class IndexTests : IDisposable {
    readonly TestLibrary library = new();

    public void Dispose() => library.Dispose();

    /// <summary>
    ///     Random edits against a live index: every edit a person or a pull can make. The rows must match a build
    ///     from nothing, and search must agree, since its table cannot be dumped.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void A_rescan_after_any_edits_equals_a_full_rebuild() =>
        Gen.Select(Gen.Int, Gen.Int[1, 40], Gen.Int[1, 6])
            .Sample(
                static (seed, edits, rounds) => {
                    using var scratch = new TestLibrary();
                    var random = new Random(seed);
                    var model = new EditScript(scratch, random);
                    string rescanned;
                    string found;
                    using (var open = scratch.Open()) {
                        open.Rescan();
                        for (var round = 0; round < rounds; round++) {
                            for (var i = 0; i < edits; i++) {
                                model.Edit();
                            }

                            open.Rescan();
                        }

                        rescanned = scratch.DumpIndex();
                        found = Search(open);
                    }

                    scratch.DeleteCache();
                    using var rebuilt = scratch.Open();
                    rebuilt.Rescan();
                    Assert.Equal(rescanned, scratch.DumpIndex());
                    Assert.Equal(found, Search(rebuilt));
                },
                iter: 25
            );

    static string Search(OpenLibrary open) =>
        string.Join(
            '\n',
            EditScript.Words.Select(word => {
                    var projects = open.Projects(new(word, Limit: 500)).Items.Select(static p => p.Path).Order();
                    var assets = open.Assets(new(word, Limit: 500)).Items.Select(static a => a.Path).Order();
                    return $"{word}: {string.Join(',', projects)} / {string.Join(',', assets)}";
                }
            )
        );

    [Fact]
    public void Deleting_the_cache_loses_nothing() {
        var asset = TestLibrary.Asset("Rust");
        library.Write("graphs/rust.vxtexgraph", asset);
        library.Write("props/crate", TestLibrary.Project("Crate", AssetReference.Floating(asset.Id)));
        string before;
        using (var open = library.Open()) {
            open.Rescan();
            before = library.DumpIndex();
        }

        library.DeleteCache();
        using var again = library.Open();
        again.Rescan();

        Assert.Equal(before, library.DumpIndex());
    }

    [Fact]
    public void An_unchanged_file_is_not_read_again() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        using var open = library.Open();
        open.Rescan();

        var scan = open.Rescan();

        Assert.Equal(0, scan.Hashed);
    }

    [Fact]
    public void A_touched_file_with_the_same_bytes_is_hashed_and_not_parsed() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        using var open = library.Open();
        open.Rescan();

        library.Age(LibraryFiles.ProjectFile(library.Root, "props/crate"));
        var scan = open.Rescan();

        Assert.Equal((1, 0), (scan.Hashed, scan.Changed));
    }

    [Fact]
    public void An_edit_that_keeps_size_and_time_right_after_a_scan_is_still_seen() {
        var path = LibraryFiles.ProjectFile(library.Root, "props/crate");
        var document = TestLibrary.Project("AAAA");
        LibraryFiles.WriteProject(library.Root, "props/crate", document);
        var time = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(path, time);
        using var open = library.Open();
        open.Rescan();

        LibraryFiles.WriteProject(library.Root, "props/crate", document with { Name = "BBBB" });
        File.SetLastWriteTimeUtc(path, time);
        open.Rescan();

        Assert.Equal("BBBB", open.Projects(new()).Items.Single().Name);
    }

    [Fact]
    public void A_file_that_does_not_parse_stops_only_itself() {
        library.Write("props/good", TestLibrary.Project("Good"));
        library.SaveFile("projects/props/bad/project.yaml", "<<<<<<< HEAD\nname: [\n");
        using var open = library.Open();

        open.Rescan();

        Assert.Equal("Good", open.Projects(new()).Items.Single().Name);
    }

    [Fact]
    public void A_fixed_file_comes_back() {
        var document = TestLibrary.Project("Crate");
        library.SaveFile("projects/props/crate/project.yaml", "not: [valid");
        using var open = library.Open();
        open.Rescan();
        Assert.Empty(open.Projects(new()).Items);

        library.Write("props/crate", document);
        open.Rescan();

        Assert.Equal(document.Id.Value, open.Projects(new()).Items.Single().Id);
    }

    [Fact]
    public void A_damaged_index_is_replaced() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        using (var open = library.Open()) {
            open.Rescan();
        }

        var index = LibraryLayout.IndexPath(library.Root);
        File.Delete(index + "-wal");
        File.Delete(index + "-shm");
        File.WriteAllText(index, new('x', 8192));

        using var again = library.Open();
        again.Rescan();

        Assert.Single(again.Projects(new()).Items);
    }

    [Fact]
    public void A_cancelled_scan_leaves_the_last_index() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        using var open = library.Open();
        open.Rescan();
        var before = library.DumpIndex();

        library.Write("props/door", TestLibrary.Project("Door"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => open.Rescan(cancelled.Token));

        Assert.Equal(before, library.DumpIndex());
    }

    [Fact]
    public void A_folder_inside_a_project_is_not_a_project() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        library.Write("props/crate/nested", TestLibrary.Project("Nested"));
        using var open = library.Open();

        open.Rescan();

        Assert.Equal("Crate", open.Projects(new()).Items.Single().Name);
    }

    [Fact]
    public void Hidden_folders_are_not_scanned() {
        library.Write("graphs/rust.vxtexgraph", TestLibrary.Asset("Rust"));
        LibraryFiles.KeepRevision(library.Root, "graphs/rust.vxtexgraph");
        using var open = library.Open();

        var scan = open.Rescan();

        Assert.Equal(2, scan.Files);
    }

    [Fact]
    public void Opening_reads_the_index_without_scanning() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        using (var first = library.Open()) {
            first.Rescan();
        }

        library.Write("props/door", TestLibrary.Project("Door"));
        using var open = library.Open();

        Assert.Equal(1, open.Describe().ProjectCount);
    }

    [Fact]
    public void Pages_cover_everything_once_in_name_order() {
        for (var i = 0; i < 25; i++) {
            library.Write($"props/p{i:D2}", TestLibrary.Project($"Project {24 - i:D2}"));
        }

        using var open = library.Open();
        open.Rescan();

        var names = Enumerable.Range(0, 3)
            .SelectMany(page => open.Projects(new(Offset: page * 10, Limit: 10)).Items)
            .Select(static p => p.Name)
            .ToList();

        Assert.Equal(25, names.Count);
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Equal(25, open.Projects(new(Limit: 10)).Total);
    }

    [Fact]
    public void Search_matches_prefixes_of_name_tags_and_path() {
        library.Write("props/crate", TestLibrary.Project("Rusty Crate") with { Tags = ["hero"] });
        library.Write("vehicles/cart", TestLibrary.Project("Oak Cart") with { Tags = ["wood"] });
        using var open = library.Open();
        open.Rescan();

        Assert.Equal(["Rusty Crate"], Names(open, "rus"));
        Assert.Equal(["Rusty Crate"], Names(open, "hero"));
        Assert.Equal(["Oak Cart"], Names(open, "vehic"));
        Assert.Equal(["Rusty Crate"], Names(open, "RUSTY cra"));
        Assert.Empty(Names(open, "rusty cart"));
    }

    [Fact]
    public void A_name_match_outranks_a_path_match() {
        library.Write("rust/a", TestLibrary.Project("Plain Thing"));
        library.Write("other/b", TestLibrary.Project("Rust Thing"));
        using var open = library.Open();
        open.Rescan();

        Assert.Equal("Rust Thing", open.Projects(new("rust")).Items[0].Name);
    }

    [Fact]
    public void Tags_filter_listing_and_search_alike() {
        library.Write("a", TestLibrary.Project("One") with { Tags = ["hero", "wood"] });
        library.Write("b", TestLibrary.Project("Two") with { Tags = ["hero"] });
        using var open = library.Open();
        open.Rescan();

        Assert.Equal(2, open.Projects(new(Tags: ["hero"])).Total);
        Assert.Equal("One", open.Projects(new(Tags: ["hero", "wood"])).Items.Single().Name);
        Assert.Equal("One", open.Projects(new("one", ["hero"])).Items.Single().Name);
        Assert.Equal(["hero", "wood"], open.Projects(new("one")).Items[0].Tags);
    }

    [Fact]
    [Trait("Category", "Property")]
    public void No_search_text_is_a_syntax_error() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        using var open = library.Open();
        open.Rescan();

        Gen.OneOf(
                Gen.String,
                Gen.OneOfConst("\"", "AND", "OR NOT", "NEAR(", "*", "a:b", "(", "-", "^x", "'", "\0", "\"\"", "name:x")
            )
            .Sample(text => {
                    _ = open.Projects(new(text));
                    _ = open.Assets(new(text));
                }
            );
    }

    [Fact]
    public void Usage_counts_are_direct_references() {
        var used = TestLibrary.Asset("Used");
        library.Write("graphs/used.vxtexgraph", used);
        library.Write("a", TestLibrary.Project("A", AssetReference.Floating(used.Id)));
        library.Write("b", TestLibrary.Project("B", AssetReference.Floating(used.Id)));
        using var open = library.Open();
        open.Rescan();

        var asset = open.Assets(new("used")).Items.Single();

        Assert.Equal(2, asset.UsageCount);
        Assert.Equal(1, open.Projects(new("A")).Items.Single().ReferenceCount);
        Assert.Equal(Revision.Of("content"u8).Text, asset.Revision);
    }

    static string[] Names(OpenLibrary open, string search) =>
        [..open.Projects(new(search)).Items.Select(static p => p.Name)];

    /// <summary>Makes one random edit of the kinds that reach a library: saves, moves, deletes, conflicts.</summary>
    sealed class EditScript(TestLibrary library, Random random) {
        public static IReadOnlyList<string> Words { get; } = ["rust", "oak", "steel", "hero", "props", "graphs"];

        readonly Dictionary<string, ProjectDocument> projects = [];
        readonly Dictionary<string, AssetDocument> assets = [];
        int serial;

        public void Edit() {
            switch (random.Next(13)) {
                case 0 or 1 or 2:
                    AddProject();
                    break;
                case 3 or 4:
                    AddAsset();
                    break;
                case 5 when projects.Count > 0:
                    var path = Any(projects);
                    projects[path] = projects[path] with { Name = Name(), Tags = [Word()] };
                    library.Write(path, projects[path]);
                    break;
                case 6 when projects.Count > 0:
                    MoveProject();
                    break;
                case 7 when projects.Count > 0:
                    var gone = Any(projects);
                    projects.Remove(gone);
                    Directory.Delete(Path.GetDirectoryName(LibraryFiles.ProjectFile(library.Root, gone))!, true);
                    break;
                case 8 when assets.Count > 0:
                    MoveAsset();
                    break;
                case 9 when assets.Count > 0:
                    library.Age(library.SaveFile($"projects/{Any(assets)}", $"new content {serial++}"));
                    break;
                case 10 when assets.Count > 0:
                    var removed = Any(assets);
                    assets.Remove(removed);
                    library.RemoveAsset(removed);
                    File.Delete(LibraryFiles.AssetFile(library.Root, removed));
                    break;
                case 11 when projects.Count > 0:
                    library.SaveFile($"projects/{Any(projects)}/project.yaml", $"<<<<<<< {serial++}\n");
                    break;
                case 12 when projects.Count > 0:
                    library.Age(LibraryFiles.ProjectFile(library.Root, Any(projects)));
                    break;
                default:
                    AddProject();
                    break;
            }
        }

        void AddProject() {
            var path = $"{(random.Next(2) == 0 ? "props" : "rust")}/p{serial++}";
            var references = assets.Values.OrderBy(_ => random.Next())
                .Take(random.Next(3))
                .Select(a => random.Next(4) == 0
                        ? AssetReference.Pinned(a.Id, Revision.Of([(byte)random.Next(256)]))
                        : AssetReference.Floating(a.Id)
                );
            projects[path] = TestLibrary.Project(Name(), [..references]) with { Tags = [Word()] };
            library.Write(path, projects[path]);
        }

        void AddAsset() {
            var path = $"graphs/a{serial++}.vxtexgraph";
            var dependency = assets.Values.OrderBy(_ => random.Next()).Take(random.Next(2));
            assets[path] = TestLibrary.Asset(Name(), [..dependency.Select(static a => AssetReference.Floating(a.Id))]);
            library.Write(path, assets[path], Name());
        }

        void MoveProject() {
            var from = Any(projects);
            var to = $"moved/p{serial++}";
            library.MoveProject(from, to);
            projects[to] = projects[from];
            projects.Remove(from);
        }

        void MoveAsset() {
            var from = Any(assets);
            var to = $"brushes/a{serial++}.vxtexgraph";
            library.MoveAsset(from, to);
            assets[to] = assets[from];
            assets.Remove(from);
        }

        string Name() => $"{Word()} {serial++}";

        string Word() => Words[random.Next(Words.Count)];

        string Any<T>(Dictionary<string, T> items) => items.Keys.Order().ElementAt(random.Next(items.Count));
    }
}
