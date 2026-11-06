using CsCheck;
using Patina.Bench;

namespace Patina.Library.Tests;

public sealed class CheckTests : IDisposable {
    const string Rust = "graphs/rust.vxtexgraph";

    readonly TestLibrary library = new();

    public void Dispose() => library.Dispose();

    CheckReport Check() {
        using var open = library.Open();
        return open.Check();
    }

    AssetDocument WriteRust() {
        var asset = TestLibrary.Asset("Rust");
        library.Write(Rust, asset);
        return asset;
    }

    static Finding[] Of(CheckReport report, FindingKind kind) => [..report.Findings.Where(f => f.Kind == kind)];

    [Fact]
    public void A_healthy_library_has_no_findings() {
        var asset = WriteRust();
        library.Write("props/crate", TestLibrary.Project("Crate", AssetReference.Floating(asset.Id)));

        var report = Check();

        Assert.Empty(report.Findings);
        Assert.Equal((2, 1), (report.Projects, report.Assets)); // the document's own project counts
        Assert.False(report.HasErrors);
    }

    [Fact]
    public void A_reference_to_nothing_is_missing() {
        var ghost = AssetId.New();
        library.Write("props/crate", TestLibrary.Project("Crate", AssetReference.Floating(ghost)));

        var finding = Assert.Single(Of(Check(), FindingKind.MissingReference));

        Assert.Equal("projects/props/crate", finding.Path);
        Assert.Equal(ghost.ToString(), finding.Detail);
        Assert.True(finding.IsError);
    }

    [Fact]
    public void A_dependency_on_nothing_is_missing() {
        library.Write(Rust, TestLibrary.Asset("Rust", AssetReference.Floating(AssetId.New())));

        Assert.Equal($"projects/{Rust}", Assert.Single(Of(Check(), FindingKind.MissingReference)).Path);
    }

    [Fact]
    public void An_asset_nothing_reaches_is_no_fault() {
        var graph = WriteRust();
        var material = TestLibrary.Asset("Material", AssetReference.Floating(graph.Id));
        library.Write("smart-materials/m.vxtexgraph", material);
        var lonely = TestLibrary.Asset("Lonely");
        library.Write("graphs/lonely.vxtexgraph", lonely);
        library.Write("props/crate", TestLibrary.Project("Crate", AssetReference.Floating(material.Id)));

        var report = Check();

        Assert.False(report.HasErrors);
    }

    [Fact]
    public void A_broken_document_is_an_error_with_its_path() {
        library.SaveFile("projects/props/bad/project.yaml", "<<<<<<< HEAD\n");

        var report = Check();

        Assert.Equal("projects/props/bad/project.yaml", Assert.Single(Of(report, FindingKind.Unreadable)).Path);
        Assert.True(report.HasErrors);
    }

    [Fact]
    public void A_newer_document_is_named_as_such() {
        library.SaveFile("projects/a/project.yaml", $"schema: 99\nid: {ProjectId.New()}\nname: x\n");

        Assert.Single(Of(Check(), FindingKind.NewerSchema));
    }

    [Fact]
    public void Unknown_keys_are_drift_and_the_document_still_loads() {
        library.SaveFile("projects/a/project.yaml", $"schema: 1\nid: {ProjectId.New()}\nname: x\nextra: 1\n");

        var report = Check();

        Assert.Contains("extra", Assert.Single(Of(report, FindingKind.SchemaDrift)).Detail, StringComparison.Ordinal);
        Assert.Equal(1, report.Projects);
        Assert.False(report.HasErrors);
    }

    [Fact]
    public void Drift_in_library_yaml_is_reported_too() {
        var file = Path.Combine(library.Root, LibraryLayout.LibraryFile);
        File.AppendAllText(file, "extra: 1\n");

        Assert.Equal(LibraryLayout.LibraryFile, Assert.Single(Of(Check(), FindingKind.SchemaDrift)).Path);
    }

