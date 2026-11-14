using Patina.Contracts;
using Patina.Export.Materials;
using Patina.Export.Presets;
using System.Text.Json;
using System.Xml.Linq;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>
///     What ADR 0019 adds to the preset language, the editor's writer, and the material files beside textures.
/// </summary>
public sealed class PresetLanguageTests : IDisposable {
    const int Side = 16;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-language-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    string Path(string name) => System.IO.Path.GetFullPath(System.IO.Path.Combine(folder.FullName, name));

    static ExportPreset Read(string text) => ExportPresetReader.Read(text, "test");

    static string One(string format, string space, string channels, string texture = "", string preset = "") =>
        $"""
         version: 1
         id: one
         name: One
         normal: y-up
         {preset}
         textures:
           - file: "{"{"}set{"}"}_x"
             format: {format}
             space: {space}
             channels: {channels}
         {texture}
         """;

    /// <summary>A flat map of one value in every lane.</summary>
    static MapPicture Flat(string name, int lanes, params float[] value) {
        var samples = new float[Side * Side * lanes];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = value[at % lanes];
        }

        return new(name, new(Side, Side, lanes, samples), GreenAxis.Up);
    }

    [Theory]
    [InlineData("{mesh}", "crate_low.png")]
    [InlineData("{material}", "Steel.png")]
    [InlineData("{set}_{udim}", "Body_1001.png")]
    [InlineData("{tile}", "u1_v1.png")]
    [InlineData("{space}_{size}", "srgb_2048.png")]
    public void Every_name_resolves_from_what_the_project_and_the_file_are(string template, string expected) {
        NamingTemplate.Check(template, "test");
        var values = new NamingValues("Crate", "p") {
            Sets = new Dictionary<string, SetNaming> { ["Body"] = new("crate_low", "Steel") }
        };

        var name = NamingTemplate.Resolve(template, values.Of("Body") with { Space = "srgb", Size = 2048 }, ".png");

        Assert.Equal(expected, name);
    }

    /// <summary>Every file was named tile 1001, whichever tile its set's layout sat on (#20).</summary>
    [Theory]
    [InlineData(1001, "Body_1001_u1_v1.png")]
    [InlineData(1002, "Body_1002_u2_v1.png")]
    [InlineData(1013, "Body_1013_u3_v2.png")]
    public void The_udim_and_the_tile_are_the_sets_own(int udim, string expected) {
        var values = new NamingValues("Crate", "p") { Udim = udim };

        Assert.Equal(expected, NamingTemplate.Resolve("{set}_{udim}_{tile}", values.Of("Body"), ".png"));
    }

    [Fact]
    public void A_name_a_project_does_not_know_falls_back_to_what_holds_it() {
        var values = new NamingValues("Crate", "p");

        Assert.Equal("Crate_Crate.png", NamingTemplate.Resolve("{mesh}_{material}", values, ".png"));
        Assert.Throws<ExportPresetException>(static () => NamingTemplate.Check("{udims}", "test"));
    }

    [Fact]
    public void A_file_at_a_scale_is_that_share_of_every_other_files_side() {
        var preset = Read(One("png8", "linear", "[roughness.r]", "    scale: 0.25", "size: 8"));
        var maps = new Dictionary<string, MapPicture> { ["roughness"] = Flat("roughness", 1, 0.5f) };

        var file = Assert.Single(ExportRun.Run(preset, maps, Path("object"), null, Token).Files);

        Assert.Equal((2, 2), (file.Width, file.Height));
        Assert.Throws<ExportPresetException>(static () => Read(One("png8", "linear", "[roughness.r]", "    scale: 0.3"))
        );
    }

    /// <summary>A file's own dither and padding win over the preset's, and the other file keeps the preset's.</summary>
    [Fact]
    public void A_file_says_its_own_dither_and_padding() {
        var text = """
                   version: 1
                   id: one
                   name: One
                   normal: y-up
                   padding: none
                   textures:
                     - file: own
                       format: png8
                       space: linear
                       dither: true
                       padding: infinite
                       channels: [roughness.r]
                     - file: preset
                       format: png8
                       space: linear
                       channels: [roughness.r]
                   """;
        var preset = Read(text);
        var left = new float[Side * Side];
        var covered = new float[Side * Side];
        for (var at = 0; at < left.Length; at++) {
            left[at] = at % Side < Side / 2 ? 100.4f / 255f : 0.9f;
            covered[at] = at % Side < Side / 2 ? 1f : 0f;
        }

        var maps = new Dictionary<string, MapPicture> {
            ["roughness"] = new("roughness", new(Side, Side, 1, left), GreenAxis.Up),
            ["coverage"] = new("coverage", new(Side, Side, 1, covered), GreenAxis.Up)
        };

        var manifest = ExportRun.Run(preset, maps, Path("object"), null, Token);
        var own = MapPictures.ReadFile(Path($"object/{manifest.Files[0].File}"));
        var plain = MapPictures.ReadFile(Path($"object/{manifest.Files[1].File}"));

        Assert.True(Assert.Single(preset.Textures, static t => t.File == "own").Dither);
        Assert.InRange(own.Samples[(Side - 1) * own.Channels], 100f / 255f - 1e-4f, 101f / 255f + 1e-4f);
        Assert.Equal(0.9f, plain.Samples[(Side - 1) * plain.Channels], 2);
        Assert.Contains(
            Enumerable.Range(0, Side / 2).Select(x => own.Samples[x * own.Channels]),
            static value => Math.Abs(value - 101f / 255f) < 1e-4f
        );
        Assert.DoesNotContain(
            Enumerable.Range(0, Side / 2).Select(x => plain.Samples[x * plain.Channels]),
            static value => Math.Abs(value - 101f / 255f) < 1e-4f
        );
    }

    /// <summary>
    ///     Specular-glossiness from metal-roughness: a dielectric reflects four per cent and scatters its colour, a
    ///     metal reflects its colour and scatters none, and glossiness is one minus roughness.
    /// </summary>
    [Theory]
    [InlineData(0f, 0.04f, 0.6f)]
    [InlineData(1f, 0.6f, 0f)]
    public void Specular_and_diffuse_come_from_base_colour_and_metalness(float metal, float specular, float diffuse) {
        var colour = MipChain.ToSrgb(0.6f);
        var maps = new Dictionary<string, MapPicture> {
            ["baseColor"] = Flat("baseColor", 3, colour, colour, colour),
            ["metalness"] = Flat("metalness", 1, metal),
            ["roughness"] = Flat("roughness", 1, 0.25f)
        };

        var made = DerivedMaps.Complete(maps, ["specular", "diffuse", "glossiness"]);

        Assert.Equal(specular, MipChain.ToLinear(made["specular"].Image.Samples[0]), 3);
        Assert.Equal(diffuse, MipChain.ToLinear(made["diffuse"].Image.Samples[1]), 3);
        Assert.Equal(0.75f, made["glossiness"].Image.Samples[0], 4);
        Assert.Same(maps, DerivedMaps.Complete(maps, ["roughness"]));
    }

    [Fact]
    public void A_project_with_none_of_a_derived_maps_sources_writes_no_file_for_it() {
        var preset = ExportPresetCatalog.Read(null).Find("spec-gloss")!;
        var maps = new Dictionary<string, MapPicture> { ["height"] = Flat("height", 1, 0.5f) };

        var manifest = ExportRun.Run(preset, maps, Path("object"), null, Token);

        Assert.Empty(manifest.Files);
        Assert.Contains("{project}_{set}_SpecularGlossiness", manifest.Skipped);
    }

    /// <summary>
    ///     Every factory preset survives the editor: its file read, written as a document and read again.
    /// </summary>
    [Fact]
    public void Every_factory_preset_comes_back_from_the_editor_as_it_was() {
        foreach (var preset in ExportPresetCatalog.Read(null).Presets) {
            var again = ExportPresetWriter.Check(ExportPresetWriter.DocumentOf(preset));

            Assert.Equal(
                JsonSerializer.Serialize(ExportPresetWriter.DocumentOf(preset)),
                JsonSerializer.Serialize(ExportPresetWriter.DocumentOf(again))
            );
        }
    }

    /// <summary>
    ///     What a client types cannot become a key or a list in the file: it comes back as the text it was.
    /// </summary>
    [Fact]
    public void A_name_with_yaml_in_it_is_kept_as_text() {
        var document = ExportPresetWriter.DocumentOf(ExportPresetCatalog.Read(null).Find("gltf")!) with {
            Name = "Mine: \"quoted\"\nengine: hacked", Engine = "true"
        };

        var read = ExportPresetWriter.Check(document);

        Assert.Equal(document.Name, read.Name);
        Assert.Equal("true", read.Engine);
    }

    [Fact]
    public void The_editor_keeps_a_preset_where_it_is_asked_and_renames_one() {
        var document = ExportPresetWriter.DocumentOf(ExportPresetCatalog.Read(null).Find("unreal")!) with {
            Id = "mine", Name = "Mine"
        };
        var presets = System.IO.Path.Combine(Path("library"), "projects", "crate", "export-presets");

        ExportPresetWriter.Save(presets, document);
        ExportPresetWriter.Save(presets, document with { Id = "ours" }, "mine");

        var found = ExportPresetCatalog.Read(Path("library"), "crate");
        Assert.Equal("project", found.Find("ours")!.Origin);
        Assert.Null(found.Find("mine"));
        Assert.Null(ExportPresetCatalog.Read(Path("library")).Find("ours"));
        Assert.Throws<ExportPresetException>(() => ExportPresetWriter.Save(presets, document with { Id = "Bad Id" }));
        Assert.Throws<ArgumentException>(() => ExportPresetCatalog.Read(Path("library"), "../crate"));
    }

    [Fact]
    public void A_preset_lists_its_material_files_once_each() {
        Assert.Equal(
            ["gltf", "usda"],
            Read(One("png8", "linear", "[roughness.r]", preset: "materials: [gltf, usda]")).Materials
        );
        Assert.Throws<ExportPresetException>(static () =>
            Read(One("png8", "linear", "[roughness.r]", preset: "materials: [fbx]"))
        );
        Assert.Throws<ExportPresetException>(static () =>
            Read(One("png8", "linear", "[roughness.r]", preset: "materials: [gltf, gltf]"))
        );
        Assert.Throws<ExportPresetException>(static () =>
            Read(One("png8", "linear", "[roughness.r]", preset: "material: gltf"))
        );
    }

    [Fact]
    public void An_export_chooses_a_files_compression_quality_and_mips() {
        var preset = ExportPresetCatalog.Read(null).Find("unreal")!;
        var options = new ExportOptions {
            Files = [
                new("T_{project}_ORM", true, ExportFormats.Ktx2) {
                    Compression = ExportCompressions.Bc7, Quality = "fast", Mips = true
                },
                new("T_{project}_E", true, ExportFormats.Jpeg) { Quality = "70" }
            ]
        };

        var changed = ExportOverrides.Apply(preset, options);

        var orm = changed.Textures.Single(static t => t.File == "T_{project}_ORM");
        Assert.Equal(
            (ExportFormats.Ktx2, ExportCompressions.Bc7, "fast", true),
            (orm.Format, orm.Compression, orm.Quality, orm.Mips)
        );
        Assert.Equal("70", changed.Textures.Single(static t => t.File == "T_{project}_E").Quality);
        Assert.Throws<ExportPresetException>(() => ExportOverrides.Apply(
                preset,
                new() {
                    Files = [new("T_{project}_ORM", true, ExportFormats.Png8) { Compression = ExportCompressions.Bc7 }]
                }
            )
        );
        Assert.Throws<ExportPresetException>(() => ExportOverrides.Apply(
                preset,
                new() {
                    Files = [new("T_{project}_N", true, ExportFormats.Ktx2) { Compression = ExportCompressions.Bc4 }]
                }
            )
        );
    }

    [Fact]
    public void One_set_takes_its_own_size_and_files_and_the_rest_take_the_exports() {
        var preset = ExportOverrides.Apply(
            Read(One("png8", "linear", "[roughness.r]")),
            new() { Size = 8, PerSet = [new("Head") { Size = 4 }] }
        );
        ExportSet[] sets = [
            new("Body", new Dictionary<string, MapPicture> { ["roughness"] = Flat("roughness", 1, 0.5f) }),
            new("Head", new Dictionary<string, MapPicture> { ["roughness"] = Flat("roughness", 1, 0.5f) })
        ];
        List<string> told = [];

        var manifest = ExportRun.Run(preset, sets, Path("object"), (_, file) => told.Add(file), Token);

        Assert.Equal([8, 4], manifest.Files.Select(static f => f.Width));
        Assert.Equal(["Body: {set}_x", "Head: {set}_x", string.Empty], told);
        Assert.Throws<ExportPresetException>(() => ExportOverrides.Apply(
                preset,
                new() { PerSet = [new("Head"), new("Head")] }
            )
        );
    }

    /// <summary>
    ///     A set's size is the size it is authored at, which need not be square (ADR 0020); an export's size, the
    ///     whole export's or one set's, is the longest side its files are written at and changes nothing else (ADR
    ///     0019). So a set four times as wide as it is high is written four times as wide at either size.
    /// </summary>
    [Fact]
    public void A_set_that_is_not_square_keeps_its_shape_at_each_size_an_export_writes() {
        var preset = ExportOverrides.Apply(
            Read(One("png8", "linear", "[roughness.r]")),
            new() { Size = 8, PerSet = [new("Head") { Size = 4 }] }
        );
        static MapPicture Wide() => new("roughness", new(32, 8, 1, new float[32 * 8]), GreenAxis.Up);
        ExportSet[] sets = [
            new("Body", new Dictionary<string, MapPicture> { ["roughness"] = Wide() }),
            new("Head", new Dictionary<string, MapPicture> { ["roughness"] = Wide() })
        ];

        var manifest = ExportRun.Run(preset, sets, Path("object"), static (_, _) => { }, Token);

        Assert.Equal([(8, 2), (4, 1)], manifest.Files.Select(static f => (f.Width, f.Height)));
    }

    /// <summary>
    ///     The glTF preset writes a glTF beside its images that names each one in its slot, which Vixen's reader,
    ///     Assimp, reads back: the file is what a DCC binds.
    /// </summary>
    [Fact]
    public void The_gltf_preset_writes_a_material_naming_its_images() {
        var preset = ExportPresetCatalog.Read(null).Find("gltf")!;
        var manifest = ExportRun.Run(preset, Surface(), Path("object"), null, Token);

        var delivered = ExportRun.Deliver(
            "key",
            Path("object"),
            manifest,
            Path("out"),
            new("Crate", "gltf"),
            preset: preset
        );

        var gltf = Assert.Single(delivered.Files, static f => f.EndsWith(".gltf", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(File.ReadAllText(gltf));
        var images = document.RootElement.GetProperty("images")
            .EnumerateArray()
            .Select(static i => i.GetProperty("uri").GetString()!)
            .ToList();
        Assert.Equal(["Crate_baseColor.png", "Crate_occlusionRoughnessMetallic.png", "Crate_normal.png"], images);
        Assert.All(images, image => Assert.True(File.Exists(Path($"out/{image}"))));
        var material = document.RootElement.GetProperty("materials")[0];
        Assert.Equal(
            1,
            material.GetProperty("pbrMetallicRoughness")
                .GetProperty("metallicRoughnessTexture")
                .GetProperty("index")
                .GetInt32()
        );
        Assert.Equal(1, material.GetProperty("occlusionTexture").GetProperty("index").GetInt32());
        Assert.False(material.TryGetProperty("alphaMode", out _));
        Assert.Empty(delivered.Warnings);
    }

    /// <summary>
    ///     A preset whose packing glTF cannot read leaves those slots out and says so, rather than wiring them wrong.
    /// </summary>
    [Fact]
    public void A_packing_gltf_cannot_read_is_left_out_and_said() {
        var unreal = ExportPresetCatalog.Read(null).Find("unreal")! with { Materials = ["gltf"] };
        var manifest = ExportRun.Run(unreal, Surface(), Path("object"), null, Token);

        var delivered = ExportRun.Deliver(
            "key",
            Path("object"),
            manifest,
            Path("out"),
            new("Crate", "unreal"),
            preset: unreal
        );

        Assert.Contains(delivered.Warnings, static w => w.Contains("green up", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(File.ReadAllText(Path("out/Crate.gltf")));
        Assert.False(document.RootElement.GetProperty("materials")[0].TryGetProperty("normalTexture", out _));
    }

    [Fact]
    public void A_usd_material_reads_each_lane_and_turns_a_y_down_normal_over() {
        var unreal = ExportPresetCatalog.Read(null).Find("unreal")! with { Materials = ["usda"] };
        var manifest = ExportRun.Run(unreal, Surface(), Path("object"), null, Token);

        ExportRun.Deliver("key", Path("object"), manifest, Path("out"), new("Crate", "unreal"), preset: unreal);

        var usd = File.ReadAllText(Path("out/Crate.usda"));
        Assert.StartsWith("#usda 1.0", usd, StringComparison.Ordinal);
        Assert.Contains("asset inputs:file = @T_Crate_ORM.png@", usd, StringComparison.Ordinal);
        Assert.Contains("float inputs:roughness.connect = </Crate/Texture1.outputs:g>", usd, StringComparison.Ordinal);
        Assert.Contains("float inputs:metallic.connect = </Crate/Texture1.outputs:b>", usd, StringComparison.Ordinal);
        Assert.Contains("float inputs:occlusion.connect = </Crate/Texture1.outputs:r>", usd, StringComparison.Ordinal);
        Assert.Contains("float4 inputs:scale = (2, -2, 2, 1)", usd, StringComparison.Ordinal);
        Assert.Contains("token inputs:sourceColorSpace = \"sRGB\"", usd, StringComparison.Ordinal);
    }

    [Fact]
    public void A_materialx_document_extracts_each_lane_and_inverts_smoothness() {
        var urp = ExportPresetCatalog.Read(null).Find("unity-urp")! with { Materials = ["mtlx"] };
        var manifest = ExportRun.Run(urp, Surface(), Path("object"), null, Token);

        ExportRun.Deliver("key", Path("object"), manifest, Path("out"), new("Crate", "unity-urp"), preset: urp);

        var root = XDocument.Load(Path("out/Crate.mtlx")).Root!;
        Assert.Equal("1.38", root.Attribute("version")!.Value);
        var graph = root.Element("nodegraph")!;
        var files = graph.Elements("image").Select(static i => i.Element("input")!.Attribute("value")!.Value).ToList();
        Assert.All(files, file => Assert.True(File.Exists(Path($"out/{file}"))));
        Assert.Single(graph.Elements("invert"));
        var shader = root.Element("standard_surface")!;
        var inputs = shader.Elements("input").Select(static i => i.Attribute("name")!.Value).ToList();
        Assert.Contains("specular_roughness", inputs);
        Assert.Contains("metalness", inputs);
        Assert.Contains("normal", inputs);
        Assert.All(
            shader.Elements("input").Where(static i => i.Attribute("output") is not null),
            input => Assert.Single(
                graph.Elements("output"),
                o => o.Attribute("name")!.Value == input.Attribute("output")!.Value
            )
        );
    }

    /// <summary>
    ///     What a USD or MaterialX file says comes back as the maps it was written from (#157): each grey map is the
    ///     lane of the packed file it was put in, a Y- normal is read as one whose green runs down, and Unity's
    ///     smoothness is roughness turned over.
    /// </summary>
    [Fact]
    public void A_usd_or_materialx_file_reads_back_as_the_maps_it_was_written_from() {
        var unreal = ExportPresetCatalog.Read(null).Find("unreal")! with { Materials = ["usda", "mtlx"] };
        var manifest = ExportRun.Run(unreal, Surface(), Path("object"), null, Token);
        ExportRun.Deliver("key", Path("object"), manifest, Path("out"), new("Crate", "unreal"), preset: unreal);
        var urp = ExportPresetCatalog.Read(null).Find("unity-urp")! with { Materials = ["mtlx"] };
        var urpManifest = ExportRun.Run(urp, Surface(), Path("urp-object"), null, Token);
        ExportRun.Deliver("key", Path("urp-object"), urpManifest, Path("urp"), new("Crate", "unity-urp"), preset: urp);

        var usd = UsdaReader.Read(Path("out/Crate.usda"));
        foreach (var read in (IReadOnlyList<ImportedMaterial>[])[usd, MaterialXReader.Read(Path("out/Crate.mtlx"))]) {
            var material = Assert.Single(read);
            Assert.Equal("Crate", material.Name);
            var slots = material.Slots.Where(static s => s.Texture is not null).ToDictionary(static s => s.Usage);
            var orm = Path("out/T_Crate_ORM.png");
            var roughness = slots["roughness"];
            Assert.Equal((orm, 1, false), (roughness.Texture, roughness.Lane, roughness.Inverted));
            Assert.Equal((orm, 2), (slots["metalness"].Texture, slots["metalness"].Lane));
            Assert.Equal(-1, slots["baseColor"].Lane);
            Assert.True(File.Exists(slots["baseColor"].Texture));
            Assert.False(slots["normal"].GreenUp);
        }

        // Standard surface has no occlusion, so only USD carries it.
        var occlusion = usd[0].Slots.Single(static s => s.Usage == "occlusion");
        Assert.Equal((Path("out/T_Crate_ORM.png"), 0), (occlusion.Texture, occlusion.Lane));

        var smooth = Assert.Single(MaterialXReader.Read(Path("urp/Crate.mtlx")));
        Assert.True(smooth.Slots.Single(static s => s.Usage == "roughness").Inverted);
        Assert.True(smooth.Slots.Single(static s => s.Usage == "normal").GreenUp);
    }

    /// <summary>A material file a graph cannot be made from is refused with the reason.</summary>
    [Fact]
    public void A_file_that_is_no_material_is_refused_with_the_reason() {
        Directory.CreateDirectory(Path("in"));
        File.WriteAllText(Path("in/binary.usd"), "PXR-USDC");
        File.WriteAllText(Path("in/empty.mtlx"), """<materialx version="1.38"/>""");
        File.WriteAllText(Path("in/broken.mtlx"), "<materialx");

        Assert.Contains(
            ".usdc or .usdz",
            Assert.Throws<InvalidDataException>(() => UsdaReader.Read(Path("in/binary.usd"))).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "no standard_surface",
            Assert.Throws<InvalidDataException>(() => MaterialXReader.Read(Path("in/empty.mtlx"))).Message,
            StringComparison.Ordinal
        );
        Assert.Throws<InvalidDataException>(() => MaterialXReader.Read(Path("in/broken.mtlx")));
    }

    /// <summary>A whole metal-roughness surface: colour, normal, roughness, metalness and occlusion.</summary>
    static Dictionary<string, MapPicture> Surface() =>
        new() {
            ["baseColor"] = Flat("baseColor", 3, 0.5f, 0.4f, 0.3f),
            ["normal"] = Flat("normal", 3, 0.5f, 0.5f, 1f),
            ["roughness"] = Flat("roughness", 1, 0.6f),
            ["metalness"] = Flat("metalness", 1, 0f),
            ["occlusion"] = Flat("occlusion", 1, 1f)
        };
}
