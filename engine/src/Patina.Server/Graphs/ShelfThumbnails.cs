using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Stacks;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Patina.Server.Graphs;

/// <summary>
///     The shelf's pictures of its materials. Each is drawn by the Worker into the content-addressed preview cache, and
///     a client is given its hash, never its pixels. What a material looks like is named by a key made of everything
///     that decides it: the document, the compounds and pictures it reads, and the size. The key's picture is
///     remembered in a small file in the cache, so a restart draws nothing again, and a deleted cache costs only the
///     drawing.
/// </summary>
public sealed class ShelfThumbnails(WorkerLink worker, PreviewStore store) : IDisposable {
    /// <summary>The side of a picture, which is also the size a material is computed at for it.</summary>
    public const int Side = 256;

    /// <summary>Goes up when a picture would come out differently for the same files.</summary>
    const string Version = "shelf-thumbnail-1";

    const string KeysFolder = "shelf";
    const string NothingToDraw = "-";
    const string OutputNode = "Output/Output";
    const string BitmapNode = "Source/Bitmap";
    const string UsageSetting = "Usage";
    const string SourceSetting = "Source";
    const string DefaultUsage = "baseColor";
    const string StarterPrefix = "starter:";
    static readonly TimeSpan WorkerPatience = TimeSpan.FromSeconds(10);

    readonly ConcurrentDictionary<string, string> known = new(StringComparer.Ordinal);

    /// <summary>
    ///     One drawing at a time: the Worker draws one at a time anyway, and a shelf asks for many at once.
    /// </summary>
    readonly SemaphoreSlim drawing = new(1, 1);

    public void Dispose() => drawing.Dispose();

    /// <summary>The pictures of some materials, drawing those the cache lacks.</summary>
    /// <returns>Each source with its picture's hash, or none for a material with nothing to show.</returns>
    /// <exception cref="TimeoutException">No Worker came to draw what had to be drawn.</exception>
    public async Task<IReadOnlyList<MaterialThumbnail>> DrawAsync(
        OpenLibrary library,
        IReadOnlyList<string> sources,
        CancellationToken cancellationToken
    ) {
        List<MaterialThumbnail> drawn = [];
        foreach (var source in sources) {
            var ask = Readable(library, source);
            var hash = ask is null ? null : await HashAsync(ask.Value.Key, ask.Value.Ask, cancellationToken);
            drawn.Add(new(source, hash));
        }

        return drawn;
    }

    async Task<string?> HashAsync(string key, MaterialThumbnailAsk ask, CancellationToken cancellationToken) {
        if (Held(key) is { } held) {
            return held.Length == 0 ? null : held;
        }

        await drawing.WaitAsync(cancellationToken);
        try {
            if (Held(key) is { } now) {
                return now.Length == 0 ? null : now;
            }

            var hash = await DrawAsync(ask, cancellationToken);
            Remember(key, hash);
            return hash;
        } finally {
            drawing.Release();
        }
    }

    /// <summary>What a key's picture is, if its file is still in the cache: empty for nothing to draw.</summary>
    string? Held(string key) {
        if (!known.TryGetValue(key, out var hash)) {
            var file = KeyFile(key);
            if (!File.Exists(file)) {
                return null;
            }

            hash = File.ReadAllText(file).Trim();
            hash = hash == NothingToDraw ? string.Empty : hash;
        }

        if (hash.Length > 0 && store.Find(hash) is null) {
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

    async Task<string?> DrawAsync(MaterialThumbnailAsk ask, CancellationToken cancellationToken) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(WorkerPatience);
        try {
            var connection = await worker.ConnectionAsync(patience.Token);
            var reply = await connection.SendAsync(
                new() { Type = IpcTypes.MaterialThumbnail, MaterialThumbnail = ask },
                patience.Token
            );
            return reply.ThumbnailHash;
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RendererMissingException();
        } catch (IpcException) {
            // A material that cannot draw shows its name alone, as it did before there were pictures.
            return null;
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

        if (source.StartsWith(MaterialShelf.ShippedPrefix, StringComparison.Ordinal)) {
            return OfStack(source, library.RootPath, Path.Combine(library.RootPath, LibraryLayout.ProjectsFolder));
        }

        var full = Path.GetFullPath(source, library.RootPath);
        if (!full.StartsWith(Path.GetFullPath(library.RootPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !File.Exists(full)) {
            return null;
        }

        return full.EndsWith(LibraryLayout.GraphExtension, StringComparison.OrdinalIgnoreCase)
            ? OfGraph(
                source,
                File.ReadAllText(full),
                full,
                CompoundFolders.Of(full),
                CompoundFolders.PictureRoots(full, library.RootPath)
            )
            : OfStack(source, library.RootPath, full);
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
        var outputs = graph.Nodes.Where(static node => node.Type == OutputNode)
            .Select(static node => (node.Id, Usage: node.Texts.GetValueOrDefault(UsageSetting, DefaultUsage)))
            .ToList();
        if (outputs.Count == 0) {
            return null;
        }

        var (node, usage) = outputs.Find(static output => output.Usage == DefaultUsage) is { Usage: not null } chosen
            ? chosen
            : outputs[0];
        var small = graph with {
            Settings = new(graph.Settings, StringComparer.Ordinal) {
                ["baseWidth"] = $"{Side}", ["baseHeight"] = $"{Side}"
            }
        };
        var document = GraphDocuments.Text(small);
        var pictures = graph.Nodes.Where(static n => n.Type == BitmapNode)
            .Select(static n => n.Texts.GetValueOrDefault(SourceSetting))
            .OfType<string>()
            .SelectMany(picture => roots.Select(root => Path.GetFullPath(picture, root)));
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
    ///     and its compounds below it, so those are part of what it looks like.
    /// </summary>
    (string Key, MaterialThumbnailAsk Ask) OfStack(string source, string library, string file) {
        var material = MaterialShelf.Read(library, source);
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
    static IEnumerable<string> Stamps(IEnumerable<string> files) =>
        files.Where(File.Exists)
            .Select(static file => {
                    var info = new FileInfo(file);
                    return $"{file}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
                }
            );
}
