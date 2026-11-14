using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     Libraries used to keep what projects shared in <c>shared/</c>. Anything in any project can be used now, so that
///     folder has no job (ADR 0012). A library that still has one becomes one more project, with each document keeping
///     its GUID so that references to it still resolve. Files are the truth, so this runs on the files, each time the
///     library opens: a <c>shared/</c> that comes back, from an old copy or a teammate's clone, is moved as well.
/// </summary>
/// <remarks>
///     Nothing in <c>shared/</c> is lost (#292). A file whose place in the project is taken arrives beside it under a
///     free name, a graph or layered material with no sidecar is listed with a GUID of its own, and a second move adds
///     its documents to the Shared Assets project the first one made. The scan never reads <c>shared/</c>, so until the
///     next open a file put there is in no list; the folder goes once nothing is left in it.
/// </remarks>
static class SharedFolderMove {
    const string OldFolder = "shared";
    const string OldGraphs = "graphs";
    const string OldSidecar = ".asset.yaml";
    const string Project = "shared-assets";
    const string ProjectName = "Shared Assets";

    public static void Run(string root) {
        var shared = Path.Combine(root, OldFolder);
        if (!Directory.Exists(shared)) {
            return;
        }

        MoveAll(Path.Combine(shared, LibraryLayout.PresetsFolder), Path.Combine(root, LibraryLayout.PresetsFolder));
        MoveAll(Path.Combine(shared, LibraryLayout.RevisionsFolder), Path.Combine(root, LibraryLayout.RevisionsFolder));

        var projectFile = LibraryFiles.ProjectFile(root, Project);
        var target = Path.GetDirectoryName(projectFile)!;
        var before = File.Exists(projectFile)
            ? DocumentCodec.ReadProject(SharedFile.ReadAllText(projectFile), projectFile).Document
            : null;
        HashSet<AssetId> listed = [..before?.Documents.Select(static d => d.Id) ?? []];
        List<ProjectItem> items = [..Described(shared, target, listed), ..Loose(shared, target)];

        Directory.CreateDirectory(target);
        LibraryFiles.WriteProject(
            root,
            Project,
            before is null
                ? new(ProjectId.New(), ProjectName, [], [], []) { Documents = items }
                : before with { Documents = [..before.Documents, ..items] }
        );
        RemoveEmpty(shared);
    }

    /// <summary>The documents a sidecar describes, moved into the project under the GUID the sidecar keeps.</summary>
    static List<ProjectItem> Described(string shared, string target, HashSet<AssetId> listed) {
        List<ProjectItem> items = [];
        foreach (var sidecar in Directory.EnumerateFiles(shared, "*" + OldSidecar, SearchOption.AllDirectories)
                     .ToList()) {
            var file = sidecar[..^OldSidecar.Length];
            // A GUID the project lists already is that document's, so a second copy is loose: it gets one of its own.
            if (File.Exists(file) && Item(sidecar, Path.GetFileName(file)) is { } item && listed.Add(item.Id)) {
                items.Add(item with { File = Path.GetFileName(Move(file, Path.Combine(target, item.File))) });
            }

            File.Delete(sidecar);
        }

        return items;
    }

    /// <summary>
    ///     Everything else, which keeps its place below the folder, <c>graphs/</c> going to the project's own folder.
    ///     A graph or layered material that lands beside <c>project.yaml</c> is listed as a new document.
    /// </summary>
    static List<ProjectItem> Loose(string shared, string target) {
        List<ProjectItem> items = [];
        foreach (var file in Directory.EnumerateFiles(shared, "*", SearchOption.AllDirectories).ToList()) {
            var relative = Path.GetRelativePath(shared, file);
            var graphs = OldGraphs + Path.DirectorySeparatorChar;
            if (relative.StartsWith(graphs, StringComparison.Ordinal)) {
                relative = relative[graphs.Length..];
            }

            var placed = Move(file, Path.Combine(target, relative));
            if (Path.GetDirectoryName(placed) == target && Kind(placed) is { } kind) {
                var name = Path.GetFileName(placed);
                items.Add(new(AssetId.New(), kind, name, Stem(name)));
            }
        }

        return items;
    }

    static ProjectItem? Item(string sidecar, string file) {
        var asset = DocumentCodec.ReadAsset(SharedFile.ReadAllText(sidecar), sidecar).Document;
        return Kind(file) is { } kind
            ? new(asset.Id, kind, file, asset.Name) { Tags = asset.Tags, Dependencies = asset.Dependencies }
            : null;
    }

    static string? Kind(string file) =>
        file.EndsWith(StackInputs.Extension, StringComparison.Ordinal) ? DocumentKinds.LayeredMaterial
        : file.EndsWith(LibraryLayout.GraphExtension, StringComparison.Ordinal) ? DocumentKinds.Graph
        : null;

    /// <summary>A file's name without the extension that says its kind, which may hold a dot of its own.</summary>
    static string Stem(string file) {
        var extension = file.EndsWith(StackInputs.Extension, StringComparison.Ordinal)
            ? StackInputs.Extension
            : LibraryLayout.GraphExtension;
        return file[..^extension.Length];
    }

    static void MoveAll(string from, string to) {
        if (!Directory.Exists(from)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories).ToList()) {
            Move(file, Path.Combine(to, Path.GetRelativePath(from, file)));
        }
    }

    /// <summary>
    ///     Moves a file to <paramref name="to" />, or beside it under a free name when another file is there. The same
    ///     bytes there already, such as a revision kept under its hash, make the move only a delete.
    /// </summary>
    /// <returns>Where the file is now.</returns>
    static string Move(string from, string to) {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (File.Exists(to) && SharedFile.ReadAllBytes(from).AsSpan().SequenceEqual(SharedFile.ReadAllBytes(to))) {
            File.Delete(from);
            return to;
        }

        var free = Free(to);
        SharedFile.MoveOver(from, free);
        return free;
    }

    /// <summary><paramref name="path" />, or the first free one of <c>name-2.ext</c>, <c>name-3.ext</c>…</summary>
    static string Free(string path) {
        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        var (stem, extension) = dot > 0 ? (name[..dot], name[dot..]) : (name, string.Empty);
        var free = path;
        for (var n = 2; File.Exists(free) || Directory.Exists(free); n++) {
            free = Path.Combine(folder, $"{stem}-{n}{extension}");
        }

        return free;
    }

    /// <summary>Deletes the folders below <paramref name="folder" /> and itself that hold no file.</summary>
    static void RemoveEmpty(string folder) {
        foreach (var child in Directory.EnumerateDirectories(folder)) {
            RemoveEmpty(child);
        }

        if (!Directory.EnumerateFileSystemEntries(folder).Any()) {
            Directory.Delete(folder);
        }
    }
}
