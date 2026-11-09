using Patina.Library;
using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Patina.Export.Presets;

/// <summary>A preset file that cannot be used, and why, which names the field.</summary>
public sealed class ExportPresetException : Exception {
    public ExportPresetException() { }

    public ExportPresetException(string message) : base(message) { }

    public ExportPresetException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Reads a preset, refusing whatever would silently write the wrong thing into a game.</summary>
public static class ExportPresetReader {
    const int Version = 1;
    const string Lanes = "rgba";

    /// <summary>Reads a preset.</summary>
    /// <param name="text">The file.</param>
    /// <param name="where">What to call it in a refusal.</param>
    /// <param name="path">Where it lives, or null for a factory preset.</param>
    /// <exception cref="ExportPresetException">It is not a preset this can use.</exception>
    public static ExportPreset Read(string text, string where, string? path = null) {
        var root = Parse(text, where);
        var version = Field(root, "version", where);
        if (version != Version.ToString(CultureInfo.InvariantCulture)) {
            throw new ExportPresetException($"{where}: version {version} is not one this reads, which is {Version}.");
        }

        var id = Field(root, "id", where);
        if (id.Length == 0 || !id.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')) {
            throw new ExportPresetException($"{where}: id '{id}' must be lower-case letters, digits and hyphens.");
        }

        var name = Field(root, "name", where);
        var engine = Optional(root, "engine") ?? name;
        var normal = Field(root, "normal", where) switch {
            "y-up" => GreenAxis.Up,
            "y-down" => GreenAxis.Down,
            var other => throw new ExportPresetException($"{where}: normal '{other}' is neither y-up nor y-down.")
        };

        if (Child(root, "textures") is not YamlSequenceNode { Children.Count: > 0 } textures) {
            throw new ExportPresetException($"{where}: it has no textures.");
        }

        List<ExportTexture> read = [];
        foreach (var node in textures) {
            read.Add(ReadTexture(node as YamlMappingNode, $"{where}: texture {read.Count + 1}"));
        }

        var names = read.GroupBy(static t => t.File, StringComparer.Ordinal).FirstOrDefault(static g => g.Count() > 1);
        if (names is not null) {
            throw new ExportPresetException($"{where}: two textures are called '{names.Key}'.");
        }

        var size = Optional(root, "size") is { } sizeText ? Whole(sizeText, "size", where) : (int?)null;
        Require(size is null or (>= 1 and <= MaxSize), $"{where}: size is a side from 1 to {MaxSize}, not {size}.");
        var filter = Optional(root, "filter") ?? ExportFilters.Box;
        Require(
            ExportFilters.All.Contains(filter, StringComparer.Ordinal),
            $"{where}: filter '{filter}' is not one of {string.Join(", ", ExportFilters.All)}."
        );
        var dither = Flag(Optional(root, "dither"), "dither", where) ?? false;
        foreach (var key in root.Children.Keys.OfType<YamlScalarNode>()) {
            Require(
                PresetFields.Contains(key.Value, StringComparer.Ordinal),
                $"{where}: '{key.Value}' is not a field of a preset. The fields are {string.Join(", ", PresetFields)}."
            );
        }

        return new(id, name, engine, normal, read, Revision.Of(System.Text.Encoding.UTF8.GetBytes(text)).Text, path) {
            Padding = ReadPadding(Child(root, "padding"), where),
            Dither = dither,
            Size = size,
            Filter = filter,
            Materials = ReadMaterials(Child(root, "materials"), where)
        };
    }

    /// <summary>The fields a preset has, in the order the preset editor writes them.</summary>
    public static IReadOnlyList<string> PresetFields { get; } = [
        "version", "id", "name", "engine", "normal", "size", "filter", "dither", "padding", "materials", "textures"
    ];

    /// <summary>The fields a texture has, in the order the preset editor writes them.</summary>
    public static IReadOnlyList<string> TextureFields { get; } = [
        "file", "format", "compression", "quality", "space", "mips", "scale", "dither", "padding", "channels"
    ];

    /// <summary>The material files beside the textures: a list of <see cref="ExportMaterials" />, each once.</summary>
    static List<string> ReadMaterials(YamlNode? node, string where) {
        if (node is null) {
            return [];
        }

        if (node is not YamlSequenceNode list) {
            throw new ExportPresetException($"{where}: materials is a list, such as [gltf, usda].");
        }

        List<string> kinds = [];
        foreach (var item in list) {
            var kind = (item as YamlScalarNode)?.Value ?? string.Empty;
            Require(
                ExportMaterials.All.Contains(kind, StringComparer.Ordinal),
                $"{where}: material '{kind}' is not one of {string.Join(", ", ExportMaterials.All)}."
            );
            Require(!kinds.Contains(kind, StringComparer.Ordinal), $"{where}: material '{kind}' is listed twice.");
            kinds.Add(kind);
        }

        return kinds;
    }

