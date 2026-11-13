using Vixen.Core.Imaging;

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
/// <param name="Quality">How hard BC7 and BC6H search: fast, balanced or best. For JPEG, 1 to 100.</param>
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

    /// <summary>
    ///     Its side as a share of the side every other file is written at: one, or a half, a quarter, an eighth or a
    ///     sixteenth, so that a file the eye reads less of costs less without breaking a power of two.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <summary>Whether this file is dithered, or null for what the preset and the export say.</summary>
    public bool? Dither { get; init; }

    /// <summary>What fills this file's background, or null for what the preset and the export say.</summary>
    public ExportPadding? Padding { get; init; }

    /// <summary>The scales a file can take, largest first.</summary>
    public static IReadOnlyList<double> Scales { get; } = [1, 0.5, 0.25, 0.125, 0.0625];
}

/// <summary>What fills the texels a mesh's layout does not cover, before a file is written.</summary>
/// <param name="Mode">None, grow without end, or grow by <paramref name="Distance" /> and fill the rest.</param>
/// <param name="Distance">How many texels the islands grow by, for every mode but infinite.</param>
/// <param name="Colour">For <see cref="PaddingMode.Colour" />, the value of each lane of the file.</param>
public sealed record ExportPadding(PaddingMode Mode, int Distance, IReadOnlyList<float> Colour) {
    /// <summary>The distance a mode that needs one gets when the preset gives none.</summary>
    public const int DefaultDistance = 16;

    /// <summary>No padding: the file holds what the maps held.</summary>
    public static ExportPadding None { get; } = new(PaddingMode.None, 0, []);

    static readonly string[] names = ["none", "infinite", "transparent", "colour", "diffusion"];

    /// <summary>The names a preset and the API use, in <see cref="PaddingMode" />'s order.</summary>
    public static IReadOnlyList<string> Names => names;

    /// <summary>A mode's name in a preset and on the wire.</summary>
    public static string NameOf(PaddingMode mode) => names[(int)mode];

    /// <summary>The mode a name names, or null.</summary>
    public static PaddingMode? Parse(string? name) =>
        Array.IndexOf(names, name) is var at and >= 0 ? (PaddingMode)at : null;
}

/// <summary>How a file is made smaller or larger than its maps.</summary>
public static class ExportFilters {
    public const string Box = "box";
    public const string Bilinear = "bilinear";
    public const string Nearest = "nearest";

    public static IReadOnlyList<string> All { get; } = [Box, Bilinear, Nearest];

    /// <summary>The filter a name names.</summary>
    public static ResizeFilter Of(string name) =>
        name switch {
            Bilinear => ResizeFilter.Bilinear,
            Nearest => ResizeFilter.Nearest,
            _ => ResizeFilter.Box
        };
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
) {
    /// <summary>What fills the background of a mesh's layout.</summary>
    public ExportPadding Padding { get; init; } = ExportPadding.None;

    /// <summary>Whether a value between two 8-bit levels is dithered rather than rounded, against banding.</summary>
    public bool Dither { get; init; }

    /// <summary>The longest side of every file, or null for the size of the maps.</summary>
    public int? Size { get; init; }

    /// <summary>How a file changes size: one of <see cref="ExportFilters" />.</summary>
    public string Filter { get; init; } = ExportFilters.Box;

    /// <summary>The texture sets exported, or null for all of them.</summary>
    public IReadOnlyList<string>? Sets { get; init; }

    /// <summary>
    ///     Whether the sets exported are written as one set of files, each set where its layout puts it
    ///     (<see cref="SetMerge" />).
    /// </summary>
    public bool Merge { get; init; }

    /// <summary>The material files written beside the textures: any of <see cref="ExportMaterials" />.</summary>
    public IReadOnlyList<string> Materials { get; init; } = [];

    /// <summary>One set's changes over the whole export's, by set name (<c>ExportOptions.PerSet</c>).</summary>
    public IReadOnlyDictionary<string, ExportPreset> PerSet { get; init; } =
        new Dictionary<string, ExportPreset>(StringComparer.Ordinal);

    /// <summary>
    ///     Where it was read from: factory, library or project (<see cref="ExportPresetCatalog.Origins" />).
    /// </summary>
    public string Origin { get; init; } = ExportPresetCatalog.Origins.Factory;

    /// <summary>The preset a set is written with: its own changes, or this.</summary>
    public ExportPreset For(string set) => PerSet.GetValueOrDefault(set) ?? this;
}

/// <summary>The material files a preset can write beside its textures, which name them by their slots.</summary>
public static class ExportMaterials {
    /// <summary>A glTF 2.0 file holding only materials, images and textures.</summary>
    public const string Gltf = "gltf";

    /// <summary>A USD layer in text, with a UsdPreviewSurface material.</summary>
    public const string Usd = "usda";

    /// <summary>A MaterialX document with a standard_surface material.</summary>
    public const string MaterialX = "mtlx";

    public static IReadOnlyList<string> All { get; } = [Gltf, Usd, MaterialX];

    /// <summary>A material file's name for a person.</summary>
    public static string LabelOf(string kind) =>
        kind switch {
            Gltf => "glTF material",
            Usd => "USD preview surface",
            MaterialX => "MaterialX standard surface",
            _ => kind
        };
}

/// <summary>The file formats a preset can name.</summary>
public static class ExportFormats {
    public const string Png8 = "png8";
    public const string Png16 = "png16";
    public const string Exr16 = "exr16";
    public const string Exr32 = "exr32";
    public const string Ktx2 = "ktx2";
    public const string Dds = "dds";
    public const string Tga = "tga";
    public const string Jpeg = "jpeg";
    public const string Tiff8 = "tiff8";
    public const string Tiff16 = "tiff16";
    public const string Tiff32 = "tiff32";
    public const string WebP = "webp";
    public const string Hdr = "hdr";

