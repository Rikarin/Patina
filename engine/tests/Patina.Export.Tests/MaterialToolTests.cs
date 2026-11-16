using Patina.Export.Materials;
using Patina.Export.Presets;
using System.Diagnostics;
using System.Text.Json;

namespace Patina.Export.Tests;

/// <summary>
///     Patina's USD and MaterialX material files read in their own libraries (#157, #46): Pixar's USD and the MaterialX
///     library, which Blender, Houdini, Maya, Omniverse and Unreal read them with. Every shipped preset writes each
///     material file beside its textures, so each packing is read: ORM in one file, smoothness inverted in a mask, a
///     normal with either green. Each file must open, pass its schema's own validation, wire every input from a picture
///     that is there, and read each picture in the space its numbers are in. Patina's own readers agreeing with its
///     writers proves nothing about either, so this is the check that does.
/// </summary>
/// <remarks>
///     It needs a Python with <c>usd-core</c> and <c>MaterialX</c>, named by <c>PATINA_MATERIAL_PYTHON</c>
///     (<c>tools/requirements.txt</c> pins them with hashes). Without one it is skipped, except where
///     <c>PATINA_REQUIRE_MATERIAL_TOOLS</c> is set, as Linux CI sets it, and then a missing Python fails.
/// </remarks>
public sealed class MaterialToolTests : IDisposable {
    const int Side = 8;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-material-tools-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    public static TheoryData<string> Presets() => [..ExportPresetCatalog.Read(null).Presets.Select(static p => p.Id)];

    static Dictionary<string, MapPicture> Maps() =>
        ExportMaps.All.Except(ExportMaps.Derived)
            .ToDictionary(
                static map => map,
                static map => new MapPicture(map, new(Side, Side, 3, Grey()), GreenAxis.Up),
                StringComparer.Ordinal
            );

    static float[] Grey() => [..Enumerable.Repeat(0.5f, Side * Side * 3)];

