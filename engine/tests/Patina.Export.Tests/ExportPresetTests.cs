using Patina.Contracts;
using Patina.Export.Presets;
using Vixen.Interchange;

namespace Patina.Export.Tests;

public sealed class ExportPresetTests : IDisposable {
    const string Minimal = """
                           version: 1
                           id: mine
                           name: Mine
                           normal: y-up
                           textures:
                             - file: "{project}_x"
                               format: png8
                               space: linear
                               channels: [roughness]
                           """;

    readonly DirectoryInfo library = Directory.CreateTempSubdirectory("patina-presets-");

    string Shared => Directory.CreateDirectory(Path.Combine(library.FullName, "export-presets")).FullName;

    public void Dispose() => library.Delete(true);

    [Fact]
    public void The_factory_has_each_engine_with_the_green_it_reads() {
        var found = ExportPresetCatalog.Read(null);

        Assert.Empty(found.Problems);
        Assert.Equal(["godot", "unity-hdrp", "unity-urp", "unreal", "vixen"], found.Presets.Select(static p => p.Id));
        Assert.Equal(GreenAxis.Down, found.Find("unreal")!.Normal);
        Assert.Equal(GreenAxis.Up, found.Find("unity-urp")!.Normal);
        Assert.Equal(GreenAxis.Up, found.Find("unity-hdrp")!.Normal);
        Assert.Equal(GreenAxis.Up, found.Find("godot")!.Normal);
        Assert.Equal(GreenAxis.Down, found.Find("vixen")!.Normal);
    }

    [Fact]
    public void A_mask_map_holds_what_unity_documents_in_each_lane() {
        var mask = ExportPresetCatalog.Read(null).Find("unity-hdrp")!.Textures.Single(static t => t.File.EndsWith(
                "_MaskMap",
                StringComparison.Ordinal
            )
        );

        Assert.Equal(
            [
                new(ExportMaps.Metalness, 0, false, 0),
                new(ExportMaps.Occlusion, 0, false, 0),
                new(ExportMaps.Mask, 0, false, 0),
                new ChannelSource(ExportMaps.Roughness, 0, true, 0)
            ],
            mask.Channels
        );
        Assert.Equal("linear", mask.Space);
    }

    [Fact]
    public void A_user_adds_a_preset_with_a_file() {
        File.WriteAllText(Path.Combine(Shared, "mine.yaml"), Minimal);

        var found = ExportPresetCatalog.Read(library.FullName);

        Assert.Equal(6, found.Presets.Count);
        Assert.Equal(Minimal, ExportPresetCatalog.TextOf(found.Find("mine")!));
    }

    [Fact]
    public void A_library_preset_wins_over_the_factory_one_of_the_same_id() {
        File.WriteAllText(
            Path.Combine(Shared, "unreal.yaml"),
            Minimal.Replace("id: mine", "id: unreal", StringComparison.Ordinal)
        );

        var preset = ExportPresetCatalog.Read(library.FullName).Find("unreal")!;

        Assert.Equal("Mine", preset.Name);
        Assert.NotNull(preset.Path);
    }

