using System.Security.Cryptography;
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
                              # Patina's bin: what was deleted here, kept on this machine until it is emptied.
                              .trash/

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

        Directory.CreateDirectory(Path.Combine(root, LibraryLayout.ProjectsFolder));
        WriteIfAbsent(Path.Combine(root, ".gitignore"), IgnoreFile);
        WriteIfAbsent(Path.Combine(root, ".gitattributes"), AttributesFile);
        Replace(file, DocumentCodec.Write(document));
    }

    /// <summary>Lays out a new library with the settings every library starts with.</summary>
    public static void CreateDefaultLibrary(string root, string name) =>
        CreateLibrary(
            root,
            new LibraryDocument(
                LibraryId.New(),
                name,
                1024,
                ["baseColor", "normal", "roughness", "metallic"],
                "pbr",
                []
            )
        );

    /// <summary>What a file holds, as a name for it: two files with one digest are the same bytes.</summary>
    public static string Digest(string file) {
        using var stream = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>
    ///     The full path of the document of the project at <paramref name="path" /> below <c>projects/</c>.
    /// </summary>
    public static string ProjectFile(string root, string path) =>
        LibraryLayout.Full(root, $"{LibraryLayout.ProjectsFolder}/{path}/{LibraryLayout.ProjectFile}");

    /// <summary>The full path of the document at <paramref name="path" /> below <c>projects/</c>.</summary>
    public static string AssetFile(string root, string path) =>
        LibraryLayout.Full(root, $"{LibraryLayout.ProjectsFolder}/{path}");

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

    /// <summary>Writes a new graph at <paramref name="path" /> below the library, in its project.</summary>
    /// <exception cref="ProjectExistsException">The file is taken.</exception>
    /// <exception cref="ProjectRefusedException">It names a project that is not there.</exception>
    public static void CreateGraph(string root, string path, string text) {
        var file = LibraryLayout.Full(root, path);
        var folder = Path.GetDirectoryName(file)!;
        if (!File.Exists(Path.Combine(folder, LibraryLayout.ProjectFile))) {
            throw new ProjectRefusedException($"There is no project at '{Path.GetRelativePath(root, folder)}'.");
        }

        try {
            Write(file, Utf8.GetBytes(text), false);
        } catch (IOException e) when (File.Exists(file)) {
            throw new ProjectExistsException($"'{path}' exists already.", e);
        }
    }

    /// <summary>Writes a new compound.</summary>
    /// <exception cref="ProjectExistsException">The file is taken.</exception>
    public static void CreateCompound(string file, string text) {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try {
            Write(file, Utf8.GetBytes(text), false);
        } catch (IOException e) when (File.Exists(file)) {
            throw new ProjectExistsException($"'{file}' exists already.", e);
        }
    }

    /// <summary>
    ///     Writes a document into a project, listing it there, and makes the project if it has to. Anything a project
    ///     holds is usable from any other, so this is all it takes for something to be an asset of the library.
    /// </summary>
    /// <param name="root">The library.</param>
    /// <param name="project">The project, below <c>projects/</c>.</param>
    /// <param name="content">The document's bytes.</param>
    /// <param name="item">What the project lists.</param>
    public static void WriteDocument(string root, string project, ReadOnlySpan<byte> content, ProjectItem item) {
        var listing = ProjectFile(root, project);
        var document = File.Exists(listing)
            ? DocumentCodec.ReadProject(File.ReadAllText(listing), project).Document
            : new(ProjectId.New(), Path.GetFileName(project), [], [], []);
        Replace(AssetFile(root, $"{project}/{item.File}"), content);
        WriteProject(
            root,
            project,
            document with { Documents = [..document.Documents.Where(d => d.File != item.File), item] }
        );
    }

    /// <summary>Writes a document at <paramref name="path" />, whose folder is its project.</summary>
    public static void WriteAsset(string root, string path, ReadOnlySpan<byte> content, AssetDocument document) {
        var kind = DocumentKinds.Known(document.Kind) ? document.Kind : DocumentKinds.Graph;
        var item = new ProjectItem(document.Id, kind, Path.GetFileName(path), document.Name) {
            Tags = document.Tags, Dependencies = document.Dependencies
        };
        WriteDocument(root, Path.GetDirectoryName(path)!.Replace('\\', '/'), content, item);
    }

    /// <summary>
    ///     Keeps the bytes the document at <paramref name="path" /> has now, so that a pin to them stays
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
                stream.Flush(true); // on the disk before the name is: a crash must not leave an empty file in its place
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
