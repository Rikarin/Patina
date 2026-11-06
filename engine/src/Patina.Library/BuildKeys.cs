using Patina.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace Patina.Library;

/// <summary>
///     The name of a build's result: a hash tree of its inputs and the version of what builds it. An output is stale
///     only when its key changes, and a key match reuses the file, on this machine or another (plan §5). It
///     identifies what should be built; it does not promise that two GPUs produce the same bytes.
/// </summary>
public readonly record struct BuildKey(string Hex) {
    public override string ToString() => Hex;
}

/// <summary>
///     The installed versions of what builds outputs. A tool that publishes no version of its own falls to the
///     engine's release, which stales too much on an upgrade and never too little: the safe side, until the Worker
///     reports its kernels' versions.
/// </summary>
public sealed class ToolCatalog(IReadOnlyDictionary<string, string> versions, string fallback) {
    /// <summary>What a process knows with no Worker to ask.</summary>
    public static ToolCatalog Default { get; } = new(new Dictionary<string, string>(), ProductInfo.Version);

    public string VersionOf(string tool) => versions.GetValueOrDefault(tool, fallback);
}

/// <summary>Hashes fields so that their boundaries count: ("ab", "c") must not meet ("a", "bc").</summary>
sealed class KeyHash : IDisposable {
    readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    /// <param name="domain">The kind of key, which also keeps two kinds of key from ever colliding.</param>
    public KeyHash(string domain) {
        Add(domain);
    }

    public KeyHash Add(string text) {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Add(bytes.Length).Append(bytes);
    }

    public KeyHash Add(int value) {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(bytes, value);
        hash.AppendData(bytes);
        return this;
    }

    public BuildKey Finish() => new(Convert.ToHexStringLower(hash.GetHashAndReset()));

    public void Dispose() => hash.Dispose();

    KeyHash Append(byte[] bytes) {
        hash.AppendData(bytes);
        return this;
    }
}

/// <summary>The key of a bake: the files it reads, what it was asked for, and what bakes.</summary>
public static class BakeKeys {
    /// <summary>The tool whose version a bake's key carries.</summary>
    public const string Tool = "mesh-maps";

    /// <summary>
    ///     The key. Inputs count by content and by role, so renaming a mesh or moving it reuses the bake, and swapping
    ///     the high and the low does not. Where the maps are going is not in it, nor is the name of a file.
    /// </summary>
    /// <param name="files">Each input's role and revision.</param>
    /// <param name="settings">Everything that changes a pixel.</param>
    /// <param name="tools">The installed tool versions.</param>
    public static BuildKey Compute(
        IReadOnlyDictionary<string, Revision> files,
        IReadOnlyDictionary<string, string> settings,
        ToolCatalog tools
    ) {
        using var hash = new KeyHash("patina.bake.v1").Add(Tool).Add(tools.VersionOf(Tool));

        hash.Add(files.Count);
        foreach (var (role, revision) in files.OrderBy(static f => f.Key, StringComparer.Ordinal)) {
            hash.Add(role).Add(revision.Text);
        }

        hash.Add(settings.Count);
        foreach (var (name, value) in settings.OrderBy(static s => s.Key, StringComparer.Ordinal)) {
            hash.Add(name).Add(value);
        }

        return hash.Finish();
    }
}

/// <summary>A shared asset as keys see it.</summary>
/// <param name="Id">The asset.</param>
/// <param name="Content">The hash of its file, or null when the file is missing.</param>
/// <param name="Dependencies">The other assets it reads.</param>
public sealed record AssetNode(AssetId Id, Revision? Content, IReadOnlyList<AssetReference> Dependencies);

/// <summary>What an edit to an asset does to something that references it, directly or through other assets.</summary>
public enum AssetReach {
    /// <summary>It does not read that asset at all.</summary>
    None,

    /// <summary>It reads the asset only through a pin to that asset, so the edit does not reach it.</summary>
    Held,

    /// <summary>The edit changes its key.</summary>
    Moves
}

