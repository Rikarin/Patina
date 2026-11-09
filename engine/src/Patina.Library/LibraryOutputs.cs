using Microsoft.Data.Sqlite;
using Patina.Contracts;
using System.Collections.Concurrent;

namespace Patina.Library;

/// <summary>Which projects a build looks at.</summary>
/// <param name="Asset">Only what reads this asset, or null for all.</param>
/// <param name="Projects">Only these, or null for all.</param>
public sealed record OutputSelection(AssetId? Asset = null, IReadOnlyList<ProjectId>? Projects = null) {
    public static OutputSelection All { get; } = new();

    /// <summary>Whether this is the whole library.</summary>
    public bool IsEverything => Asset is null && Projects is not { Count: > 0 };
}

/// <summary>An output as a build sees it.</summary>
/// <param name="Output">Its declaration.</param>
/// <param name="Key">Its key, or null when an input cannot be resolved.</param>
/// <param name="Reach">How the selected asset reaches it. Moves when no asset was selected.</param>
/// <param name="Assets">
///     The full path of each shared file it reads, in <see cref="OutputDeclaration.Uses" />
///     order.
/// </param>
/// <param name="Problem">Why it has no key.</param>
public sealed record ScannedOutput(
    OutputDeclaration Output,
    BuildKey? Key,
    AssetReach Reach,
    IReadOnlyList<string> Assets,
    string? Problem
);

/// <summary>A project as a build sees it.</summary>
/// <param name="Id">The project.</param>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its path below <c>projects/</c>.</param>
/// <param name="Outputs">Everything it declares.</param>
public sealed record ScannedProject(ProjectId Id, string Name, string Path, IReadOnlyList<ScannedOutput> Outputs);

/// <summary>Every output in a selection, by project path.</summary>
/// <param name="Projects">The projects with at least one output.</param>
public sealed record OutputScan(IReadOnlyList<ScannedProject> Projects);

/// <summary>
///     Works out outputs' keys for a build, by the same rules as the health check, which is why it shares the graph and
///     the input hashes with it. Each project's file is read once; a selection reads only the projects it names.
/// </summary>
sealed class LibraryOutputs(string root, IndexDatabase database, LibraryQueries queries, ToolCatalog tools) {
    const int BatchSize = 256;

    public OutputScan Scan(OutputSelection selection, CancellationToken cancellationToken) {
        using var connection = database.Open();
        var graph = LibraryCheck.LoadGraph(connection);
        var paths = LibraryCheck.LoadAssetPaths(connection);
        var hashes = new LibraryCheck.InputHashes(root, connection);
        var found = new ConcurrentBag<ScannedProject>();

        foreach (var batch in Selected(connection, selection).Chunk(BatchSize)) {
            cancellationToken.ThrowIfCancellationRequested();
            Parallel.ForEach(
                batch,
                new() { CancellationToken = cancellationToken },
                path => {
                    if (Read(path, selection.Asset, graph, paths, hashes) is { Outputs.Count: > 0 } project) {
                        found.Add(project);
                    }
                }
            );
        }

        if (selection.IsEverything) {
            hashes.Save(connection); // A part of the library must not replace the whole table.
        }

        return new([..found.OrderBy(static p => p.Path, StringComparer.Ordinal)]);
    }

    IReadOnlyList<string> Selected(SqliteConnection connection, OutputSelection selection) {
        if (selection.Asset is { } asset) {
            var reaching = queries.ProjectsReaching(asset);
            return selection.Projects is { Count: > 0 } named
                ? [..reaching.Intersect(queries.ProjectPaths(named), StringComparer.Ordinal)]
                : reaching;
        }

        if (selection.Projects is { Count: > 0 } projects) {
            return queries.ProjectPaths(projects);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path FROM project ORDER BY path";
        using var reader = command.ExecuteReader();
        List<string> all = [];
        while (reader.Read()) {
            all.Add(reader.GetString(0));
        }

        return all;
    }

    ScannedProject? Read(
        string path,
        AssetId? asset,
        AssetGraph graph,
        Dictionary<AssetId, string> paths,
        LibraryCheck.InputHashes hashes
    ) {
        var location = $"{LibraryLayout.ProjectsFolder}/{path}";
        ProjectDocument project;
        try {
            var text = SharedFile.ReadAllText(LibraryLayout.Full(root, $"{location}/{LibraryLayout.ProjectFile}"));
            project = DocumentCodec.ReadProject(text, location).Document;
        } catch (Exception e) when (e is IOException or LibraryFormatException) {
            return null; // changed since the rescan; the next build sees it
        }

        return new(
            project.Id,
            project.Name,
            path,
            [
                ..ProjectOutputs.Of(project, LibraryLayout.Full(root, location))
                    .Select(output => Scan(output, project, location, asset, graph, paths, hashes))
            ]
        );
    }

    ScannedOutput Scan(
        OutputDeclaration output,
        ProjectDocument project,
        string location,
        AssetId? asset,
        AssetGraph graph,
        Dictionary<AssetId, string> paths,
        LibraryCheck.InputHashes hashes
    ) {
        string? problem = null;

        Revision? Input(string file) {
            var revision = hashes.Get($"{location}/{file}");
            problem = revision is null ? $"it reads {file}, which is not there" : problem;
            return revision;
        }

        var references = project.References.ToLookup(static r => r.Asset);
        if (output.Uses.FirstOrDefault(used => !references.Contains(used)) is { } stray && stray != default) {
            return new(
                output,
                null,
                AssetReach.None,
                [],
                $"it reads asset {stray}, which the project does not reference"
            );
        }

        var key = OutputKeys.Compute(output, project, Input, graph, tools, hashes.StacksIn(location));
        var read = output.Uses.Select(used => references[used].First()).ToList();
        var reach = asset is { } target
            ? read.Select(reference => graph.Reach(reference, target)).DefaultIfEmpty(AssetReach.None).Max()
            : AssetReach.Moves;
        return new(
            output,
            key,
            reach,
            [..read.Select(reference => Located(reference, paths))],
            key is null ? problem ?? "a shared asset it reads is missing or in a cycle" : null
        );
    }

    /// <summary>A reference's bytes: the kept revision for a pin, unless the file has not moved past it.</summary>
    string Located(AssetReference reference, Dictionary<AssetId, string> paths) {
        if (reference.Revision is { } revision) {
            var kept = LibraryLayout.RevisionPath(root, revision);
            if (File.Exists(kept)) {
                return kept;
            }
        }

        return paths.TryGetValue(reference.Asset, out var path) ? LibraryLayout.Full(root, path) : string.Empty;
    }
}
