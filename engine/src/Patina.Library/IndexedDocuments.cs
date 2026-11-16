using Patina.Contracts;
using System.Globalization;

namespace Patina.Library;

/// <summary>A document of a project's, as the index holds it.</summary>
/// <param name="Project">Its project, below <c>projects/</c>.</param>
/// <param name="ProjectName">Its project's name.</param>
/// <param name="Document">The document, as its project lists it, with no mesh or bake.</param>
public sealed record IndexedDocument(string Project, string ProjectName, ProjectDocumentInfo Document);

/// <summary>
///     The documents the index holds, each with its project, so a list of every project's documents is one query and
///     not a read of every project's file and a walk of its folder. A texturing document is no asset and is not here;
///     a graph beside a project that its file does not list is, with no id, as the project lists it.
/// </summary>
static class IndexedDocuments {
    const char TagSeparator = '\u001f';

    /// <summary>The assets' rows, with their tags in order.</summary>
    const string AssetRows = """
                             SELECT a.id, a.kind, a.document, a.name, a.path,
                                 (SELECT group_concat(tag, char(31))
                                  FROM (SELECT tag FROM asset_tag t WHERE t.asset = a.rid ORDER BY tag))
                             FROM asset a ORDER BY a.path
                             """;

    /// <summary>
    ///     The graphs beside a project that its file does not list: kind 2 is <see cref="TrackedKind.Content" />.
    /// </summary>
    const string UnlistedRows = """
                                SELECT f.path FROM file f
                                WHERE f.kind = 2 AND NOT EXISTS(SELECT 1 FROM asset a WHERE a.path = f.path)
                                ORDER BY f.path
                                """;

    /// <summary>Every document of every project the index knows, by path.</summary>
    public static IReadOnlyList<IndexedDocument> All(IndexDatabase database) {
        using var connection = database.Open();
        var projects = IndexedPictures.Projects(connection);
        List<IndexedDocument> found = [];
        using (var command = connection.CreateCommand()) {
            command.CommandText = AssetRows;
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                var path = reader.GetString(4);
                if (IndexedPictures.ProjectOf(projects, path) is not { } project) {
                    continue;
                }

                var kind = reader.GetString(2);
                var role = reader.GetString(1);
                ProjectDocumentInfo document = new(
                    Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                    kind,
                    reader.GetString(3),
                    path,
                    null,
                    role == kind ? null : role
                ) { Tags = reader.IsDBNull(5) ? [] : reader.GetString(5).Split(TagSeparator) };
                found.Add(new(project, projects[project], document));
            }
        }

        using (var command = connection.CreateCommand()) {
            command.CommandText = UnlistedRows;
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                var path = reader.GetString(0);
                if (path.EndsWith(LibraryLayout.GraphExtension, StringComparison.Ordinal)
                    && IndexedPictures.ProjectOf(projects, path) is { } project) {
                    ProjectDocumentInfo document = new(
                        null,
                        DocumentKinds.Graph,
                        Path.GetFileNameWithoutExtension(path),
                        path,
                        null
                    );
                    found.Add(new(project, projects[project], document));
                }
            }
        }

        return found;
    }
}
