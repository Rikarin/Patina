using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using System.Collections.Concurrent;

namespace Patina.Server.Stacks;

/// <summary>
///     The brush and tool presets a project can use: the ones Patina ships, its own and every other project's (#124).
///     A preset is a document of a project and its <c>.ptbrush</c> file is the truth; the shipped ones are CC0 and
///     cannot change, so changing one keeps a copy in the project.
/// </summary>
public static class BrushShelf {
    public const string StarterPrefix = "starter:";
    const string StarterResource = "Brush/";
    const string Shipped = "shipped";
    const string Own = "project";
    const string Elsewhere = "library";
    const int ElsewhereLimit = 300;
    const int ProjectPage = 200;

    /// <summary>
    ///     Each preset file as last read, kept while its file stands (#124): every shelf load lists every other project's
    ///     presets, and reading each file every time was most of what a load cost in a library of many projects. A file
    ///     that would not read is kept as null, so it is not tried again until it changes.
    /// </summary>
    static readonly ConcurrentDictionary<string, (DateTime Stamp, BrushPreset? Preset)> Kept =
        new(StringComparer.Ordinal);

    /// <summary>Every preset a project can use, favourites first, then shipped, its own and others', by name.</summary>
    /// <param name="library">The library.</param>
    /// <param name="project">The project, or null for the shipped ones and the library's.</param>
    /// <param name="search">Words every preset listed has in its name or path, for other projects' only.</param>
    public static IReadOnlyList<BrushPresetInfo> List(OpenLibrary library, string? project, string? search = null) {
        List<BrushPresetInfo> found = [..Starters()];
        if (project is not null) {
            found.AddRange(Of(library, project, Own, null));
        }

        var words = search?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        found.AddRange(Others(library, project, words).Take(ElsewhereLimit));
        return [
            ..found.OrderByDescending(static preset => preset.Preset.Favourite)
                .ThenBy(static preset => Rank(preset.Origin))
                .ThenBy(static preset => preset.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>Saves a new preset in a project.</summary>
    /// <exception cref="ProjectExistsException">The project has one of that name.</exception>
    public static BrushPresetInfo Create(OpenLibrary library, BrushPresetCreateRequest request) {
        var path = library.CreateBrushPreset(request.Project, request.Name, request.Preset);
        return new(Named(library, request.Project, path) ?? request.Name.Trim(), path, Own, request.Preset);
    }

    /// <summary>
    ///     Rewrites a preset. A shipped one is copied into the project first, under its own name or the next one free,
    ///     so a favourite or an edit of it is the project's.
    /// </summary>
    /// <exception cref="FileNotFoundException">There is no such preset.</exception>
    public static BrushPresetInfo Update(OpenLibrary library, BrushPresetUpdateRequest request) {
        if (request.Source.StartsWith(StarterPrefix, StringComparison.Ordinal)) {
            var name = request.Source[StarterPrefix.Length..];
            _ = StarterText(name);
            for (var other = 1;; other++) {
                var called = other == 1 ? name : $"{name} {other}";
                try {
                    return Create(library, new(request.Project, called, request.Preset));
                } catch (ProjectExistsException) when (other < 100) {
                    // Taken: the next number.
                }
            }
        }

        var full = Path.GetFullPath(request.Source, library.RootPath);
        var root = Path.GetFullPath(library.RootPath) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.Ordinal)
            || !full.EndsWith(BrushPresets.Extension, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(full)) {
            throw new FileNotFoundException($"There is no brush preset '{request.Source}' in this library.");
        }

        BrushPresets.Write(full, request.Preset);
        var owner = OwnerOf(request.Source);
        return new(
            owner is null
                ? Path.GetFileNameWithoutExtension(full)
                : Named(library, owner, request.Source)
                ?? Path.GetFileNameWithoutExtension(full),
            request.Source,
            owner == request.Project ? Own : Elsewhere,
            request.Preset,
            owner == request.Project ? null : owner
        );
    }

    /// <summary>The presets that ship with Patina, from <c>content/brushes</c>.</summary>
    static IEnumerable<BrushPresetInfo> Starters() =>
        typeof(BrushShelf).Assembly.GetManifestResourceNames()
            .Where(static resource => resource.StartsWith(StarterResource, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(static resource => {
                    var name = resource[StarterResource.Length..];
                    return new BrushPresetInfo(
                        name,
                        StarterPrefix + name,
                        Shipped,
                        BrushPresets.Parse(StarterText(name), resource)
                    );
                }
            );

    /// <summary>A shipped preset's file.</summary>
    /// <exception cref="FileNotFoundException">None has that name.</exception>
    static string StarterText(string name) {
        using var stream = typeof(BrushShelf).Assembly.GetManifestResourceStream(StarterResource + name)
            ?? throw new FileNotFoundException($"There is no shipped brush preset '{name}'.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>One project's presets. A file that will not read is left off rather than emptying the shelf.</summary>
    static IEnumerable<BrushPresetInfo> Of(OpenLibrary library, string project, string origin, string[]? words) {
        foreach (var document in MaterialShelf.DocumentsOf(library, project)) {
            if (document.Kind != DocumentKinds.BrushPreset
                || (words is not null && !Found(document, words))
                || Read(Path.GetFullPath(document.Path, library.RootPath)) is not { } preset) {
                continue;
            }

            yield return new(document.Name, document.Path, origin, preset, origin == Own ? null : project);
        }
    }

    /// <summary>A preset file, from <see cref="Kept" /> while it stands; null when it will not read.</summary>
    static BrushPreset? Read(string file) {
        var stamp = File.GetLastWriteTimeUtc(file);
        if (Kept.TryGetValue(file, out var held) && held.Stamp == stamp) {
            return held.Preset;
        }

        BrushPreset? preset;
        try {
            preset = BrushPresets.Read(file);
        } catch (Exception e) when (e is LibraryFormatException or IOException or UnauthorizedAccessException) {
            preset = null;
        }

        Kept[file] = (stamp, preset);
        return preset;
    }

    static IEnumerable<BrushPresetInfo> Others(OpenLibrary library, string? project, string[] words) {
        for (var offset = 0;; offset += ProjectPage) {
            var page = library.Projects(new(Offset: offset, Limit: ProjectPage)).Items;
            foreach (var summary in page.Where(summary => summary.Path != project)) {
                IReadOnlyList<BrushPresetInfo> presets;
                try {
                    presets = [..Of(library, summary.Path, Elsewhere, words)];
                } catch (Exception e) when (e is LibraryFormatException or IOException) {
                    continue;
                }

                foreach (var preset in presets) {
                    yield return preset with { Project = summary.Name };
                }
            }

            if (page.Count < ProjectPage) {
                yield break;
            }
        }
    }

    static bool Found(ProjectDocumentInfo document, string[] words) =>
        words.All(word => document.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
            || document.Path.Contains(word, StringComparison.OrdinalIgnoreCase)
        );

    static int Rank(string origin) =>
        origin switch {
            Shipped => 0,
            Own => 1,
            _ => 2
        };

    /// <summary>The project a preset's path is in, or null for a path outside <c>projects/</c>.</summary>
    static string? OwnerOf(string source) {
        var below = $"{LibraryLayout.ProjectsFolder}/";
        return source.StartsWith(below, StringComparison.Ordinal)
            ? Path.GetDirectoryName(source[below.Length..])?.Replace('\\', '/')
            : null;
    }

    static string? Named(OpenLibrary library, string project, string path) =>
        library.Contents(project).Documents.FirstOrDefault(document => document.Path == path)?.Name;
}
