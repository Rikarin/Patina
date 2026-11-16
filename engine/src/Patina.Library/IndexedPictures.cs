namespace Patina.Library;

/// <summary>A picture in a project, as the index holds it (#306).</summary>
/// <param name="Path">The file, below the library.</param>
/// <param name="Project">Its project, below <c>projects/</c>.</param>
/// <param name="ProjectName">Its project's name.</param>
/// <param name="Width">Its width in texels, or null when its header did not say.</param>
/// <param name="Height">Its height in texels, or null when its header did not say.</param>
public sealed record LibraryPicture(string Path, string Project, string ProjectName, int? Width, int? Height);

/// <summary>
///     The pictures the index holds, each with its project, so a list of every project's pictures is one query and
///     not a walk of every project's folder (#306).
/// </summary>
static class IndexedPictures {
    /// <summary>The pictures' rows: kind 3 is <see cref="TrackedKind.Picture" />.</summary>
    const string PictureRows = "SELECT path, width, height FROM file WHERE kind = 3 ORDER BY path";

    /// <summary>Every picture of every project the index knows, by path.</summary>
    public static IReadOnlyList<LibraryPicture> All(IndexDatabase database) {
        using var connection = database.Open();
        Dictionary<string, string> projects = new(StringComparer.Ordinal);
        using (var command = connection.CreateCommand()) {
            command.CommandText = "SELECT path, name FROM project";
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                projects[reader.GetString(0)] = reader.GetString(1);
            }
        }

        List<LibraryPicture> found = [];
        using (var command = connection.CreateCommand()) {
            command.CommandText = PictureRows;
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                var path = reader.GetString(0);
                if (ProjectOf(projects, path) is { } project) {
                    found.Add(
                        new(
                            path,
                            project,
                            projects[project],
                            reader.IsDBNull(1) ? null : reader.GetInt32(1),
                            reader.IsDBNull(2) ? null : reader.GetInt32(2)
                        )
                    );
                }
            }
        }

        return found;
    }

    /// <summary>The project a file is in: the nearest folder above it that is one, or null for none indexed.</summary>
    static string? ProjectOf(Dictionary<string, string> projects, string path) {
        var below = path[(LibraryLayout.ProjectsFolder.Length + 1)..];
        for (var end = below.LastIndexOf('/'); end > 0; end = below.LastIndexOf('/', end - 1)) {
            if (projects.ContainsKey(below[..end])) {
                return below[..end];
            }
        }

        return null;
    }
}
