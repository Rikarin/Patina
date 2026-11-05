using System.Globalization;
using System.Text;

namespace Patina.Library;

/// <summary>A project about to exist.</summary>
/// <param name="Name">The display name. The folder is made from it.</param>
/// <param name="Folder">Folders below <c>projects/</c>, with forward slashes, or null for none.</param>
/// <param name="Tags">The project's tags.</param>
public sealed record NewProject(string Name, string? Folder = null, IReadOnlyList<string>? Tags = null);

/// <summary>A graph about to exist.</summary>
/// <param name="Name">The graph's name. The file name is made from it.</param>
/// <param name="Project">A project's path below <c>projects/</c>, or null for <c>shared/graphs</c>.</param>
public sealed record NewGraph(string Name, string? Project = null);

/// <summary>A new project that cannot be written as asked; the message says why, to the person who asked.</summary>
public class ProjectRefusedException : Exception {
    public ProjectRefusedException() { }

    public ProjectRefusedException(string message) : base(message) { }

    public ProjectRefusedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>A new project whose folder is taken, which trying again with the same request cannot fix.</summary>
public sealed class ProjectExistsException : ProjectRefusedException {
    public ProjectExistsException() { }

    public ProjectExistsException(string message) : base(message) { }

    public ProjectExistsException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
///     The rules a new project's name, folder and tags meet. A library is a Git repository shared between platforms,
///     so a folder has to be legal on all of them, and has to be one the scan will walk into: it skips hidden folders.
/// </summary>
static class NewProjects {
    const int MaxNameLength = 200;
    const int MaxSegmentLength = 64;
    const int MaxTagLength = 64;

    static readonly string[] ReservedNames = [
        "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1",
        "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
    ];

    /// <summary>The document and the path below <c>projects/</c> for <paramref name="project" />.</summary>
    /// <exception cref="ProjectRefusedException">The request breaks one of the rules.</exception>
    public static (string Path, ProjectDocument Document) Resolve(NewProject project) {
        var name = project.Name.Trim();
        if (name.Length is 0 or > MaxNameLength || name.Any(char.IsControl)) {
            throw new ProjectRefusedException($"A project's name is 1 to {MaxNameLength} characters of text.");
        }

        var slug = Slug(name);
        if (slug.Length == 0) {
            throw new ProjectRefusedException("A project's name needs a letter or a digit, to name its folder.");
        }

        var folder = project.Folder?.Trim().Trim('/') ?? string.Empty;
        foreach (var segment in folder.Length == 0 ? [] : folder.Split('/')) {
            Require(segment);
        }

        Require(slug);
        var path = folder.Length == 0 ? slug : $"{folder}/{slug}";
        return (path, new(ProjectId.New(), name, Tags(project.Tags), [], []));
    }

    /// <summary>The path below the library, with forward slashes, for <paramref name="graph" />.</summary>
    /// <exception cref="ProjectRefusedException">The request breaks one of the rules.</exception>
    public static string GraphPath(NewGraph graph) {
        var name = graph.Name.Trim();
        if (name.Length is 0 or > MaxNameLength || name.Any(char.IsControl)) {
            throw new ProjectRefusedException($"A graph's name is 1 to {MaxNameLength} characters of text.");
        }

        var slug = Slug(name);
        if (slug.Length == 0) {
            throw new ProjectRefusedException("A graph's name needs a letter or a digit, to name its file.");
        }

        Require(slug);
        var file = slug + LibraryLayout.GraphExtension;
        if (graph.Project?.Trim().Trim('/') is not { Length: > 0 } project) {
            return $"{LibraryLayout.SharedFolder}/graphs/{file}";
        }

        foreach (var segment in project.Split('/')) {
            Require(segment);
        }

        return $"{LibraryLayout.ProjectsFolder}/{project}/{file}";
    }

    /// <summary>The folder for a name: lower case, with letters and digits kept and every other run a hyphen.</summary>
    internal static string Slug(string name) {
        var slug = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormC)) {
            if (char.IsLetterOrDigit(c)) {
                slug.Append(char.ToLowerInvariant(c));
            } else if (slug.Length > 0 && slug[^1] != '-') {
                slug.Append('-');
            }
        }

        var text = slug.ToString().TrimEnd('-');
        return text.Length <= MaxSegmentLength ? text : text[..MaxSegmentLength].TrimEnd('-');
    }

    internal static void Require(string segment) {
        var legal = segment.Length is > 0 and <= MaxSegmentLength
            && segment[0] != '.'
            && !segment.EndsWith('.')
            && segment == segment.Trim()
            && !segment.Any(static c => char.IsControl(c) || """\:*?"<>|""".Contains(c, StringComparison.Ordinal))
            && !ReservedNames.Contains(segment.Split('.')[0].ToLower(CultureInfo.InvariantCulture));
        if (!legal) {
            throw new ProjectRefusedException($"'{segment}' cannot be a folder in a library.");
        }
    }

    static string[] Tags(IReadOnlyList<string>? tags) {
        var cleaned = (tags ?? []).Select(static t => t.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (cleaned.Any(static t => t.Length is 0 or > MaxTagLength || t.Any(char.IsControl))) {
            throw new ProjectRefusedException($"A tag is 1 to {MaxTagLength} characters of text.");
        }

        Array.Sort(cleaned, StringComparer.Ordinal);
        return cleaned;
    }
}
