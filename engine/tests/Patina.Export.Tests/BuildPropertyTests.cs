using CsCheck;
using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Library;
using System.Text;

namespace Patina.Export.Tests;

/// <summary>
///     A build over a real library's files and keys, with an executor that writes small files in place of
///     pixels.
/// </summary>
public sealed class BuildPropertyTests {
    static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>An edit: one shared file, or one project file, or nothing a key reads.</summary>
    /// <param name="Asset">The asset to rewrite, or -1.</param>
    /// <param name="Project">The project whose input to rewrite, or -1.</param>
    /// <param name="Text">What to write.</param>
    sealed record Edit(int Asset, int Project, int Text);

    static readonly Gen<Edit> Edits =
        Gen.Select(Gen.Int[-1, 3], Gen.Int[-1, 5], Gen.Int[0, 3]).Select(static (a, p, t) => new Edit(a, p, t));

    /// <summary>Which asset each of six projects reads, of four; two read the same one with the same input.</summary>
    static readonly Gen<int[]> Wiring = Gen.Int[0, 3].Array[6];

    sealed class Fixture : IDisposable {
        readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-build-");

        public Fixture(int[] wiring) {
            Directory.CreateDirectory(Root);
            LibraryFiles.CreateLibrary(Root, new(LibraryId.From(Guid.NewGuid()), "L", 1024, [], "pbr", ["unreal"]));
            for (var a = 0; a < 4; a++) {
                var id = AssetId.From(Guid.NewGuid());
                Assets.Add((id, $"graphs/a{a}.vxtexgraph"));
                // Masks, which their project reads and does not ship: its own exports are not what these tests count.
                LibraryFiles.WriteDocument(
                    Root,
                    "graphs",
                    Encoding.UTF8.GetBytes($"asset {a}"),
                    new(id, DocumentKinds.Graph, $"a{a}.vxtexgraph", $"a{a}", Role: GraphRoles.Mask)
                );
            }

            for (var p = 0; p < wiring.Length; p++) {
                var asset = Assets[wiring[p]].Id;
                var reference = p == 5
                    ? AssetReference.Pinned(asset, Revision.Of(Encoding.UTF8.GetBytes($"asset {wiring[p]}")))
                    : AssetReference.Floating(asset);
                Projects.Add(ProjectId.From(Guid.NewGuid()));
                var path = $"p{p}";
                LibraryFiles.WriteProject(
                    Root,
                    path,
                    new(
                        Projects[p],
                        $"Project {p}",
                        [],
                        [reference],
                        [
                            new OutputDeclaration(
                                "maps",
                                "texture-graph",
                                ["input.txt"],
                                [asset],
                                new Dictionary<string, string>()
                            )
                        ]
                    )
                );
                File.WriteAllText(Input(p), p < 2 ? "shared input" : $"input {p}");
                File.WriteAllText(Path.Combine(ProjectFolder(p), "notes.txt"), "nothing reads this");
            }
        }

        public string Root => Path.Combine(folder.FullName, "L.ptlib");

        public List<(AssetId Id, string Path)> Assets { get; } = [];

        public List<ProjectId> Projects { get; } = [];

        public string ProjectFolder(int project) => Path.Combine(Root, "projects", $"p{project}");

        public string Input(int project) => Path.Combine(ProjectFolder(project), "input.txt");

        public void Apply(Edit edit) {
            if (edit.Asset >= 0) {
                File.WriteAllText(LibraryFiles.AssetFile(Root, Assets[edit.Asset].Path), $"edit {edit.Text}");
            }

            if (edit.Project >= 0) {
                File.WriteAllText(Input(edit.Project), $"edit {edit.Text}");
            }

            // Written with the same size in the same instant must still be seen, so nothing here sleeps; the index
            // trusts a stamp only once a file is two seconds old, and these are not.
        }

        public void Dispose() => folder.Delete(true);
    }

    sealed class FakeExecutor : IBuildExecutor {
        public List<string> Outputs { get; } = [];

        public int Exports { get; private set; }

        public Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) {
            Outputs.Add(work.Folder);
            Directory.CreateDirectory(work.Folder);
            var height = new Vixen.Core.Imaging.FloatImage(2, 2, 1, [0f, 0.25f, 0.5f, 1f]);
            var map = MapPictures.Write(work.Folder, "roughness", height, true, null);
            return Task.FromResult<BuildManifest>(new(work.Tool, [map], [], []));
        }

