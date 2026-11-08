using Patina.Contracts;
using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     The <c>export</c> block of a <c>project.yaml</c>: what the project's last export was set to. Written
///     canonically, as the rest of the document is: fields in a fixed order, absent ones left out, files sorted by
///     preset and template, so saving the same settings changes no byte.
/// </summary>
static class ExportCodec {
    const string PresetKey = "preset";
    const string FolderKey = "folder";
    const string SizeKey = "size";
    const string FilterKey = "filter";
    const string PaddingKey = "padding";
    const string ModeKey = "mode";
    const string DistanceKey = "distance";
    const string ColourKey = "colour";
    const string DitherKey = "dither";
    const string SkippedKey = "skipped";
    const string FilesKey = "files";
    const string TemplateKey = "template";
    const string EnabledKey = "enabled";
    const string FormatKey = "format";

    public static YamlMappingNode Write(ProjectExportSettings settings) {
        List<KeyValuePair<YamlNode, YamlNode>> pairs = [];
        Add(pairs, PresetKey, settings.Preset);
        Add(pairs, FolderKey, settings.Folder);
        if (settings.Size is { } size) {
            pairs.Add(Pair(SizeKey, YamlText.Plain(size)));
        }

        Add(pairs, FilterKey, settings.Filter);
        if (settings.Padding is { } padding) {
            pairs.Add(
                Pair(
                    PaddingKey,
                    new YamlMappingNode(
                        Pair(ModeKey, YamlText.Text(padding.Mode)),
                        Pair(DistanceKey, YamlText.Plain(padding.Distance)),
                        Pair(
                            ColourKey,
                            new YamlSequenceNode(
                                padding.Colour.Select(static c =>
                                    YamlText.Plain(c.ToString("R", CultureInfo.InvariantCulture))
                                )
                            )
                        )
                    )
                )
            );
        }

        if (settings.Dither is { } dither) {
            pairs.Add(Pair(DitherKey, YamlText.Plain(dither ? "true" : "false")));
        }

        pairs.Add(
            Pair(
                SkippedKey,
                YamlText.Texts(settings.Skipped.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            )
        );
        pairs.Add(
            Pair(
                FilesKey,
                new YamlSequenceNode(
                    settings.Files.OrderBy(static f => f.Preset, StringComparer.Ordinal)
                        .ThenBy(static f => f.Template, StringComparer.Ordinal)
                        .Select(static f => new YamlMappingNode(
                                Pair(PresetKey, YamlText.Text(f.Preset)),
                                Pair(TemplateKey, YamlText.Text(f.Template)),
                                Pair(EnabledKey, YamlText.Plain(f.Enabled ? "true" : "false")),
                                Pair(FormatKey, YamlText.Text(f.Format))
                            )
                        )
                )
            )
        );
        return new(pairs);
    }

    public static ProjectExportSettings Read(YamlFields fields) =>
        new() {
            Preset = fields.Optional(PresetKey),
            Folder = fields.Optional(FolderKey),
            Size = fields.Optional(SizeKey) is { } size ? Whole(fields, SizeKey, size) : null,
            Filter = fields.Optional(FilterKey),
            Padding = fields.Map(PaddingKey) is { } padding ? ReadPadding(padding) : null,
            Dither = fields.Optional(DitherKey) is { } dither ? Truth(fields, DitherKey, dither) : null,
            Skipped = fields.Texts(SkippedKey),
            Files = [
                ..fields.Maps(FilesKey)
                    .Select(static file => new ExportFileSetting(
                            file.Require(PresetKey),
                            file.Require(TemplateKey),
                            Truth(file, EnabledKey, file.Require(EnabledKey)),
                            file.Require(FormatKey)
                        )
                    )
            ]
        };

    static ExportPaddingInfo ReadPadding(YamlFields padding) =>
        new(
            padding.Require(ModeKey),
            (int)padding.Number(DistanceKey, 0),
            [
                ..padding.Texts(ColourKey)
                    .Select(lane => float.TryParse(
                            lane,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var value
                        )
                            ? value
                            : throw padding.Problem($"a padding colour lane is a number, not '{lane}'.")
                    )
            ]
        );

    static int Whole(YamlFields fields, string key, string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw fields.Problem($"'export.{key}' is not a whole number.");

    static bool Truth(YamlFields fields, string key, string text) =>
        text switch {
            "true" => true,
            "false" => false,
            _ => throw fields.Problem($"'{key}' is true or false, not '{text}'.")
        };

    static void Add(List<KeyValuePair<YamlNode, YamlNode>> pairs, string key, string? text) {
        if (text is not null) {
            pairs.Add(Pair(key, YamlText.Text(text)));
        }
    }

    static KeyValuePair<YamlNode, YamlNode> Pair(string key, YamlNode value) => new(new YamlScalarNode(key), value);
}
