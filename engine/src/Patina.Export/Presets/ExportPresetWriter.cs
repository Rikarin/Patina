using Patina.Contracts;
using Patina.Library;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vixen.Core.Imaging;

namespace Patina.Export.Presets;

/// <summary>
///     A preset as a document a client edits, and back to the file (ADR 0019). The file is the truth: a document is
///     written as the language spells it, read back by <see cref="ExportPresetReader" />, and kept only if that reads
///     it, so the editor can make nothing a hand-written file could not say. A hand-written file's comments above its
///     fields and textures survive an edit in the editor (<see cref="PresetComments" />); one inside a texture does not.
/// </summary>
public static partial class ExportPresetWriter {
    const string Header =
        "# An export preset (docs/adr/0019-export-presets.md), written by Patina's preset editor. Edit it by hand too.";

    /// <summary>What a preset's file says, as a document.</summary>
    public static ExportPresetDocument DocumentOf(ExportPreset preset) {
        ArgumentNullException.ThrowIfNull(preset);
        return new() {
            Id = preset.Id,
            Name = preset.Name,
            Engine = preset.Engine,
            Normal = preset.Normal == GreenAxis.Up ? "y-up" : "y-down",
            Size = preset.Size,
            Filter = preset.Filter,
            Dither = preset.Dither,
            Padding = Info(preset.Padding),
            Materials = preset.Materials,
            Textures = [
                ..preset.Textures.Select(static texture => new ExportPresetTextureDocument {
                        File = texture.File,
                        Format = texture.Format,
                        Space = texture.Space,
                        Compression = texture.Compression,
                        Quality = texture.Quality == ExportPresetReader.DefaultQuality(texture.Format)
                            ? null
                            : texture.Quality,
                        Mips = texture.Mips,
                        Scale = texture.Scale,
                        Dither = texture.Dither,
                        Padding = texture.Padding is { } padding ? Info(padding) : null,
                        Channels = [..texture.Channels.Select(Lane)]
                    }
                )
            ]
        };
    }

    /// <summary>A lane as the language writes it: <c>roughness.r</c>, <c>1-roughness.r</c> or a number.</summary>
    public static string Lane(ChannelSource source) =>
        source.Map is { } map
            ? $"{(source.Inverted ? "1-" : string.Empty)}{map}.{"rgba"[source.Lane]}"
            : source.Constant.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    ///     The file a document is, fields in the order <see cref="ExportPresetReader.PresetFields" /> gives.
    /// </summary>
    public static string Text(ExportPresetDocument document) {
        ArgumentNullException.ThrowIfNull(document);
        var text = new StringBuilder();
        text.Append(Header).Append('\n');
        text.Append("version: 1\n");
        Line(text, 0, "id", Quote(document.Id));
        Line(text, 0, "name", Quote(document.Name));
        Optional(text, 0, "engine", document.Engine.Length > 0 ? Quote(document.Engine) : null);
        Line(text, 0, "normal", Quote(document.Normal));
        Optional(text, 0, "size", document.Size?.ToString(CultureInfo.InvariantCulture));
        Optional(text, 0, "filter", document.Filter != ExportFilters.Box ? Quote(document.Filter) : null);
        Optional(text, 0, "dither", document.Dither ? "true" : null);
        Padding(text, 0, document.Padding);
        Optional(text, 0, "materials", document.Materials.Count > 0 ? List(document.Materials) : null);
        text.Append("textures:\n");
        foreach (var texture in document.Textures ?? []) {
            Texture(text, texture);
        }

        return text.ToString();
    }

    static void Texture(StringBuilder text, ExportPresetTextureDocument texture) {
        Line(text, 2, "- file", Quote(texture.File));
        Line(text, 4, "format", Quote(texture.Format));
        Optional(
            text,
            4,
            "compression",
            texture.Compression != ExportCompressions.None ? Quote(texture.Compression) : null
        );
        Optional(text, 4, "quality", texture.Quality is { } quality ? Quote(quality) : null);
        Line(text, 4, "space", Quote(texture.Space));
        Optional(text, 4, "mips", texture.Mips ? "true" : null);
        Optional(
            text,
            4,
            "scale",
            texture.Scale != 1 ? texture.Scale.ToString("R", CultureInfo.InvariantCulture) : null
        );
        Optional(
            text,
            4,
            "dither",
            texture.Dither switch {
                true => "true",
                false => "false",
                null => null
            }
        );
        if (texture.Padding is { } padding) {
            Padding(text, 4, padding);
        }

        Line(text, 4, "channels", List(texture.Channels ?? []));
    }