    /// <summary>The JPEG quality a preset that names none gets.</summary>
    public const int DefaultJpegQuality = 90;

    public static IReadOnlyList<string> All { get; } =
        [Png8, Png16, Tga, Jpeg, Tiff8, Tiff16, Tiff32, WebP, Exr16, Exr32, Hdr, Ktx2, Dds];

    /// <summary>What a format's files end in.</summary>
    public static string ExtensionOf(string format) =>
        format switch {
            Png8 or Png16 => ".png",
            Exr16 or Exr32 => ".exr",
            Ktx2 => ".ktx2",
            Dds => ".dds",
            Tga => ".tga",
            Jpeg => ".jpg",
            Tiff8 or Tiff16 or Tiff32 => ".tif",
            WebP => ".webp",
            Hdr => ".hdr",
            _ => throw new ArgumentException($"'{format}' is not a format.", nameof(format))
        };

    /// <summary>A format's name for a person.</summary>
    public static string LabelOf(string format) =>
        format switch {
            Png8 => "PNG 8-bit",
            Png16 => "PNG 16-bit",
            Tga => "TGA 8-bit",
            Jpeg => "JPEG 8-bit",
            Tiff8 => "TIFF 8-bit",
            Tiff16 => "TIFF 16-bit",
            Tiff32 => "TIFF 32-bit float",
            WebP => "WebP lossless",
            Exr16 => "OpenEXR 16-bit float",
            Exr32 => "OpenEXR 32-bit float",
            Hdr => "Radiance HDR",
            Ktx2 => "KTX2",
            Dds => "DDS",
            _ => format
        };

    /// <summary>How many lanes a file of a format can hold.</summary>
    public static IReadOnlyList<int> LanesOf(string format) =>
        format switch {
            Png8 or Png16 or Tga or WebP => [1, 3, 4],
            Jpeg or Hdr => [1, 3],
            _ => [1, 2, 3, 4]
        };

    /// <summary>Whether a format holds float numbers, which are linear.</summary>
    public static bool IsFloat(string format) => format is Exr16 or Exr32 or Tiff32 or Hdr;

    /// <summary>Whether a format holds 16-bit integers, which here are linear.</summary>
    public static bool IsWide(string format) => format is Png16 or Tiff16;

    /// <summary>Whether a format's samples are eight bits, which is where dithering applies.</summary>
    public static bool IsEightBit(string format) => format is Png8 or Tga or Jpeg or Tiff8 or WebP;

    /// <summary>Whether a format is a GPU container, the only kind with mips and block compression.</summary>
    public static bool IsContainer(string format) => format is Ktx2 or Dds;
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

    /// <summary>A compression's name for a person.</summary>
    public static string LabelOf(string compression) =>
        compression switch {
            None => "None",
            Half => "Half float",
            Wide => "Float",
            Bc1 => "BC1 (RGB)",
            Bc3 => "BC3 (RGBA)",
            Bc4 => "BC4 (one lane)",
            Bc5 => "BC5 (two lanes)",
            Bc6H => "BC6H (HDR)",
            Bc7 => "BC7",
            _ => compression
        };
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

    // Between the two groups, as in ExportCompressions: SK7020 honours no suppression, and reads fifteen constants in a
    // row as a clone of IpcTypes.
    public static IReadOnlyList<string> All { get; } = [
        BaseColor, Normal, Roughness, Metalness, Occlusion, Height, Emissive, Opacity, Mask, Curvature, Thickness, Id,
        Position, WorldNormal, BentNormal, Coverage, Specular, Diffuse, Glossiness
    ];

    /// <summary>
    ///     The maps a preset can read that no output makes: each is worked out from others when a file reads it
    ///     (<see cref="DerivedMaps" />), so a specular-glossiness file comes from a metal-roughness project.
    /// </summary>
    public static IReadOnlyList<string> Derived { get; } = [Specular, Diffuse, Glossiness];

    /// <summary>
    ///     The colour at normal incidence: four per cent for a dielectric, the base colour for a metal. sRGB.
    /// </summary>
    public const string Specular = "specular";

    /// <summary>The colour a dielectric scatters: the base colour, black where it is metal. sRGB.</summary>
    public const string Diffuse = "diffuse";

    /// <summary>One minus roughness.</summary>
    public const string Glossiness = "glossiness";

    // The maps only a bake makes, under the names the bake gives them.
    public const string Curvature = "curvature";
    public const string Thickness = "thickness";
    public const string Id = "id";
    public const string Position = "position";
    public const string WorldNormal = "world";
    public const string BentNormal = "bent";

    /// <summary>
    ///     Which texels a mesh's layout covers, one where it does: what a stack on a mesh makes beside its maps, and
    ///     what padding grows from.
    /// </summary>
    public const string Coverage = "coverage";

    /// <summary>
    ///     A lane's value where a project has no such map: what a surface without that map would be. A texture that
    ///     reads no map a project has is not written at all; these fill the lanes of one it does write.
    /// </summary>
    public static float Default(string map, int lane) =>
        (map, lane) switch {
            (Normal or WorldNormal or BentNormal, 2) => 1f,
            (Normal or WorldNormal or BentNormal, not 3) => 0.5f,
            (Opacity or Occlusion or Mask or Coverage, _) => 1f,
            (_, 3) => 1f,
            (BaseColor or Roughness or Height or Curvature or Position or Diffuse or Glossiness, _) => 0.5f,
            (Specular, _) => DerivedMaps.DielectricSrgb,
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
    public static bool IsColour(string map) => map is BaseColor or Specular or Diffuse;
}
