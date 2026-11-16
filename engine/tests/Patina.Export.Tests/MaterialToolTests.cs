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

    static Dictionary<string, string[]> Read(string python, IReadOnlyList<string> files) {
        ProcessStartInfo start = new(python) {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(Script);
        foreach (var file in files) {
            start.ArgumentList.Add(file);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(Token);
        var error = process.StandardError.ReadToEndAsync(Token);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error.Result);
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(output.Result)!;
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
