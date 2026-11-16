using Patina.Contracts;
using Patina.Graphs;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Vixen.NodeGraph;

namespace Patina.Server;

/// <summary>
///     Pictures of what a library holds, for any list that shows them: the Library's grid, the shelf, and whatever
///     client comes next. Each is drawn by the Worker into the content-addressed preview cache, and a client is given
///     its hash, never its pixels. What a thing looks like is named by a key made of everything that decides it: the
///     document, the compounds and pictures it reads, and the size. The key's picture is remembered in a small file in
///     the cache, so a restart draws nothing again, and a deleted cache costs only the drawing. A project is drawn as
///     the document that best stands for it, so its picture is that document's and costs nothing more.
/// </summary>
public sealed partial class Thumbnails(WorkerLink worker, PreviewStore store) : IDisposable {
    /// <summary>The side of a picture, which is also the size a material is computed at for it.</summary>
    public const int Side = 256;

    /// <summary>Goes up when a picture would come out differently for the same files.</summary>
    const string Version = "thumbnail-1";

    const string KeysFolder = "thumbnails";
    const string NothingToDraw = "-";
    const string OutputNode = "Output/Output";
    const string UsageSetting = "Usage";
    const string DefaultUsage = "baseColor";
    const string StarterPrefix = "starter:";

    readonly ConcurrentDictionary<string, string> known = new(StringComparer.Ordinal);

    /// <summary>
    ///     One drawing at a time: the Worker draws one at a time anyway, and a shelf asks for many at once.
    /// </summary>
    readonly SemaphoreSlim drawing = new(1, 1);

    /// <summary>
    ///     How long a picture waits for a Worker to be connected, and never for the one connected to finish (#275): a
    ///     panorama is drawn at <see cref="PanoramaSide" />, which a busy GPU took more than ten seconds over while the
    ///     viewport answered that no GPU process was connected.
    /// </summary>
    public TimeSpan WorkerPatience { get; init; } = TimeSpan.FromSeconds(10);

    public void Dispose() => drawing.Dispose();

    /// <summary>The pictures of some documents, projects or shelf materials, drawing those the cache lacks.</summary>
    /// <returns>Each source with its picture's hash, or none for one with nothing to show.</returns>
    /// <exception cref="TimeoutException">No Worker came to draw what had to be drawn.</exception>
    public async Task<IReadOnlyList<Thumbnail>> DrawAsync(
        OpenLibrary library,
        IReadOnlyList<string> sources,
        CancellationToken cancellationToken
    ) {
        List<Thumbnail> drawn = [];
        foreach (var source in sources) {
            var ask = Readable(library, source);
            var hash = ask is null ? null : await HashAsync(ask.Value.Key, ask.Value.Ask, cancellationToken);
            drawn.Add(new(source, hash));
        }

        return drawn;
    }

    async Task<string?> HashAsync(string key, MaterialThumbnailAsk ask, CancellationToken cancellationToken) {
        var extension = ask.Deep ? GraphPreviewFiles.Radiance : GraphPreviewFiles.Extension;
        if (Held(key, extension) is { } held) {
            return held.Length == 0 ? null : held;
        }

        await drawing.WaitAsync(cancellationToken);
        try {
            if (Held(key, extension) is { } now) {
                return now.Length == 0 ? null : now;
            }

            var (hash, lasting) = await DrawAsync(ask, cancellationToken);
            if (lasting) {
                Remember(key, hash);
            }

            return hash;
        } finally {
            drawing.Release();
        }
    }

    /// <summary>What a key's picture is, if its file is still in the cache: empty for nothing to draw.</summary>
    string? Held(string key, string extension) {
        if (!known.TryGetValue(key, out var hash)) {
            var file = KeyFile(key);
            if (!File.Exists(file)) {
                return null;
            }

            hash = SharedFile.ReadAllText(file).Trim();
            hash = hash == NothingToDraw ? string.Empty : hash;
        }

        if (hash.Length > 0 && store.Find(hash, extension) is null) {
            // The cache was trimmed or deleted under it, and it is drawn again.
            known.TryRemove(key, out _);
            return null;
        }

        known[key] = hash;
        return hash;
    }

    void Remember(string key, string? hash) {
        known[key] = hash ?? string.Empty;
        var file = KeyFile(key);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        LibraryFiles.Replace(file, hash ?? NothingToDraw);
    }

    string KeyFile(string key) => Path.Combine(store.Root, KeysFolder, key[..2], key);

