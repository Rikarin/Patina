using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     One mapping of a document being read. It remembers what was asked for, so that whatever nobody asked for can
///     be reported: a key this schema does not know would be lost by the next save, which is drift the check must
///     see.
/// </summary>
sealed class YamlFields(YamlMappingNode node, string path, string prefix = "") {
    readonly HashSet<string> read = [];
    readonly List<YamlFields> children = [];

    /// <summary>Keys nobody read, here and below, dotted from the root.</summary>
    public IReadOnlyList<string> Unread {
        get {
            var unread = node.Children.Keys.OfType<YamlScalarNode>()
                .Select(static k => k.Value ?? string.Empty)
                .Where(k => !read.Contains(k))
                .Select(k => prefix + k)
                .ToList();
            unread.AddRange(children.SelectMany(static c => c.Unread));
            unread.Sort(StringComparer.Ordinal);
            return unread;
        }
    }

    public LibraryFormatException Problem(string problem) => new(path, problem, false);

    public string Require(string key) => Optional(key) ?? throw Problem($"'{prefix}{key}' is missing.");

    public string? Optional(string key) =>
        Find(key) switch {
            null => null,
            YamlScalarNode scalar => scalar.Value ?? string.Empty,
            _ => throw Problem($"'{prefix}{key}' is not a single value.")
        };

    public double Number(string key, double fallback) {
        if (Optional(key) is not { } text) {
            return fallback;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Problem($"'{prefix}{key}' is not a number.");
    }

    public T Id<T>(string key) where T : IGuidId<T> {
        var text = Require(key);
        return GuidId.TryParse<T>(text, out var id) ? id! : throw Problem($"'{prefix}{key}' is not an identity.");
    }

    public IReadOnlyList<string> Texts(string key) => [
        ..Sequence(key)
            .Select(item => item is YamlScalarNode { Value: { } text }
                    ? text
                    : throw Problem($"'{prefix}{key}' holds something that is not text.")
            )
    ];

    public IReadOnlyList<YamlFields> Maps(string key) {
        var index = 0;
        return [..Sequence(key).Select(item => Child(item, $"{prefix}{key}[{index++}]."))];
    }

    /// <summary>A mapping under a key, or null when there is none.</summary>
    public YamlFields? Map(string key) => Find(key) is { } value ? Child(value, $"{prefix}{key}.") : null;

    public IReadOnlyDictionary<string, string> TextMap(string key) {
        if (Find(key) is not { } value) {
            return new Dictionary<string, string>();
        }

        if (value is not YamlMappingNode map) {
            throw Problem($"'{prefix}{key}' is not a mapping.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, text) in map) {
            if (name is not YamlScalarNode { Value: { } nameText }
                || text is not YamlScalarNode { Value: { } valueText }) {
                throw Problem($"'{prefix}{key}' holds something that is not text.");
            }

            result[nameText] = valueText;
        }

        return result;
    }

    YamlNode? Find(string key) {
        read.Add(key);
        return node.Children.TryGetValue(key, out var value) && !IsNull(value) ? value : null;
    }

    YamlSequenceNode Sequence(string key) =>
        Find(key) switch {
            null => new(),
            YamlSequenceNode sequence => sequence,
            _ => throw Problem($"'{prefix}{key}' is not a list.")
        };

    YamlFields Child(YamlNode item, string childPrefix) {
        var child = new YamlFields(
            item as YamlMappingNode ?? throw Problem($"'{childPrefix.TrimEnd('.')}' is not a mapping."),
            path,
            childPrefix
        );
        children.Add(child);
        return child;
    }

    /// <summary>An empty <c>key:</c> reads as absent; a quoted empty string does not.</summary>
    static bool IsNull(YamlNode node) => node is YamlScalarNode { Value: null or "" or "~", Style: ScalarStyle.Plain };
}
