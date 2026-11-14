using Patina.Export.Materials;
using Patina.Export.Presets;
using Vixen.Core.Imaging;
using Vixen.Core.Imaging.BlockCompression;
using Vixen.Graphics;

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
        ("gltf", "_emissive", Srgb(Emissive)),
        // Blender's manual: an Image Texture node reads a colour picture as sRGB and a data map as Non-Color, the
        // Normal Map node reads tangent space with green up (OpenGL), and Emission Color is a colour.
        ("blender", "_base_color", BaseColor),
        ("blender", "_normal", Normal),
        ("blender", "_roughness", [Roughness]),
        ("blender", "_metallic", [Metalness]),
        ("blender", "_occlusion", [Occlusion]),
        ("blender", "_alpha", [Opacity]),
        ("blender", "_emission", Srgb(Emissive)),
        ("blender", "_height", [Height]),
        // Vixen's TexturedOrmSurface: colour and alpha in one sRGB BC7 file, a two-lane BC5 normal with green down,
        // occlusion, roughness and metalness in that order, emission as linear BC6H and height as BC4. Its blocks are
        // decoded here, so a lane the encoder moved shows.
        ("vixen", "_baseColor", Colour),
        ("vixen", "_normal", Down(Normal)[..2]),
        ("vixen", "_orm", Orm),
        ("vixen", "_emissive", Emissive),
        ("vixen", "_height", [Height])
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

    /// <summary>
    ///     A file's first texel as numbers: a picture as Patina reads one, or a KTX2 file's blocks decoded by Vixen's
    ///     decoder, which an Apple GPU agreed with block for block (ADR 0011), into four lanes.
    /// </summary>
    static FloatImage Picture(string path) {
        if (!path.EndsWith(".ktx2", StringComparison.Ordinal)) {
            return MapPictures.ReadFile(path);
        }

        var decoded = BlockCompressor.Decode(Ktx2.Read(File.ReadAllBytes(path)));
        var bytes = decoded.PixelSpan();
        var half = decoded.Format == PixelFormat.Rgba16Float;
        var first = new float[4];
        for (var lane = 0; lane < first.Length; lane++) {
            first[lane] = half ? (float)BitConverter.ToHalf(bytes.Slice(lane * 2, 2)) : bytes[lane] / 255f;
        }

        return new(1, 1, 4, first);
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Each_file_holds_what_its_engine_reads_in_each_lane(string engine, string ending, float[] lanes) {
        var preset = ExportPresetCatalog.Read(null).Find(engine)!;
        var made = Path.Combine(folder.FullName, engine);

        var manifest = ExportRun.Run(preset, Maps(), made, null, Token);

        var file = Assert.Single(manifest.Files, f => f.Template.EndsWith(ending, StringComparison.Ordinal));
        var picture = Picture(Path.Combine(made, file.File));
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
    [InlineData("blender")]
    [InlineData("vixen")]
    public void Every_file_of_an_engines_preset_has_a_contract(string engine) {
        var endings = Table().Where(row => row.Engine == engine).Select(static row => row.Ending).ToList();

        var files = ExportPresetCatalog.Read(null).Find(engine)!.Textures.Select(static texture => texture.File);

        Assert.DoesNotContain(
            files,
            file => !endings.Exists(ending => file.EndsWith(ending, StringComparison.Ordinal))
        );
    }

    /// <summary>
    ///     Blender reads the preset's files through the USD material beside them (#46): its importer turns a
    ///     <c>UsdPreviewSurface</c> into a Principled BSDF, input by input. So the material file is held to the schema:
    ///     read back, each input names the file the table above checked for its map, the whole colour or lane red, not
    ///     inverted, and the normal with its green up as Blender's Normal Map node reads it. Occlusion is in the file
    ///     too, which the schema has and Blender leaves unwired.
    /// </summary>
    [Fact]
    public void Blenders_usd_material_wires_each_file_to_the_input_that_reads_it() {
        var preset = ExportPresetCatalog.Read(null).Find("blender")!;
        var made = Path.Combine(folder.FullName, "made");
        var target = Path.Combine(folder.FullName, "out");
        var manifest = ExportRun.Run(preset, Maps(), made, null, Token);

        var delivered = ExportRun.Deliver("key", made, manifest, target, new("Crate", "blender"), preset: preset);

        Assert.Empty(delivered.Warnings);
        var usd = Assert.Single(delivered.Files, static f => f.EndsWith(".usda", StringComparison.Ordinal));
        var material = Assert.Single(UsdaReader.Read(usd));
        var slots = material.Slots.ToDictionary(static s => s.Usage, StringComparer.Ordinal);
        (string Usage, string Ending, int Lane)[] wired = [
            (ExportMaps.BaseColor, "_base_color", -1), (ExportMaps.Normal, "_normal", -1),
            (ExportMaps.Roughness, "_roughness", 0), (ExportMaps.Metalness, "_metallic", 0),
            (ExportMaps.Occlusion, "_occlusion", 0), (ExportMaps.Opacity, "_alpha", 0),
            (ExportMaps.Emissive, "_emission", -1), (ExportMaps.Height, "_height", 0)
        ];
        Assert.Equal(wired.Select(static w => w.Usage).Order(), slots.Keys.Order());
        foreach (var (usage, ending, lane) in wired) {
            var slot = slots[usage];
            Assert.EndsWith(ending, Path.GetFileNameWithoutExtension(slot.Texture), StringComparison.Ordinal);
            Assert.True(File.Exists(slot.Texture), slot.Texture);
            Assert.Equal((lane, false), (slot.Lane, slot.Inverted));
        }

        Assert.True(slots[ExportMaps.Normal].GreenUp);
    }
}