    /// <summary>The Worker's picture of an ask, and whether its answer holds for these files anywhere.</summary>
    async Task<(string? Hash, bool Lasting)> DrawAsync(MaterialThumbnailAsk ask, CancellationToken cancellationToken) {
        var connection = await WorkerWait.ConnectionAsync(worker, WorkerPatience, cancellationToken);
        try {
            var reply = await connection.SendAsync(
                new() { Type = IpcTypes.MaterialThumbnail, MaterialThumbnail = ask },
                cancellationToken
            );
            return (reply.ThumbnailHash, true);
        } catch (IpcException e) when (!ask.Deep) {
            // A material that cannot draw shows its name alone, as it did before there were pictures. A panorama is
            // asked for by name, so the Worker's reason reaches whoever asked, and nothing is remembered for it. A
            // Worker with no device says nothing about the material, so that is not remembered either (#302): the
            // same library opened where there is a GPU draws it.
            return (null, !e.AboutTheDevice);
        }
    }

    /// <summary>A source as the Worker draws it, and its key. One that cannot be read has no picture.</summary>
    (string Key, MaterialThumbnailAsk Ask)? Readable(OpenLibrary library, string source) {
        try {
            return Ask(library, source);
        } catch (Exception e) when (e is IOException
                                        or UnauthorizedAccessException
                                        or StackEditRefusedException
                                        or ProjectRefusedException
                                        or LibraryFormatException
                                        or FormatException
                                        or ArgumentException
                                        or InvalidOperationException) {
            return null;
        }
    }

    (string Key, MaterialThumbnailAsk Ask)? Ask(OpenLibrary library, string source) {
        if (source.StartsWith(StarterPrefix, StringComparison.Ordinal)) {
            var text = MaterialShelf.StarterText(source[StarterPrefix.Length..]);
            return OfGraph(source, text, Path.Combine(library.RootPath, source[StarterPrefix.Length..]), null, []);
        }

        if (source.StartsWith(ShelfCompounds.Prefix, StringComparison.Ordinal)) {
            return ShelfCompounds.NodeOf(source) is { } node ? OfCompound(library, node) : null;
        }

        if (ShelfEnvironments.PictureOf(source) is { } panorama) {
            return Ask(library, panorama); // an environment on the shelf is drawn as the picture it is (#203)
        }

        if (source.StartsWith(ShelfAlphas.Prefix, StringComparison.Ordinal)) {
            return null; // a client draws an alpha as a stamp of its brush
        }

        if (source.StartsWith(MaterialShelf.ShippedPrefix, StringComparison.Ordinal)) {
            return OfStack(source, library.RootPath, Path.Combine(library.RootPath, LibraryLayout.ProjectsFolder));
        }

        var full = Path.GetFullPath(source, library.RootPath);
        if (!full.StartsWith(
                Path.GetFullPath(library.RootPath) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal
            )) {
            return null;
        }

        if (Directory.Exists(full)) {
            // An empty material, or one that cannot be read, gives way to the next.
            return Standing(library, source)
                .Select(document => Readable(library, document))
                .FirstOrDefault(static ask => ask is not null);
        }

        if (!File.Exists(full)) {
            return null;
        }

        if (PictureFormats.IsPicture(full)) {
            return OfPicture(source, full);
        }

        if (MaterialPresets.Names(full)) {
            return OfPreset(library, source, full);
        }

        if (!full.EndsWith(LibraryLayout.GraphExtension, StringComparison.OrdinalIgnoreCase)
            && !full.EndsWith(StackDocuments.Extension, StringComparison.OrdinalIgnoreCase)) {
            return null; // a brush preset or a mesh: nothing a material picture can show
        }

        return full.EndsWith(LibraryLayout.GraphExtension, StringComparison.OrdinalIgnoreCase)
            ? OfGraph(
                source,
                SharedFile.ReadAllText(full),
                full,
                CompoundFolders.Of(full),
                CompoundFolders.PictureRoots(full, library.RootPath)
            )
            : OfStack(source, library.RootPath, full);
    }

    /// <summary>
    ///     The documents a project's picture could be, best first: a material made to be used, then a graph, then what
    ///     is painted on its mesh. The first that can be drawn is the picture. None for a folder that is no project.
    /// </summary>
    static IEnumerable<string> Standing(OpenLibrary library, string folder) {
        var prefix = LibraryLayout.ProjectsFolder + "/";
        if (!folder.StartsWith(prefix, StringComparison.Ordinal)
            || !File.Exists(Path.Combine(Path.GetFullPath(folder, library.RootPath), LibraryLayout.ProjectFile))) {
            return [];
        }

        return library.Contents(folder[prefix.Length..])
            .Documents.Where(static document => Rank(document) >= 0)
            .OrderBy(Rank)
            .Select(static document => document.Path);
    }

    static int Rank(ProjectDocumentInfo document) =>
        document.Kind switch {
            DocumentKinds.LayeredMaterial => 0,
            DocumentKinds.ImageMaterial => 1,
            DocumentKinds.Graph when document.Role is null or GraphRoles.Material => 2,
            DocumentKinds.Texturing => 3,
            DocumentKinds.Graph => 4,
            _ => -1
        };

