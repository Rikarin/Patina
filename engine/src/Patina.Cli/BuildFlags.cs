using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Library;
using System.Globalization;

namespace Patina.Cli;

/// <summary>
///     The export settings <c>patina build</c> takes on its command line (#153): what the editor's Export dialog sends as
///     <see cref="ExportOptions" />, and <c>--kept</c>, which starts from what a project's, or the library's,
///     <c>export</c> block keeps, as the dialog does. A flag given wins over what is kept.
/// </summary>
static class BuildFlags {
    public const string Usage = """
                                               [--size <side>] [--filter box|bilinear|nearest] [--padding <mode>]
                                               [--distance <texels>] [--dither | --no-dither] [--set <name>]...
                                               [--file <template>=off|<format>[:<compression>]]... [--kept]
                                """;

    /// <summary>Takes one flag of export settings, and says whether it was one.</summary>
    /// <param name="flag">The flag.</param>
    /// <param name="value">The word after it, or null.</param>
    /// <param name="flags">What has been read so far.</param>
    /// <param name="used">Whether the flag took the word after it.</param>
    /// <returns>Whether it was a flag of these, read well.</returns>
    public static bool Take(string flag, string? value, Read flags, out bool used) {
        ArgumentNullException.ThrowIfNull(flags);
        used = value is not null && flag is not ("--dither" or "--no-dither" or "--kept");
        switch (flag) {
            case "--size" when int.TryParse(value, CultureInfo.InvariantCulture, out var size):
                flags.Size = size;
                return true;
            case "--filter" when value is not null:
                flags.Filter = value;
                return true;
            case "--padding" when value is not null:
                flags.Padding = value;
                return true;
            case "--distance" when int.TryParse(value, CultureInfo.InvariantCulture, out var distance):
                flags.Distance = distance;
                return true;
            case "--dither" or "--no-dither":
                flags.Dither = flag == "--dither";
                return true;
            case "--kept":
                flags.Kept = true;
                return true;
            case "--set" when value is not null:
                flags.Sets.Add(value);
                return true;
            case "--file" when value is not null && File(value) is { } file:
                flags.Files.Add(file);
                return true;
            default:
                used = false;
                return false;
        }
    }

    /// <summary><c>T_{project}_N=off</c>, <c>T_{project}_N=ktx2:bc5</c> or <c>T_{project}_N=png16</c>.</summary>
    static ExportFileChoice? File(string value) {
        var equals = value.LastIndexOf('=');
        if (equals <= 0) {
            return null;
        }

        var (template, choice) = (value[..equals], value[(equals + 1)..]);
        if (choice == "off") {
            return new(template, false);
        }

        var colon = choice.IndexOf(':', StringComparison.Ordinal);
        return colon < 0
            ? new(template, true, choice)
            : new(template, true, choice[..colon]) { Compression = choice[(colon + 1)..] };
    }

    /// <summary>The request with the flags' settings, over what is kept when <c>--kept</c> asks for it.</summary>
    /// <exception cref="ArgumentException"><c>--kept</c> with more than one project.</exception>
    public static BuildRequest Apply(OpenLibrary library, BuildRequest request, Read flags) {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(flags);
        var kept = flags.Kept ? Kept(library, request) : null;
        var preset = request.Presets is [var named] ? named : kept?.Preset;
        var start = kept is null || preset is null ? new ExportOptions() : ExportOverrides.OptionsOf(kept, preset)!;
        var options = start with {
            Size = flags.Size ?? start.Size,
            Filter = flags.Filter ?? start.Filter,
            Padding = flags.Padding is { } mode
                ? new(mode, flags.Distance ?? ExportPadding.DefaultDistance, [])
                : start.Padding,
            Dither = flags.Dither ?? start.Dither,
            Sets = flags.Sets.Count > 0 ? flags.Sets : Unskipped(library, request, kept) ?? start.Sets,
            Files = flags.Files.Count > 0 ? [..start.Files ?? [], ..flags.Files] : start.Files
        };
        var empty = options is { Size: null, Filter: null, Padding: null, Dither: null, Sets: null }
            && options.Files is null or []
            && options.PerSet is null or [];
        return request with {
            Presets = request.Presets ?? (preset is null ? null : [preset]),
            Folder = request.Folder ?? kept?.Folder,
            Options = empty ? request.Options : options
        };
    }

    /// <summary>What is kept: the one project's, or the library's when no project is named.</summary>
    static ProjectExportSettings? Kept(OpenLibrary library, BuildRequest request) {
        switch (request.Projects) {
            case null or []:
                return library.Document.Export;
            case [var id]:
                var path = PathOf(library, id);
                return path is null ? null : library.Contents(path).Export;
            default:
                throw new ArgumentException("--kept reads one project's settings, or the library's: name one project or none.");
        }
    }

    /// <summary>The sets a kept project export writes: all but the ones it left out, or null for all.</summary>
    static List<string>? Unskipped(OpenLibrary library, BuildRequest request, ProjectExportSettings? kept) {
        if (kept is not { Skipped.Count: > 0 } || request.Projects is not [var id]) {
            return null;
        }

        var path = PathOf(library, id);
        return path is null
            ? null
            : [..library.Contents(path).ExportSets.Where(set => !kept.Skipped.Contains(set, StringComparer.Ordinal))];
    }

    /// <summary>A project's path, from its id, or null when no project has it.</summary>
    static string? PathOf(OpenLibrary library, Guid id) =>
        library.ScanOutputs(new(null, [ProjectId.From(id)]), ToolCatalog.Default, CancellationToken.None).Projects is
            [var project, ..]
            ? project.Path
            : null;

    /// <summary>What the flags said.</summary>
    public sealed class Read {
        public int? Size { get; set; }
        public string? Filter { get; set; }
        public string? Padding { get; set; }
        public int? Distance { get; set; }
        public bool? Dither { get; set; }
        public bool Kept { get; set; }
        public List<string> Sets { get; } = [];
        public List<ExportFileChoice> Files { get; } = [];
    }
}
