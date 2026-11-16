using Patina.Export.Materials;
using Patina.Export.Presets;
using System.Xml.Linq;

namespace Patina.Export.Tests;

/// <summary>
///     OpenPBR's layer maps (#142), a coat, a sheen and transmission, reach the material files an export writes beside
///     its textures, under each surface's own names, and come back as the maps they were written from.
/// </summary>
public sealed class OpenPbrLayerExportTests : IDisposable {
    const int Side = 8;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-openpbr-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    string Path(string name) => System.IO.Path.GetFullPath(System.IO.Path.Combine(folder.FullName, name));

    static MapPicture Flat(string name, int lanes, params float[] value) {
        var samples = new float[Side * Side * lanes];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = value[at % lanes];
        }

        return new(name, new(Side, Side, lanes, samples), GreenAxis.Up);
    }

    /// <summary>The layers packed as a studio would: four grey maps in a file, the sheen's colour in one.</summary>
    static ExportPreset Preset(params string[] materials) =>
        ExportPresetReader.Read(
            """
            version: 1
            id: layered
            name: Layered
            normal: y-up
            textures:
              - file: "{project}_BaseColor"
                format: png8
                space: srgb
                channels: [baseColor.r, baseColor.g, baseColor.b]
              - file: "{project}_Layers"
                format: png8
                space: linear
                channels: [coat.r, coatRoughness.r, sheen.r, transmission.r]
              - file: "{project}_Sheen"
                format: png8
                space: linear
                channels: [sheenColor.r, sheenColor.g, sheenColor.b, sheenRoughness.r]
            """,
            "test"
        ) with {
            Materials = materials
        };

    static Dictionary<string, MapPicture> Layered() =>
        new() {
            ["baseColor"] = Flat("baseColor", 3, 0.5f, 0.4f, 0.3f),
            ["coat"] = Flat("coat", 1, 1f),
            ["coatRoughness"] = Flat("coatRoughness", 1, 0.2f),
            ["sheen"] = Flat("sheen", 1, 0.7f),
            ["sheenColor"] = Flat("sheenColor", 3, 0.9f, 0.8f, 0.7f),
            ["sheenRoughness"] = Flat("sheenRoughness", 1, 0.4f),
            ["transmission"] = Flat("transmission", 1, 0.3f)
        };

    List<string> Delivered(ExportPreset preset, Dictionary<string, MapPicture> maps, string name) {
        var manifest = ExportRun.Run(preset, maps, Path($"{name}-object"), null, Token);
        var delivered = ExportRun.Deliver(
            "key",
            Path($"{name}-object"),
            manifest,
            Path(name),
            new("Car", preset.Id),
            preset: preset
        );
        return [..delivered.Warnings];
    }

    static List<string> Inputs(XElement root, string surface) =>
        [..root.Element(surface)!.Elements("input").Select(static input => input.Attribute("name")!.Value)];

    /// <summary>
    ///     An OpenPBR document wires each layer map to OpenPBR's input, a standard surface one to its own, and both
    ///     read back as the maps and lanes they were written from.
    /// </summary>
    [Fact]
    public void Each_layer_map_reaches_openpbrs_inputs_and_standard_surfaces_and_reads_back() {
        Delivered(Preset("mtlx", "openpbr"), Layered(), "out");

        var openPbr = Inputs(XDocument.Load(Path("out/Car.openpbr.mtlx")).Root!, "open_pbr_surface");
        Assert.All(
            (string[]) [
                "coat_weight", "coat_roughness", "fuzz_weight", "fuzz_color", "fuzz_roughness", "transmission_weight"
            ],
            input => Assert.Contains(input, openPbr)
        );
        var standard = Inputs(XDocument.Load(Path("out/Car.mtlx")).Root!, "standard_surface");
        Assert.All(
            (string[])["coat", "coat_roughness", "sheen", "sheen_color", "sheen_roughness", "transmission"],
            input => Assert.Contains(input, standard)
        );

        foreach (var file in (string[])["out/Car.openpbr.mtlx", "out/Car.mtlx"]) {
            var slots = Assert.Single(MaterialXReader.Read(Path(file))).Slots.ToDictionary(static s => s.Usage);
            Assert.Equal((Path("out/Car_Layers.png"), 0), (slots["coat"].Texture, slots["coat"].Lane));
            Assert.Equal(1, slots["coatRoughness"].Lane);
            Assert.Equal(2, slots["sheen"].Lane);
            Assert.Equal(3, slots["transmission"].Lane);
            Assert.Equal((Path("out/Car_Sheen.png"), -1), (slots["sheenColor"].Texture, slots["sheenColor"].Lane));
            Assert.Equal(
                (Path("out/Car_Sheen.png"), 3),
                (slots["sheenRoughness"].Texture, slots["sheenRoughness"].Lane)
            );
        }
    }

    /// <summary>
    ///     A set without the layer channels wires none of them, though the preset's files read them, and a USD file
    ///     carries the clear coat, which UsdPreviewSurface has, and says it left the sheen and transmission out.
    /// </summary>
    [Fact]
    public void A_set_without_layers_wires_none_and_usd_keeps_only_the_coat() {
        Delivered(Preset("openpbr"), new() { ["baseColor"] = Flat("baseColor", 3, 0.5f, 0.4f, 0.3f) }, "bare");
        var bare = Inputs(XDocument.Load(Path("bare/Car.openpbr.mtlx")).Root!, "open_pbr_surface");
        Assert.DoesNotContain("coat_weight", bare);
        Assert.DoesNotContain("fuzz_color", bare);

        var said = Delivered(Preset("usda", "gltf"), Layered(), "usd");
        var usd = File.ReadAllText(Path("usd/Car.usda"));
        Assert.Contains("float inputs:clearcoat.connect", usd, StringComparison.Ordinal);
        Assert.Contains("float inputs:clearcoatRoughness.connect", usd, StringComparison.Ordinal);
        Assert.Contains(
            said,
            static warning => warning.StartsWith("USD: the sheen is left out", StringComparison.Ordinal)
        );
        Assert.Contains(said, static warning => warning.StartsWith("USD: transmission", StringComparison.Ordinal));
        Assert.Contains(said, static warning => warning.StartsWith("glTF: the coat", StringComparison.Ordinal));
        var read = Assert.Single(UsdaReader.Read(Path("usd/Car.usda"))).Slots.Select(static s => s.Usage).ToList();
        Assert.Contains("coat", read);
        Assert.Contains("coatRoughness", read);
    }

    /// <summary>
    ///     A material file a tool wrote with every input at its default brings in no layer channel, and a layer's
    ///     roughness without the layer's weight comes in with nothing either.
    /// </summary>
    [Fact]
    public void Layer_inputs_left_at_their_defaults_bring_in_no_channel() {
        File.WriteAllText(
            Path("tool.mtlx"),
            """
            <?xml version="1.0"?>
            <materialx version="1.39">
              <open_pbr_surface name="SR_Tool" type="surfaceshader">
                <input name="base_color" type="color3" value="0.8, 0.8, 0.8" />
                <input name="coat_weight" type="float" value="0" />
                <input name="coat_roughness" type="float" value="0.1" />
                <input name="fuzz_weight" type="float" value="0.5" />
                <input name="fuzz_color" type="color3" value="1, 1, 1" />
                <input name="fuzz_roughness" type="float" value="0.3" />
                <input name="transmission_weight" type="float" value="0" />
              </open_pbr_surface>
              <surfacematerial name="M_Tool" type="material">
                <input name="surfaceshader" type="surfaceshader" nodename="SR_Tool" />
              </surfacematerial>
            </materialx>
            """
        );

        var usages = Assert.Single(MaterialXReader.Read(Path("tool.mtlx"))).Slots.Select(static s => s.Usage).ToList();

        Assert.Equal(["baseColor", "sheen", "sheenRoughness"], usages);
    }
}
