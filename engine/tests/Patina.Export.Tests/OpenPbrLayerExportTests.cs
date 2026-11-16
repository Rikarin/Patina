using Patina.Export.Materials;
using Patina.Export.Presets;
using System.Text.Json;
using System.Xml.Linq;
using Vixen.Core.Imaging;

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
        Assert.Contains(said, static warning => warning.StartsWith("glTF: the sheen", StringComparison.Ordinal));
        Assert.Contains(said, static warning => warning.StartsWith("glTF: transmission", StringComparison.Ordinal));
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

    /// <summary>
    ///     The shipped glTF preset writes a layered set's coat, sheen and transmission in the lanes glTF's extensions
    ///     read, and its material file names them: the sheen's colour times its weight, since glTF's sheen has none.
    /// </summary>
    [Fact]
    public void The_gltf_preset_carries_the_coat_sheen_and_transmission_through_gltfs_extensions() {
        var preset = ExportPresetCatalog.Read(null).Find("gltf")!;
        var maps = Layered();
        maps["coatNormal"] = Flat("coatNormal", 3, 0.5f, 0.5f, 1f);

        var said = Delivered(preset, maps, "gltf");

        using var document = JsonDocument.Parse(File.ReadAllText(Path("gltf/Car.gltf")));
        var root = document.RootElement;
        var images = root.GetProperty("images")
            .EnumerateArray()
            .Select(static i => i.GetProperty("uri").GetString()!)
            .ToList();
        var extensions = root.GetProperty("materials")[0].GetProperty("extensions");
        string Image(JsonElement slot) => images[slot.GetProperty("index").GetInt32()];

        var coat = extensions.GetProperty("KHR_materials_clearcoat");
        Assert.Equal("Car_clearcoat.png", Image(coat.GetProperty("clearcoatTexture")));
        Assert.Equal("Car_clearcoat.png", Image(coat.GetProperty("clearcoatRoughnessTexture")));
        Assert.Equal("Car_clearcoatNormal.png", Image(coat.GetProperty("clearcoatNormalTexture")));
        var sheen = extensions.GetProperty("KHR_materials_sheen");
        Assert.Equal("Car_sheen.png", Image(sheen.GetProperty("sheenColorTexture")));
        Assert.Equal("Car_sheen.png", Image(sheen.GetProperty("sheenRoughnessTexture")));
        Assert.Equal(
            "Car_transmission.png",
            Image(extensions.GetProperty("KHR_materials_transmission").GetProperty("transmissionTexture"))
        );
        Assert.DoesNotContain(said, static warning => warning.StartsWith("glTF:", StringComparison.Ordinal));
        // Every extension the file names is listed in extensionsUsed, or another reader drops it without a word (#46).
        HashSet<string> listed = [
            ..root.GetProperty("extensionsUsed").EnumerateArray().Select(static name => name.GetString()!)
        ];
        Assert.Superset(
            new HashSet<string> { "KHR_materials_clearcoat", "KHR_materials_sheen", "KHR_materials_transmission" },
            listed
        );

        // The sheen's colour (0.9, 0.8, 0.7) times its weight of 0.7, stored as sRGB, with its roughness in alpha.
        var tint = PngCodec.Decode(File.ReadAllBytes(Path("gltf/Car_sheen.png")));
        float[] linear = [0.63f, 0.56f, 0.49f];
        for (var lane = 0; lane < 3; lane++) {
            Assert.Equal(MipChain.ToSrgb(linear[lane]), tint.Pixels[lane] / 255f, 2);
        }

        Assert.Equal(0.4f, tint.Pixels[3] / 255f, 2);
    }

    /// <summary>
    ///     A coat's roughness or a sheen's without its weight is no coat and no sheen (#310): glTF's preset writes no
    ///     clear coat or sheen file for it, where it wrote a coat file of nothing and a sheen file whose colour was a
    ///     default, and the material names neither extension.
    /// </summary>
    [Fact]
    public void A_layers_roughness_without_its_weight_writes_no_file_of_the_layer() {
        var preset = ExportPresetCatalog.Read(null).Find("gltf")!;
        var maps = new Dictionary<string, MapPicture> {
            ["baseColor"] = Flat("baseColor", 3, 0.5f, 0.4f, 0.3f),
            ["coatRoughness"] = Flat("coatRoughness", 1, 0.2f),
            ["coatNormal"] = Flat("coatNormal", 3, 0.5f, 0.5f, 1f),
            ["sheenRoughness"] = Flat("sheenRoughness", 1, 0.4f)
        };

        var said = Delivered(preset, maps, "bare");

        string[] written = [..Directory.GetFiles(Path("bare")).Select(System.IO.Path.GetFileName).OfType<string>()];
        Assert.Contains("Car_baseColor.png", written);
        Assert.DoesNotContain(written, static file => file.Contains("clearcoat", StringComparison.Ordinal));
        Assert.DoesNotContain(written, static file => file.Contains("sheen", StringComparison.Ordinal));
        Assert.DoesNotContain(said, static warning => warning.Contains("sheenTint", StringComparison.Ordinal));
        Assert.DoesNotContain("KHR_materials_", File.ReadAllText(Path("bare/Car.gltf")), StringComparison.Ordinal);
    }

    /// <summary>
    ///     OpenPBR's specular lobe, a coat's colour and its own normal reach OpenPBR's and standard surface's inputs
    ///     and read back as the maps they were written from; USD and glTF say they leave them out.
    /// </summary>
    [Fact]
    public void The_specular_lobe_and_the_coats_colour_and_normal_reach_materialx_and_read_back() {
        var preset = ExportPresetReader.Read(
            """
            version: 1
            id: lobes
            name: Lobes
            normal: y-down
            textures:
              - file: "{project}_Lobe"
                format: png8
                space: linear
                channels: [specularWeight.r, anisotropy.r, coat.r]
              - file: "{project}_SpecularColor"
                format: png8
                space: srgb
                channels: [specularColor.r, specularColor.g, specularColor.b]
              - file: "{project}_CoatColor"
                format: png8
                space: srgb
                channels: [coatColor.r, coatColor.g, coatColor.b]
              - file: "{project}_CoatNormal"
                format: png8
                space: linear
                channels: [coatNormal.r, coatNormal.g, coatNormal.b]
            """,
            "test"
        ) with {
            Materials = ["mtlx", "openpbr", "usda", "gltf"]
        };
        Dictionary<string, MapPicture> maps = new() {
            ["specularWeight"] = Flat("specularWeight", 1, 0.8f),
            ["anisotropy"] = Flat("anisotropy", 1, 0.3f),
            ["coat"] = Flat("coat", 1, 1f),
            ["specularColor"] = Flat("specularColor", 3, 1f, 0.9f, 0.8f),
            ["coatColor"] = Flat("coatColor", 3, 0.9f, 0.9f, 1f),
            ["coatNormal"] = Flat("coatNormal", 3, 0.5f, 0.5f, 1f)
        };

        var said = Delivered(preset, maps, "lobes");

        var openPbr = Inputs(XDocument.Load(Path("lobes/Car.openpbr.mtlx")).Root!, "open_pbr_surface");
        Assert.All(
            (string[]) [
                "specular_weight", "specular_color", "specular_roughness_anisotropy", "coat_color",
                "geometry_coat_normal"
            ],
            input => Assert.Contains(input, openPbr)
        );
        var standard = Inputs(XDocument.Load(Path("lobes/Car.mtlx")).Root!, "standard_surface");
        Assert.All(
            (string[])["specular", "specular_color", "specular_anisotropy", "coat_color", "coat_normal"],
            input => Assert.Contains(input, standard)
        );
        foreach (var file in (string[])["lobes/Car.openpbr.mtlx", "lobes/Car.mtlx"]) {
            var slots = Assert.Single(MaterialXReader.Read(Path(file))).Slots.ToDictionary(static s => s.Usage);
            Assert.Equal(
                (Path("lobes/Car_Lobe.png"), 0),
                (slots["specularWeight"].Texture, slots["specularWeight"].Lane)
            );
            Assert.Equal(1, slots["anisotropy"].Lane);
            Assert.Equal(-1, slots["specularColor"].Lane);
            Assert.Equal(Path("lobes/Car_CoatColor.png"), slots["coatColor"].Texture);
            Assert.Equal(
                (Path("lobes/Car_CoatNormal.png"), false),
                (slots["coatNormal"].Texture, slots["coatNormal"].GreenUp)
            );
        }

        Assert.Contains(said, static w => w.StartsWith("USD: the coat's color and normal", StringComparison.Ordinal));
        Assert.Contains(said, static w => w.StartsWith("USD: specular weight", StringComparison.Ordinal));
        Assert.Contains(said, static w => w.StartsWith("glTF: the coat's color", StringComparison.Ordinal));
        Assert.Contains(said, static w => w.StartsWith("glTF: the coat is left out", StringComparison.Ordinal));
        Assert.Contains(said, static w => w.StartsWith("glTF: specular weight", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A <c>gltf_pbr</c>'s sheen has no weight, so a coloured one comes in with a whole weight; a black one brings
    ///     in nothing, and nor does a coat's colour without a coat or a specular colour left white.
    /// </summary>
    [Fact]
    public void A_gltf_pbr_sheen_comes_in_with_a_whole_weight_while_its_colour_is_not_black() {
        File.WriteAllText(
            Path("gltf.mtlx"),
            """
            <?xml version="1.0"?>
            <materialx version="1.39">
              <gltf_pbr name="SR_Velvet" type="surfaceshader">
                <input name="sheen_color" type="color3" value="0.5, 0.2, 0.1" />
                <input name="sheen_roughness" type="float" value="0.3" />
              </gltf_pbr>
              <gltf_pbr name="SR_Plain" type="surfaceshader">
                <input name="sheen_color" type="color3" value="0, 0, 0" />
                <input name="sheen_roughness" type="float" value="0.3" />
              </gltf_pbr>
              <open_pbr_surface name="SR_Tinted" type="surfaceshader">
                <input name="coat_color" type="color3" value="0.5, 0.5, 1" />
                <input name="specular_color" type="color3" value="1, 1, 1" />
              </open_pbr_surface>
              <surfacematerial name="M_Velvet" type="material">
                <input name="surfaceshader" type="surfaceshader" nodename="SR_Velvet" />
              </surfacematerial>
              <surfacematerial name="M_Plain" type="material">
                <input name="surfaceshader" type="surfaceshader" nodename="SR_Plain" />
              </surfacematerial>
              <surfacematerial name="M_Tinted" type="material">
                <input name="surfaceshader" type="surfaceshader" nodename="SR_Tinted" />
              </surfacematerial>
            </materialx>
            """
        );

        var read = MaterialXReader.Read(Path("gltf.mtlx")).ToDictionary(static m => m.Name);

        var velvet = read["Velvet"].Slots.ToDictionary(static s => s.Usage);
        Assert.Equal(1f, velvet["sheen"].Value[0]);
        Assert.Equal([0.5f, 0.2f, 0.1f], velvet["sheenColor"].Value.Take(3));
        Assert.Equal(0.3f, velvet["sheenRoughness"].Value[0]);
        Assert.DoesNotContain(read["Plain"].Slots, static s => s.Usage.StartsWith("sheen", StringComparison.Ordinal));
        Assert.Empty(read["Tinted"].Slots);
    }
}
