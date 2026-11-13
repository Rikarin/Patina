using Patina.Contracts;
using Patina.Library;

namespace Patina.Export.Builds;

/// <summary>
///     What differs between two builds (#217), from their reports alone: an output by its build key, since the same key
///     is the same result, and an exported file by the digest the build kept of it when it was delivered. The second
///     build has written over the first's files by then, so the digests are what remembers them.
/// </summary>
public static class BuildComparisons {
    /// <summary>Which builds touched a project.</summary>
    public static class Touched {
        /// <summary>Both builds touched it.</summary>
        public const string Both = "both";

        /// <summary>Only the earlier build touched it.</summary>
        public const string Before = "before";

        /// <summary>Only the later build touched it.</summary>
        public const string After = "after";
    }

    /// <summary>Each file's SHA-256, or an empty text for one that is not there.</summary>
    /// <param name="files">The files, absolute.</param>
    /// <returns>The digests, in the same order.</returns>
    public static IReadOnlyList<string> Digests(IEnumerable<string> files) =>
        [..files.Select(static file => File.Exists(file) ? LibraryFiles.Digest(file) : string.Empty)];

    /// <summary>Compares two finished builds' reports.</summary>
    /// <param name="before">The earlier build's report folder.</param>
    /// <param name="after">The later build's report folder.</param>
    /// <returns>Each project either build touched, by path, and what differs in it.</returns>
    public static BuildComparison Compare(string before, string after) {
        var earlier = Reports(before);
        var later = Reports(after);
        List<ProjectComparison> projects = [];
        foreach (var id in earlier.Keys.Union(later.Keys)) {
            var first = earlier.GetValueOrDefault(id);
            var second = later.GetValueOrDefault(id);
            var any = (second ?? first)!;
            projects.Add(
                new(
                    id,
                    any.Name,
                    any.Path,
                    first is null ? Touched.After : second is null ? Touched.Before : Touched.Both,
                    Outputs(first, second),
                    Files(first, second)
                )
            );
        }

        return new(
            Path.GetFileName(before),
            Path.GetFileName(after),
            [..projects.OrderBy(static p => p.Path, StringComparer.Ordinal)]
        );
    }

    static Dictionary<Guid, ProjectReport> Reports(string folder) =>
        BuildJournal.ReadProjects(folder, 0, int.MaxValue).Items.ToDictionary(static p => p.Id);

    static List<OutputComparison> Outputs(ProjectReport? before, ProjectReport? after) {
        var first = (before?.Outputs ?? []).ToDictionary(static o => o.Name, StringComparer.Ordinal);
        var second = (after?.Outputs ?? []).ToDictionary(static o => o.Name, StringComparer.Ordinal);
        return [
            ..first.Keys.Union(second.Keys, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(name => {
                        var key = first.GetValueOrDefault(name)?.Key;
                        var then = second.GetValueOrDefault(name)?.Key;
                        return new OutputComparison(name, key, then, Change(key, then));
                    }
                )
        ];
    }

    static List<FileComparison> Files(ProjectReport? before, ProjectReport? after) {
        var first = Delivered(before);
        var second = Delivered(after);
        return [
            ..first.Keys.Union(second.Keys)
                .OrderBy(static file => file.Preset, StringComparer.Ordinal)
                .ThenBy(static file => file.File, StringComparer.Ordinal)
                .Select(file => new FileComparison(
                        file.Preset,
                        file.File,
                        Change(first.GetValueOrDefault(file), second.GetValueOrDefault(file))
                    )
                )
        ];
    }

    /// <summary>
    ///     How a thing stands from its key or digest in each build: null where the build did not have it, and empty
    ///     where it did and kept no digest.
    /// </summary>
    static string Change(string? before, string? after) =>
        (before, after) switch {
            (null, null) => BuildChanges.Unknown,
            (null, _) => BuildChanges.Added,
            (_, null) => BuildChanges.Removed,
            ({ Length: 0 }, _) or (_, { Length: 0 }) => BuildChanges.Unknown,
            _ => string.Equals(before, after, StringComparison.Ordinal) ? BuildChanges.Same : BuildChanges.Changed
        };

    /// <summary>A project's exported files, by preset and place in the export folder, with their digests.</summary>
    static Dictionary<(string Preset, string File), string> Delivered(ProjectReport? report) {
        Dictionary<(string, string), string> files = [];
        foreach (var export in report?.Exports ?? []) {
            for (var at = 0; at < export.Files.Count; at++) {
                var file = export.Files[at];
                var place = export.Folder is { } folder ? Path.GetRelativePath(folder, file) : Path.GetFileName(file);
                files[(export.Preset, place.Replace('\\', '/'))] =
                    at < export.Digests.Count ? export.Digests[at] : string.Empty;
            }
        }

        return files;
    }
}
