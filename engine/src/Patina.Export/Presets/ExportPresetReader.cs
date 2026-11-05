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

        return new(id, name, engine, normal, read, Revision.Of(System.Text.Encoding.UTF8.GetBytes(text)).Text, path);
    }

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
                key.Value is "file" or "format" or "compression" or "quality" or "space" or "mips" or "channels",
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

        var quality = Optional(node, "quality") ?? "balanced";
        Require(quality is "fast" or "balanced" or "best", $"{where}: '{quality}' is not a quality.");
        var space = Field(node, "space", where);
        Require(space is "srgb" or "linear", $"{where}: space '{space}' is neither srgb nor linear.");
        var mips = Optional(node, "mips") is "true";
        Require(!mips || container, $"{where}: only ktx2 and dds carry mips.");

        var lanes = Child(node, "channels") as YamlSequenceNode;
        Require(lanes is { Children.Count: >= 1 and <= 4 }, $"{where}: channels must list one to four lanes.");
        List<ChannelSource> channels = [..lanes!.Select(lane => Channel((lane as YamlScalarNode)?.Value, where))];
        CheckShape(format, compression, space, channels.Count, where);
        return new(file, format, compression, quality, space, mips, channels);
    }

    /// <summary>Compressed blocks hold fixed lanes, so a list that does not fit must not be cut to fit.</summary>
    static void CheckShape(string format, string compression, string space, int count, string where) {
        var (least, most) = compression switch {
            ExportCompressions.Bc4 => (1, 1),
            ExportCompressions.Bc5 => (2, 2),
            ExportCompressions.Bc1 or ExportCompressions.Bc6H => (3, 3),
            ExportCompressions.Bc3 or ExportCompressions.Bc7 => (3, 4),
            ExportCompressions.Half or ExportCompressions.Wide or ExportCompressions.None => (1, 4),
            _ => (1, 4)
        };
        Require(count >= least && count <= most, $"{where}: {compression} takes {least} to {most} lanes, not {count}.");

        var plain = format is ExportFormats.Png8 or ExportFormats.Png16;
        Require(!plain || count != 2, $"{where}: a PNG has one, three or four lanes, not two.");

        var floating = compression is ExportCompressions.Bc6H or ExportCompressions.Half or ExportCompressions.Wide
            || format is ExportFormats.Exr16 or ExportFormats.Exr32;
        Require(!(floating && space == "srgb"), $"{where}: {format} {compression} holds linear numbers, not sRGB.");
        Require(
            !(compression is ExportCompressions.Bc4 or ExportCompressions.Bc5 && space == "srgb"),
            $"{where}: {compression} has no sRGB form."
        );
        Require(format != ExportFormats.Png16 || space == "linear", $"{where}: a 16-bit PNG here holds linear numbers.");
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