    /// <summary>
    ///     A picture file, as a graph that reads it into its base colour draws it (#203): the Worker decodes it as a
    ///     layer would, and the file's stamp is part of its key, so a picture saved over is drawn again.
    /// </summary>
    (string Key, MaterialThumbnailAsk Ask)? OfPicture(string source, string full) {
        var folder = Path.GetDirectoryName(full)!;
        NodeGraphAsset graph = new() {
            Name = Path.GetFileNameWithoutExtension(full),
            Nodes = [
                new() {
                    Id = 1,
                    Type = GraphFiles.BitmapNode,
                    Texts = new() { [GraphFiles.SettingOf(GraphFiles.BitmapNode)!] = Path.GetFileName(full) }
                },
                new() { Id = 2, Type = OutputNode, Texts = new() { [UsageSetting] = DefaultUsage } }
            ],
            Edges = [new() { FromNode = 1, FromPort = "Out", ToNode = 2, ToPort = "Input" }]
        };
        var file = Path.Combine(folder, graph.Name + LibraryLayout.GraphExtension);
        return OfGraph(source, GraphDocuments.Text(graph), file, null, [folder]);
    }

    /// <summary>A graph's Output, preferring its base colour, computed at the picture's size.</summary>
    (string Key, MaterialThumbnailAsk Ask)? OfGraph(
        string source,
        string text,
        string file,
        string? folder,
        IReadOnlyList<string> roots
    ) {
        var graph = GraphDocuments.Parse(text);
        if (Output(graph) is not var (node, usage)) {
            return null;
        }

        var small = graph with {
            Settings = new(graph.Settings, StringComparer.Ordinal) {
                ["baseWidth"] = $"{Side}", ["baseHeight"] = $"{Side}"
            }
        };
        var document = GraphDocuments.Text(small);
        var pictures = FilesRead(graph, roots);
        var key = Key(
            MaterialThumbnailKinds.Graph,
            source,
            document,
            [..Stamps(folder is null ? [] : Files(folder, true)), ..Stamps(pictures)]
        );
        return (key, new(MaterialThumbnailKinds.Graph, file, document, folder, roots, node, usage, store.Root));
    }

    /// <summary>
    ///     A layered material, as the shelf hands it to a layer, at the picture's size. Its paint lies beside its file
    ///     and its compounds below it, so those are part of what it looks like. One with no layers yet is nothing to
    ///     show, and a project's picture passes it by.
    /// </summary>
    (string Key, MaterialThumbnailAsk Ask)? OfStack(string source, string library, string file) {
        var material = MaterialShelf.Read(library, source);
        if (material.Sets.All(static set => set.Layers.Count == 0)) {
            return null;
        }

        var document = StackDocuments.Text(material with { BaseWidth = Side, BaseHeight = Side });
        var folder = Path.GetDirectoryName(file)!;
        var key = Key(
            MaterialThumbnailKinds.Stack,
            source,
            document,
            source.StartsWith(MaterialShelf.ShippedPrefix, StringComparison.Ordinal)
                ? []
                : [
                    ..Stamps(Files(folder, false)),
                    ..Stamps(Files(Path.Combine(folder, LibraryLayout.CompoundsFolder), true))
                ]
        );
        var ask = new MaterialThumbnailAsk(
            MaterialThumbnailKinds.Stack,
            file,
            document,
            folder,
            [library],
            0,
            DefaultUsage,
            store.Root
        );
        return (key, ask);
    }

    static string Key(string kind, string source, string document, IReadOnlyList<string> stamps) {
        StringBuilder text = new();
        text.Append(Version).Append('\n').Append(Side).Append('\n').Append(kind).Append('\n').Append(source);
        foreach (var stamp in stamps) {
            text.Append('\n').Append(stamp);
        }

        text.Append('\n').Append(document);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    static IEnumerable<string> Files(string folder, bool below) {
        if (!Directory.Exists(folder)) {
            return [];
        }

        var depth = below ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(folder, "*", depth).Order(StringComparer.Ordinal);
    }

    /// <summary>When and how large each file is, which is what changes when it is saved.</summary>
    /// <summary>
    ///     Where each file a graph reads may be, a font and an SVG as much as a picture (#306), so that one saved over
    ///     draws it again.
    /// </summary>
    static IEnumerable<string> FilesRead(NodeGraphAsset graph, IReadOnlyList<string> roots) =>
        GraphFiles.Read(graph)
            .Select(static read => read.File)
            .Where(static file => !GraphFiles.IsScheme(file))
            .SelectMany(file => roots.Select(root => Path.GetFullPath(file, root)));

    static IEnumerable<string> Stamps(IEnumerable<string> files) =>
        files.Where(File.Exists)
            .Select(static file => {
                    var info = new FileInfo(file);
                    return $"{file}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
                }
            );
}
