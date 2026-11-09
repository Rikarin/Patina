using Patina.Contracts;
using Patina.Export;
using Patina.Export.Presets;
using Patina.Ipc;
using Patina.Library;
using System.Globalization;

namespace Patina.Server.Stacks;

/// <summary>
///     Judges a texture set before export (#159). The Worker measures the layout and the maps, which only it reads; the
///     Server holds them to the project: its budget, its size limit, its texel density, and its export preset. Every
///     problem has a stable code, the numbers its sentence was written from, and where it is, so any client can list
///     it in its own words and go to it.
/// </summary>
public static class StackChecking {
    /// <summary>A share of the covered texels under this is a few stray texels, and not worth a line.</summary>
    public const double TexelShare = 0.005;

    /// <summary>An overlap under this share of the covered texels is rounding where islands touch.</summary>
    public const double OverlapShare = 0.001;

    const string Error = "error";
    const string Warning = "warning";

    /// <summary>The project a stack belongs to, by its folder, or null for a stack beside no project.</summary>
    public static ProjectContents? ProjectOf(OpenLibrary? library, string stackPath) {
        if (library is null) {
            return null;
        }

        var root = Path.Combine(library.RootPath, LibraryLayout.ProjectsFolder);
        var folder = Path.GetDirectoryName(stackPath);
        if (folder is null || !folder.StartsWith(root, StringComparison.Ordinal)) {
            return null;
        }

        try {
            return library.Contents(Path.GetRelativePath(root, folder).Replace('\\', '/'));
        } catch (FileNotFoundException) {
            return null;
        }
    }

    /// <summary>The density a project holds its sets to: its own, or the library's.</summary>
    public static double TargetDensity(ProjectContents? project, double library) =>
        project?.Checks?.TexelDensity is > 0 and var own ? own : library;

    /// <summary>
    ///     What the texel density view measures a worn stack against: its project's density, and its maps' side for a
    ///     material whose own size the Worker cannot read.
    /// </summary>
    public static (double Target, int Resolution) Density(OpenLibrary? library, StackSession stack) {
        ArgumentNullException.ThrowIfNull(stack);
        var state = stack.State;
        return (TargetDensity(ProjectOf(library, stack.Path), library?.Describe().TexelDensity ?? 0),
            Math.Max(state.Width, state.Height));
    }

    /// <summary>
    ///     The preset a project exports with: the one it last chose, or the first there is, which with no library is
    ///     the first factory preset.
    /// </summary>
    public static ExportPreset? PresetOf(ProjectContents? project, string? libraryRoot) {
        var found = ExportPresetCatalog.Read(libraryRoot);
        return project?.Export?.Preset is { } chosen && found.Find(chosen) is { } preset
            ? preset
            : found.Presets.Count > 0
                ? found.Presets[0]
                : null;
    }

    /// <summary>Judges what the Worker measured.</summary>
    /// <param name="state">The stack as it stands.</param>
    /// <param name="measured">What the Worker measured of it.</param>
    /// <param name="project">Its project, or null for a stack beside none.</param>
    /// <param name="libraryDensity">The library's texel density, in texels per unit.</param>
    /// <param name="preset">The export preset, or null when the library has none.</param>
    public static StackChecks Judge(
        StackState state,
        StackMeasured measured,
        ProjectContents? project,
        double libraryDensity,
        ExportPreset? preset
    ) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(measured);
        var settings = project?.Checks ?? new ProjectCheckSettings();
        var name = state.Set.Length > 0 ? $"Texture set '{state.Set}'" : $"'{state.Name}'";
        var (width, height) = (measured.Width, measured.Height);
        var target = TargetDensity(project, libraryDensity);
        var density = measured.Layout is { } layout ? LayoutCheck.Density(layout, Math.Max(width, height)) : 0;
        var bytes = preset is null ? 0 : Memory(preset, project?.Export, state, width, height);
        var budget = (long)(settings.MemoryBudget * 1024 * 1024);
        List<CheckProblem> problems = [..Limits(settings, name, (width, height), (bytes, budget), preset)];
        problems.AddRange(Missing(preset, project?.Export, state, name));
        problems.AddRange(Density(density, target, settings.DensityTolerance, name));
        if (measured.Layout is { } measures) {
            problems.AddRange(Layout(measures, name));
        }