    /// <summary>true, false, or null when the field is absent. YAML's other spellings are refused.</summary>
    static bool? Flag(string? text, string field, string where) =>
        text switch {
            null => null,
            "true" => true,
            "false" => false,
            _ => throw new ExportPresetException($"{where}: {field} is true or false, not '{text}'.")
        };

    /// <summary>The largest side a file is made at, which is also the largest a stack paints.</summary>
    public const int MaxSize = 8192;

    /// <summary>The quality a texture has when its preset names none.</summary>
    public static string DefaultQuality(string format) =>
        format == ExportFormats.Jpeg
            ? ExportFormats.DefaultJpegQuality.ToString(CultureInfo.InvariantCulture)
            : "balanced";

    /// <summary>Why a quality is not one a format takes, or null.</summary>
    public static string? QualityProblem(string format, string quality) {
        if (format == ExportFormats.Jpeg) {
            var fits = int.TryParse(quality, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 100;
            return fits ? null : $"a JPEG's quality is 1 to 100, not '{quality}'.";
        }

        return quality is "fast" or "balanced" or "best" ? null : $"'{quality}' is not a quality.";
    }

    /// <summary>Padding: a mode, and for every mode but none and infinite, a distance; a colour for colour.</summary>
    static ExportPadding ReadPadding(YamlNode? node, string where) {
        switch (node) {
            case null:
                return ExportPadding.None;
            case YamlScalarNode scalar:
                return Padding(scalar.Value, null, [], where);
            case YamlMappingNode mapping:
                foreach (var key in mapping.Children.Keys.OfType<YamlScalarNode>()) {
                    Require(
                        key.Value is "mode" or "distance" or "colour",
                        $"{where}: '{key.Value}' is not a field of padding."
                    );
                }

                var colour = Child(mapping, "colour") is YamlSequenceNode lanes
                    ? lanes.Select(lane => Number((lane as YamlScalarNode)?.Value, "padding colour", where)).ToList()
                    : [];
                var distance = Optional(mapping, "distance") is { } text
                    ? Whole(text, "padding distance", where)
                    : (int?)null;
                return Padding(Field(mapping, "mode", where), distance, colour, where);
            default:
                throw new ExportPresetException($"{where}: padding is a mode or a mapping.");
        }
    }

    /// <summary>Checks a padding and fills in its defaults.</summary>
    public static ExportPadding Padding(string? mode, int? distance, IReadOnlyList<float> colour, string where) {
        var parsed = ExportPadding.Parse(mode)
            ?? throw new ExportPresetException(
                $"{where}: padding '{mode}' is not one of {string.Join(", ", ExportPadding.Names)}."
            );
        Require(distance is null or (>= 0 and <= MaxSize), $"{where}: a padding distance is 0 to {MaxSize} texels.");
        Require(colour.Count <= 4, $"{where}: a padding colour has at most four lanes.");
        return new(parsed, distance ?? ExportPadding.DefaultDistance, colour);
    }

    static int Whole(string text, string field, string where) =>
        int.TryParse(text, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new ExportPresetException($"{where}: {field} is a whole number, not '{text}'.");

    static float Number(string? text, string field, string where) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new ExportPresetException($"{where}: {field} is a number, not '{text}'.");

    static YamlMappingNode Parse(string text, string where) {
        var stream = new YamlStream();
        try {
            stream.Load(new StringReader(text));
        } catch (YamlException e) {
            throw new ExportPresetException($"{where}: {e.Message}", e);
        }

        return stream.Documents.FirstOrDefault()?.RootNode as YamlMappingNode
            ?? throw new ExportPresetException($"{where}: it is not a mapping.");
    }

    static ExportTexture ReadTexture(YamlMappingNode? node, string where) {
        if (node is null) {
            throw new ExportPresetException($"{where}: it is not a mapping.");
        }

        // A misspelt key would otherwise quietly mean "uncompressed" in a shipped game.
        foreach (var key in node.Children.Keys.OfType<YamlScalarNode>()) {
            Require(
                TextureFields.Contains(key.Value, StringComparer.Ordinal),
                $"{where}: '{key.Value}' is not a field of a texture."
            );
        }

        var file = Field(node, "file", where);
        NamingTemplate.Check(file, where);

        var format = Field(node, "format", where);
        Require(ExportFormats.All.Contains(format, StringComparer.Ordinal), $"{where}: '{format}' is not a format.");
        var container = format is ExportFormats.Ktx2 or ExportFormats.Dds;
        var compression = Optional(node, "compression") ?? ExportCompressions.None;
        Require(
            ExportCompressions.All.Contains(compression, StringComparer.Ordinal),
            $"{where}: '{compression}' is not a compression."
        );
        Require(container || compression == ExportCompressions.None, $"{where}: {format} cannot be compressed.");

        var quality = Optional(node, "quality") ?? DefaultQuality(format);
        Require(QualityProblem(format, quality) is null, $"{where}: {QualityProblem(format, quality)}");
        var space = Field(node, "space", where);
        Require(space is "srgb" or "linear", $"{where}: space '{space}' is neither srgb nor linear.");
        var mips = Flag(Optional(node, "mips"), "mips", where) ?? false;
        Require(!mips || container, $"{where}: only ktx2 and dds carry mips.");
        var scale = Optional(node, "scale") is { } scaleText ? Number(scaleText, "scale", where) : 1f;
        Require(
            ExportTexture.Scales.Contains(scale),
            $"{where}: scale is one of {string.Join(", ", ExportTexture.Scales.Select(Text))}, not {Text(scale)}."
        );

        var lanes = Child(node, "channels") as YamlSequenceNode;
        Require(lanes is { Children.Count: >= 1 and <= 4 }, $"{where}: channels must list one to four lanes.");
        List<ChannelSource> channels = [..lanes!.Select(lane => Channel((lane as YamlScalarNode)?.Value, where))];
        if (ShapeProblem(format, compression, space, channels.Count) is { } problem) {
            throw new ExportPresetException($"{where}: {problem}");
        }

        return new(file, format, compression, quality, space, mips, channels) {
            Scale = scale,
            Dither = Flag(Optional(node, "dither"), "dither", where),
            Padding = Child(node, "padding") is { } padding ? ReadPadding(padding, where) : null
        };
    }

    static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Why a file of this shape cannot be written, or null. Compressed blocks hold fixed lanes, and a file format
    ///     holds what it holds, so a list that does not fit must not be cut to fit.
    /// </summary>
    public static string? ShapeProblem(string format, string compression, string space, int count) =>
        LaneProblem(format, compression, count) ?? SpaceProblem(format, compression, space);

    /// <summary>Why a file cannot hold this many lanes, or null.</summary>
    static string? LaneProblem(string format, string compression, int count) {
        var (least, most) = compression switch {
            ExportCompressions.Bc4 => (1, 1),
            ExportCompressions.Bc5 => (2, 2),
            ExportCompressions.Bc1 or ExportCompressions.Bc6H => (3, 3),
            ExportCompressions.Bc3 or ExportCompressions.Bc7 => (3, 4),

            _ => (1, 4)
        };
        if (count < least || count > most) {
            return $"{compression} takes {least} to {most} lanes, not {count}.";
        }

        if (!ExportFormats.IsContainer(format) && compression != ExportCompressions.None) {
            return $"{format} cannot be compressed.";
        }

        var lanes = ExportFormats.LanesOf(format);
        return lanes.Contains(count)
            ? null
            : $"{ExportFormats.LabelOf(format)} holds {string.Join(", ", lanes)} lanes, not {count}.";
    }

    /// <summary>Why a file cannot hold numbers of this space, or null.</summary>
    static string? SpaceProblem(string format, string compression, string space) {
        var floating = compression is ExportCompressions.Bc6H or ExportCompressions.Half or ExportCompressions.Wide
            || ExportFormats.IsFloat(format);
        if (floating && space == "srgb") {
            return $"{format} {compression} holds linear numbers, not sRGB.";
        }

        if (compression is ExportCompressions.Bc4 or ExportCompressions.Bc5 && space == "srgb") {
            return $"{compression} has no sRGB form.";
        }

        return ExportFormats.IsWide(format) && space != "linear"
            ? $"a 16-bit {ExportFormats.LabelOf(format)[..4].Trim()} here holds linear numbers."
            : null;
    }


    static ChannelSource Channel(string? text, string where) {
        if (string.IsNullOrWhiteSpace(text)) {
            throw new ExportPresetException($"{where}: a lane is empty.");
        }

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var constant)) {
            return new(null, 0, false, constant);
        }

        var inverted = text.StartsWith("1-", StringComparison.Ordinal);
        var source = inverted ? text[2..] : text;
        var dot = source.IndexOf('.', StringComparison.Ordinal);
        var map = dot < 0 ? source : source[..dot];
        var lane = dot < 0 ? 0 : source.Length == dot + 2 ? Lanes.IndexOf(source[dot + 1]) : -1;
        if (!ExportMaps.All.Contains(map, StringComparer.Ordinal)) {
            throw new ExportPresetException(
                $"{where}: '{map}' is not a map. The maps are {string.Join(", ", ExportMaps.All)}."
            );
        }

        return lane >= 0
            ? new(map, lane, inverted, 0)
            : throw new ExportPresetException($"{where}: '{text}' names no lane of r, g, b or a.");
    }

    static YamlNode? Child(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

    static string? Optional(YamlMappingNode node, string key) => (Child(node, key) as YamlScalarNode)?.Value;

    static string Field(YamlMappingNode node, string key, string where) =>
        Optional(node, key) ?? throw new ExportPresetException($"{where}: it has no {key}.");

    static void Require(bool condition, string message) {
        if (!condition) {
            throw new ExportPresetException(message);
        }
    }
}
