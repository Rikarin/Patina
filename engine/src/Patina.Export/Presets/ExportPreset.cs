namespace Patina.Export.Presets;

/// <summary>
///     Which way a tangent-space normal's green runs in a picture, as an engine reads it or a source wrote
///     it.
/// </summary>
public enum GreenAxis {
    /// <summary>More green points up the image: Y+, often called OpenGL.</summary>
    Up,

    /// <summary>More green points down the image: Y-, often called DirectX.</summary>
    Down
}

/// <summary>What fills one lane of an exported file.</summary>
/// <param name="Map">The map it reads, or null for a constant.</param>
/// <param name="Lane">The map's lane, 0 to 3.</param>
/// <param name="Inverted">Whether the value is one minus the map's.</param>
/// <param name="Constant">The value, when no map is read.</param>
public readonly record struct ChannelSource(string? Map, int Lane, bool Inverted, float Constant);

/// <summary>One file a preset makes.</summary>
/// <param name="File">Its naming template, without an extension.</param>
/// <param name="Format">One of <see cref="ExportFormats" />.</param>
/// <param name="Compression">One of <see cref="ExportCompressions" />.</param>
/// <param name="Quality">How hard BC7 and BC6H search: fast, balanced or best.</param>
/// <param name="Space">What the file's numbers are: srgb or linear.</param>
/// <param name="Mips">Whether it carries a full mip chain.</param>
/// <param name="Channels">One to four lanes, in file order.</param>
public sealed record ExportTexture(
    string File,
    string Format,
    string Compression,
    string Quality,
    string Space,
    bool Mips,
    IReadOnlyList<ChannelSource> Channels
) {
    /// <summary>The maps it reads.</summary>
    public IEnumerable<string> Maps =>
        Channels.Where(static c => c.Map is not null).Select(static c => c.Map!).Distinct(StringComparer.Ordinal);
}

/// <summary>A preset: data, read from a file, so that adding an engine needs no build of Patina.</summary>
/// <param name="Id">Its name in <c>library.yaml</c> and on the command line.</param>
/// <param name="Name">Its label.</param>
/// <param name="Engine">What it is for.</param>
/// <param name="Normal">The green the target reads.</param>
/// <param name="Textures">The files.</param>
/// <param name="Revision">The hash of the file, which is what its results are keyed on.</param>
/// <param name="Path">Where a library's preset was read from, or null for a factory one.</param>
public sealed record ExportPreset(
    string Id,
    string Name,
    string Engine,
    GreenAxis Normal,
    IReadOnlyList<ExportTexture> Textures,
    string Revision,
    string? Path
);

/// <summary>The file formats a preset can name.</summary>
public static class ExportFormats {
    public const string Png8 = "png8";
    public const string Png16 = "png16";
    public const string Exr16 = "exr16";
    public const string Exr32 = "exr32";
    public const string Ktx2 = "ktx2";
    public const string Dds = "dds";

    public static IReadOnlyList<string> All { get; } = [Png8, Png16, Exr16, Exr32, Ktx2, Dds];

    /// <summary>What a format's files end in.</summary>
    public static string ExtensionOf(string format) =>
        format switch {
            Png8 or Png16 => ".png",
            Exr16 or Exr32 => ".exr",
            Ktx2 => ".ktx2",
            Dds => ".dds",
            _ => throw new ArgumentException($"'{format}' is not a format.", nameof(format))
        };
}

/// <summary>What a KTX2 or DDS file can hold.</summary>
public static class ExportCompressions {
    public const string None = "none";
    public const string Half = "half";
    public const string Wide = "float";

    public static IReadOnlyList<string> All { get; } = [None, Half, Wide, Bc1, Bc3, Bc4, Bc5, Bc6H, Bc7];

    public const string Bc1 = "bc1";
    public const string Bc3 = "bc3";
    public const string Bc4 = "bc4";
    public const string Bc5 = "bc5";
    public const string Bc6H = "bc6h";
    public const string Bc7 = "bc7";
}

/// <summary>
///     The maps a preset can read: a graph's Output usages, which is also what a bake's maps are mapped
///     to.
/// </summary>
public static class ExportMaps {
    public const string BaseColor = "baseColor";
    public const string Normal = "normal";
    public const string Roughness = "roughness";
    public const string Metalness = "metalness";
    public const string Occlusion = "occlusion";
    public const string Height = "height";
    public const string Emissive = "emissive";
    public const string Opacity = "opacity";
    public const string Mask = "mask";

    public static IReadOnlyList<string> All { get; } =
        [BaseColor, Normal, Roughness, Metalness, Occlusion, Height, Emissive, Opacity, Mask];

    /// <summary>
    ///     A lane's value where a project has no such map: what a surface without that map would be. A texture that
    ///     reads no map a project has is not written at all; these fill the lanes of one it does write.
    /// </summary>
    public static float Default(string map, int lane) =>
        (map, lane) switch {
            (Normal, 2) => 1f,
            (Normal, _) => 0.5f,
            (Opacity or Occlusion or Mask, _) => 1f,
            (_, 3) => 1f,
            (BaseColor or Roughness or Height, _) => 0.5f,
            _ => 0f
        };

    /// <summary>Whether a map's numbers are sRGB-encoded rather than linear.</summary>
    /// <remarks>
    ///     ⚠ Only base colour. Vixen's kernels apply no transfer function, its material bake puts a graph's bytes
    ///     straight into an sRGB texture, and Patina's viewport reads them as sRGB, so those numbers are sRGB at any
    ///     depth, whatever a comment in <c>TexturePixels</c> says of linear, and exporting them unchanged is what the
    ///     viewport showed. Emissive is radiance, which goes past one, and no viewport shows it yet, so its numbers are
    ///     linear.
    /// </remarks>
    public static bool IsColour(string map) => map == BaseColor;
}