        problems.AddRange(
            measured.Maps.Where(static measure => measure.Share >= TexelShare).Select(measure => Values(measure, name))
        );
        return new(
            state.Id,
            state.Set,
            width,
            height,
            density,
            target,
            bytes,
            budget,
            preset?.Name ?? string.Empty,
            problems
        );
    }

    /// <summary>The project's limits a set breaks: its memory budget and its size limit, which are errors.</summary>
    static IEnumerable<CheckProblem> Limits(
        ProjectCheckSettings settings,
        string name,
        (int Width, int Height) size,
        (long Bytes, long Budget) memory,
        ExportPreset? preset
    ) {
        if (memory.Budget > 0 && memory.Bytes > memory.Budget && preset is not null) {
            yield return Problem(
                CheckCodes.OverBudget,
                Error,
                $"{name} takes {Mebibytes(memory.Bytes)} MiB of GPU memory as {preset.Name} writes it, over the budget "
                + $"of {Mebibytes(memory.Budget)} MiB.",
                new() {
                    ["bytes"] = Number(memory.Bytes), ["budget"] = Number(memory.Budget), ["preset"] = preset.Name
                },
                new(Setting: "checks.memoryBudget")
            );
        }

        if (settings.MaxSize > 0 && Math.Max(size.Width, size.Height) > settings.MaxSize) {
            yield return Problem(
                CheckCodes.TooLarge,
                Error,
                $"{name} is {size.Width} by {size.Height} texels, larger than the limit of {settings.MaxSize}.",
                new() {
                    ["width"] = Number(size.Width),
                    ["height"] = Number(size.Height),
                    ["limit"] = Number(settings.MaxSize)
                },
                new(Setting: "checks.maxSize")
            );
        }
    }

    /// <summary>
    ///     A density further from the project's than its tolerance, either way, shown in the density view.
    /// </summary>
    static IEnumerable<CheckProblem> Density(double density, double target, double tolerance, string name) {
        if (target <= 0 || density <= 0) {
            yield break;
        }

        var ratio = density / target;
        string? code = null;
        if (ratio < 1 - tolerance) {
            code = CheckCodes.DensityLow;
        } else if (ratio > 1 + tolerance) {
            code = CheckCodes.DensityHigh;
        }

        if (code is not null) {
            yield return Problem(
                code,
                Warning,
                $"{name} has {Round(density)} texels per unit where the project asks for {Round(target)}.",
                new() { ["density"] = Round(density), ["target"] = Round(target) },
                new(ViewportViews.TexelDensity, Setting: "checks.texelDensity")
            );
        }
    }

    /// <summary>
    ///     The GPU memory a set's files take: each file the preset writes and the project has not turned off, at the
    ///     size the export writes it, in the format it is held in on a GPU, with a third more for a mip chain. A file
    ///     reading none of the set's maps is not written, so it costs nothing.
    /// </summary>
    public static long Memory(
        ExportPreset preset,
        ProjectExportSettings? export,
        StackState state,
        int width,
        int height
    ) {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(state);
        var maps = Maps(state);
        var (across, down) = Sized(export?.Size ?? preset.Size, width, height);
        double total = 0;
        foreach (var texture in Written(preset, export)) {
            var reads = texture.Maps.ToArray();
            if (reads.Length > 0 && !reads.Any(maps.Contains)) {
                continue;
            }

            var format = SettingOf(export, preset.Id, texture.File)?.Format is { Length: > 0 } other
                ? other
                : texture.Format;
            total += (double)across
                * down
                * BytesPerTexel(format, texture.Compression, texture.Channels.Count)
                * (texture.Mips ? 4d / 3d : 1d);
        }

        return (long)Math.Ceiling(total);
    }

    /// <summary>
    ///     What one texel takes on a GPU: a block-compressed size, or each lane at its depth, three lanes as four.
    /// </summary>
    public static double BytesPerTexel(string format, string compression, int lanes) {
        var held = lanes switch {
            <= 1 => 1,
            2 => 2,
            _ => 4
        };
        return compression switch {
            ExportCompressions.Bc1 or ExportCompressions.Bc4 => 0.5,
            ExportCompressions.Bc3 or ExportCompressions.Bc5 or ExportCompressions.Bc6H or ExportCompressions.Bc7 => 1,
            ExportCompressions.Half => 2 * held,
            ExportCompressions.Wide => 4 * held,
            _ => format switch {
                ExportFormats.Png16 or ExportFormats.Tiff16 or ExportFormats.Exr16 => 2 * held,
                ExportFormats.Exr32 or ExportFormats.Tiff32 or ExportFormats.Hdr => 4 * held,
                _ => held
            }
        };
    }

    static IEnumerable<ExportTexture> Written(ExportPreset preset, ProjectExportSettings? export) =>
        preset.Textures.Where(texture => SettingOf(export, preset.Id, texture.File) is not { Enabled: false });

    /// <summary>What the project says of one file of a preset: off, or in another format, or nothing.</summary>
    static ExportFileSetting? SettingOf(ProjectExportSettings? export, string preset, string template) {
        foreach (var file in export?.Files ?? []) {
            if (file.Preset == preset && file.Template == template) {
                return file;
            }
        }

        return null;
    }

    /// <summary>The maps a set makes. One with a height channel makes a normal whether or not it says so.</summary>
    static HashSet<string> Maps(StackState state) {
        HashSet<string> maps = [..state.Channels.Select(static channel => channel.Usage)];
        if (maps.Contains("height")) {
            maps.Add("normal");
        }

        return maps;
    }

    static (int Width, int Height) Sized(int? longest, int width, int height) {
        if (longest is not > 0 || Math.Max(width, height) == 0) {
            return (width, height);
        }

        var scale = (double)longest.Value / Math.Max(width, height);
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>A map the preset reads that the set does not make, once, under the first file that reads it.</summary>
    static IEnumerable<CheckProblem> Missing(
        ExportPreset? preset,
        ProjectExportSettings? export,
        StackState state,
        string name
    ) {
        if (preset is null) {
            yield break;
        }

        var maps = Maps(state);
        HashSet<string> said = new(StringComparer.Ordinal);
        foreach (var texture in Written(preset, export)) {
            var reads = texture.Maps.ToArray();
            if (!reads.Any(maps.Contains)) {
                continue;
            }

            foreach (var map in reads.Where(map => !maps.Contains(map) && said.Add(map))) {
                yield return Problem(
                    CheckCodes.ChannelMissing,
                    Warning,
                    $"{preset.Name} reads {map} into {texture.File}, and {name} has no {map} channel, so a default "
                    + "fills it.",
                    new() { ["map"] = map, ["preset"] = preset.Name, ["file"] = texture.File },
                    new(Setting: "export.preset")
                );
            }
        }
    }

    static IEnumerable<CheckProblem> Layout(LayoutMeasures measures, string name) {
        CheckTarget At(string code) =>
            measures.Spots.FirstOrDefault(spot => spot.Code == code) is { } spot
                ? new(Camera: spot.Camera, U: spot.U, V: spot.V)
                : new();

        if (measures.Outside > 0) {
            yield return Problem(
                CheckCodes.UvOutside,
                Warning,
                $"{measures.Outside} of {name}'s triangles reach past UDIM tile 1001, which is the only tile Patina "
                + "paints.",
                new() { ["count"] = Number(measures.Outside) },
                At(CheckCodes.UvOutside)
            );
        }

        if (measures.Overlap >= OverlapShare) {
            yield return Problem(
                CheckCodes.UvOverlap,
                Warning,
                $"{Percent(measures.Overlap)} of the texels {name} covers are under more than one triangle, so a "
                + "stroke on one lands on the other.",
                new() { ["share"] = Share(measures.Overlap) },
                At(CheckCodes.UvOverlap)
            );
        }

        if (measures.Degenerate > 0) {
            yield return Problem(
                CheckCodes.UvDegenerate,
                Warning,
                $"{measures.Degenerate} of {name}'s triangles have no area in the layout, so no texel lands on them.",
                new() { ["count"] = Number(measures.Degenerate) },
                At(CheckCodes.UvDegenerate)
            );
        }

        if (measures.Flipped > 0) {
            yield return Problem(
                CheckCodes.UvFlipped,
                Warning,
                $"{measures.Flipped} of {name}'s triangles are turned over in the layout, so a normal map reads them "
                + "backwards.",
                new() { ["count"] = Number(measures.Flipped) },
                At(CheckCodes.UvFlipped)
            );
        }
    }

    static CheckProblem Values(MapMeasure measure, string name) {
        var (sentence, view) = measure.Code switch {
            CheckCodes.AlbedoDark => ("base colour on a surface that is not metal is darker than sRGB 30", "baseColor"),
            CheckCodes.AlbedoBright => ("base colour on a surface that is not metal is brighter than sRGB 240",
                "baseColor"),
            CheckCodes.MetalDark => ("base colour on bare metal is darker than sRGB 180", "baseColor"),
            _ => ("metalness is between 0.1 and 0.9", "metallic")
        };
        return Problem(
            measure.Code,
            Warning,
            $"In {Percent(measure.Share)} of {name}'s texels the {sentence}.",
            new() { ["share"] = Share(measure.Share) },
            new(view, measure.Spot.Camera, measure.Spot.U, measure.Spot.V)
        );
    }

    static CheckProblem Problem(
        string code,
        string severity,
        string detail,
        Dictionary<string, string> values,
        CheckTarget target
    ) =>
        new(code, severity, detail, values, target);

    static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    static string Round(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);

    static string Share(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    static string Percent(double value) => (value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    static string Mebibytes(long bytes) => (bytes / (1024d * 1024d)).ToString("0.#", CultureInfo.InvariantCulture);
}
