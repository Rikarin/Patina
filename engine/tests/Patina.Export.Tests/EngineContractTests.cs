using Patina.Export.Presets;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>
///     What each engine's factory preset writes, checked without the engine (#46): every map at a value of its own goes
///     through the whole export, and each file is read back and held to what the engine's importer is documented to
///     read in each lane, the green it reads and whether it takes sRGB or linear numbers. The table is written from the
///     engines' pages, not from the presets, so a preset that drifts from its engine fails here. What still needs a
///     person with the engine installed is listed in ADR 0011.
/// </summary>
public sealed class EngineContractTests : IDisposable {
    const int Side = 8;
    const float Tolerance = 1.5f / 255f;

    // Each map at a value no other lane holds, on an 8-bit level, so any lane in the wrong place shows.
    const float Opacity = 204 / 255f;
    const float Roughness = 89 / 255f;
    const float Metalness = 166 / 255f;
    const float Occlusion = 230 / 255f;
    const float DetailMask = 38 / 255f;
    const float Height = 0.123f;
    const float Smoothness = 1f - Roughness;
    static readonly float[] BaseColor = [51 / 255f, 102 / 255f, 153 / 255f];
    static readonly float[] Normal = [128 / 255f, 77 / 255f, 230 / 255f];
    static readonly float[] Emissive = [26 / 255f, 128 / 255f, 77 / 255f];

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-engines-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     A linear map's numbers as an sRGB file holds them. Emissive is linear in a project, as HDR light is.
    /// </summary>
    static float[] Srgb(float[] linear) => [..linear.Select(MipChain.ToSrgb)];

    /// <summary>A normal made with green up as a file read with green down holds it.</summary>
    static float[] Down(float[] normal) => [normal[0], 1f - normal[1], normal[2]];

    static float[] Colour => [..BaseColor, Opacity];

    static float[] Orm => [Occlusion, Roughness, Metalness];

    /// <summary>
    ///     What each engine reads, file by file: the ending of the file's name and its lanes in order. Sources:
    ///     Unreal's texture import (sRGB colour, "Flip Green Channel" for OpenGL normals, which makes its own Y−) and
    ///     the common ORM packing; Unity HDRP's Mask Map page (R metallic, G occlusion, B detail mask, A smoothness)
    ///     and its emission in linear HDR; Unity URP's Lit page (metallic in R, smoothness in alpha by default); Godot
    ///     4's ORMMaterial3D (occlusion, roughness, metallic, in glTF's order) with OpenGL normals; glTF 2.0 §3.9
    ///     (occlusion R, roughness G, metallic B, +Y up).
    /// </summary>
    static IReadOnlyList<(string Engine, string Ending, float[] Lanes)> Table() => [
        ("unreal", "_D", Colour),
        ("unreal", "_N", Down(Normal)),
        ("unreal", "_ORM", Orm),
        ("unreal", "_E", Srgb(Emissive)),
        ("unreal", "_H", [Height]),
        ("unity-hdrp", "_BaseColor", Colour),
        ("unity-hdrp", "_Normal", Normal),
        ("unity-hdrp", "_MaskMap", [Metalness, Occlusion, DetailMask, Smoothness]),
        ("unity-hdrp", "_Emission", Emissive),
        ("unity-hdrp", "_Height", [Height]),
        ("unity-urp", "_BaseMap", Colour),
        ("unity-urp", "_Normal", Normal),
        ("unity-urp", "_MetallicSmoothness", [Metalness, Metalness, Metalness, Smoothness]),
        ("unity-urp", "_Occlusion", [Occlusion]),
        ("unity-urp", "_Emission", Srgb(Emissive)),
        ("unity-urp", "_Height", [Height]),
        ("godot", "_albedo", Colour),
        ("godot", "_normal", Normal),
        ("godot", "_orm", Orm),
        ("godot", "_emission", Srgb(Emissive)),
        ("godot", "_height", [Height]),
        ("gltf", "_baseColor", Colour),
        ("gltf", "_normal", Normal),
        ("gltf", "_occlusionRoughnessMetallic", Orm),
        ("gltf", "_emissive", Srgb(Emissive))
    ];

    public static TheoryData<string, string, float[]> Contracts() {
        TheoryData<string, string, float[]> data = [];
        foreach (var (engine, ending, lanes) in Table()) {
            data.Add(engine, ending, lanes);
        }

        return data;
    }

    static MapPicture Constant(string name, params float[] lanes) {
        var samples = new float[Side * Side * lanes.Length];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = lanes[at % lanes.Length];
        }

        return new(name, new(Side, Side, lanes.Length, samples), GreenAxis.Up);
    }

    static Dictionary<string, MapPicture> Maps() =>
        new(StringComparer.Ordinal) {
            [ExportMaps.BaseColor] = Constant(ExportMaps.BaseColor, BaseColor),
            [ExportMaps.Opacity] = Constant(ExportMaps.Opacity, Opacity),
            [ExportMaps.Normal] = Constant(ExportMaps.Normal, Normal),
            [ExportMaps.Roughness] = Constant(ExportMaps.Roughness, Roughness),
            [ExportMaps.Metalness] = Constant(ExportMaps.Metalness, Metalness),
            [ExportMaps.Occlusion] = Constant(ExportMaps.Occlusion, Occlusion),
            [ExportMaps.Mask] = Constant(ExportMaps.Mask, DetailMask),
            [ExportMaps.Emissive] = Constant(ExportMaps.Emissive, Emissive),
            [ExportMaps.Height] = Constant(ExportMaps.Height, Height)
        };

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Each_file_holds_what_its_engine_reads_in_each_lane(string engine, string ending, float[] lanes) {
        var preset = ExportPresetCatalog.Read(null).Find(engine)!;
        var made = Path.Combine(folder.FullName, engine);

        var manifest = ExportRun.Run(preset, Maps(), made, null, Token);

        var file = Assert.Single(manifest.Files, f => f.Template.EndsWith(ending, StringComparison.Ordinal));
        var picture = MapPictures.ReadFile(Path.Combine(made, file.File));
        Assert.True(picture.Channels >= lanes.Length, $"{file.Template} has {picture.Channels} lanes.");
        for (var lane = 0; lane < lanes.Length; lane++) {
            Assert.True(
                Math.Abs(picture.Samples[lane] - lanes[lane]) <= Tolerance,
                $"{engine} {file.Template} lane {lane}: {picture.Samples[lane]}, not {lanes[lane]}."
            );
        }
    }

    /// <summary>
    ///     Every file of an engine's preset is in the table, so a file added to a preset needs its engine's word.
    /// </summary>
    [Theory]
    [InlineData("unreal")]
    [InlineData("unity-hdrp")]
    [InlineData("unity-urp")]
    [InlineData("godot")]
    [InlineData("gltf")]
    public void Every_file_of_an_engines_preset_has_a_contract(string engine) {
        var endings = Table().Where(row => row.Engine == engine).Select(static row => row.Ending).ToList();

        var files = ExportPresetCatalog.Read(null).Find(engine)!.Textures.Select(static texture => texture.File);

        Assert.DoesNotContain(
            files,
            file => !endings.Exists(ending => file.EndsWith(ending, StringComparison.Ordinal))
        );
    }
}
