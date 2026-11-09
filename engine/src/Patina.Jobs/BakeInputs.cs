using Patina.Contracts;
using Patina.Library;
using System.Text.Json;

namespace Patina.Jobs;

/// <summary>Why a bake was refused: a wrong number or a wrong name, not a missing file.</summary>
public sealed class BakeRefusedException : Exception {
    public BakeRefusedException() { }

    public BakeRefusedException(string message) : base(message) { }

    public BakeRefusedException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Refused for options a client says in its own words, by <paramref name="refusal" />'s code.</summary>
    public BakeRefusedException(BakeOptionsRefusal refusal) : base(refusal?.Detail) {
        ArgumentNullException.ThrowIfNull(refusal);
        Code = refusal.Code;
        Values = refusal.Values;
    }

    /// <summary>A stable code a client can explain, or null for a refusal it shows as it is.</summary>
    public string? Code { get; }

    /// <summary>The values the message was written from, by name.</summary>
    public IReadOnlyDictionary<string, string> Values { get; } = new Dictionary<string, string>();
}

/// <summary>What a bake is allowed to ask for, and the key of what it asks for.</summary>
public static class BakeInputs {
    public const string Png8 = "png8";
    public const string Png16 = "png16";
    public const string Exr16 = "exr16";
    public const string Exr32 = "exr32";

    /// <summary>
    ///     What a map's numbers are relative to, and how its rows run. A bake made before this was a per-triangle
    ///     basis upside down, and a cache hit must not hand one back.
    /// </summary>
    const string TangentBasis = "mikktspace, v down";

    /// <summary>The maps a request may ask for by name, in the order they are reported.</summary>
    public static IReadOnlyList<string> Maps => BakeOptions.AllMaps;

    /// <summary>The refusal a request earns, or null when it is fine. Files are looked at last.</summary>
    /// <exception cref="BakeRefusedException">It asks for something no bake can do.</exception>
    /// <exception cref="FileNotFoundException">An input file is not there.</exception>
    public static void Check(BakeRequest request) {
        var options = request.Options;
        if (options.Refusal() is { } refusal) {
            throw new BakeRefusedException(refusal);
        }

        Require(request.High.Count > 0, "A bake needs a high mesh, which may be the low mesh itself.");
        Require(request.Output.Length > 0, "A bake needs an output folder.");
        foreach (var picture in (string[])[options.Rays.SkewMap, options.Rays.DistanceMap, ColourPicture(options)]) {
            Require(
                picture.Length == 0 || PictureFormats.IsPicture(picture),
                $"'{Path.GetFileName(picture)}' is not a picture this reads."
            );
        }

        foreach (var file in Files(request).Values) {
            if (!File.Exists(file)) {
                throw new FileNotFoundException($"There is no file at '{file}'.", file);
            }
        }
    }

    /// <summary>The key of what a request would make, which reads every input whole.</summary>
    public static BuildKey KeyOf(BakeRequest request, ToolCatalog tools) => KeyOf(request, tools, null);

    /// <summary>The key of what a request would make, with some inputs read for what the bake uses of them.</summary>
    /// <param name="request">The bake.</param>
    /// <param name="tools">The tools' versions.</param>
    /// <param name="reads">
    ///     Revisions by role that stand in for a file's bytes, or add inputs no path names: the low mesh's geometry for
    ///     its texture set, or the pictures a model names. Null reads every file whole.
    /// </param>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         A revision in place of the bytes is only sound when it covers everything the bake reads of that
    ///         file
    ///     </b>
    ///     , and a reader that cannot say leaves the role out, so the bytes decide: a full rebake is the
    ///     answer whenever it is unclear (#151).
    /// </remarks>
    public static BuildKey KeyOf(
        BakeRequest request,
        ToolCatalog tools,
        Func<BakeRequest, IReadOnlyDictionary<string, Revision>>? reads
    ) {
        ArgumentNullException.ThrowIfNull(request);
        var files = Files(request).ToDictionary(static file => file.Key, static file => Revision.OfFile(file.Value));
        foreach (var (role, revision) in reads?.Invoke(request) ?? new Dictionary<string, Revision>()) {
            files[role] = revision;
        }

        return BakeKeys.Compute(files, Settings(request), tools);
    }

    /// <summary>The picture a colour map carries across, or empty when it carries none.</summary>
    static string ColourPicture(BakeOptions options) =>
        options.Colour.Source == "texture" && options.Maps.Contains("colour", StringComparer.Ordinal)
            ? options.Colour.Texture
            : string.Empty;

    /// <summary>The files a bake reads, by their part in it. The high meshes are numbered: order counts.</summary>
    static Dictionary<string, string> Files(BakeRequest request) {
        Dictionary<string, string> files = new(StringComparer.Ordinal) { ["low"] = request.Low };
        for (var at = 0; at < request.High.Count; at++) {
            files[at == 0 ? "high" : $"high{at}"] = request.High[at];
        }

        if (request.Cage is { } cage) {
            files["cage"] = cage;
        }

        if (request.Options.Rays.SkewMap is { Length: > 0 } skew) {
            files["skew"] = skew;
        }

        if (request.Options.Rays.DistanceMap is { Length: > 0 } distance) {
            files["distance"] = distance;
        }

        if (ColourPicture(request.Options) is { Length: > 0 } colour) {
            files["colour"] = colour;
        }

        return files;
    }

    /// <summary>Everything that changes a pixel, and nothing that does not: paths and names stay out.</summary>
    /// <remarks>
    ///     ⚠ <b>The options whole, as JSON, rather than a line per setting.</b> A setting added to the options and
    ///     forgotten here would be a cache that hands back a bake made without it, and nothing would say so. The skew
    ///     map's path is taken out because its bytes are already one of the key's files.
    /// </remarks>
    static Dictionary<string, string> Settings(BakeRequest request) {
        var options = request.Options with {
            Maps = [..request.Options.Maps.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            Rays = request.Options.Rays with { SkewMap = string.Empty, DistanceMap = string.Empty },
            Colour = request.Options.Colour with { Texture = string.Empty }
        };
        var settings = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["options"] = JsonSerializer.Serialize(options, ContractsJsonContext.Default.BakeOptions),
            ["basis"] = TangentBasis
        };
        if (request.Material is { } material) {
            settings["material"] = material; // only then, so a bake of a whole mesh keeps the key it had
        }

        return settings;
    }

    static void Require(bool condition, string message) {
        if (!condition) {
            throw new BakeRefusedException(message);
        }
    }
}
