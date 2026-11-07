using Patina.Export.Presets;
using Vixen.Core.Imaging;
using Vixen.Core.Imaging.BlockCompression;
using Vixen.Graphics;

namespace Patina.Export.Tests;

public sealed class ChannelPackerTests {
    const int Side = 16;

    static CancellationToken Token => TestContext.Current.CancellationToken;

    static MapPicture Grey(string name, float value, GreenAxis green = GreenAxis.Up, int side = Side) =>
        new(name, new(side, side, 1, [..Enumerable.Repeat(value, side * side)]), green);

    static MapPicture Colour(string name, float r, float g, float b, GreenAxis green = GreenAxis.Up) {
        var values = new float[Side * Side * 3];
        for (var at = 0; at < Side * Side; at++) {
            (values[at * 3], values[at * 3 + 1], values[at * 3 + 2]) = (r, g, b);
        }

        return new(name, new(Side, Side, 3, values), green);
    }

    static Dictionary<string, MapPicture> Maps(params MapPicture[] maps) => maps.ToDictionary(static m => m.Name);

    static ExportTexture Texture(
        string format,
        string space,
        string compression = "none",
        bool mips = false,
        params ChannelSource[] lanes
    ) =>
        new("x", format, compression, "fast", space, mips, lanes);

    static ChannelSource Lane(string map, int lane = 0, bool inverted = false) => new(map, lane, inverted, 0);

    [Fact]
    public void Each_lane_comes_from_its_own_map() {
        var packed = ChannelPacker.Pack(
            Texture("png8", "linear", "none", false, Lane("occlusion"), Lane("roughness"), Lane("metalness")),
            Maps(Grey("occlusion", 0.25f), Grey("roughness", 0.5f), Grey("metalness", 0.75f)),
            GreenAxis.Up
        );

        Assert.Equal([0.25f, 0.5f, 0.75f], packed.Samples[..3]);
    }

    [Fact]
    public void Smoothness_is_one_minus_roughness() {
        var packed = ChannelPacker.Pack(
            Texture("png8", "linear", "none", false, Lane("roughness", 0, true)),
            Maps(Grey("roughness", 0.25f)),
            GreenAxis.Up
        );

        Assert.Equal(0.75f, packed.Samples[0]);
    }

    [Theory]
    [InlineData(GreenAxis.Up, GreenAxis.Up, 0.7f)]
    [InlineData(GreenAxis.Down, GreenAxis.Down, 0.7f)]
    [InlineData(GreenAxis.Up, GreenAxis.Down, 0.3f)]
    [InlineData(GreenAxis.Down, GreenAxis.Up, 0.3f)]
    public void Green_turns_over_only_when_source_and_target_disagree(GreenAxis source, GreenAxis target, float green) {
        var packed = ChannelPacker.Pack(
            Texture("png8", "linear", "none", false, Lane("normal"), Lane("normal", 1), Lane("normal", 2)),
            Maps(Colour("normal", 0.6f, 0.7f, 0.9f, source)),
            target
        );

        Assert.Equal(0.6f, packed.Samples[0], 5);
        Assert.Equal(green, packed.Samples[1], 5);
        Assert.Equal(0.9f, packed.Samples[2], 5);
    }

    [Fact]
    public void Rows_are_never_turned_over() {
        var values = new float[4];
        values[0] = 1f; // Row zero, which is v = 0 in every map.
        var packed = ChannelPacker.Pack(
            Texture("png8", "linear", "none", false, Lane("height")),
            Maps(new MapPicture("height", new(2, 2, 1, values), GreenAxis.Up)),
            GreenAxis.Down
        );

        Assert.Equal([1f, 0f, 0f, 0f], packed.Samples);
    }

    [Fact]
    public void An_srgb_file_of_a_colour_map_keeps_the_numbers_the_viewport_showed() {
        var packed = ChannelPacker.Pack(
            Texture("png8", "srgb", "none", false, Lane("baseColor"), Lane("baseColor", 1), Lane("baseColor", 2)),
            Maps(Colour("baseColor", 0.2f, 0.4f, 0.8f)),
            GreenAxis.Up
        );

        Assert.Equal([0.2f, 0.4f, 0.8f], packed.Samples[..3]);
    }

