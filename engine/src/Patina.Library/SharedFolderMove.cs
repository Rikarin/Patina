using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     Libraries used to keep what projects shared in <c>shared/</c>. Anything in any project can be used now, so that
///     folder has no job (ADR 0012). A library that still has one becomes one more project, with each document keeping
///     its GUID so that references to it still resolve. Files are the truth, so this runs on the files, once.
/// </summary>
static class SharedFolderMove {
    const string OldFolder = "shared";
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

        var target = Path.GetDirectoryName(LibraryFiles.ProjectFile(root, Project))!;
        List<ProjectItem> items = [];
        foreach (var sidecar in Directory.EnumerateFiles(shared, "*" + OldSidecar, SearchOption.AllDirectories)
                     .ToList()) {
            var file = sidecar[..^OldSidecar.Length];
            if (File.Exists(file) && Item(sidecar, Path.GetFileName(file)) is { } item) {
                items.Add(item);
                Move(file, Path.Combine(target, item.File));
            }

            File.Delete(sidecar);
        }

        // What is left has no document kind, such as brushes and alphas, and keeps its place in the folder.
        foreach (var folder in Directory.EnumerateDirectories(shared).ToList()) {
            var name = Path.GetFileName(folder);
            MoveAll(folder, Path.Combine(target, name == "graphs" ? string.Empty : name));
        }

        Directory.CreateDirectory(target);
        if (!File.Exists(LibraryFiles.ProjectFile(root, Project))) {
            LibraryFiles.WriteProject(
                root,
                Project,
                new(ProjectId.New(), ProjectName, [], [], []) { Documents = items }
            );
        }

        Directory.Delete(shared, true);
    }

    static ProjectItem? Item(string sidecar, string file) {
        var asset = DocumentCodec.ReadAsset(SharedFile.ReadAllText(sidecar), sidecar).Document;
        var kind = file.EndsWith(StackInputs.Extension, StringComparison.Ordinal) ? DocumentKinds.LayeredMaterial
            : file.EndsWith(LibraryLayout.GraphExtension, StringComparison.Ordinal) ? DocumentKinds.Graph
            : null;
        return kind is null
            ? null
            : new(asset.Id, kind, file, asset.Name) { Tags = asset.Tags, Dependencies = asset.Dependencies };
    }

    static void MoveAll(string from, string to) {
        if (!Directory.Exists(from)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories).ToList()) {
            Move(file, Path.Combine(to, Path.GetRelativePath(from, file)));
        }
    }

    static void Move(string from, string to) {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Move(from, to, true);
    }
}
