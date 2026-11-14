using Patina.Contracts;
using Patina.Export.Builds;

namespace Patina.Export.Tests;

/// <summary>
///     Two builds compared from their reports alone (#217): the second has written over the first's files, and the
///     digests each kept are what says which changed.
/// </summary>
public sealed class BuildComparisonsTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-compare-");

    public void Dispose() => folder.Delete(true);

    static readonly ProjectIdentity Crate = new(Guid.NewGuid(), "Crate", "crate");
    static readonly ProjectIdentity Barrel = new(Guid.NewGuid(), "Barrel", "barrel");

    string Exports => Path.Combine(folder.FullName, "out");

    /// <summary>Writes the export folder's files as given, then a build's report of them.</summary>
    string Build(string name, ProjectIdentity project, string outputKey, params (string File, string Text)[] files) {
        Directory.CreateDirectory(Exports);
        foreach (var (file, text) in files) {
            File.WriteAllText(Path.Combine(Exports, file), text);
        }

        var reports = Path.Combine(folder.FullName, "reports", name);
        var journal = new BuildJournal(reports, name, DateTimeOffset.UnixEpoch);
        journal.Ended("o", project, new OutputReport("Main", "graph", outputKey, BuildStatuses.Built, 1, null));
        string[] paths = [..files.Select(f => Path.Combine(Exports, f.File))];
        journal.Ended(
            "e",
            project,
            new ExportReport("gltf", BuildStatuses.Built, Exports, paths, [], [], null) {
                Digests = BuildComparisons.Digests(paths)
            }
        );
        journal.Finish(0, 0, null, DateTimeOffset.UnixEpoch);
        return reports;
    }

    [Fact]
    public void A_file_rewritten_with_other_bytes_is_changed_and_one_rewritten_the_same_is_not() {
        var before = Build("a", Crate, "k1", ("albedo.png", "red"), ("normal.png", "flat"), ("old.png", "x"));
        var after = Build("b", Crate, "k2", ("albedo.png", "blue"), ("normal.png", "flat"), ("new.png", "y"));

        var comparison = BuildComparisons.Compare(before, after);

        var project = Assert.Single(comparison.Projects);
        Assert.Equal(BuildComparisons.Touched.Both, project.Touched);
        Assert.Equal(
            [
                ("albedo.png", BuildChanges.Changed), ("new.png", BuildChanges.Added),
                ("normal.png", BuildChanges.Same), ("old.png", BuildChanges.Removed)
            ],
            project.Files.Select(static f => (f.File, f.Change))
        );
        Assert.Equal(new("Main", "k1", "k2", BuildChanges.Changed), Assert.Single(project.Outputs));
        Assert.Equal((3, 1), (comparison.ChangedFiles, comparison.SameFiles));
    }

    /// <summary>
    ///     A build that finds a project fresh reports it as fresh, with the digests its delivery kept, so a comparison
    ///     says its files are the same rather than that the later build removed them (#280). Fresh counts in no total,
    ///     and never stands over how a step of the same build ended.
    /// </summary>
    [Fact]
    public void A_fresh_project_is_in_the_report_and_compares_as_the_same() {
        var before = Build("a", Crate, "k1", ("albedo.png", "red"));
        var reports = Path.Combine(folder.FullName, "reports", "b");
        var journal = new BuildJournal(reports, "b", DateTimeOffset.UnixEpoch);
        journal.Ended("o", Barrel, new OutputReport("Main", "graph", "k9", BuildStatuses.Built, 1, null));
        journal.Fresh(Barrel, new("Main", "graph", "k9", BuildStatuses.Fresh, 0, null), null);
        string[] files = [Path.Combine(Exports, "albedo.png")];
        journal.Fresh(Crate, new("Main", "graph", "k1", BuildStatuses.Fresh, 0, null), null);
        journal.Fresh(
            Crate,
            null,
            new ExportReport("gltf", BuildStatuses.Fresh, Exports, files, [], [], null) {
                Digests = BuildComparisons.Digests(files)
            }
        );

        var totals = journal.Finish(0, 0, null, DateTimeOffset.UnixEpoch);

        Assert.Equal((1, 1, 0L), (totals.Projects, totals.Built, totals.Files));
        var barrel = BuildJournal.ReadProjects(reports, 0, 10).Items.Single(static p => p.Path == "barrel");
        Assert.Equal(BuildStatuses.Built, Assert.Single(barrel.Outputs).Status);
        var crate = BuildComparisons.Compare(before, reports).Projects.Single(static p => p.Path == "crate");
        Assert.Equal(BuildComparisons.Touched.Both, crate.Touched);
        Assert.Equal(BuildChanges.Same, Assert.Single(crate.Files).Change);
        Assert.Equal(BuildChanges.Same, Assert.Single(crate.Outputs).Change);
    }

    [Fact]
    public void A_project_only_one_build_touched_says_so_and_a_report_without_digests_is_unknown() {
        var before = Build("a", Crate, "k1", ("albedo.png", "red"));
        var after = Build("b", Barrel, "k1", ("barrel.png", "oak"));
        var stale = Path.Combine(after, "projects");
        foreach (var file in Directory.EnumerateFiles(stale)) {
            File.WriteAllText(
                file,
                File.ReadAllText(file).Replace("\"digests\"", "\"ignored\"", StringComparison.Ordinal)
            );
        }

        var again = Build("c", Barrel, "k1", ("barrel.png", "oak"));

        var comparison = BuildComparisons.Compare(before, after);
        Assert.Equal(
            [("barrel", BuildComparisons.Touched.After), ("crate", BuildComparisons.Touched.Before)],
            comparison.Projects.Select(static p => (p.Path, p.Touched))
        );
        var unknown = BuildComparisons.Compare(after, again).Projects.Single().Files.Single();
        Assert.Equal(BuildChanges.Unknown, unknown.Change);
    }
}