    [Fact]
    public void A_copied_folder_shows_as_a_duplicate_id() {
        var project = TestLibrary.Project("Crate");
        library.Write("a", project);
        library.Write("b", project);

        Assert.Equal(2, Of(Check(), FindingKind.DuplicateId).Length);
    }

    [Fact]
    public void A_document_its_project_does_not_list_cannot_be_referenced() {
        library.Write("props/crate", TestLibrary.Project("Crate"));
        library.SaveFile("projects/props/crate/scratch.vxtexgraph", "name: Scratch");

        Assert.Equal(
            "projects/props/crate/scratch.vxtexgraph",
            Assert.Single(Of(Check(), FindingKind.UnidentifiedFile)).Path
        );
    }

    [Fact]
    public void A_listed_document_with_no_file_is_an_error() {
        var asset = WriteRust();
        library.Write("props/crate", TestLibrary.Project("Crate", AssetReference.Floating(asset.Id)));
        File.Delete(LibraryFiles.AssetFile(library.Root, Rust));

        Assert.Equal($"projects/{Rust}", Assert.Single(Of(Check(), FindingKind.MissingAssetFile)).Path);
    }

    [Fact]
    public void A_pin_to_kept_bytes_is_healthy_after_the_asset_moves_on() {
        var asset = WriteRust();
        var pinned = LibraryFiles.KeepRevision(library.Root, Rust);
        library.Write("props/crate", TestLibrary.Project("Crate", AssetReference.Pinned(asset.Id, pinned)));
        library.Write(Rust, asset, "newer bytes");

        Assert.Empty(Check().Findings);
    }

