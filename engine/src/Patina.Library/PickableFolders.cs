using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     The folders of a library a client can choose to write into, such as an export's or a bake's, where a dialog of
///     its own machine cannot see the Server's disk (#4, ADR 0033): every project's folder and the folders in it, the
///     asked project's first, then the library's exports folder and the folders in that. A hidden folder, and a project
///     inside a project, are not offered.
/// </summary>
static class PickableFolders {
    /// <summary>The folders, with paths below the library, and whether more were left out.</summary>
    /// <param name="root">The library.</param>
    /// <param name="first">The project, below <c>projects/</c>, whose folders come first, if any.</param>
    /// <param name="limit">How many to give at most.</param>
    public static (IReadOnlyList<LibraryFile> Folders, bool More) List(string root, string? first, int limit) {
        List<LibraryFile> found = [];
        var projects = Path.Combine(root, LibraryLayout.ProjectsFolder);
        var owned = Directory.Exists(projects)
            ? ProjectFiles.InOrder(projects, first)
                .Select(project => (
                        Full: Path.Combine(projects, project),
                        Below: $"{LibraryLayout.ProjectsFolder}/{project}",
                        Project: project
                    )
                )
            : [];
        var exports = LibraryLayout.ExportsPath(root);
        var all = Directory.Exists(exports)
            ? owned.Append((Full: exports, Below: Path.GetFileName(exports), Project: string.Empty))
            : owned;
        foreach (var (full, below, project) in all) {
            foreach (var folder in Below(full, below)) {
                if (found.Count == limit) {
                    return (found, true);
                }

                found.Add(new(folder, folder[(folder.LastIndexOf('/') + 1)..], project));
            }
        }

        return (found, false);
    }

    /// <summary>A folder and those in it, depth first by name, stopping at hidden folders and other projects.</summary>
    static IEnumerable<string> Below(string full, string below) {
        yield return below;
        IEnumerable<string> inside;
        try {
            inside = Directory.EnumerateDirectories(full).Order(StringComparer.OrdinalIgnoreCase).ToList();
        } catch (IOException) {
            yield break; // gone since it was listed
        } catch (UnauthorizedAccessException) {
            yield break;
        }

        foreach (var folder in inside) {
            var name = Path.GetFileName(folder);
            if (name.StartsWith('.') || File.Exists(Path.Combine(folder, LibraryLayout.ProjectFile))) {
                continue;
            }

            foreach (var nested in Below(folder, $"{below}/{name}")) {
                yield return nested;
            }
        }
    }
}
