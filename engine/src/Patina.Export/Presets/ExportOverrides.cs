using Patina.Contracts;
using System.Text.Json;

namespace Patina.Export.Presets;

/// <summary>
///     A preset with one export's changes: another size, padding or format, files left out, sets chosen. The result is
///     a preset like any other, so everything after this reads one thing, and its revision moves with the changes, so
///     an export made under other settings is never taken for this one.
/// </summary>
public static class ExportOverrides {
    /// <summary>The preset with the changes made.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="options">The changes, or null for none.</param>
    /// <exception cref="ExportPresetException">A change the preset cannot take; the message says which.</exception>
    public static ExportPreset Apply(ExportPreset preset, ExportOptions? options) {
        ArgumentNullException.ThrowIfNull(preset);
        if (options is null) {
            return preset;
        }

        var where = $"Export through '{preset.Name}'";
        var textures = Textures(preset, options.Files ?? [], where);
        if (options.Size is { } size && size is < 1 or > ExportPresetReader.MaxSize) {
            throw new ExportPresetException(
                $"{where}: size is a side from 1 to {ExportPresetReader.MaxSize}, not {size}."
            );
        }

        if (options.Filter is { } filter && !ExportFilters.All.Contains(filter, StringComparer.Ordinal)) {
            throw new ExportPresetException(
                $"{where}: filter '{filter}' is not one of {string.Join(", ", ExportFilters.All)}."
            );
        }

        return preset with {
            Textures = textures,
            Revision = Revision(preset.Revision, options),
            Size = options.Size ?? preset.Size,
            Filter = options.Filter ?? preset.Filter,
            Dither = options.Dither ?? preset.Dither,
            Padding = options.Padding is { } padding
                ? ExportPresetReader.Padding(padding.Mode, padding.Distance, padding.Colour, where)
                : preset.Padding,
            Sets = options.Sets is { } sets ? [..sets.Distinct(StringComparer.Ordinal)] : preset.Sets
        };
    }

    /// <summary>The preset's files with some turned off and some in another format.</summary>
    static List<ExportTexture> Textures(ExportPreset preset, IReadOnlyList<ExportFileChoice> files, string where) {
        if (files.FirstOrDefault(c => preset.Textures.All(texture => texture.File != c.Template)) is { } missing) {
            throw new ExportPresetException($"{where}: the preset has no file '{missing.Template}'.");
        }

        // The last choice for a file wins, as a later edit would.
        Dictionary<string, ExportFileChoice> chosen = new(StringComparer.Ordinal);
        foreach (var choice in files) {
            chosen[choice.Template] = choice;
        }

        List<ExportTexture> textures = [];
        foreach (var texture in preset.Textures) {
            var choice = chosen.GetValueOrDefault(texture.File);
            if (choice is not { Enabled: false }) {
                textures.Add(
                    choice?.Format is { } format && format != texture.Format
                        ? Reformat(texture, format, where)
                        : texture
                );
            }
        }

        return textures.Count > 0
            ? textures
            : throw new ExportPresetException($"{where}: every file is turned off, so there is nothing to write.");
    }

    /// <summary>The revision a preset has under some changes: its own when there are none.</summary>
    public static string Revision(string revision, ExportOptions? options) =>
        options is null
            ? revision
            : Library.Revision.Of(
                    [
                        ..System.Text.Encoding.UTF8.GetBytes(revision),
                        ..JsonSerializer.SerializeToUtf8Bytes(options, ContractsJsonContext.Default.ExportOptions)
                    ]
                )
                .Text;

    /// <summary>
    ///     A file in another format. A container's compression and mips do not carry over to a plain file, and a format
    ///     that holds only linear numbers takes the file as linear, which is what its numbers then are.
    /// </summary>
    static ExportTexture Reformat(ExportTexture texture, string format, string where) {
        if (!ExportFormats.All.Contains(format, StringComparer.Ordinal)) {
            throw new ExportPresetException($"{where}: '{format}' is not a format.");
        }

        var container = ExportFormats.IsContainer(format);
        var compression = container ? texture.Compression : ExportCompressions.None;
        var space = ExportFormats.IsFloat(format) || ExportFormats.IsWide(format) ? "linear" : texture.Space;
        var quality = ExportPresetReader.QualityProblem(format, texture.Quality) is null
            ? texture.Quality
            : ExportPresetReader.DefaultQuality(format);
        if (ExportPresetReader.ShapeProblem(format, compression, space, texture.Channels.Count) is { } problem) {
            throw new ExportPresetException($"{where}: {texture.File} as {ExportFormats.LabelOf(format)}: {problem}");
        }

        return texture with {
            Format = format,
            Compression = compression,
            Space = space,
            Quality = quality,
            Mips = container && texture.Mips
        };
    }
}
