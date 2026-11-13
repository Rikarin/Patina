using Patina.Contracts;
using System.Text.RegularExpressions;

namespace Patina.Library;

/// <summary>A folder of projects as the disk has it.</summary>
/// <param name="Path">Its path below <c>projects/</c>, with forward slashes.</param>
/// <param name="Name">Its name, which is its folder's name.</param>
/// <param name="Color">Its colour as <c>#rrggbb</c>, or null for none.</param>
public sealed record LibraryFolder(string Path, string Name, string? Color);

/// <summary>
///     The folders that group a library's projects. A folder here is a folder on disk, because a library is a Git
///     repository people also read as files, and every project keeps its GUID when one moves, so nothing that refers to
///     it notices. The index knows nothing of folders: it holds projects, and a listing of one folder costs one
///     directory.
/// </summary>
static partial class LibraryFolders {
    /// <summary>The folders directly in <paramref name="parent" />, by name.</summary>
    /// <exception cref="FileNotFoundException">There is no such folder.</exception>
    /// <exception cref="ProjectRefusedException">The path is not one a library can hold.</exception>
    public static IReadOnlyList<LibraryFolder> List(string root, string parent) {
        var path = Clean(parent);
        var full = Required(root, path);
        return [
            ..LibraryScanner.VisibleFolders(full)
                .Where(static child => !IsProject(child))
                .Select(child => Describe(child, Join(path, Path.GetFileName(child))))
                .OrderBy(static folder => folder.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>Makes a folder and returns it.</summary>
    /// <exception cref="ProjectExistsException">The name is taken.</exception>
    /// <exception cref="ProjectRefusedException">The name or the colour cannot be used.</exception>
    public static LibraryFolder Create(string root, string parent, string name, string? color) {
        var into = Clean(parent);
        var trimmed = Name(name);
        var document = new FolderDocument(Colour(color));
        var path = Join(into, trimmed);
        _ = Required(root, into);
        var full = Full(root, path);
        if (Exists(full)) {
            throw new ProjectExistsException($"'{trimmed}' already exists here.");
        }

        Directory.CreateDirectory(full);
        Write(full, document);
        return new(path, trimmed, document.Color);
    }

    /// <summary>Gives a folder a colour, or none.</summary>
    /// <exception cref="FileNotFoundException">There is no such folder.</exception>
    /// <exception cref="ProjectRefusedException">It is no colour, or the path is the top of the library.</exception>
    public static void Recolour(string root, string path, string? color) {
        var document = new FolderDocument(Colour(color));
        Write(Required(root, Existing(path)), document);
    }

    /// <summary>Renames a folder and returns its new path.</summary>
    /// <exception cref="FileNotFoundException">There is no such folder.</exception>
    /// <exception cref="ProjectExistsException">The name is taken.</exception>
    /// <exception cref="ProjectRefusedException">The name cannot be used.</exception>
    public static string Rename(string root, string path, string name) {
        var from = Existing(path);
        var trimmed = Name(name);
        var full = Required(root, from);
        var to = Join(Parent(from), trimmed);
        var target = Full(root, to);
        if (to == from) {
            return to;
        }

        // A change of case alone is the same folder on macOS and Windows, and still a rename.
        if (Exists(target) && !string.Equals(to, from, StringComparison.OrdinalIgnoreCase)) {
            throw new ProjectExistsException($"'{trimmed}' already exists here.");
        }

        SharedFile.MoveFolder(full, target);
        return to;
    }

    /// <summary>
    ///     Moves folders and projects into a folder, all of them or none: everything is checked before anything moves,
    ///     and what moved is put back if the disk refuses partway.
    /// </summary>
    /// <returns>The new paths, in the order asked.</returns>
    /// <exception cref="FileNotFoundException">Something named is not there.</exception>
    /// <exception cref="ProjectExistsException">A name is taken where it is going.</exception>
    /// <exception cref="ProjectRefusedException">The move cannot be made.</exception>
    public static IReadOnlyList<string> Move(string root, IReadOnlyList<string> paths, string into) {
        var target = Clean(into);
        _ = Required(root, target);
        List<(string From, string To)> moves = [];
        foreach (var path in paths.Select(Existing).Distinct(StringComparer.Ordinal)) {
            if (!Exists(Full(root, path))) {
                throw new FileNotFoundException($"There is no '{path}' in this library.", path);
            }

            if (target == path || target.StartsWith(path + "/", StringComparison.Ordinal)) {
                throw new ProjectRefusedException($"'{path}' cannot go inside itself.");
            }

            moves.Add((path, Join(target, Path.GetFileName(path))));
        }

        foreach (var clash in moves.GroupBy(static m => m.To, StringComparer.OrdinalIgnoreCase)) {
            if (clash.Count() > 1) {
                throw new ProjectExistsException($"Two of these are called '{Path.GetFileName(clash.Key)}'.");
            }
        }

        foreach (var (from, to) in moves.Where(static m => m.From != m.To)) {
            if (Exists(Full(root, to))) {
                throw new ProjectExistsException($"'{Path.GetFileName(from)}' already exists in '{Display(target)}'.");
            }
        }

        Apply(root, moves);
        return [..moves.Select(static m => m.To)];
    }

    /// <summary>
    ///     Deletes a folder. One with something in it is refused unless <paramref name="lift" /> says to move what is
    ///     in it up a level first, so this never deletes a project.
    /// </summary>
    /// <exception cref="FileNotFoundException">There is no such folder.</exception>
    /// <exception cref="ProjectExistsException">It holds something.</exception>
    /// <exception cref="ProjectRefusedException">It holds a file that is no project or folder.</exception>
    public static void Delete(string root, string path, bool lift) {
        var folder = Existing(path);
        var full = Required(root, folder);
        var ownFile = Path.Combine(full, LibraryLayout.FolderFile);
        var stray = Directory.EnumerateFiles(full).FirstOrDefault(file => file != ownFile);
        if (stray is not null) {
            throw new ProjectRefusedException($"'{folder}' holds '{Path.GetFileName(stray)}', which is not a project.");
        }

        List<string> children = [
            ..Directory.EnumerateDirectories(full).Select(child => Join(folder, Path.GetFileName(child)))
        ];
        if (children.Count > 0) {
            if (!lift) {
                throw new ProjectExistsException($"'{folder}' is not empty.");
            }

            _ = Move(root, children, Parent(folder));
        }

        File.Delete(ownFile);
        Directory.Delete(full);
    }

    /// <summary>A path below <c>projects/</c> of something that is there to act on, so never the top.</summary>
    /// <exception cref="ProjectRefusedException">The path is the top or not one a library can hold.</exception>
    internal static string Existing(string path) {
        var clean = Clean(path);
        return clean.Length > 0
            ? clean
            : throw new ProjectRefusedException("The top of the library cannot be changed.");
    }

    /// <summary>A path a client gave, with its slashes tidied and every segment one a library can hold.</summary>
    /// <exception cref="ProjectRefusedException">A segment cannot be a folder.</exception>
    internal static string Clean(string? path) {
        var clean = path?.Trim().Trim('/') ?? string.Empty;
        foreach (var segment in clean.Length == 0 ? [] : clean.Split('/')) {
            NewProjects.Require(segment);
        }

        return clean;
    }

    internal static string Full(string root, string path) =>
        LibraryLayout.Full(
            root,
            path.Length == 0 ? LibraryLayout.ProjectsFolder : $"{LibraryLayout.ProjectsFolder}/{path}"
        );

    static void Apply(string root, List<(string From, string To)> moves) {
        var moved = 0;
        try {
            foreach (var (from, to) in moves) {
                if (from != to) {
                    SharedFile.MoveFolder(Full(root, from), Full(root, to));
                }

                moved++;
            }
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            foreach (var (from, to) in moves.Take(moved).Where(static m => m.From != m.To)) {
                SharedFile.MoveFolder(Full(root, to), Full(root, from));
            }

            throw;
        }
    }

    /// <summary>The folder at a path, which has to be a folder: a project is not one to put things in.</summary>
    static string Required(string root, string path) {
        var segments = path.Length == 0 ? [] : path.Split('/');
        for (var i = 1; i <= segments.Length; i++) {
            var at = Full(root, string.Join('/', segments[..i]));
            if (IsProject(at)) {
                throw new ProjectRefusedException($"'{Path.GetFileName(at)}' is a project, and projects do not nest.");
            }
        }

        var full = Full(root, path);
        return Directory.Exists(full)
            ? full
            : throw new FileNotFoundException($"There is no folder '{path}' in this library.", path);
    }

    static LibraryFolder Describe(string full, string path) {
        var file = Path.Combine(full, LibraryLayout.FolderFile);
        string? color = null;
        if (File.Exists(file)) {
            try {
                color = DocumentCodec.ReadFolder(SharedFile.ReadAllText(file), file).Document.Color;
            } catch (LibraryFormatException) {
                // A colour is a decoration, and a merge conflict in one should not hide the projects under it.
            }
        }

        return new(path, Path.GetFileName(full), color);
    }

    static void Write(string full, FolderDocument document) =>
        LibraryFiles.Replace(Path.Combine(full, LibraryLayout.FolderFile), DocumentCodec.Write(document));

    static string Name(string name) {
        var trimmed = name.Trim();
        NewProjects.Require(trimmed);
        return trimmed.Contains('/', StringComparison.Ordinal)
            ? throw new ProjectRefusedException("A folder's name has no slash in it.")
            : trimmed;
    }

    static string? Colour(string? color) {
        var text = color?.Trim();
        if (string.IsNullOrEmpty(text)) {
            return null;
        }

        return HexColour().IsMatch(text)
            ? text.ToLowerInvariant()
            : throw new ProjectRefusedException($"'{color}' is not a color like #d05a3c.");
    }

    static bool IsProject(string full) => File.Exists(Path.Combine(full, LibraryLayout.ProjectFile));

    static bool Exists(string full) => Directory.Exists(full) || File.Exists(full);

    static string Join(string parent, string name) => parent.Length == 0 ? name : $"{parent}/{name}";

    static string Parent(string path) =>
        path.Contains('/', StringComparison.Ordinal) ? path[..path.LastIndexOf('/')] : string.Empty;

    static string Display(string path) => path.Length == 0 ? "the top of the library" : path;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColour();
}
