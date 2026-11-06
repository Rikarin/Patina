using Microsoft.Data.Sqlite;
using System.Text;

namespace Patina.Library.Tests;

/// <summary>A library in a temporary folder, and the small verbs the tests are written in.</summary>
sealed class TestLibrary : IDisposable {
    static readonly DateTime LongAgo = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    int clock;

    public TestLibrary() {
        Folder = Directory.CreateTempSubdirectory("patina-library-").FullName;
        Root = Path.Combine(Folder, "Game.ptlib");
        LibraryFiles.CreateLibrary(Root, new(LibraryId.New(), "Game", 1024, ["baseColor"], "pbr", ["default"]));
    }

    public string Folder { get; }

    public string Root { get; }

    public void Dispose() => Directory.Delete(Folder, true);

    public OpenLibrary Open() => OpenLibrary.Open(Root);

    public static ProjectDocument Project(string name, params AssetReference[] references) =>
        new(ProjectId.New(), name, [], references, []);

    public static AssetDocument Asset(string name, params AssetReference[] dependencies) =>
        new(AssetId.New(), "graph", name, [], dependencies);

    public void Write(string path, ProjectDocument project) {
        LibraryFiles.WriteProject(Root, path, project);
        Age(LibraryFiles.ProjectFile(Root, path));
    }

    public void Write(string path, AssetDocument asset, string content = "content") {
        LibraryFiles.WriteAsset(Root, path, Encoding.UTF8.GetBytes(content), asset);
        Age(LibraryFiles.AssetFile(Root, path));
        Age(LibraryFiles.ProjectFile(Root, Path.GetDirectoryName(path)!));
    }

    public string SaveFile(string relative, string content) {
        var file = LibraryLayout.Full(Root, relative);
        LibraryFiles.Replace(file, content);
        Age(file);
        return file;
    }

    /// <summary>
    ///     Gives the file a distinct time well in the past. A file written just now is never trusted by the rescan,
    ///     so without this a test would hash everything and prove nothing about the size-and-time path.
    /// </summary>
    public void Age(string file) => File.SetLastWriteTimeUtc(file, LongAgo.AddSeconds(++clock));

    public void MoveProject(string from, string to) {
        var target = Path.GetDirectoryName(LibraryFiles.ProjectFile(Root, to))!;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(Path.GetDirectoryName(LibraryFiles.ProjectFile(Root, from))!, target);
    }

    public void MoveAsset(string from, string to) {
        var item = RemoveAsset(from);
        LibraryFiles.WriteDocument(
            Root,
            Path.GetDirectoryName(to)!,
            File.ReadAllBytes(LibraryFiles.AssetFile(Root, from)),
            item with { File = Path.GetFileName(to) }
        );
        File.Delete(LibraryFiles.AssetFile(Root, from));
        Age(LibraryFiles.ProjectFile(Root, Path.GetDirectoryName(to)!));
    }

    /// <summary>Takes a document out of its project's list, which is what stops it being an asset.</summary>
    public ProjectItem RemoveAsset(string path) => Unlist(Root, path);

    public static ProjectItem Unlist(string root, string path) {
        var project = Path.GetDirectoryName(path)!;
        var listing = LibraryFiles.ProjectFile(root, project);
        var document = DocumentCodec.ReadProject(File.ReadAllText(listing), listing).Document;
        var item = document.Documents.Single(held => held.File == Path.GetFileName(path));
        LibraryFiles.WriteProject(
            root,
            project,
            document with { Documents = [..document.Documents.Where(held => held != item)] }
        );
        return item;
    }

    public void DeleteCache() => Directory.Delete(Path.Combine(Root, LibraryLayout.CacheFolder), true);

    /// <summary>
    ///     Everything the index knows, in a canonical order, without what depends on when or in which order it was
    ///     built (row ids, scan times).
    /// </summary>
    public string DumpIndex() {
        var dump = new StringBuilder();
        using var connection = new SqliteConnection($"Data Source={LibraryLayout.IndexPath(Root)};Pooling=False");
        connection.Open();
        foreach (var query in (string[]) [
                     "SELECT path, kind, size, mtime, hash FROM file ORDER BY path",
                     "SELECT path, id, name FROM project ORDER BY path",
                     """
                     SELECT p.path, t.tag FROM project_tag t JOIN project p ON p.rid = t.project ORDER BY p.path, t.tag
                     """,
                     """
                     SELECT p.path, r.asset, r.mode, r.revision
                     FROM project_reference r JOIN project p ON p.rid = r.project ORDER BY p.path, r.asset
                     """,
                     "SELECT path, id, kind, name FROM asset ORDER BY path",
                     "SELECT a.path, t.tag FROM asset_tag t JOIN asset a ON a.rid = t.asset ORDER BY a.path, t.tag",
                     """
                     SELECT a.path, d.target, d.mode, d.revision
                     FROM asset_dependency d JOIN asset a ON a.rid = d.asset ORDER BY a.path, d.target
                     """,
                     "SELECT path, code, detail FROM problem ORDER BY path"
                 ]) {
            using var command = connection.CreateCommand();
            command.CommandText = query;
            using var reader = command.ExecuteReader();
            dump.AppendLine(query);
            while (reader.Read()) {
                for (var i = 0; i < reader.FieldCount; i++) {
                    dump.Append(reader.GetValue(i)).Append('|');
                }

                dump.AppendLine();
            }
        }

        return dump.ToString();
    }
}
