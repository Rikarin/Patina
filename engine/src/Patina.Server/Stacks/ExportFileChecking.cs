using Patina.Contracts;
using Patina.Export;
using Patina.Export.Presets;
using System.Globalization;

namespace Patina.Server.Stacks;

/// <summary>
///     The files a texture set's export writes, each with the GPU memory it takes, and each held to what an engine
///     reads well (#159): how its lanes are packed, the space its numbers are in, its compression and its mips. A file
///     reading none of the set's maps is not written, so it is neither counted nor judged.
/// </summary>
public static class ExportFileChecking {
    const int AlphaLane = 3;

    /// <summary>One file the export writes: the preset's texture, and its name as written.</summary>
    /// <param name="Texture">The preset's texture.</param>
    /// <param name="Name">Its name as the export writes it, without an extension.</param>
    public readonly record struct Written(ExportTexture Texture, string Name);

    /// <summary>The preset's files that the export writes for a set making these maps.</summary>
    public static IEnumerable<Written> Writes(ExportPreset preset, NamingValues naming, IReadOnlySet<string> maps) {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(maps);
        return Writing(preset, naming, maps);
    }

    static IEnumerable<Written> Writing(ExportPreset preset, NamingValues naming, IReadOnlySet<string> maps) {
        foreach (var texture in preset.Textures) {
            if (!ExportMaps.Written([..texture.Maps], maps.Contains)) {
                continue;
            }

            yield return new(
                texture,
                NamingTemplate.Resolve(texture.File, naming, string.Empty).Replace(Path.DirectorySeparatorChar, '/')
            );
        }
    }

    /// <summary>Each file written, at the size the export writes it from maps of this size.</summary>
    public static IReadOnlyList<CheckFile> Files(
        ExportPreset preset,
        NamingValues naming,
        IReadOnlySet<string> maps,
        int width,
        int height
    ) =>
        [..Writes(preset, naming, maps).Select(written => FileOf(preset, written, width, height))];

    static CheckFile FileOf(ExportPreset preset, Written written, int width, int height) {
        var texture = written.Texture;
        var (across, down) = ExportMemory.Sized(preset, texture, width, height);
        return new(
            written.Name + ExportFormats.ExtensionOf(texture.Format),
            texture.Format,
            texture.Compression,
            across,
            down,
            texture.Mips,
            (long)Math.Ceiling(ExportMemory.Bytes(texture, across, down))
        );
    }

    /// <summary>
    ///     What is wrong with the files written, file by file in the preset's order: packing first, then space,
    ///     compression and mips.
    /// </summary>
    /// <param name="preset">The preset as the project's export writes it.</param>
    /// <param name="naming">What its file names are made from.</param>
    /// <param name="depths">Each map the set makes, with how many bits a lane it is painted at: 8, 16 or 32.</param>
    public static IEnumerable<CheckProblem> Judge(
        ExportPreset preset,
        NamingValues naming,
        IReadOnlyDictionary<string, int> depths
    ) {
        ArgumentNullException.ThrowIfNull(depths);
        List<CheckProblem> problems = [];
        foreach (var written in Writes(preset, naming, depths.Keys.ToHashSet(StringComparer.Ordinal))) {
            problems.AddRange(Packing(written));
            problems.AddRange(Depth(written, depths));
            problems.AddRange(Compression(written));
        }

        return problems.OrderBy(static problem => IndexOf(problem.Code));
    }

    static int IndexOf(string code) => CheckCodes.All.ToList().IndexOf(code);

    /// <summary>
    ///     A file's colour lanes in one space: colour maps sRGB, data maps linear. A file mixing them holds one in
    ///     the wrong encoding, which says it before either space would. Alpha is linear in any file, so it is left out,
    ///     and so is an ID map, whose colours are names and not values.
    /// </summary>
    static IEnumerable<CheckProblem> Packing(Written written) {
        var texture = written.Texture;
        var lanes = texture.Channels.Take(AlphaLane).Select(static lane => lane.Map).OfType<string>().ToArray();
        var colour = lanes.FirstOrDefault(IsColour);
        var data = lanes.FirstOrDefault(static map => !IsColour(map) && map != ExportMaps.Id);
        if (colour is not null && data is not null) {
            yield return Problem(
                CheckCodes.PackingMixed,
                $"{written.Name} packs {Terms.Label(colour)} with {Terms.Label(data)} in its color lanes, so one of "
                + "them is held in the other's encoding.",
                new() { ["file"] = written.Name, ["color"] = Terms.Label(colour), ["data"] = Terms.Label(data) }
            );
            yield break;
        }

        var srgb = texture.Space == "srgb";
        if (srgb && data is not null) {
            yield return Problem(
                CheckCodes.SpaceDataSrgb,
                $"{written.Name} holds {Terms.Label(data)} in sRGB, which an engine reads as color, not as data.",
                new() { ["file"] = written.Name, ["channel"] = Terms.Label(data) }
            );
        } else if (!srgb && colour is not null && colour != ExportMaps.Emissive && EightBit(texture)) {
            yield return Problem(
                CheckCodes.SpaceColourLinear,
                $"{written.Name} holds {Terms.Label(colour)} as 8-bit linear numbers, which band in the darks.",
                new() { ["file"] = written.Name, ["channel"] = Terms.Label(colour) }
            );
        }
    }

