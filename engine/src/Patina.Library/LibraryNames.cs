using Patina.Contracts;
using System.Text;

namespace Patina.Library;

/// <summary>
///     A name of a library file that a file holds (#162, #297): a picture of a project's <c>pictures/</c> folder, or a
///     file in the library's uploads, from its folder's name on. Both are lower case, with forward slashes.
/// </summary>
/// <param name="Before">The text right before the name, back to a slash, when a slash comes just before it.</param>
/// <param name="Name">The name, from <c>pictures/</c> or <c>uploads/</c> to the end of a picture's or a mesh's extension.</param>
readonly record struct LibraryName(string Before, string Name);

/// <summary>
///     Finds the names of pictures and uploads in a file's text, however it writes them: either slash, a JSON string's
///     doubled backslash, a link's <c>%20</c> for a space, any case. What it finds errs towards more: every way the text
///     could end a name with a known extension is one, so a name is found wherever the text holds it.
/// </summary>
static class LibraryNameReader {
    /// <summary>The largest file read for names. A larger one is taken to name anything (null names).</summary>
    public const long MostRead = 64L << 20;

    /// <summary>How far a name runs past its folder at the most.</summary>
    const int LongestName = 1024;

    /// <summary>How much of the text before a name is kept: a project's folder's name, as a folder name runs.</summary>
    const int LongestBefore = 255;

    static readonly string[] Markers = [ProjectFiles.PictureFolder + "/", LibraryUploads.Folder + "/"];

    static readonly string[] Extensions = [
        ..PictureFormats.Pictures.Concat(MeshFormats.Meshes).Distinct(StringComparer.Ordinal)
    ];

    /// <summary>The names a file holds, or null when it is too large to read.</summary>
    public static HashSet<LibraryName>? ReadFile(string file) {
        try {
            return new FileInfo(file).Length > MostRead ? null : Read(Encoding.UTF8.GetString(SharedFile.ReadAllBytes(file)));
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return []; // gone as it was read
        }
    }

    /// <summary>The names a text holds.</summary>
    public static HashSet<LibraryName> Read(string text) {
        var normal = Normal(text);
        HashSet<LibraryName> found = [];
        foreach (var marker in Markers) {
            for (var at = normal.IndexOf(marker, StringComparison.Ordinal);
                 at >= 0;
                 at = normal.IndexOf(marker, at + 1, StringComparison.Ordinal)) {
                Add(found, normal, at);
            }
        }

        return found;
    }

    /// <summary>A name as this reads one: lower case, with forward slashes.</summary>
    public static string Normal(string text) =>
        text.Replace(@"\\", "/", StringComparison.Ordinal)
            .Replace('\\', '/')
            .Replace("%20", " ", StringComparison.Ordinal)
            .ToLowerInvariant();

    static void Add(HashSet<LibraryName> found, string text, int at) {
        var before = Before(text, at);
        var window = text.AsSpan(at, Math.Min(LongestName, text.Length - at));
        foreach (var extension in Extensions) {
            for (var end = window.IndexOf(extension, StringComparison.Ordinal);
                 end >= 0;
                 end = Next(window, extension, end)) {
                var name = window[..(end + extension.Length)];
                if (name.IndexOfAny('\n', '\r', '\t') < 0) {
                    found.Add(new(before, name.ToString()));
                }
            }
        }
    }

    static int Next(ReadOnlySpan<char> window, string extension, int end) {
        var rest = window[(end + 1)..].IndexOf(extension, StringComparison.Ordinal);
        return rest < 0 ? -1 : end + 1 + rest;
    }

    /// <summary>The text between the slash before a name and the slash before that, or none.</summary>
    static string Before(string text, int at) {
        if (at == 0 || text[at - 1] != '/') {
            return string.Empty;
        }

        var from = Math.Max(0, at - 1 - LongestBefore);
        var slash = text.LastIndexOf('/', at - 2 < 0 ? 0 : at - 2, at - 1 - from);
        var start = slash < from ? from : slash + 1;
        return at - 1 > start ? text[start..(at - 1)] : string.Empty;
    }
}