    static void Optional(StringBuilder text, int indent, string key, string? value) {
        if (value is not null) {
            Line(text, indent, key, value);
        }
    }

    /// <summary>
    ///     A document as a preset, through its file, so that a document the file could not hold is refused for the
    ///     same reason a hand-written file would be.
    /// </summary>
    /// <exception cref="ExportPresetException">It is not a preset; the message says why.</exception>
    public static ExportPreset Check(ExportPresetDocument document) =>
        ExportPresetReader.Read(Text(document), $"Preset '{document?.Id}'");

    /// <summary>
    ///     Keeps a document as a file in a folder of presets, under its id, replacing one of the same id there. A
    ///     preset named by <paramref name="replaces" /> in that folder is removed after, which is a rename.
    /// </summary>
    /// <returns>The file.</returns>
    /// <exception cref="ExportPresetException">It is not a preset, or what it replaces is not there.</exception>
    public static string Save(string folder, ExportPresetDocument document, string? replaces = null) {
        ArgumentNullException.ThrowIfNull(document);
        Check(document);
        var old = replaces is null || replaces == document.Id ? null : FileOf(folder, replaces);
        if (old is not null && !File.Exists(old)) {
            throw new ExportPresetException($"There is no preset '{replaces}' here to rename.");
        }

        var file = FileOf(folder, document.Id);
        // The comments of the file this one takes the place of: the one renamed, or the one of its id.
        var before = new[] { old, file }.FirstOrDefault(File.Exists);
        var text = Text(document);
        LibraryFiles.Replace(file, before is null ? text : PresetComments.Of(File.ReadAllText(before)).Into(text));
        if (old is not null) {
            File.Delete(old);
        }

        return file;
    }

    /// <summary>The file a preset of an id is in a folder of presets.</summary>
    /// <exception cref="ExportPresetException">The id is not one a preset can have.</exception>
    public static string FileOf(string folder, string id) {
        ArgumentNullException.ThrowIfNull(id);
        return IdPattern().IsMatch(id)
            ? Path.Combine(folder, $"{id}.yaml")
            : throw new ExportPresetException($"'{id}' is not an id: lower-case letters, digits and hyphens.");
    }

    static ExportPaddingInfo Info(ExportPadding padding) =>
        new(ExportPadding.NameOf(padding.Mode), padding.Distance, padding.Colour);

    static void Padding(StringBuilder text, int indent, ExportPaddingInfo padding) {
        var mode = ExportPadding.Parse(padding.Mode);
        if (mode is null or PaddingMode.None && indent == 0) {
            return;
        }

        if (mode is PaddingMode.None or PaddingMode.Infinite || mode is null) {
            Line(text, indent, "padding", Quote(padding.Mode));
            return;
        }

        text.Append(' ', indent).Append("padding:\n");
        Line(text, indent + 2, "mode", Quote(padding.Mode));
        Line(text, indent + 2, "distance", padding.Distance.ToString(CultureInfo.InvariantCulture));
        if (mode == PaddingMode.Colour) {
            Line(
                text,
                indent + 2,
                "colour",
                "[" + string.Join(", ", padding.Colour.Select(Number)) + "]"
            );
        }
    }

    static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    static void Line(StringBuilder text, int indent, string key, string value) =>
        text.Append(' ', indent).Append(key).Append(": ").Append(value).Append('\n');

    static string List(IEnumerable<string> values) => $"[{string.Join(", ", values.Select(Quote))}]";

    /// <summary>
    ///     A value plain when it is a word the language reads as text, and in double quotes otherwise, which YAML reads
    ///     as JSON does: so a client's text can never become a key or a list.
    /// </summary>
    static string Quote(string value) =>
        Plain().IsMatch(value) && value is not ("true" or "false" or "null" or "yes" or "no")
            ? value
            : "\"" + JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\"";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.\\-]*$")]
    private static partial Regex Plain();

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex IdPattern();
}