    [Fact]
    public void A_linear_file_of_a_colour_map_decodes_it_and_alpha_never_moves() {
        var packed = ChannelPacker.Pack(
            Texture("exr32", "linear", "none", false, Lane("baseColor"), Lane("baseColor", 3)),
            Maps(Colour("baseColor", 0.5f, 0f, 0f)),
            GreenAxis.Up
        );

        Assert.Equal(MipChain.ToLinear(0.5f), packed.Samples[0], 5);
        Assert.Equal(1f, packed.Samples[1]);
    }

    /// <summary>All five factory presets end their base colour in <c>opacity.r</c>, which is lane 0 of its map.</summary>
    [Theory]
    [InlineData(0.5f)]
    [InlineData(0.25f)]
    public void Fractional_opacity_in_a_files_alpha_is_never_encoded(float opacity) {
        var packed = ChannelPacker.Pack(
            Texture(
                "png8",
                "srgb",
                "none",
                false,
                Lane("baseColor"),
                Lane("baseColor", 1),
                Lane("baseColor", 2),
                Lane("opacity")
            ),
            Maps(Colour("baseColor", 0.5f, 0.25f, 0f), Grey("opacity", opacity)),
            GreenAxis.Up
        );

        Assert.Equal(opacity, packed.Samples[3]);
    }

    [Fact]
    public void A_factory_preset_keeps_fractional_opacity() {
        var preset = ExportPresetCatalog.Read(null).Find("unreal")!;
        var texture = preset.Textures.First(static t => t.Maps.Contains("opacity"));

        var packed = ChannelPacker.Pack(
            texture,
            Maps(Colour("baseColor", 0.5f, 0.5f, 0.5f), Grey("opacity", 0.5f)),
            preset.Normal
        );

        Assert.Equal(0.5f, packed.Samples[3]);
    }

    /// <summary>An inversion means the value, so it comes before the file's encoding and not after.</summary>
    [Fact]
    public void One_minus_a_map_is_taken_before_the_file_encodes_it() {
        var packed = ChannelPacker.Pack(
            Texture("png8", "srgb", "none", false, Lane("roughness", 0, true)),
            Maps(Grey("roughness", 0.25f)),
            GreenAxis.Up
        );

        Assert.Equal(MipChain.ToSrgb(0.75f), packed.Samples[0], 5);
    }

    [Fact]
    public void A_default_goes_through_the_files_encoding_like_a_map() {
        var packed = ChannelPacker.Pack(
            Texture("png8", "srgb", "none", false, Lane("baseColor"), Lane("roughness")),
            Maps(Grey("baseColor", 0.5f)),
            GreenAxis.Up
        );

        Assert.Equal(MipChain.ToSrgb(ExportMaps.Default("roughness", 0)), packed.Samples[1], 5);
    }

    [Fact]
    public void A_missing_map_fills_its_lane_and_says_so_and_a_texture_of_nothing_is_left_out() {
        var texture = Texture("png8", "srgb", "none", false, Lane("baseColor"), Lane("opacity"));
        var packed = ChannelPacker.Pack(texture, Maps(Grey("baseColor", 0.5f)), GreenAxis.Up);

        Assert.Equal(1f, packed.Samples[1], 5);
        Assert.Contains("no opacity map", Assert.Single(packed.Warnings), StringComparison.Ordinal);
        Assert.False(
            ChannelPacker.Applies(
                Texture("png8", "linear", "none", false, Lane("emissive")),
                Maps(Grey("baseColor", 0.5f))
            )
        );
    }

