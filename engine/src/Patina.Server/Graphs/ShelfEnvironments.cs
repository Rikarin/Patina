using Patina.Contracts;

namespace Patina.Server.Graphs;

/// <summary>
///     The panoramas on the shelf (#203), which light the viewport: a project's HDR or EXR picture is listed as a
///     picture and as an environment, whose source is the picture's path behind <see cref="Prefix" />, so the two are
///     told apart and drawn as the same picture.
/// </summary>
public static class ShelfEnvironments {
    /// <summary>The type of an environment on the shelf.</summary>
    public const string Role = "environment";

    /// <summary>What an environment's source starts with, before its picture's path below the library.</summary>
    public const string Prefix = "environment:";

    /// <summary>The environments among some pictures on the shelf.</summary>
    public static IEnumerable<MaterialInfo> Among(IEnumerable<MaterialInfo> pictures) =>
        pictures.Where(static picture => PictureFormats.IsEnvironment(picture.Source)
                && !PictureFormats.IsEnvironmentGraph(picture.Source)
            )
            .Select(static picture => picture with { Source = Prefix + picture.Source, Role = Role });

    /// <summary>The picture an environment's source names, or null when it names none.</summary>
    public static string? PictureOf(string source) =>
        source.StartsWith(Prefix, StringComparison.Ordinal) ? source[Prefix.Length..] : null;
}