/// <summary>
///     The dependency graph of the shared assets, and the key each one contributes to what reads it. The key of an
///     asset covers its bytes and the keys of everything it reads. A pin replaces only the bytes, which is what keeps
///     a pin from hiding a change further down: the file is frozen, the things it reads are not.
/// </summary>
/// <remarks>Holds the shared assets only, which is a small part of a library, and memoises their keys.</remarks>
public sealed class AssetGraph {
    readonly Dictionary<AssetId, AssetNode> nodes = [];
    readonly Dictionary<(AssetId, Revision?), BuildKey?> keys = [];
    readonly HashSet<AssetId> visiting = [];

    public AssetGraph(IEnumerable<AssetNode> assets) {
        foreach (var asset in assets) {
            nodes.TryAdd(asset.Id, asset); // Two assets sharing an id are a finding; the first by path stands in.
        }
    }

    /// <summary>The key a reference resolves to, or null when it reaches a missing asset or a cycle.</summary>
    public BuildKey? KeyOf(AssetReference reference) {
        lock (keys) {
            return Key(reference.Asset, reference.Revision);
        }
    }

    /// <summary>How an edit to <paramref name="target" /> reaches what reads <paramref name="reference" />.</summary>
    public AssetReach Reach(AssetReference reference, AssetId target) {
        ArgumentNullException.ThrowIfNull(reference);
        HashSet<AssetId> seen = [];
        var reach = AssetReach.None;

        void Walk(AssetReference from) {
            if (from.Asset == target) {
                reach = from.Mode == ReferenceMode.Pinned && reach == AssetReach.None
                    ? AssetReach.Held
                    : AssetReach.Moves;
                if (from.Mode != ReferenceMode.Pinned) {
                    return;
                }
            }

            if (seen.Add(from.Asset) && nodes.TryGetValue(from.Asset, out var node)) {
                foreach (var dependency in node.Dependencies) {
                    Walk(dependency);
                }
            }
        }

        Walk(reference);
        return reach;
    }

    /// <summary>Assets that take part in a dependency cycle: each cycle is named by one of its members.</summary>
    public IReadOnlyList<AssetId> FindCycles() {
        HashSet<AssetId> done = [];
        HashSet<AssetId> path = [];
        List<AssetId> cycles = [];

        void Visit(AssetId id) {
            if (path.Contains(id)) {
                cycles.Add(id);
                return;
            }

            if (!done.Add(id) || !nodes.TryGetValue(id, out var node)) {
                return;
            }

            path.Add(id);
            foreach (var dependency in node.Dependencies) {
                Visit(dependency.Asset);
            }

            path.Remove(id);
        }

        foreach (var id in nodes.Keys) {
            Visit(id);
        }

        return cycles;
    }

    BuildKey? Key(AssetId id, Revision? pinned) {
        if (keys.TryGetValue((id, pinned), out var known)) {
            return known;
        }

        BuildKey? key = null;
        if (nodes.TryGetValue(id, out var node) && (pinned ?? node.Content) is { } bytes && visiting.Add(id)) {
            key = Compute(node, bytes);
            visiting.Remove(id);
        }

        // Null is as final as a key: whatever meets the walk's own path is itself on a cycle, whichever walk asks.
        keys[(id, pinned)] = key;
        return key;
    }

    BuildKey? Compute(AssetNode node, Revision bytes) {
        using var hash = new KeyHash("patina.asset.v1").Add(bytes.Text).Add(node.Dependencies.Count);
        foreach (var dependency in node.Dependencies.OrderBy(static d => d.Asset.ToString(), StringComparer.Ordinal)) {
            if (Key(dependency.Asset, dependency.Revision) is not { } key) {
                return null;
            }

            hash.Add(dependency.Asset.ToString()).Add(key.Hex);
        }

        return hash.Finish();
    }
}