    [Fact]
    public void A_smaller_map_is_scaled_to_the_largest_and_says_so() {
        var packed = ChannelPacker.Pack(
            Texture("png8", "linear", "none", false, Lane("roughness"), Lane("metalness"), Lane("height")),
            Maps(Grey("roughness", 0.5f), Grey("metalness", 0.25f, GreenAxis.Up, 4), Grey("height", 0.5f)),
            GreenAxis.Up
        );

        Assert.Equal(Side, packed.Width);
        Assert.Equal(0.25f, packed.Samples[1], 5);
        Assert.Contains("metalness was 4×4", Assert.Single(packed.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void A_sixteen_bit_map_comes_out_of_a_sixteen_bit_file_with_every_level() {
        // Levels one apart at sixteen bits, which eight bits would make equal.
        var levels = new ushort[Side * Side];
        for (var at = 0; at < levels.Length; at++) {
            levels[at] = (ushort)(30000 + at);
        }

        using var folder = new TemporaryFolder();
        var source = folder.File("height.png");
        File.WriteAllBytes(source, PngCodec.Encode16(Side, Side, 1, levels));
        var maps = Maps(new MapPicture("height", MapPictures.ReadFile(source), GreenAxis.Up));
        var texture = Texture(ExportFormats.Png16, "linear", "none", false, Lane("height"));

        var (bytes, _) = ExportEncoder.Encode(ChannelPacker.Pack(texture, maps, GreenAxis.Up), texture, Token);

        Assert.Equal(levels, PngCodec.Decode16(bytes).Samples);
    }

    [Theory]
    [InlineData(ExportFormats.Exr32, 0f)]
    [InlineData(ExportFormats.Exr16, 0.01f)]
    public void A_float_map_keeps_values_past_one_and_below_zero(string format, float tolerance) {
        float[] values = [-3.5f, 0.125f, 7.25f, 1234.5f];
        var maps = Maps(new MapPicture("emissive", new(2, 2, 1, values), GreenAxis.Up));
        var texture = Texture(format, "linear", "none", false, Lane("emissive"));

        var (bytes, _) = ExportEncoder.Encode(ChannelPacker.Pack(texture, maps, GreenAxis.Up), texture, Token);

        var decoded = ExrCodec.Decode(bytes).Samples;
        for (var at = 0; at < values.Length; at++) {
            Assert.InRange(MathF.Abs(decoded[at] - values[at]), 0f, tolerance * MathF.Abs(values[at]));
        }
    }

    [Fact]
    public void A_ktx2_holds_a_whole_mip_chain_and_decodes_to_the_source() {
        var maps = Maps(Colour("baseColor", 0.2f, 0.4f, 0.8f));
        var texture = Texture(
            ExportFormats.Ktx2,
            "srgb",
            ExportCompressions.Bc7,
            true,
            Lane("baseColor"),
            Lane("baseColor", 1),
            Lane("baseColor", 2)
        );

        var (bytes, levels) = ExportEncoder.Encode(ChannelPacker.Pack(texture, maps, GreenAxis.Up), texture, Token);

        Assert.Equal(5, levels);
        var read = Ktx2.Read(bytes);
        Assert.Equal(PixelFormat.Bc7RgbaUNormSrgb, read.Format);
        Assert.Equal(5, read.LevelCount);
        var decoded = BlockCompressor.Decode(read);
        Assert.InRange(decoded.Pixels[0], 50, 52);
        Assert.InRange(decoded.Pixels[1], 101, 103);
    }

    [Fact]
    public void A_two_lane_normal_is_turned_over_before_it_is_compressed() {
        var maps = Maps(Colour("normal", 0.5f, 0.75f, 0.9f));
        var texture = Texture(
            ExportFormats.Dds,
            "linear",
            ExportCompressions.Bc5,
            false,
            Lane("normal"),
            Lane("normal", 1)
        );

        var (bytes, _) = ExportEncoder.Encode(ChannelPacker.Pack(texture, maps, GreenAxis.Down), texture, Token);

        var read = DdsCodec.Read(bytes);
        Assert.Equal(PixelFormat.Bc5RgUNorm, read.Format);
        var decoded = BlockCompressor.Decode(read);
        Assert.InRange(decoded.Pixels[1], 62, 66); // 0.25 of 255: the sum of up and down is one.
    }

    [Fact]
    public void An_hdr_map_reaches_bc6h_at_its_own_value() {
        var maps = Maps(Colour("emissive", 8f, 2f, 0.5f));
        var texture = Texture(
            ExportFormats.Ktx2,
            "linear",
            ExportCompressions.Bc6H,
            false,
            Lane("emissive"),
            Lane("emissive", 1),
            Lane("emissive", 2)
        );

        var (bytes, _) = ExportEncoder.Encode(ChannelPacker.Pack(texture, maps, GreenAxis.Up), texture, Token);

        var decoded = BlockCompressor.Decode(Ktx2.Read(bytes));
        var red = (float)BitConverter.ToHalf(decoded.Pixels[..2]);
        Assert.InRange(red, 7.9f, 8.1f);
    }

    sealed class TemporaryFolder : IDisposable {
        readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-pack-");

        public string File(string name) => Path.Combine(folder.FullName, name);

        public void Dispose() => folder.Delete(true);
    }
}
