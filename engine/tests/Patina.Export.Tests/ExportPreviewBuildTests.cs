using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Export.Presets;
using Patina.Library;
using System.Globalization;

namespace Patina.Export.Tests;

/// <summary>
///     Project presets in a build, and the compression preview over what a build left: a real library's files and
///     keys, with an executor that writes a small map in place of a GPU.
/// </summary>
public sealed class ExportPreviewBuildTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-preview-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    string Root => Path.Combine(folder.FullName, "L.ptlib");

    public void Dispose() => folder.Delete(true);

    /// <summary>Two projects, each with one graph output that a build makes a roughness map of.</summary>
    OpenLibrary Library() {
        LibraryFiles.CreateLibrary(Root, new(LibraryId.From(Guid.NewGuid()), "L", 1024, [], "pbr", ["vixen"]));
        foreach (var name in new[] { "crate", "barrel" }) {
            LibraryFiles.WriteProject(
                Root,
                name,
                new(
                    ProjectId.From(Guid.NewGuid()),
                    name,
                    [],
                    [],
                    [
                        new OutputDeclaration(
                            "maps",
                            "texture-graph",
                            ["input.txt"],
                            [],
                            new Dictionary<string, string>()
                        )
                    ]
                )
            );
            File.WriteAllText(Path.Combine(Root, "projects", name, "input.txt"), name);
        }

        return OpenLibrary.Open(Root);
    }

    /// <summary>Writes a roughness map 32 texels square, or the side a graph's size setting asks for.</summary>
    sealed class Maps : IBuildExecutor {
        public List<OutputStep> Steps { get; } = [];

        public Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) {
            lock (Steps) {
                Steps.Add(work);
            }

            Directory.CreateDirectory(work.Folder);
            var side = work.Settings.TryGetValue(TextureSizes.OutputSetting, out var asked)
                ? int.Parse(asked, CultureInfo.InvariantCulture)
                : 32;
            var samples = new float[side * side];
            var random = new Random(3);
            for (var at = 0; at < samples.Length; at++) {
                samples[at] = (float)random.NextDouble();
            }

            var map = MapPictures.Write(work.Folder, "roughness", new(side, side, 1, samples), true, null);
            return Task.FromResult<BuildManifest>(new(work.Tool, [map], [], []));
        }

        public Task<ExportManifest> ExportAsync(
            ExportStep work,
            Action<double, string>? progress,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(ExportSteps.Run(work, progress, cancellationToken));
    }

    Task<LibraryReport> Build(OpenLibrary library, BuildRequest request, Maps? maps = null) =>
        new BuildPipeline(library, maps ?? new Maps(), ToolCatalog.Default, MeshFileReads.Instance)
            .RunAsync(
                new(
                    Path.Combine(LibraryLayout.ReportsPath(Root), Guid.NewGuid().ToString("N")),
                    "b",
                    DateTimeOffset.UnixEpoch
                ),
                request,
                null,
                null,
                TimeProvider.System,
                Token
            );

    /// <summary>
    ///     A project's own preset exports that project, and one nothing has is refused before anything runs.
    /// </summary>
    [Fact]
    public async Task A_projects_own_preset_exports_only_that_project() {
        using var library = Library();
        var document = ExportPresetWriter.DocumentOf(ExportPresetCatalog.Read(null).Find("unreal")!) with {
            Id = "crate-only", Name = "Crate only"
        };
        ExportPresetWriter.Save(ExportPresetCatalog.ProjectFolder(Root, "crate"), document);

        var plan = BuildPipeline.Plan(library, new() { Presets = ["crate-only"] }, ToolCatalog.Default, Token);
        var report = await Build(library, new() { Presets = ["crate-only"] });

        Assert.Single(plan.Projects.Single(static p => p.Project.Path == "crate").Exports);
        Assert.Empty(plan.Projects.Single(static p => p.Project.Path == "barrel").Exports);
        Assert.Equal(1, report.Exported);
        Assert.True(
            File.Exists(Path.Combine(LibraryLayout.ExportsPath(Root), "crate-only", "crate", "T_crate_ORM.png"))
        );
        Assert.Throws<BuildRefusedException>(() =>
            BuildPipeline.Plan(library, new() { Presets = ["nobody"] }, ToolCatalog.Default, Token)
        );
    }

    /// <summary>
    ///     An export's own size computes a graph at that size (#96, ADR 0028): the graph is built again with the size
    ///     setting, under a key of its own, and its file is what was computed, with nothing enlarged. A set's own size
    ///     wins for that set, and the graph's own build is still there for an export at its own size.
    /// </summary>
    [Fact]
    public async Task An_exports_size_computes_a_graph_at_it_and_enlarges_nothing() {
        using var library = Library();
        var maps = new Maps();
        var plain = BuildPipeline.Plan(library, new() { Presets = ["unreal"] }, ToolCatalog.Default, Token);
        await Build(library, new() { Presets = ["unreal"] }, maps);
        var report = await Build(library, new() { Presets = ["unreal"], Options = new() { Size = 64 } }, maps);
        var sized = BuildPipeline.Plan(
            library,
            new() { Presets = ["unreal"], Options = new() { Size = 64 } },
            ToolCatalog.Default,
            Token
        );

        Assert.Equal(["32", "32", "64", "64"], maps.Steps.Select(Side).Order(StringComparer.Ordinal));
        Assert.Equal(2, report.Exported);
        var exported = ExportRun.Find(new ObjectStore(Root).FolderOf(sized.Projects[0].Exports[0].Key));
        Assert.NotNull(exported);
        Assert.DoesNotContain(
            exported.Warnings,
            static warning => warning.Contains("enlarged", StringComparison.Ordinal)
        );
        Assert.All(exported.Files, static file => Assert.Equal((64, 64), (file.Width, file.Height)));
        Assert.NotEmpty(exported.Files);
        Assert.NotEqual(plain.Projects[0].Project.Outputs[0].Key, sized.Projects[0].Project.Outputs[0].Key);

        var own = new GraphSizes(64, new Dictionary<string, int> { ["crate"] = 128 });
        var declared = new OutputDeclaration(
            "maps",
            BuildTools.TextureGraph,
            [],
            [],
            new Dictionary<string, string> { [ProjectOutputs.SetSetting] = "crate" }
        );
        Assert.Equal("128", own.Apply(declared).Settings[TextureSizes.OutputSetting]);
        Assert.Equal("64", own.Apply(declared with { Settings = new Dictionary<string, string>() }).Settings["size"]);
        var stack = declared with { Tool = BuildTools.LayerStack };
        Assert.Same(stack, own.Apply(stack));

        static string Side(OutputStep step) => step.Settings.GetValueOrDefault(TextureSizes.OutputSetting, "32");
    }

    /// <summary>
    ///     A preset's own size computes a project's graphs at it when every preset the project exports through agrees
    ///     (#280): its file is what was computed, with nothing enlarged. Before, the graph was computed at its own size
    ///     and the file enlarged from it. Presets that disagree leave the graph at its own size, and resample.
    /// </summary>
    [Fact]
    public async Task A_presets_own_size_computes_a_graph_at_it() {
        using var library = Library();
        var unreal = ExportPresetCatalog.Read(null).Find("unreal")!;
        foreach (var (id, size) in (IEnumerable<(string, int)>)[("sized", 64), ("smaller", 16)]) {
            ExportPresetWriter.Save(
                ExportPresetCatalog.ProjectFolder(Root, "crate"),
                ExportPresetWriter.DocumentOf(unreal) with { Id = id, Name = id, Size = size }
            );
        }

        var maps = new Maps();
        await Build(library, new() { Presets = ["sized"] }, maps);
        var sized = BuildPipeline.Plan(library, new() { Presets = ["sized"] }, ToolCatalog.Default, Token);
        var both = BuildPipeline.Plan(library, new() { Presets = ["sized", "smaller"] }, ToolCatalog.Default, Token);

        // The barrel has no such preset, so its graph is computed at its own size.
        Assert.Equal(
            ["32", "64"],
            maps.Steps.Select(static step => step.Settings.GetValueOrDefault(TextureSizes.OutputSetting, "32"))
                .Order(StringComparer.Ordinal)
        );
        var crate = sized.Projects.Single(static p => p.Project.Path == "crate");
        var exported = ExportRun.Find(new ObjectStore(Root).FolderOf(crate.Exports[0].Key))!;
        Assert.All(exported.Files, static file => Assert.Equal((64, 64), (file.Width, file.Height)));
        Assert.NotEmpty(exported.Files);
        Assert.DoesNotContain(exported.Warnings, static w => w.Contains("enlarged", StringComparison.Ordinal));
        var mixed = both.Projects.Single(static p => p.Project.Path == "crate").Project.Outputs[0].Output;
        Assert.False(mixed.Settings.ContainsKey(TextureSizes.OutputSetting));
    }

    /// <summary>
    ///     Before a build there is nothing to compress, and after one a preview of the Vixen preset's BC7 file has an
    ///     error, a gain, its three pictures, and the check's memory for the file; asked again, it is read back.
    /// </summary>
    [Fact]
    public async Task A_preview_compresses_what_a_build_left() {
        using var library = Library();
        var request = new CompressionPreviewRequest("crate", "vixen", "{project}_orm") { Side = 16 };

        Assert.Throws<ExportUnbuiltException>(() => ExportPreviews.Make(library, request, ToolCatalog.Default, Token));
        await Build(library, new() { Presets = [] });
        var preview = ExportPreviews.Make(library, request, ToolCatalog.Default, Token);

        Assert.Equal((ExportFormats.Ktx2, ExportCompressions.Bc7), (preview.Format, preview.Compression));
        Assert.Equal(6, preview.Levels);
        Assert.Equal([8, 8, 16, 16], preview.Region);
        Assert.Equal(3, preview.Lanes.Count);
        Assert.True(preview.Lanes[1].MaxError > 0);
        Assert.Equal((long)Math.Ceiling(32 * 32 * 4d / 3d), preview.Bytes);
        Assert.Equal((long)Math.Ceiling(32 * 32 * 4 * 4d / 3d), preview.Uncompressed);
        Assert.All(
            ExportPreviews.Pictures,
            picture => Assert.NotNull(ExportPreviews.PictureOf(Root, preview.Id, picture))
        );
        Assert.Null(ExportPreviews.PictureOf(Root, preview.Id, "../preview.json"));
        Assert.Equal(preview.Id, ExportPreviews.Make(library, request, ToolCatalog.Default, Token).Id);

        var png = ExportPreviews.Make(
            library,
            request with { Options = new() { Files = [new("{project}_orm", true, ExportFormats.Png8)] } },
            ToolCatalog.Default,
            Token
        );
        Assert.NotEqual(preview.Id, png.Id);
        Assert.Equal(1, png.Levels);
        Assert.Equal(png.Bytes, png.Uncompressed);
    }
}