/// <summary>The keys of a project's outputs.</summary>
public static class OutputKeys {
    /// <summary>
    ///     The key of <paramref name="output" />. The project's identity, the output's name and the library's
    ///     location are not in it: two projects building the same thing from the same inputs share the result, and
    ///     moving a project rebuilds nothing.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="project">The project declaring it.</param>
    /// <param name="fileRevision">The revision of a project file, or null when the file is missing.</param>
    /// <param name="graph">The shared assets.</param>
    /// <param name="tools">The installed tool versions.</param>
    /// <param name="stacks">How to look inside a stack this output declares, for the files it reads.</param>
    /// <returns>The key, or null when an input cannot be resolved.</returns>
    public static BuildKey? Compute(
        OutputDeclaration output,
        ProjectDocument project,
        Func<string, Revision?> fileRevision,
        AssetGraph graph,
        ToolCatalog tools,
        StackFiles? stacks = null
    ) {
        using var hash = new KeyHash("patina.output.v1").Add(output.Tool).Add(tools.VersionOf(output.Tool));

        hash.Add(output.Settings.Count);
        foreach (var (name, value) in output.Settings.OrderBy(static s => s.Key, StringComparer.Ordinal)) {
            hash.Add(name).Add(value);
        }

        var files = output.Files.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        hash.Add(files.Count);
        foreach (var file in files) {
            if (fileRevision(file) is not { } revision) {
                return null;
            }

            hash.Add(file).Add(revision.Text);
        }

        if (stacks is not null) {
            AddStackInputs(hash, files, stacks);
        }

        var uses = output.Uses.Distinct().OrderBy(static u => u.ToString(), StringComparer.Ordinal).ToList();
        hash.Add(uses.Count);
        var references = project.References.ToLookup(static r => r.Asset);
        foreach (var asset in uses) {
            var reference = references[asset].First();
            if (graph.KeyOf(reference) is not { } key) {
                return null;
            }

            hash.Add(asset.ToString()).Add(key.Hex);
        }

        return hash.Finish();
    }

    static void AddStackInputs(KeyHash hash, List<string> files, StackFiles stacks) {
        foreach (var stack in files.Where(static file => file.EndsWith(StackInputs.Extension, StringComparison.Ordinal))) {
            if (stacks.Text(stack) is not { } text) {
                continue;
            }

            foreach (var input in StackInputs.Of(stack, text)) {
                hash.Add(input).Add(stacks.Revision(input)?.Text ?? string.Empty);
            }
        }
    }
}

/// <summary>The keys of what an export preset makes of a project's outputs.</summary>
public static class ExportKeys {
    /// <summary>The tool whose version an export's key carries.</summary>
    public const string Tool = "export";

    /// <summary>
    ///     The key. A preset counts by the bytes of its file, and outputs by their keys, so an edit to either
    ///     re-encodes and nothing else does. The project's name is not in it: names go on when the files are delivered,
    ///     so two projects with the same maps share one encode.
    /// </summary>
    /// <param name="presetRevision">The hash of the preset's file.</param>
    /// <param name="outputs">The keys of the outputs read, in any order.</param>
    /// <param name="tools">The installed tool versions.</param>
    public static BuildKey Compute(string presetRevision, IEnumerable<BuildKey> outputs, ToolCatalog tools) {
        ArgumentNullException.ThrowIfNull(tools);
        var keys = outputs.Select(static key => key.Hex).Distinct().Order(StringComparer.Ordinal).ToList();
        using var hash = new KeyHash("patina.export.v1").Add(Tool).Add(tools.VersionOf(Tool)).Add(presetRevision);
        hash.Add(keys.Count);
        foreach (var key in keys) {
            hash.Add(key);
        }

        return hash.Finish();
    }
}

/// <summary>
///     The content-addressed results under <c>.ptcache/objects</c>. An output is fresh exactly when an object exists
///     under its current key; a deleted cache only means everything is rebuilt.
/// </summary>
public sealed class ObjectStore(string root) {
    public string PathOf(BuildKey key) => Path.Combine(LibraryLayout.ObjectsPath(root), key.Hex[..2], key.Hex);

    public bool Contains(BuildKey key) => File.Exists(PathOf(key));

    public void Put(BuildKey key, ReadOnlySpan<byte> content) => LibraryFiles.Replace(PathOf(key), content);

    /// <summary>
    ///     The folder an object's files live in. The object itself, the file at <see cref="PathOf" />, is written last,
    ///     so a result is whole whenever the object exists.
    /// </summary>
    public string FolderOf(BuildKey key) => $"{PathOf(key)}.d";
}
