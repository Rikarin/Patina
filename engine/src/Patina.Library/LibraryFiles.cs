using System.Text;

namespace Patina.Library;

/// <summary>
///     Writes the library's files. Every write replaces the file whole, so a crash, or a scan running beside it,
///     never sees half a document.
/// </summary>
public static class LibraryFiles {
    const string IgnoreFile = """
                              # Derived: deleting it loses nothing but time.
                              .ptcache/

                              """;

    // Binary payloads go through LFS so that clones stay small; every YAML document stays text.
    const string AttributesFile = """
                                  * text=auto eol=lf
                                  *.vxpaint filter=lfs diff=lfs merge=lfs -text
                                  *.glb filter=lfs diff=lfs merge=lfs -text
                                  *.fbx filter=lfs diff=lfs merge=lfs -text
                                  *.exr filter=lfs diff=lfs merge=lfs -text
                                  *.png filter=lfs diff=lfs merge=lfs -text

                                  """;

    static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Lays out a new library in <paramref name="root" />, which may exist but must not be a library yet.</summary>
    public static void CreateLibrary(string root, LibraryDocument document) {
        var file = Path.Combine(root, LibraryLayout.LibraryFile);
        if (File.Exists(file)) {
            throw new IOException($"'{root}' is a library already.");
        }

        foreach (var folder in LibraryLayout.SharedFolders) {
            Directory.CreateDirectory(Path.Combine(root, LibraryLayout.SharedFolder, folder));
        }

        Directory.CreateDirectory(Path.Combine(root, LibraryLayout.ProjectsFolder));
        WriteIfAbsent(Path.Combine(root, ".gitignore"), IgnoreFile);
        WriteIfAbsent(Path.Combine(root, ".gitattributes"), AttributesFile);
        Replace(file, DocumentCodec.Write(document));
    }

    /// <summary>The full path of the document of the project at <paramref name="path" /> below <c>projects/</c>.</summary>
    public static string ProjectFile(string root, string path) =>
        LibraryLayout.Full(root, $"{LibraryLayout.ProjectsFolder}/{path}/{LibraryLayout.ProjectFile}");

    /// <summary>The full path of the shared asset at <paramref name="path" /> below <c>shared/</c>.</summary>
    public static string AssetFile(string root, string path) =>
        LibraryLayout.Full(root, $"{LibraryLayout.SharedFolder}/{path}");

    /// <summary>Writes a project's document at <paramref name="path" /> below <c>projects/</c>.</summary>
    public static void WriteProject(string root, string path, ProjectDocument document) =>
        Replace(ProjectFile(root, path), DocumentCodec.Write(document));

    /// <summary>Writes a shared asset and its sidecar at <paramref name="path" /> below <c>shared/</c>.</summary>
    public static void WriteAsset(string root, string path, ReadOnlySpan<byte> content, AssetDocument document) {
        var file = AssetFile(root, path);
        Replace(file, content);
        Replace(file + LibraryLayout.SidecarSuffix, DocumentCodec.Write(document));
    }

    /// <summary>
    ///     Keeps the bytes the shared asset at <paramref name="path" /> has now, so that a pin to them stays
    ///     resolvable after the asset moves on. Returns the revision to pin. Keeping one twice is harmless: the name
    ///     is the content.
    /// </summary>
    public static Revision KeepRevision(string root, string path) {
        var file = AssetFile(root, path);
        var revision = Revision.OfFile(file);
        var kept = LibraryLayout.RevisionPath(root, revision);
        if (!File.Exists(kept)) {
            Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
            File.Copy(file, kept);
        }

        return revision;
    }

    /// <summary>Writes <paramref name="text" /> over <paramref name="path" /> in one step.</summary>
    public static void Replace(string path, string text) => Replace(path, Utf8.GetBytes(text));

    /// <summary>Writes <paramref name="content" /> over <paramref name="path" /> in one step.</summary>
    public static void Replace(string path, ReadOnlySpan<byte> content) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Environment.ProcessId}.{Environment.CurrentManagedThreadId}.tmp";
        try {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                stream.Write(content);
            }

            File.Move(temporary, path, true);
        } finally {
            File.Delete(temporary); // already moved, unless the write failed
        }
    }

    static void WriteIfAbsent(string path, string text) {
        if (!File.Exists(path)) {
            Replace(path, text);
        }
    }
}
