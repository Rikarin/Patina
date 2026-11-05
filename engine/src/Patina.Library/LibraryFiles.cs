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

    /// <summary>
    ///     Lays out a new library in <paramref name="root" />, which may exist but must not be a library yet.
    /// </summary>
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

    /// <summary>
    ///     The full path of the document of the project at <paramref name="path" /> below <c>projects/</c>.
    /// </summary>
    public static string ProjectFile(string root, string path) =>
        LibraryLayout.Full(root, $"{LibraryLayout.ProjectsFolder}/{path}/{LibraryLayout.ProjectFile}");

    /// <summary>The full path of the shared asset at <paramref name="path" /> below <c>shared/</c>.</summary>
    public static string AssetFile(string root, string path) =>
        LibraryLayout.Full(root, $"{LibraryLayout.SharedFolder}/{path}");

    /// <summary>Writes a project's document at <paramref name="path" /> below <c>projects/</c>.</summary>
    public static void WriteProject(string root, string path, ProjectDocument document) =>
        Replace(ProjectFile(root, path), DocumentCodec.Write(document));

    /// <summary>
    ///     Writes a new project's document at <paramref name="path" /> below <c>projects/</c>. Projects do not nest, so
    ///     the scan stops at the first <c>project.yaml</c> on the way down: a project inside another would never be
    ///     found, and one above others would hide them. Neither is written.
    /// </summary>
    /// <exception cref="ProjectExistsException">The folder, or a folder on the way to it, is taken.</exception>
    public static void CreateProject(string root, string path, ProjectDocument document) {
        var segments = path.Split('/');
        for (var i = 1; i < segments.Length; i++) {
            var parent = string.Join('/', segments[..i]);
            var parentFolder = LibraryLayout.Full(root, $"{LibraryLayout.ProjectsFolder}/{parent}");
            if (File.Exists(ProjectFile(root, parent)) || File.Exists(parentFolder)) {
                throw new ProjectExistsException($"'{parent}' is a project, and projects do not nest.");
            }
        }

        var file = ProjectFile(root, path);
        var folder = Path.GetDirectoryName(file)!;
        if (File.Exists(folder) || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())) {
            throw new ProjectExistsException($"'{path}' exists already.");
        }

        try {
            Write(file, Utf8.GetBytes(DocumentCodec.Write(document)), false);
        } catch (IOException e) when (File.Exists(file)) {
            throw new ProjectExistsException($"'{path}' exists already.", e); // lost a race for the same folder
        }
    }

    /// <summary>
    ///     Writes a new graph at <paramref name="path" /> below the library. A shared one gets the sidecar that makes
    ///     it an asset; a project's graph belongs to its project.
    /// </summary>
    /// <exception cref="ProjectExistsException">The file is taken.</exception>
    /// <exception cref="ProjectRefusedException">It names a project that is not there.</exception>
    public static void CreateGraph(string root, string path, string text, string name) {
        var file = LibraryLayout.Full(root, path);
        var folder = Path.GetDirectoryName(file)!;
        var shared = path.StartsWith($"{LibraryLayout.SharedFolder}/", StringComparison.Ordinal);
        if (!shared && !File.Exists(Path.Combine(folder, LibraryLayout.ProjectFile))) {
            throw new ProjectRefusedException($"There is no project at '{Path.GetRelativePath(root, folder)}'.");
        }

        try {
            Write(file, Utf8.GetBytes(text), false);
        } catch (IOException e) when (File.Exists(file)) {
            throw new ProjectExistsException($"'{path}' exists already.", e);
        }

        if (shared) {
            Replace(
                file + LibraryLayout.SidecarSuffix,
                DocumentCodec.Write(new AssetDocument(AssetId.New(), LibraryLayout.GraphKind, name, [], []))
            );
        }
    }

    /// <summary>Writes a new compound, and a sidecar when it has a <paramref name="sharedName" />.</summary>
    /// <exception cref="ProjectExistsException">The file is taken.</exception>
    public static void CreateCompound(string file, string text, string? sharedName) {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try {
            Write(file, Utf8.GetBytes(text), false);
        } catch (IOException e) when (File.Exists(file)) {
            throw new ProjectExistsException($"'{file}' exists already.", e);
        }

        if (sharedName is not null) {
            Replace(
                file + LibraryLayout.SidecarSuffix,
                DocumentCodec.Write(new AssetDocument(AssetId.New(), LibraryLayout.GraphKind, sharedName, [], []))
            );
        }
    }

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
    public static void Replace(string path, ReadOnlySpan<byte> content) => Write(path, content, true);

    static void Write(string path, ReadOnlySpan<byte> content, bool overwrite) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Environment.ProcessId}.{Environment.CurrentManagedThreadId}.tmp";
        try {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                stream.Write(content);
            }

            File.Move(temporary, path, overwrite);
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
