using Patina.Contracts;
using Patina.Export;
using Patina.Export.Presets;
using Patina.Ipc;
using Patina.Library;
using System.Globalization;
using Vixen.Texturing.Painting;

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

    /// <summary>The density limits hold sets to: their own, or the library's.</summary>
    public static double TargetDensity(ProjectCheckSettings? limits, double library) =>
        limits?.TexelDensity is > 0 and var own ? own : library;

    /// <summary>
    ///     What the texel density view measures a worn stack against: its project's density on the platform its preset
    ///     writes for, and its maps' side for a material whose own size the Worker cannot read.
    /// </summary>
    public static (double Target, int Resolution) Density(OpenLibrary? library, StackSession stack) {
        ArgumentNullException.ThrowIfNull(stack);
        var state = stack.State;
        var project = ProjectOf(library, stack.Path);
        var limits = project?.Checks?.For(PresetOf(project, library)?.Id);
        return (TargetDensity(limits, library?.Describe().TexelDensity ?? 0), Math.Max(state.Width, state.Height));
    }

    /// <summary>A stack's file as its project lists it: below the library, with forward slashes.</summary>
    public static string DocumentOf(OpenLibrary? library, string stackPath) =>
        library is null ? stackPath : Path.GetRelativePath(library.RootPath, stackPath).Replace('\\', '/');

    /// <summary>
    ///     The preset a project exports with: the one it last chose, else the library's default, else the factory's
    ///     starter, as the Export dialog starts (#240).
    /// </summary>
    public static ExportPreset? PresetOf(ProjectContents? project, OpenLibrary? library) {
        var root = library?.RootPath;
        var found = ExportPresetCatalog.Read(root, root is null ? null : project?.Path);
        return project?.Export?.Preset is { } chosen && found.Find(chosen) is { } preset
            ? preset
            : found.Default(library?.Document.DefaultPreset);
    }

    /// <summary>
    ///     The preset a check measures with: the one a client asked for, which is a platform the project ships to, or
    ///     else the project's own.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No preset of the asked id is in the library or the project.</exception>
    public static ExportPreset? PresetOf(ProjectContents? project, OpenLibrary? library, string? asked) {
        if (asked is null) {
            return PresetOf(project, library);
        }

        var root = library?.RootPath;
        return ExportPresetCatalog.Read(root, root is null ? null : project?.Path).Find(asked)
            ?? throw new KeyNotFoundException($"There is no preset '{asked}' in this library.");
    }

    /// <summary>
    ///     Presets the project names that neither the library nor the project has any more: the one it exports with,
    ///     which goes to its export setting, and each platform it keeps limits for, which hold no set until a preset of
    ///     that id comes back (ADR 0043) and go to the project's limits.
    /// </summary>
    public static IEnumerable<CheckProblem> Platforms(ProjectContents? project, OpenLibrary? library) {
        if (project is null) {
            return [];
        }

        var root = library?.RootPath;
        var presets = ExportPresetCatalog.Read(root, root is null ? null : project.Path);
        return [..Chosen(project, library, presets), ..Kept(project, presets)];
    }

    /// <summary>
    ///     The preset the project last chose, when no library or project has it any more: its checks measure with the
    ///     default instead, which is said rather than done without a word (#305).
    /// </summary>
    static CheckProblem[] Chosen(
        ProjectContents project,
        OpenLibrary? library,
        ExportPresetCatalog.Found presets
    ) {
        if (project.Export?.Preset is not { Length: > 0 } chosen || presets.Find(chosen) is not null) {
            return [];
        }

        var fallback = presets.Default(library?.Document.DefaultPreset)?.Id ?? string.Empty;
        return [
            Problem(
                CheckCodes.PresetUnknown,
                Warning,
                $"The project exports with '{chosen}', which no export preset is any more, so the default "
                + (fallback.Length > 0 ? $"'{fallback}' stands in for it." : "stands in for it, and there is none.")
                + " Choose its preset again.",
                new() { ["preset"] = chosen, ["fallback"] = fallback },
                new(Setting: "export.preset")
            )
        ];
    }

    static CheckProblem[] Kept(ProjectContents project, ExportPresetCatalog.Found presets) {
        if (project.Checks is not { Platforms.Count: > 0 } checks) {
            return [];
        }

        return [
            ..checks.Platforms.Where(platform => presets.Find(platform.Preset) is null)
                .Select(static platform => Problem(
                        CheckCodes.PlatformUnknown,
                        Warning,
                        $"The project keeps limits for '{platform.Preset}', which no export preset is any more, so "
                        + "they hold no set. Name the preset again or remove its limits.",
                        new() { ["preset"] = platform.Preset },
                        new(Setting: "checks.platforms")
                    )
                )
        ];
    }

    /// <summary>Judges what the Worker measured, or what the Server knows of a set nobody has open.</summary>
    /// <param name="state">The stack as it stands.</param>
    /// <param name="measured">What the Worker measured of it, or null when it was not asked.</param>
    /// <param name="project">Its project, or null for a stack beside none.</param>
    /// <param name="libraryDensity">The library's texel density, in texels per unit.</param>
    /// <param name="preset">The export preset, or null when the library has none.</param>
    public static StackChecks Judge(
        StackState state,
        StackMeasured? measured,
        ProjectContents? project,
        double libraryDensity,
        ExportPreset? preset
    ) {
        ArgumentNullException.ThrowIfNull(state);
        var settings = (project?.Checks ?? new ProjectCheckSettings()).For(preset?.Id);
        var name = state.Set.Length > 0 ? $"Texture set '{state.Set}'" : $"'{state.Name}'";
        var (width, height) = measured is null ? (state.Width, state.Height) : (measured.Width, measured.Height);
        var target = TargetDensity(settings, libraryDensity);
        var density = measured?.Layout is { } layout ? LayoutCheck.Density(layout, Math.Max(width, height)) : 0;
        var naming = new NamingValues(project?.Name ?? state.Name, preset?.Id ?? string.Empty, state.Set);
        var written = preset is null ? null : Written(preset, project?.Export).For(state.Set);
        var files = written is null ? [] : ExportFileChecking.Files(written, naming, Maps(state), width, height);
        var bytes = files.Sum(static file => file.Bytes);
        var budget = (long)(settings.MemoryBudget * 1024 * 1024);
        List<CheckProblem> problems = [..Limits(settings, name, (width, height), (bytes, budget), preset)];
        if (written is not null) {
            problems.AddRange(Missing(written, naming, state, name));
            problems.AddRange(ExportFileChecking.Judge(written, naming, Depths(state)));
        }

        if (state.BakeStale is { } stale) {
            problems.Add(Stale(stale, state.UvSet, name));
        }

        problems.AddRange(Density(density, target, settings.DensityTolerance, name));
        if (measured?.Layout is { } measures) {
            problems.AddRange(Layout(measures, name));
        }

        problems.AddRange(
            (measured?.Maps ?? []).Where(static measure => measure.Share >= TexelShare)
            .Select(measure => Values(measure, name))
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
            problems,
            files,
            state.Path,
            measured is not null
        );
    }

    /// <summary>A bake that no longer fits the set: an error, as what reads it lands in the wrong place.</summary>
    static CheckProblem Stale(string reason, int uvSet, string name) =>
        Problem(
            CheckCodes.BakeStale,
            Error,
            reason switch {
                BakeFit.UvSet =>
                    $"{name} is laid out in UV set {uvSet + 1}, and its mesh maps were baked in another. "
                    + "Bake it again.",
                BakeFit.Shape => $"{name}'s Width or Height changed after its mesh maps were baked. Bake it again.",
                _ => $"{name}'s mesh changed after its mesh maps were baked. Bake it again."
            },
            new() { ["reason"] = reason, ["uvSet"] = Number(uvSet + 1) },
            new(Setting: "bake")
        );

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
        var written = Written(preset, export).For(state.Set);
        var naming = new NamingValues(state.Name, preset.Id, state.Set);
        return ExportFileChecking.Files(written, naming, Maps(state), width, height).Sum(static file => file.Bytes);
    }

    /// <summary>
    ///     The preset as the project's export writes it, with the files it turned off left out and the formats and
    ///     compression it chose. Settings the preset can no longer take leave it as it is.
    /// </summary>
    static ExportPreset Written(ExportPreset preset, ProjectExportSettings? export) {
        try {
            return ExportOverrides.Apply(preset, ExportOverrides.OptionsOf(export, preset.Id));
        } catch (ExportPresetException) {
            return preset;
        }
    }

    /// <summary>
    ///     The maps a set makes. One with a height channel makes a normal whether or not it says so, and one with
    ///     metal-roughness maps makes the specular-glossiness maps the export works out from them.
    /// </summary>
    static HashSet<string> Maps(StackState state) => Depths(state).Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>
    ///     The maps a set makes, each with how many bits a lane it is painted at (ADR 0044): a channel's own depth, a
    ///     normal from height as deep as the height, a map the export works out as deep as the deepest it comes from,
    ///     and a bake's maps at eight, the least any bake is.
    /// </summary>
    static Dictionary<string, int> Depths(StackState state) {
        Dictionary<string, int> maps = new(StringComparer.Ordinal);
        foreach (var channel in state.Channels) {
            maps[channel.Usage] = Math.Max(maps.GetValueOrDefault(channel.Usage), BitsOf(channel.Format));
        }

        if (maps.TryGetValue("height", out var height)) {
            maps.TryAdd("normal", height);
        }

        // A bake beneath the set writes its normal and occlusion whether or not it has those channels (#86).
        var baked = BakedMapPicture.Shading(state.MeshMaps);
        if (baked.Contains("normal") && state.BakedNormal != "Off") {
            maps.TryAdd("normal", Bits8);
        }

        if (baked.Contains("ao") && state.BakedOcclusion != "Off") {
            maps.TryAdd("occlusion", Bits8);
        }

        // The export works out specular, diffuse and glossiness from the metal-roughness maps (DerivedMaps), and glTF's
        // sheen colour from the sheen's weight and its colour.
        if (maps.TryGetValue(ExportMaps.Roughness, out var roughness)) {
            maps.TryAdd(ExportMaps.Glossiness, roughness);
        }

        // glTF's sheen colour, from a sheen's weight and its colour (#142).
        if (maps.TryGetValue(ExportMaps.Sheen, out var sheen)) {
            maps.TryAdd(ExportMaps.SheenTint, Math.Max(sheen, maps.GetValueOrDefault(ExportMaps.SheenColor)));
        }

        var colour = Math.Max(
            maps.GetValueOrDefault(ExportMaps.BaseColor),
            maps.GetValueOrDefault(ExportMaps.Metalness)
        );
        if (colour > 0) {
            maps.TryAdd(ExportMaps.Specular, colour);
            maps.TryAdd(ExportMaps.Diffuse, colour);
        }

        return maps;
    }

    const int Bits8 = 8;

    /// <summary>How many bits a lane a channel of this format is painted at: 8, 16 or 32.</summary>
    static int BitsOf(string format) =>
        Enum.TryParse<PaintTexelFormat>(format, out var known) && Enum.IsDefined(known)
            ? PaintTexelFormats.BytesPerTexel(known) * 2
            : Bits8;

    /// <summary>
    ///     A map the preset reads that the set does not make, once, under the first file that reads it. The file is
    ///     named as the export would write it and the map as the app calls it, never a template or a stored value
    ///     (#198).
    /// </summary>
    static IEnumerable<CheckProblem> Missing(ExportPreset written, NamingValues naming, StackState state, string name) {
        var maps = Maps(state);
        HashSet<string> said = new(StringComparer.Ordinal);
        foreach (var (texture, file) in ExportFileChecking.Writes(written, naming, maps)) {
            if (!texture.Maps.Any(maps.Contains)) {
                continue;
            }

            foreach (var map in texture.Maps.Where(map => !maps.Contains(map) && said.Add(map))) {
                var channel = Terms.Label(map);
                yield return Problem(
                    CheckCodes.ChannelMissing,
                    Warning,
                    $"{written.Name} reads {channel} into {file}, and {name} has no {channel} channel, so a default "
                    + "fills it.",
                    new() { ["map"] = map, ["channel"] = channel, ["preset"] = written.Name, ["file"] = file },
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
                $"{measures.Outside} of {name}'s triangles reach past its UDIM tile, and a texture set paints one "
                + "tile.",
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
            CheckCodes.AlbedoDark => ("base color on a surface that is not metal is darker than sRGB 30", "baseColor"),
            CheckCodes.AlbedoBright => ("base color on a surface that is not metal is brighter than sRGB 240",
                "baseColor"),
            CheckCodes.MetalDark => ("base color on bare metal is darker than sRGB 180", "baseColor"),
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
