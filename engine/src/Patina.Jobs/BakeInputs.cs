using Patina.Contracts;
using Patina.Library;
using System.Globalization;

namespace Patina.Jobs;

/// <summary>Why a bake was refused: a wrong number or a wrong name, not a missing file.</summary>
public sealed class BakeRefusedException : Exception {
    public BakeRefusedException() { }

    public BakeRefusedException(string message) : base(message) { }

    public BakeRefusedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>What a bake is allowed to ask for, and the key of what it asks for.</summary>
public static class BakeInputs {
    public const string Png8 = "png8";
    public const string Png16 = "png16";
    public const string Exr16 = "exr16";
    public const string Exr32 = "exr32";

    /// <summary>The largest atlas edge. Larger than the cached maps a laptop can hold, not than a farm can.</summary>
    const int MaxResolution = 16384;

    /// <summary>
    ///     What a map's numbers are relative to, and how its rows run. A bake made before this was a per-triangle
    ///     basis upside down, and a cache hit must not hand one back.
    /// </summary>
    const string TangentBasis = "mikktspace, v down";

    const int MaxSupersampling = 8;
    const int MaxRays = 8192;

    /// <summary>The maps a request may ask for by name, in the order they are reported.</summary>
    public static IReadOnlyList<string> Maps { get; } =
        ["ao", "bent", "curvature", "thickness", "position", "world", "id"];

    /// <summary>The maps every bake makes.</summary>
    public static IReadOnlyList<string> Always { get; } = ["normal", "height"];

    /// <summary>The refusal a request earns, or null when it is fine. Files are looked at last.</summary>
    /// <exception cref="BakeRefusedException">It asks for something no bake can do.</exception>
    /// <exception cref="FileNotFoundException">An input file is not there.</exception>
    public static void Check(BakeRequest request) {
        Require(request.Resolution is >= 16 and <= MaxResolution, $"The resolution must be 16 to {MaxResolution}.");
        Require(
            request.Supersampling is >= 1 and <= MaxSupersampling,
            $"Supersampling must be 1 to {MaxSupersampling}."
        );
        Require(request.OcclusionRays is >= 1 and <= MaxRays, $"Occlusion rays must be 1 to {MaxRays}.");
        Require(request.Gutter is >= 0 and <= 64, "The gutter must be 0 to 64 texels.");
        Require(request.SearchRadius > 0, "The search radius must be more than nothing.");
        Require(request.OcclusionRadius >= 0, "The occlusion radius cannot be negative.");
        Require(request.Skew is >= 0 and <= 1, "The skew must be 0 to 1.");
        Require(request.Format is Png8 or Png16 or Exr16 or Exr32, $"'{request.Format}' is not a format.");
        Require(request.Space is "tangent" or "object", $"'{request.Space}' is not a space.");
        Require(request.Output.Length > 0, "A bake needs an output folder.");

        foreach (var map in request.Maps ?? []) {
            Require(
                Maps.Contains(map, StringComparer.Ordinal) || Always.Contains(map, StringComparer.Ordinal),
                $"'{map}' is not a map. The maps are {string.Join(", ", [..Always, ..Maps])}."
            );
        }

        foreach (var file in Files(request).Values) {
            if (!File.Exists(file)) {
                throw new FileNotFoundException($"There is no file at '{file}'.", file);
            }
        }
    }

    /// <summary>The key of what a request would make, which reads every input whole.</summary>
    public static BuildKey KeyOf(BakeRequest request, ToolCatalog tools) =>
        BakeKeys.Compute(
            Files(request).ToDictionary(static file => file.Key, static file => Revision.OfFile(file.Value)),
            Settings(request),
            tools
        );

    static Dictionary<string, string> Files(BakeRequest request) {
        Dictionary<string, string> files =
            new(StringComparer.Ordinal) { ["high"] = request.High, ["low"] = request.Low };
        if (request.Cage is { } cage) {
            files["cage"] = cage;
        }

        if (request.SkewMap is { } skew) {
            files["skew"] = skew;
        }

        return files;
    }

    /// <summary>Everything that changes a pixel, and nothing that does not: paths and names stay out.</summary>
    static Dictionary<string, string> Settings(BakeRequest request) {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["resolution"] = Text(request.Resolution),
            ["maps"] = string.Join(
                ',',
                (request.Maps ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            ),
            ["format"] = request.Format,
            ["supersampling"] = Text(request.Supersampling),
            ["rays"] = Text(request.OcclusionRays),
            ["search"] = Text(request.SearchRadius),
            ["reach"] = Text(request.OcclusionRadius),
            ["skew"] = Text(request.Skew),
            ["match"] = request.MatchByName ? "1" : "0",
            ["gutter"] = Text(request.Gutter),
            ["space"] = request.Space,
            ["basis"] = TangentBasis
        };
        if (request.Material is { } material) {
            settings["material"] = material; // only then, so a bake of a whole mesh keeps the key it had
        }

        return settings;
    }

    static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    static string Text(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    static void Require(bool condition, string message) {
        if (!condition) {
            throw new BakeRefusedException(message);
        }
    }
}
