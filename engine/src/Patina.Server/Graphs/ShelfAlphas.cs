using Patina.Contracts;
using Patina.Library;
using Vixen.Texturing.Painting;

namespace Patina.Server.Graphs;

/// <summary>
///     The brush alphas on the shelf (#203): the shapes a brush's stamp can take, which a click makes the brush's. A
///     client draws each named shape as a stamp of the brush in hand, so the shelf asks the Server for no picture of
///     one. Beside them are the pictures Patina ships as alphas, from <c>content/alphas</c> (#34): one is drawn as the
///     picture it is, and taking it up copies it into the project's <c>brushes/</c>, since a brush names its alpha from
///     the project.
/// </summary>
public static class ShelfAlphas {
    /// <summary>The type of an alpha on the shelf.</summary>
    public const string Role = "alpha";

    /// <summary>What an alpha's source starts with, before the name a brush takes: <c>alpha:Square</c>.</summary>
    public const string Prefix = "alpha:";

    /// <summary>A shipped picture alpha's source begins with this, then its name: <c>alpha-picture:Leaf</c>.</summary>
    public const string PicturePrefix = "alpha-picture:";

    /// <summary>The folder of a project a brush's pictures are kept in.</summary>
    public const string BrushFolder = "brushes";

    /// <summary>Where the pictures are embedded, before each one's name.</summary>
    const string Resource = "Alpha/";

    /// <summary>The folder of the preview cache a picture alpha is unpacked into, for the Worker to draw.</summary>
    const string Unpacked = "alphas";

    /// <summary>The names of the pictures Patina ships as alphas, by name.</summary>
    static readonly string[] Pictures = [
        ..typeof(ShelfAlphas).Assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(Resource, StringComparison.Ordinal))
            .Select(static name => name[Resource.Length..])
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>Every alpha a brush can name, round first, then every picture Patina ships as one.</summary>
    public static IReadOnlyList<MaterialInfo> All { get; } = [
        ..PaintAlphas.Names.Select(static name => new MaterialInfo(name, Role, Prefix + name, Role, "shipped", [])),
        ..Pictures.Select(static name => new MaterialInfo(name, Role, PicturePrefix + name, Role, "shipped", []))
    ];

    /// <summary>Whether a source is an alpha on the shelf, of either kind.</summary>
    public static bool Names(string source) =>
        source.StartsWith(Prefix, StringComparison.Ordinal)
        || source.StartsWith(PicturePrefix, StringComparison.Ordinal);

    /// <summary>
    ///     A shipped picture alpha as a file a picture can be drawn from, unpacked into a folder of the preview cache;
    ///     null for a shape, which a client draws, and for a name Patina ships no picture of.
    /// </summary>
    /// <param name="source">The alpha's source on the shelf.</param>
    /// <param name="cache">The preview cache's folder, which the Worker reads.</param>
    public static string? PictureFile(string source, string cache) {
        if (Bytes(source) is not { } bytes) {
            return null;
        }

        var file = Path.Combine(cache, Unpacked, source[PicturePrefix.Length..] + ".png");
        if (!File.Exists(file) || !SharedFile.ReadAllBytes(file).AsSpan().SequenceEqual(bytes)) {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, bytes);
        }

        return file;
    }

    /// <summary>
    ///     Copies a shipped picture alpha into a project's <c>brushes/</c> and says the path a brush names it by. A
    ///     file of that name that is the same picture is used again; one that is another picture is left, and the copy
    ///     takes the next name free.
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="project">The project, by its path below the library.</param>
    /// <param name="source">The alpha's source on the shelf.</param>
    /// <returns>The path below the project, such as <c>brushes/Splatter.png</c>, or null for no such alpha.</returns>
    public static string? Bring(OpenLibrary library, string project, string source) {
        if (Bytes(source) is not { } bytes) {
            return null;
        }

        var name = source[PicturePrefix.Length..];
        for (var other = 1;; other++) {
            var alpha = $"{BrushFolder}/{(other == 1 ? name : $"{name} {other}")}.png";
            var file = library.ProjectFile(project, alpha);
            if (File.Exists(file) && !SharedFile.ReadAllBytes(file).AsSpan().SequenceEqual(bytes)) {
                continue;
            }

            if (!File.Exists(file)) {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, bytes);
            }

            return alpha;
        }
    }

    /// <summary>A shipped picture alpha's PNG, or null for any other source.</summary>
    static byte[]? Bytes(string source) {
        if (!source.StartsWith(PicturePrefix, StringComparison.Ordinal)
            || !Pictures.Contains(source[PicturePrefix.Length..], StringComparer.Ordinal)) {
            return null;
        }

        using var stream = typeof(ShelfAlphas).Assembly
            .GetManifestResourceStream(Resource + source[PicturePrefix.Length..])!;
        using MemoryStream read = new();
        stream.CopyTo(read);
        return read.ToArray();
    }
}