    /// <summary>
    ///     A linear file of a fixed depth that reads a map painted deeper (ADR 0044), which it rounds away. A file
    ///     whose format follows its maps is as deep as they are, an sRGB file is eight bits by design, and a GPU
    ///     container's depth is its compression, which an engine chooses for its memory, so none of those is judged
    ///     here.
    /// </summary>
    static IEnumerable<CheckProblem> Depth(Written written, IReadOnlyDictionary<string, int> depths) {
        var texture = written.Texture;
        if (texture.Space == "srgb" || BitsOf(texture.Format) is not { } bits) {
            yield break;
        }

        var deepest = texture.Maps.Where(depths.ContainsKey)
            .Select(map => (Map: map, Bits: depths[map]))
            .OrderByDescending(static map => map.Bits)
            .FirstOrDefault();
        if (deepest.Map is null || deepest.Bits <= bits) {
            yield break;
        }

        var label = ExportFormats.LabelOf(texture.Format);
        var channel = Terms.Label(deepest.Map);
        yield return Problem(
            CheckCodes.DepthLost,
            $"{written.Name} is {label} and reads {channel} painted at {deepest.Bits} bits, which it rounds to {bits}. "
            + "A png, tiff or exr file is as deep as its maps.",
            new() {
                ["file"] = written.Name,
                ["format"] = label,
                ["channel"] = channel,
                ["painted"] = deepest.Bits.ToString(CultureInfo.InvariantCulture),
                ["bits"] = bits.ToString(CultureInfo.InvariantCulture)
            }
        );
    }

    /// <summary>How many bits a lane a file of a fixed format keeps, or null where its format does not say.</summary>
    static int? BitsOf(string format) =>
        format switch {
            _ when ExportFormats.Follows(format) || ExportFormats.IsContainer(format) => null,
            _ when ExportFormats.IsEightBit(format) => 8,
            ExportFormats.Png16 or ExportFormats.Tiff16 or ExportFormats.Exr16 => 16,
            ExportFormats.Exr32 or ExportFormats.Tiff32 => 32,
            _ => null
        };

    /// <summary>A normal in colour blocks, and a GPU container without the mips an engine samples.</summary>
    static IEnumerable<CheckProblem> Compression(Written written) {
        var texture = written.Texture;
        var normal = texture.Channels.Take(AlphaLane)
            .Any(static lane => lane.Map is { } map && ExportMaps.IsTangentNormal(map));
        if (normal && texture.Compression is ExportCompressions.Bc1 or ExportCompressions.Bc3) {
            var label = ExportCompressions.LabelOf(texture.Compression);
            yield return Problem(
                CheckCodes.CompressionNormal,
                $"{written.Name} holds a normal in {label} blocks, which show on a lit surface. BC5 or BC7 keep it.",
                new() { ["file"] = written.Name, ["compression"] = label }
            );
        }

        if (ExportFormats.IsContainer(texture.Format) && !texture.Mips) {
            yield return Problem(
                CheckCodes.MipsMissing,
                $"{written.Name} has no mips, so an engine samples it as it is and it shimmers at a distance.",
                new() { ["file"] = written.Name }
            );
        }
    }

    /// <summary>
    ///     Whether a map's numbers are a colour: base colour and its derived maps, emission, and OpenPBR's colours
    ///     (#142), glTF's sheen among them.
    /// </summary>
    static bool IsColour(string map) =>
        ExportMaps.IsColour(map)
        || map is ExportMaps.Emissive
            or ExportMaps.SheenColor
            or ExportMaps.SheenTint
            or ExportMaps.SpecularColor
            or ExportMaps.CoatColor
            or ExportMaps.SubsurfaceColor;

    /// <summary>Whether a file keeps eight bits a lane, where linear colour bands.</summary>
    static bool EightBit(ExportTexture texture) =>
        ExportFormats.IsEightBit(texture.Format)
        || (ExportFormats.IsContainer(texture.Format)
            && texture.Compression is ExportCompressions.None
                or ExportCompressions.Bc1
                or ExportCompressions.Bc3
                or ExportCompressions.Bc7);

    static CheckProblem Problem(string code, string detail, Dictionary<string, string> values) =>
        new(code, "warning", detail, values, new(Setting: "export.preset"));
}