    [Fact]
    public void A_broken_file_is_reported_and_takes_nothing_with_it() {
        File.WriteAllText(Path.Combine(Shared, "broken.yaml"), "version: 1\nid: broken\n");

        var found = ExportPresetCatalog.Read(library.FullName);

        Assert.Equal(5, found.Presets.Count);
        Assert.Contains("broken.yaml", Assert.Single(found.Problems), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("format: png8", "format: bmp", "'bmp' is not a format")]
    [InlineData("normal: y-up", "normal: opengl", "neither y-up nor y-down")]
    [InlineData("[roughness]", "[smoothness]", "'smoothness' is not a map")]
    [InlineData("[roughness]", "[roughness.q]", "names no lane")]
    [InlineData("[roughness]", "[roughness, metalness]", "not 2")]
    [InlineData("{project}_x", "../{project}", "inside the export folder")]
    [InlineData("{project}_x", "{name}_x", "'{name}' is not a name")]
    [InlineData("space: linear", "space: linear\n    compresion: bc7", "'compresion' is not a field")]
    [InlineData("format: png8", "format: exr16", "holds linear")]
    public void A_preset_that_would_write_the_wrong_thing_is_refused(string from, string to, string says) {
        var text = Minimal.Replace(from, to, StringComparison.Ordinal);
        if (says == "holds linear") {
            text = text.Replace("space: linear", "space: srgb", StringComparison.Ordinal);
        }

        var refusal = Assert.Throws<ExportPresetException>(() => ExportPresetReader.Read(text, "test"));

        Assert.Contains(says, refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bc4", 2)]
    [InlineData("bc5", 3)]
    [InlineData("bc6h", 4)]
    public void A_block_format_is_not_given_lanes_it_cannot_hold(string compression, int lanes) {
        var channels = string.Join(", ", Enumerable.Repeat("height", lanes));
        var text = Minimal.Replace(
                "format: png8",
                $"format: ktx2\n    compression: {compression}",
                StringComparison.Ordinal
            )
            .Replace("[roughness]", $"[{channels}]", StringComparison.Ordinal);

        Assert.Throws<ExportPresetException>(() => ExportPresetReader.Read(text, "test"));
    }

    [Theory]
    [InlineData("T_{project}_D", "Old Crate/2", "T_Old_Crate_2_D.png")]
    [InlineData("{preset}/{project}", "a", "unreal/a.png")]
    public void A_project_name_cannot_make_a_folder(string template, string project, string expected) {
        var path = NamingTemplate.Resolve(template, new(project, "unreal"), ".png");

        Assert.Equal(expected.Replace('/', Path.DirectorySeparatorChar), path);
    }

    /// <summary>YAML's other spelling once meant no mips, with no error.</summary>
    [Theory]
    [InlineData("True")]
    [InlineData("yes")]
    public void A_mips_value_it_cannot_read_is_refused(string value) {
        var text = """
                   version: 1
                   id: p
                   name: P
                   normal: y-up
                   textures:
                     - file: x
                       format: ktx2
                       compression: bc7
                       space: srgb
                       mips: VALUE
                       channels: [baseColor.r, baseColor.g, baseColor.b]
                   """.Replace("VALUE", value, StringComparison.Ordinal);

        var refused = Assert.Throws<ExportPresetException>(() => ExportPresetReader.Read(text, "test"));

        Assert.Contains(value, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("curvature", 1)]
    [InlineData("thickness", 1)]
    [InlineData("id", 3)]
    [InlineData("position", 3)]
    [InlineData("world", 3)]
    [InlineData("bent", 3)]
    public void Every_baked_map_is_one_a_preset_can_read(string map, int lanes) {
        var text = $$"""
                     version: 1
                     id: p
                     name: P
                     normal: y-up
                     textures:
                       - file: x
                         format: png8
                         space: linear
                         channels: [{{string.Join(", ", "rgb"[..lanes].Select(lane => $"{map}.{lane}"))}}]
                     """;

        Assert.Equal(map, Assert.Single(Assert.Single(ExportPresetReader.Read(text, "test").Textures).Maps));
        Assert.Equal(map, MapPictures.ExportNameOf(map));
    }

    [Fact]
    public void Every_picture_format_is_one_a_decoder_can_take() {
        var decoded = ImageDecoders.BuiltIn.SelectMany(static d => d.Extensions).Append(".exr").ToHashSet();

        Assert.Subset(decoded, PictureFormats.Pictures.ToHashSet());
        Assert.Subset(PictureFormats.Pictures.ToHashSet(), PictureFormats.Environments.ToHashSet());
    }

    [Theory]
    [InlineData("T_{project}_D", "T_{project}_{set}_D")]
    [InlineData("T_{set}_D", "T_{set}_D")]
    [InlineData("albedo", "albedo_{set}")]
    public void A_file_of_several_sets_always_says_which(string template, string expected) =>
        Assert.Equal(expected, NamingTemplate.PerSet(template));

    [Fact]
    public void A_set_is_a_name_and_a_project_with_none_is_its_own() {
        NamingTemplate.Check("{project}_{set}", "test");

        Assert.Equal("Crate_a_b.png", NamingTemplate.Resolve("{project}_{set}", new("Crate", "p", "a/b"), ".png"));
        Assert.Equal("Crate.png", NamingTemplate.Resolve("{set}", new("Crate", "p"), ".png"));
    }
}