    [Fact]
    public void A_pin_to_bytes_nobody_kept_is_an_error() {
        var asset = WriteRust();
        var lost = Revision.Of("long gone"u8);
        library.Write("props/crate", TestLibrary.Project("Crate", AssetReference.Pinned(asset.Id, lost)));

        Assert.Contains(
            lost.Text,
            Assert.Single(Of(Check(), FindingKind.MissingRevision)).Detail,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void A_cycle_is_an_error() {
        var a = TestLibrary.Asset("A");
        var b = TestLibrary.Asset("B", AssetReference.Floating(a.Id));
        library.Write("graphs/a.vxtexgraph", a with { Dependencies = [AssetReference.Floating(b.Id)] });
        library.Write("graphs/b.vxtexgraph", b);

        Assert.Single(Of(Check(), FindingKind.DependencyCycle));
    }

    (ProjectDocument Project, OutputDeclaration Output) WriteBuilt(AssetDocument asset) {
        var output = new OutputDeclaration(
            "ao",
            "bake.ao",
            ["low.glb"],
            [asset.Id],
            new Dictionary<string, string>()
        );
        var project = TestLibrary.Project("Crate", AssetReference.Floating(asset.Id)) with { Outputs = [output] };
        library.Write("props/crate", project);
        library.SaveFile("projects/props/crate/low.glb", "mesh");
        return (project, output);
    }

    void Build() {
        var key = Assert.Single(Of(Check(), FindingKind.StaleOutput)).Detail.Split(' ')[^1];
        new ObjectStore(library.Root).Put(new(key), "built"u8);
    }

    [Fact]
    public void An_output_is_stale_until_it_is_built_and_then_only_when_its_inputs_change() {
        var asset = WriteRust();
        WriteBuilt(asset);

        Build();
        Assert.Empty(Check().Findings);

        library.Write(Rust, asset, "new bytes");
        Assert.Single(Of(Check(), FindingKind.StaleOutput));

        library.Write(Rust, asset);
        Assert.Empty(Check().Findings);
    }

    [Fact]
    public void A_change_outside_an_outputs_inputs_stales_nothing() {
        var asset = WriteRust();
        var (project, _) = WriteBuilt(asset);
        Build();

        library.Write("props/crate", project with { Name = "Renamed", Tags = ["new"] });
        library.SaveFile("projects/props/crate/notes.txt", "unrelated");

        Assert.Empty(Check().Findings);
    }

    [Fact]
    public void A_changed_input_file_stales_its_output() {
        WriteBuilt(WriteRust());
        Build();

        library.SaveFile("projects/props/crate/low.glb", "other");

        Assert.Single(Of(Check(), FindingKind.StaleOutput));
    }

    [Fact]
    public void A_tool_update_stales_its_outputs() {
        WriteBuilt(WriteRust());
        Build();
        using var open = library.Open();

        var report = open.Check(new(new Dictionary<string, string> { ["bake.ao"] = "next" }, "0"));

        Assert.Single(Of(report, FindingKind.StaleOutput));
    }

    [Fact]
    public void A_missing_input_is_named() {
        WriteBuilt(WriteRust());
        File.Delete(LibraryLayout.Full(library.Root, "projects/props/crate/low.glb"));

        var report = Check();

        Assert.Contains(
            "low.glb",
            Assert.Single(Of(report, FindingKind.MissingInput)).Detail,
            StringComparison.Ordinal
        );
        Assert.Single(Of(report, FindingKind.UnresolvedOutput));
    }

    [Fact]
    public void Two_runs_report_the_same() {
        library.SaveFile("shared/alphas/a.png", "a");
        library.SaveFile("shared/alphas/b.png", "b");
        library.Write("x", TestLibrary.Project("X", AssetReference.Floating(AssetId.New())));

        Assert.Equal(Check().Findings, Check().Findings);
    }

    /// <summary>The plan's rule: moving or renaming a thousand files breaks nothing, and rebuilds nothing.</summary>
    [Fact]
    [Trait("Category", "Property")]
    public void Moving_projects_and_assets_breaks_no_reference() =>
        Gen.Int.Sample(
            static seed => {
                var random = new Random(seed);
                using var scratch = new TestLibrary();
                var generated = LibraryGenerator.Generate(Path.Combine(scratch.Folder, "G"), new(40, seed));
                var root = Path.Combine(scratch.Folder, "G");

                // Moved documents go to a project of their own, which has to exist in both pictures.
                LibraryFiles.WriteProject(root, "renamed", new(ProjectId.New(), "Renamed", [], [], []));
                using var open = OpenLibrary.Open(root);
                var before = Summarise(open, generated);

                foreach (var path in generated.Projects.Where(_ => random.Next(2) == 0)) {
                    var from = Path.GetDirectoryName(LibraryFiles.ProjectFile(root, path))!;
                    var to = Path.Combine(root, LibraryLayout.ProjectsFolder, "moved", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    Directory.Move(from, to);
                }

                foreach (var path in generated.Assets.Where(_ => random.Next(2) == 0)) {
                    var from = LibraryFiles.AssetFile(root, path);
                    var name = Guid.NewGuid().ToString("N") + Path.GetExtension(path);
                    var item = TestLibrary.Unlist(root, path);
                    LibraryFiles.WriteDocument(
                        root,
                        "renamed",
                        File.ReadAllBytes(from),
                        item with { File = name }
                    );
                    File.Delete(from);
                }

                Assert.Equal(before, Summarise(open, generated));
            },
            iter: 12
        );

    /// <summary>What must not move with the files: health, usage, impact, and the keys of every output.</summary>
    static string Summarise(OpenLibrary open, GeneratedLibrary generated) {
        var report = open.Check();
        Assert.False(report.HasErrors, string.Join('\n', report.Findings.Where(static f => f.IsError)));

        var impact = open.Impact(generated.Popular, 0, 500)!;
        var usage = open.Assets(new(Limit: 500))
            .Items.OrderBy(static a => a.Id)
            .Select(static a => $"{a.Id} {a.UsageCount}");
        var keys = report.Findings.Where(static f => f.Kind == FindingKind.StaleOutput)
            .Select(static f => f.Detail.Split(' ')[^1])
            .Order(StringComparer.Ordinal);
        return string.Join(
            '\n',
            [
                $"{report.Projects} {report.Assets} {report.Outputs}",
                $"{impact.Projects} {impact.Pinned}",
                ..impact.Items.OrderBy(static p => p.Id).Select(static p => $"{p.Id} {p.Pinned}"),
                ..usage,
                ..keys
            ]
        );
    }
}
