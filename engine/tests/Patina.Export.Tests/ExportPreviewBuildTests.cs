using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Export.Presets;
using Patina.Library;

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

    sealed class Maps : IBuildExecutor {
        public Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) {
            Directory.CreateDirectory(work.Folder);
            var samples = new float[32 * 32];
            var random = new Random(3);
            for (var at = 0; at < samples.Length; at++) {
                samples[at] = (float)random.NextDouble();
            }

            var map = MapPictures.Write(work.Folder, "roughness", new(32, 32, 1, samples), true, null);
            return Task.FromResult<BuildManifest>(new(work.Tool, [map], [], []));
        }

        public Task<ExportManifest> ExportAsync(
            ExportStep work,
            Action<double, string>? progress,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(ExportSteps.Run(work, progress, cancellationToken));
    }

    Task<LibraryReport> Build(OpenLibrary library, BuildRequest request) =>
        new BuildPipeline(library, new Maps(), ToolCatalog.Default).RunAsync(
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
