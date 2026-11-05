using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     One step of a document's history: rewrites the tree of version <paramref name="From" /> to the next.
/// </summary>
public sealed record DocumentMigration(int From, Action<YamlMappingNode> Upgrade);

/// <summary>
///     A kind of document's schema history. Files saved from 1.0 on keep loading (plan §5): a reader upgrades the raw
///     tree step by step to the current version before it looks at a single field, so the typed reader only ever
///     knows today's shape. Migrations work on the tree because the typed form of an old version no longer exists in
///     the code.
/// </summary>
public sealed class SchemaChain(int current, params IReadOnlyList<DocumentMigration> migrations) {
    public const string VersionKey = "schema";

    /// <summary>The version this build writes.</summary>
    public int Current { get; } = current;

    /// <summary>The chain of <c>library.yaml</c>. Version 1 is where all three start, so none has steps yet.</summary>
    public static SchemaChain Library { get; } = new(1);

    /// <summary>The chain of <c>project.yaml</c>.</summary>
    public static SchemaChain Project { get; } = new(1);

    /// <summary>The chain of a shared asset's sidecar.</summary>
    public static SchemaChain Asset { get; } = new(1);

    /// <summary>
    ///     Brings <paramref name="root" /> to <see cref="Current" /> in place and returns the file's version.
    /// </summary>
    internal int Upgrade(YamlMappingNode root, string path) {
        if (!root.Children.TryGetValue(VersionKey, out var node)
            || node is not YamlScalarNode { Value: { } text }
            || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var version)) {
            throw new LibraryFormatException(path, $"it has no '{VersionKey}' version.", false);
        }

        if (version > Current) {
            throw new LibraryFormatException(
                path,
                $"schema {version} is newer than the {Current} this Patina reads.",
                true
            );
        }

        for (var at = version; at < Current; at++) {
            var step = migrations.FirstOrDefault(m => m.From == at)
                ?? throw new LibraryFormatException(path, $"no migration leads on from schema {at}.", false);
            step.Upgrade(root);
        }

        return version;
    }
}