        public Task<ExportManifest> ExportAsync(ExportStep work, Action<double, string>? progress, CancellationToken cancellationToken) {
            Exports++;
            return Task.FromResult(ExportSteps.Run(work, progress, cancellationToken));
        }
    }

    static async Task<LibraryReport> Build(OpenLibrary library, IBuildExecutor executor, string name = "build") {
        var journal = new BuildJournal(
            Path.Combine(LibraryLayout.ReportsPath(library.RootPath), $"{name}-{Guid.NewGuid():N}"),
            name,
            DateTimeOffset.UnixEpoch
        );
        return await new BuildPipeline(library, executor, ToolCatalog.Default).RunAsync(
            journal,
            new(),
            null,
            null,
            TimeProvider.System,
            Token
        );
    }

    static Dictionary<ProjectId, string?> Keys(OpenLibrary library) =>
        library.ScanOutputs(OutputSelection.All)
            .Projects.ToDictionary(static p => p.Id, static p => p.Outputs.Single().Key?.Hex);

    /// <summary>
    ///     The exit of P6 as a property. After any edit, the projects a build touches are exactly those whose keys
    ///     moved, each new key runs once however many projects share it, and building again does nothing at all.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void A_build_touches_exactly_what_an_edit_moved_and_a_second_does_nothing() =>
        Gen.Select(Wiring, Edits.Array[1, 3])
            .Sample(
                static (wiring, edits) => {
                    using var fixture = new Fixture(wiring);
                    using var library = OpenLibrary.Open(fixture.Root);
                    var first = new FakeExecutor();
                    var before = Keys(library);
                    var built = Build(library, first).GetAwaiter().GetResult();
                    Assert.Equal(0, built.Failed);
                    Assert.Equal(before.Values.Distinct().Count(), first.Outputs.Count);

                    foreach (var edit in edits) {
                        fixture.Apply(edit);
                    }

                    var after = Keys(library);
                    var moved = after.Where(key => key.Value != before[key.Key])
                        .Select(static key => key.Key)
                        .ToHashSet();
                    var second = new FakeExecutor();
                    var report = Build(library, second, "second").GetAwaiter().GetResult();

                    var reports = BuildJournal.ReadProjects(report.Folder, 0, 100);
                    var touched = reports.Items.Select(static p => ProjectId.From(p.Id)).ToHashSet();
                    Assert.True(moved.SetEquals(touched), $"moved {moved.Count}, touched {touched.Count}");
                    Assert.Equal(moved.Select(id => after[id]).Distinct().Count(), second.Outputs.Count);

                    var third = new FakeExecutor();
                    var nothing = Build(library, third, "third").GetAwaiter().GetResult();
                    Assert.Empty(third.Outputs);
                    Assert.Equal(0, third.Exports);
                    Assert.Equal(0, nothing.Projects);
                    Assert.Equal(0, nothing.Built + nothing.Reused + nothing.Exported);
                },
                iter: 12
            );

    [Fact]
    public async Task Check_agrees_that_a_build_left_nothing_stale() {
        using var fixture = new Fixture([0, 1, 2, 3, 0, 1]);
        using var library = OpenLibrary.Open(fixture.Root);

        Assert.Contains(library.Check(null, Token).Findings, static f => f.Kind == FindingKind.StaleOutput);
        await Build(library, new FakeExecutor());

        Assert.DoesNotContain(library.Check(null, Token).Findings, static f => f.Kind == FindingKind.StaleOutput);
    }

    [Fact]
    public async Task A_preview_says_what_the_build_then_does() {
        using var fixture = new Fixture([0, 0, 1, 2, 3, 0]);
        using var library = OpenLibrary.Open(fixture.Root);
        await Build(library, new FakeExecutor());

        File.WriteAllText(LibraryFiles.AssetFile(fixture.Root, fixture.Assets[0].Path), "changed");
        var request = new BuildRequest { Asset = fixture.Assets[0].Id.Value };
        var preview = BuildPipeline.Plan(library, request, ToolCatalog.Default, Token).Preview(0, 50);

        // Projects 0 and 1 read asset 0 with one input, so one key: two to build, and one is shared. 5 is pinned.
        Assert.Equal(2, preview.Projects);
        Assert.Equal(2, preview.Rebuilds);
        Assert.Equal(1, preview.Shared);
        Assert.Equal(1, preview.Pinned);
        Assert.Equal(2, preview.Exports);

        var executor = new FakeExecutor();
        await Build(library, executor);
        Assert.Single(executor.Outputs);
        Assert.Equal(0, BuildPipeline.Plan(library, request, ToolCatalog.Default, Token).Preview(0, 50).Projects);
    }

    [Fact]
    public async Task A_file_nothing_reads_stales_nothing() {
        using var fixture = new Fixture([0, 1, 2, 3, 0, 1]);
        using var library = OpenLibrary.Open(fixture.Root);
        await Build(library, new FakeExecutor());

        File.WriteAllText(Path.Combine(fixture.ProjectFolder(2), "notes.txt"), "edited");

        var executor = new FakeExecutor();
        var report = await Build(library, executor);
        Assert.Empty(executor.Outputs);
        Assert.Equal(0, report.Projects);
    }

    [Fact]
    public async Task A_deleted_export_is_delivered_again_without_encoding() {
        using var fixture = new Fixture([0, 1, 2, 3, 0, 1]);
        using var library = OpenLibrary.Open(fixture.Root);
        var report = await Build(library, new FakeExecutor());
        var project = BuildJournal.ReadProjects(report.Folder, 0, 100).Items[0];
        var file = project.Exports[0].Files[0];
        Assert.True(File.Exists(file));

        File.Delete(file);
        var executor = new FakeExecutor();
        var again = await Build(library, executor);

        Assert.True(File.Exists(file));
        Assert.Equal(0, executor.Exports);
        Assert.Equal(1, again.Exported);
    }

    [Fact]
    public async Task A_step_that_kills_workers_is_reported_and_the_build_goes_on() {
        using var fixture = new Fixture([0, 1, 2, 3, 0, 1]);
        using var library = OpenLibrary.Open(fixture.Root);
        var folder = Path.Combine(LibraryLayout.ReportsPath(library.RootPath), "crashing");
        var executor = new CrashingExecutor(new FakeExecutor());

        LibraryReport? report = null;
        var lost = 0;
        while (report is null) {
            try {
                report = await new BuildPipeline(library, executor, ToolCatalog.Default).RunAsync(
                    new(folder, "crashing", DateTimeOffset.UnixEpoch),
                    new(),
                    null,
                    null,
                    TimeProvider.System,
                    Token
                );
            } catch (BuildExecutorLostException) {
                lost++;
            }
        }

        Assert.Equal(BuildJournal.MaxStarts, lost);
        Assert.Equal(1, report.Failed - 1); // The output, and the export that cannot run without it.
        Assert.Equal(5, report.Built);
    }

    /// <summary>However a build stopped, what it finished is reported, and a person reads why.</summary>
    [Fact]
    public async Task A_stopped_build_reports_what_it_finished() {
        using var fixture = new Fixture([0, 1, 2, 3, 0, 1]);
        using var library = OpenLibrary.Open(fixture.Root);
        using var stop = new CancellationTokenSource();
        var folder = Path.Combine(LibraryLayout.ReportsPath(library.RootPath), "stopped");
        var executor = new StoppingExecutor(new FakeExecutor(), stop);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new BuildPipeline(library, executor, ToolCatalog.Default).RunAsync(
                new(folder, "stopped", DateTimeOffset.UnixEpoch),
                new(),
                null,
                null,
                TimeProvider.System,
                stop.Token
            )
        );

        var report = BuildJournal.ReadLibrary(folder);
        Assert.NotNull(report);
        Assert.Null(report.Nothing);
        Assert.Equal(2, report.Built);
        Assert.Equal(2, BuildJournal.ReadProjects(folder, 0, 100).Total);
    }

    sealed class StoppingExecutor(IBuildExecutor inner, CancellationTokenSource stop) : IBuildExecutor {
        int outputs;

        public async Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) {
            var built = await inner.OutputAsync(work, cancellationToken);
            if (++outputs == 2) {
                await stop.CancelAsync();
            }

            return built;
        }

        public Task<ExportManifest> ExportAsync(ExportStep work, Action<double, string>? progress, CancellationToken cancellationToken) =>
            inner.ExportAsync(work, progress, cancellationToken);
    }

    /// <summary>Loses the Worker whenever one key comes round, however often it is tried.</summary>
    sealed class CrashingExecutor(IBuildExecutor inner) : IBuildExecutor {
        string? victim;

        public Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) {
            victim ??= work.Folder;
            return victim == work.Folder
                ? throw new BuildExecutorLostException("killed")
                : inner.OutputAsync(work, cancellationToken);
        }

        public Task<ExportManifest> ExportAsync(ExportStep work, Action<double, string>? progress, CancellationToken cancellationToken) =>
            inner.ExportAsync(work, progress, cancellationToken);
    }
}
