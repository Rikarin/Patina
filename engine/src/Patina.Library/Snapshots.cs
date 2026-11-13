using Patina.Contracts;
using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     Named checkpoints of a document (#209). A snapshot is the document's file and the paint it names, kept by
///     revision under <c>.revisions/</c> beside the bytes pins keep, and listed in a text file beside the document,
///     <c>.snapshots/&lt;file&gt;.yaml</c>. Both are files of the library, not cache: they travel with a Git clone, and
///     a snapshot of an unchanged paint file costs nothing, since its bytes are already kept.
/// </summary>
/// <remarks>
///     Restoring takes a snapshot of the document as it stands first, so a restore can itself be taken back. Paint a
///     layer gained after the snapshot is taken away again, because the snapshot remembers that the layer had none.
/// </remarks>
public static class Snapshots {
    /// <summary>The folder beside a document that lists its snapshots. Hidden, so the scan passes it by.</summary>
    public const string Folder = ".snapshots";

    const int Schema = 1;
    const string SchemaKey = "schema";
    const string ListKey = "snapshots";
    const string IdKey = "id";
    const string NameKey = "name";
    const string TakenKey = "taken";
    const string FilesKey = "files";
    const string AbsentKey = "absent";
    const int MaxName = 120;

    /// <summary>One snapshot as its list holds it.</summary>
    /// <param name="Id">Which snapshot.</param>
    /// <param name="Name">What it is called.</param>
    /// <param name="Taken">When it was taken.</param>
    /// <param name="Files">Each file by its name beside the document, and the revision of its bytes.</param>
    /// <param name="Absent">Paint the document named that did not exist yet.</param>
    sealed record Entry(
        Guid Id,
        string Name,
        DateTimeOffset Taken,
        IReadOnlyDictionary<string, string> Files,
        IReadOnlyList<string> Absent
    );

    /// <summary>The snapshots of a document, newest first.</summary>
    /// <param name="root">The library.</param>
    /// <param name="document">The document's full path.</param>
    public static DocumentSnapshotList List(string root, string document) =>
        new(
            LibraryLayout.Relative(root, document),
            [..Read(document).AsEnumerable().Reverse().Select(entry => Summary(root, entry))]
        );

    /// <summary>Takes a snapshot of a document as its file and its paint stand on disk.</summary>
    /// <param name="root">The library.</param>
    /// <param name="document">The document's full path.</param>
    /// <param name="name">What to call it; empty or null names it by number.</param>
    /// <exception cref="FileNotFoundException">The document is not there.</exception>
    public static DocumentSnapshot Take(string root, string document, string? name) {
        if (!File.Exists(document)) {
            throw new FileNotFoundException("The document is not there.", document);
        }

        var entries = Read(document);
        var text = SharedFile.ReadAllText(document);
        var folder = Path.GetDirectoryName(document)!;
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal) {
            [Path.GetFileName(document)] = Keep(root, document).Text
        };
        List<string> absent = [];
        foreach (var paint in StackInputs.PaintsOf(document, text)) {
            var full = Path.GetFullPath(paint);
            if (!full.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                continue; // paint from outside its folder, which the check reports and nothing reads
            }

            var relative = Path.GetRelativePath(folder, full).Replace('\\', '/');
            if (File.Exists(full)) {
                files[relative] = Keep(root, full).Text;
            } else {
                absent.Add(relative);
            }
        }

        var named = Named(name);
        var entry = new Entry(
            Guid.CreateVersion7(),
            named.Length > 0 ? named : $"Snapshot {entries.Count + 1}",
            DateTimeOffset.UtcNow,
            files,
            absent
        );
        Write(document, [..entries, entry]);
        return Summary(root, entry);
    }

    /// <summary>
    ///     Puts a document's file and paint back as a snapshot holds them, after taking a snapshot of how they stand.
    ///     The document must not be open: its session would write over what this puts back.
    /// </summary>
    /// <returns>The snapshot taken of the document as it stood, which undoes the restore.</returns>
    /// <exception cref="FileNotFoundException">The document or the snapshot is not there.</exception>
    /// <exception cref="ProjectRefusedException">Bytes the snapshot needs are no longer kept.</exception>
    public static DocumentSnapshot Restore(string root, string document, Guid id) {
        var entry = Find(document, id);
        var folder = Path.GetDirectoryName(document)!;
        List<(string Target, string Kept)> writes = [];
        foreach (var (name, revision) in entry.Files) {
            var kept = Revision.TryParse(revision, out var parsed) ? LibraryLayout.RevisionPath(root, parsed) : null;
            if (kept is null || !File.Exists(kept)) {
                throw new ProjectRefusedException($"The snapshot's copy of '{name}' is no longer kept.");
            }

            writes.Add((Inside(folder, name), kept));
        }

        string[] gone = [..entry.Absent.Select(name => Inside(folder, name))];
        var before = Take(root, document, $"Before restoring {entry.Name}");
        foreach (var (target, kept) in writes) {
            Copy(kept, target);
        }

        foreach (var file in gone.Where(File.Exists)) {
            File.Delete(file);
        }

        return before;
    }

    /// <summary>A file a snapshot names beside its document, which a hand-edited list may not lead out of.</summary>
    static string Inside(string folder, string name) {
        try {
            return LibraryLayout.Beside(folder, name);
        } catch (InvalidDataException e) {
            throw new ProjectRefusedException($"The snapshot names '{name}', which is not beside its document.", e);
        }
    }

    /// <summary>Forgets a snapshot. Its bytes stay under <c>.revisions/</c>, where a pin or another may share them.</summary>
    /// <exception cref="FileNotFoundException">The snapshot is not there.</exception>
    public static void Forget(string document, Guid id) {
        var entries = Read(document);
        var left = entries.Where(entry => entry.Id != id).ToList();
        if (left.Count == entries.Count) {
            throw new FileNotFoundException("That snapshot is not there.", ListPath(document));
        }

        Write(document, left);
    }

    /// <summary>Where a document's snapshots are listed.</summary>
    public static string ListPath(string document) =>
        Path.Combine(Path.GetDirectoryName(document)!, Folder, Path.GetFileName(document) + ".yaml");

    /// <summary>A name as it is kept: one line of text, no longer than a document's.</summary>
    static string Named(string? name) {
        var line = new string([..(name ?? string.Empty).Where(static c => !char.IsControl(c))]).Trim();
        return line.Length > MaxName ? line[..MaxName].TrimEnd() : line;
    }

    static Entry Find(string document, Guid id) =>
        Read(document).FirstOrDefault(entry => entry.Id == id)
        ?? throw new FileNotFoundException("That snapshot is not there.", ListPath(document));

    static DocumentSnapshot Summary(string root, Entry entry) {
        long bytes = 0;
        foreach (var revision in entry.Files.Values) {
            if (Revision.TryParse(revision, out var parsed)
                && new FileInfo(LibraryLayout.RevisionPath(root, parsed)) is { Exists: true } kept) {
                bytes += kept.Length;
            }
        }

        return new(entry.Id, entry.Name, entry.Taken, entry.Files.Count, bytes);
    }

    /// <summary>Keeps a file's bytes by their revision, once, and says which revision that is.</summary>
    static Revision Keep(string root, string file) {
        var revision = Revision.OfFile(file);
        var kept = LibraryLayout.RevisionPath(root, revision);
        if (!File.Exists(kept)) {
            Copy(file, kept);
        }

        return revision;
    }

    /// <summary>Copies a file in one step, so nobody reads half of it.</summary>
    static void Copy(string from, string to) {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        var temporary = $"{to}.{Environment.ProcessId}.snapshot.tmp";
        try {
            File.Copy(from, temporary, true);
            File.Move(temporary, to, true);
        } finally {
            File.Delete(temporary);
        }
    }

    static List<Entry> Read(string document) {
        var path = ListPath(document);
        if (!File.Exists(path)) {
            return [];
        }

        var fields = new YamlFields(YamlText.Parse(SharedFile.ReadAllText(path), path), path);
        if (fields.Number(SchemaKey, Schema) > Schema) {
            throw new LibraryFormatException(path, "a newer Patina wrote it.", true);
        }

        return [..fields.Maps(ListKey).Select(ReadEntry)];
    }

    static Entry ReadEntry(YamlFields fields) =>
        new(
            Guid.Parse(fields.Require(IdKey), CultureInfo.InvariantCulture),
            fields.Require(NameKey),
            DateTimeOffset.Parse(fields.Require(TakenKey), CultureInfo.InvariantCulture),
            fields.TextMap(FilesKey),
            fields.Texts(AbsentKey)
        );

    static void Write(string document, IReadOnlyList<Entry> entries) {
        var list = new YamlSequenceNode();
        foreach (var entry in entries) {
            list.Add(WriteEntry(entry));
        }

        var root = new YamlMappingNode {
            { SchemaKey, YamlText.Plain(Schema) },
            { ListKey, list }
        };
        LibraryFiles.Replace(ListPath(document), YamlText.Write(root));
    }

    static YamlMappingNode WriteEntry(Entry entry) {
        var files = new YamlMappingNode();
        foreach (var (name, revision) in entry.Files.OrderBy(static file => file.Key, StringComparer.Ordinal)) {
            files.Add(YamlText.Text(name), YamlText.Plain(revision));
        }

        var node = new YamlMappingNode {
            { IdKey, YamlText.Plain(entry.Id.ToString()) },
            { NameKey, YamlText.Text(entry.Name) },
            { TakenKey, YamlText.Plain(entry.Taken.ToString("O", CultureInfo.InvariantCulture)) },
            { FilesKey, files }
        };
        if (entry.Absent.Count > 0) {
            node.Add(AbsentKey, YamlText.Texts(entry.Absent));
        }

        return node;
    }
}