    [Theory]
    [MemberData(nameof(Presets))]
    public void Usd_and_materialx_read_each_presets_material_files_without_a_problem(string id) {
        var python = Python();
        var preset = ExportPresetCatalog.Read(null).Find(id)! with { Materials = ExportMaterials.All };
        var made = Path.Combine(folder.FullName, "made");
        var target = Path.Combine(folder.FullName, "out");
        var manifest = ExportRun.Run(preset, Maps(), made, null, Token);

        var delivered = ExportRun.Deliver("key", made, manifest, target, new("Crate", id), preset: preset);

        string[] files = [
            ..delivered.Files.Where(static file =>
                file.EndsWith(".usda", StringComparison.Ordinal) || file.EndsWith(".mtlx", StringComparison.Ordinal)
            )
        ];
        Assert.Equal(3, files.Length);
        var found = Read(python, files);
        Assert.Equal(files.Order(), found.Keys.Order());
        var problems = found.SelectMany(static file => file.Value.Select(problem => $"{file.Key}: {problem}"));
        Assert.True(!problems.Any(), string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    ///     The normal USD hands a renderer, worked out as UsdUVTexture defines it: each texel times the texture's scale
    ///     plus its bias, with the scale and bias as USD itself reads them from the file (#304). A preset whose normal
    ///     runs green down (Unreal) writes a scale of −2 for green, as the USD working group's reversed-Y test asset
    ///     does; it must give the same tangent-space normal as a preset whose green runs up.
    /// </summary>
    [Theory]
    [MemberData(nameof(Presets))]
    public void Usd_reads_each_presets_normal_with_its_green_up(string id) {
        var python = Python();
        var preset = ExportPresetCatalog.Read(null).Find(id)! with { Materials = [ExportMaterials.Usd] };
        var made = Path.Combine(folder.FullName, "made");
        var manifest = ExportRun.Run(preset, LeaningMaps(), made, null, Token);
        var delivered = ExportRun.Deliver(
            "key",
            made,
            manifest,
            Path.Combine(folder.FullName, "out"),
            new("Crate", id),
            preset: preset
        );
        var usd = Assert.Single(delivered.Files, static f => f.EndsWith(".usda", StringComparison.Ordinal));

        var reads = Assert.Single(Normals(python, usd)).Value;
        if (reads.Length == 0) {
            // No normal in the file: the preset writes none, or writes it where USD reads no picture, and says so.
            var writes = preset.Textures.Any(static t => t.Channels.Any(static c => c.Map == ExportMaps.Normal));
            var said = delivered.Warnings.Any(static w =>
                w.Contains("the normal is left out", StringComparison.Ordinal)
            );
            Assert.True(
                !writes || said,
                string.Join(Environment.NewLine, delivered.Warnings)
            );
            return;
        }

        var normal = Assert.Single(reads);
        Assert.Equal("rgb", normal.Output);
        var texel = MapPictures.ReadFile(Path.Combine(Path.GetDirectoryName(usd)!, normal.File)).Samples;
        for (var lane = 0; lane < 3; lane++) {
            var read = texel[lane] * normal.Scale[lane] + normal.Bias[lane];
            var meant = 2 * Leaning[lane] - 1;
            Assert.True(
                Math.Abs(read - meant) <= 3f / 255f,
                $"{id}: USD reads lane {lane} of the normal as {read}, not {meant} (scale {normal.Scale[lane]})."
            );
        }
    }

    /// <summary>
    ///     Each preset's USD material, written again by USD itself in its binary form (<c>.usdc</c>), brings in what
    ///     the text does through Patina's own reader of that form (#157), slot for slot.
    /// </summary>
    [Theory]
    [MemberData(nameof(Presets))]
    public void Patina_reads_each_presets_material_as_usd_writes_it_in_binary(string id) {
        var python = Python();
        var preset = ExportPresetCatalog.Read(null).Find(id)! with { Materials = [ExportMaterials.Usd] };
        var made = Path.Combine(folder.FullName, "made");
        var manifest = ExportRun.Run(preset, LeaningMaps(), made, null, Token);
        var delivered = ExportRun.Deliver(
            "key",
            made,
            manifest,
            Path.Combine(folder.FullName, "out"),
            new("Crate", id),
            preset: preset
        );
        var usda = Assert.Single(delivered.Files, static f => f.EndsWith(".usda", StringComparison.Ordinal));
        var usdc = Path.ChangeExtension(usda, ".usdc");

        Assert.Contains("true", Run(python, ["--binary", usda, usdc]), StringComparison.Ordinal);

        Assert.StartsWith("PXR-USDC", File.ReadAllText(usdc)[..8], StringComparison.Ordinal);
        Assert.Equal(Slots(usda), Slots(usdc));
    }

    static string[] Slots(string layer) => [
        ..UsdaReader.Read(layer)
            .SelectMany(static m => m.Slots.Select(s =>
                    $"{m.Name} {s.Usage} {s.Texture} {s.Lane} {s.Inverted} {s.GreenUp} {string.Join(',', s.Value)}"
                )
            )
    ];

    /// <summary>A normal leaning to +X and −Y, green up, on 8-bit levels, so green read upside down shows.</summary>
    static readonly float[] Leaning = [166 / 255f, 77 / 255f, 230 / 255f];

    static Dictionary<string, MapPicture> LeaningMaps() {
        var maps = Maps();
        float[] samples = [..Enumerable.Range(0, Side * Side * 3).Select(static at => Leaning[at % 3])];
        maps[ExportMaps.Normal] = new(ExportMaps.Normal, new(Side, Side, 3, samples), GreenAxis.Up);
        return maps;
    }

    sealed record NormalRead(string File, string Output, float[] Scale, float[] Bias);

    static Dictionary<string, NormalRead[]> Normals(string python, string usd) =>
        JsonSerializer.Deserialize<Dictionary<string, NormalRead[]>>(
            Run(python, ["--normals", usd]),
            Web
        )!;

    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    static Dictionary<string, string[]> Read(string python, IReadOnlyList<string> files) =>
        JsonSerializer.Deserialize<Dictionary<string, string[]>>(Run(python, files))!;

    static string Run(string python, IReadOnlyList<string> arguments) {
        ProcessStartInfo start = new(python) {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(Script);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(Token);
        var error = process.StandardError.ReadToEndAsync(Token);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error.Result);
        return output.Result;
    }

    static string Script => Path.Combine(AppContext.BaseDirectory, "tools", "check_materials.py");

    /// <summary>The Python that has USD and MaterialX, or a skip where none is named and none is required.</summary>
    static string Python() {
        var python = Environment.GetEnvironmentVariable("PATINA_MATERIAL_PYTHON");
        if (!string.IsNullOrEmpty(python)) {
            return python;
        }

        Assert.False(
            Environment.GetEnvironmentVariable("PATINA_REQUIRE_MATERIAL_TOOLS") is { Length: > 0 },
            "PATINA_REQUIRE_MATERIAL_TOOLS is set, and PATINA_MATERIAL_PYTHON names no Python with USD and MaterialX."
        );
        Assert.Skip("PATINA_MATERIAL_PYTHON names no Python with usd-core and MaterialX (tools/requirements.txt).");
        return string.Empty;
    }
}
