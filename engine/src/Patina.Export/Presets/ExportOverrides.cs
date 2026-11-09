using Patina.Contracts;
using System.Text.Json;

namespace Patina.Export.Presets;

/// <summary>
///     A preset with one export's changes: another size, padding or format, files left out, sets chosen, and any of
///     those for one set alone. The result is a preset like any other, so everything after this reads one thing, and its
///     revision moves with the changes, so an export made under other settings is never taken for this one.
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
        var changed = Change(
            preset,
            new(options.Size, options.Filter, options.Padding, options.Dither, options.Files),
            where
        ) with {
            Revision = Revision(preset.Revision, options),
            Sets = options.Sets is { } sets ? [..sets.Distinct(StringComparer.Ordinal)] : preset.Sets
        };

        Dictionary<string, ExportPreset> perSet = new(StringComparer.Ordinal);
        foreach (var set in options.PerSet ?? []) {
            if (perSet.ContainsKey(set.Set)) {
                throw new ExportPresetException($"{where}: the texture set '{set.Set}' has two sets of changes.");
            }

            perSet[set.Set] = Change(
                changed,
                new(set.Size, set.Filter, set.Padding, set.Dither, set.Files),
                $"{where}, texture set '{set.Set}'"
            );
        }

        return changed with { PerSet = perSet };
    }

    /// <summary>
    ///     What a project keeps of its export as the changes an export through one preset makes: its size, padding and
    ///     dithering when it was set for that preset, and its files and sets' changes of that preset.
    /// </summary>
    public static ExportOptions? OptionsOf(ProjectExportSettings? export, string preset) {
        if (export is null) {
            return null;
        }

        var same = export.Preset == preset;
        return new() {
            Size = same ? export.Size : null,
            Filter = same ? export.Filter : null,
            Padding = same ? export.Padding : null,
            Dither = same ? export.Dither : null,
            Files = [
                ..export.Files.Where(file => file.Preset == preset)
                    .Select(static file => new ExportFileChoice(file.Template, file.Enabled, file.Format) {
                            Compression = file.Compression, Quality = file.Quality, Mips = file.Mips
                        }
                    )
            ],
            PerSet = same && export.PerSet.Count > 0 ? export.PerSet : null
        };
    }

    /// <summary>What one set of changes holds, for the whole export or for one set.</summary>
    readonly record struct Changes(
        int? Size,
        string? Filter,
        ExportPaddingInfo? Padding,
        bool? Dither,
        IReadOnlyList<ExportFileChoice>? Files
    );

    static ExportPreset Change(ExportPreset preset, Changes changes, string where) {
        var textures = Textures(preset, changes.Files ?? [], where);
        if (changes.Size is { } size && size is < 1 or > ExportPresetReader.MaxSize) {
            throw new ExportPresetException(
                $"{where}: size is a side from 1 to {ExportPresetReader.MaxSize}, not {size}."
            );
        }

        if (changes.Filter is { } filter && !ExportFilters.All.Contains(filter, StringComparer.Ordinal)) {
            throw new ExportPresetException(
                $"{where}: filter '{filter}' is not one of {string.Join(", ", ExportFilters.All)}."
            );
        }

        return preset with {
            Textures = textures,
            Size = changes.Size ?? preset.Size,
            Filter = changes.Filter ?? preset.Filter,
            Dither = changes.Dither ?? preset.Dither,
            Padding = changes.Padding is { } padding
                ? ExportPresetReader.Padding(padding.Mode, padding.Distance, padding.Colour, where)
                : preset.Padding
        };
    }

    /// <summary>The preset's files with some turned off and some in another format or compression.</summary>
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
                textures.Add(choice is null ? texture : Reformat(texture, choice, where));
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
    ///     A file in another format, compression or quality. A container's compression and mips do not carry over to a
    ///     plain file, and a format that holds only linear numbers takes the file as linear, which is what its numbers
    ///     then are. A file made a container keeps its compression if it had one and is otherwise uncompressed, unless
    ///     the choice names one.
    /// </summary>
    static ExportTexture Reformat(ExportTexture texture, ExportFileChoice choice, string where) {
        var format = choice.Format ?? texture.Format;
        if (!ExportFormats.All.Contains(format, StringComparer.Ordinal)) {
            throw new ExportPresetException($"{where}: '{format}' is not a format.");
        }

        if (choice.Compression is { } named && !ExportCompressions.All.Contains(named, StringComparer.Ordinal)) {
            throw new ExportPresetException($"{where}: '{named}' is not a compression.");
        }

        var container = ExportFormats.IsContainer(format);
        var compression = container ? choice.Compression ?? texture.Compression : ExportCompressions.None;
        if (!container && choice.Compression is { } refused && refused != ExportCompressions.None) {
            throw new ExportPresetException(
                $"{where}: {texture.File} as {ExportFormats.LabelOf(format)} cannot be compressed; only ktx2 and dds can."
            );
        }

        var floating = compression is ExportCompressions.Bc6H or ExportCompressions.Half or ExportCompressions.Wide;
        var space = ExportFormats.IsFloat(format) || ExportFormats.IsWide(format) || floating ? "linear" : texture.Space;
        var quality = choice.Quality ?? texture.Quality;
        if (ExportPresetReader.QualityProblem(format, quality) is { } badQuality) {
            if (choice.Quality is not null) {
                throw new ExportPresetException($"{where}: {texture.File}: {badQuality}");
            }

            quality = ExportPresetReader.DefaultQuality(format);
        }

        if (ExportPresetReader.ShapeProblem(format, compression, space, texture.Channels.Count) is { } problem) {
            throw new ExportPresetException($"{where}: {texture.File} as {ExportFormats.LabelOf(format)}: {problem}");
        }

        return texture with {
            Format = format,
            Compression = compression,
            Space = space,
            Quality = quality,
            Mips = container && (choice.Mips ?? texture.Mips)
        };
    }
}
