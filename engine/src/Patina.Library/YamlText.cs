using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     The text form of Patina's documents. One emitter with fixed settings, because diffs and merges stay readable
///     only if the same document always comes out as the same bytes (plan §5): line feeds on every platform, no
///     folding, no anchors.
/// </summary>
static partial class YamlText {
    static readonly EmitterSettings Settings = EmitterSettings.Default
        .WithBestIndent(2)
        .WithBestWidth(int.MaxValue)
        .WithIndentedSequences()
        .WithNewLine("\n");

    /// <summary>The root mapping of <paramref name="text" />.</summary>
    public static YamlMappingNode Parse(string text, string path) {
        var stream = new YamlStream();
        try {
            using var reader = new StringReader(text);
            stream.Load(reader);
        } catch (YamlException e) {
            throw new LibraryFormatException(path, $"it is not valid YAML ({e.Message}).", false);
        }

        return stream.Documents is [{ RootNode: YamlMappingNode root }]
            ? root
            : throw new LibraryFormatException(path, "it does not hold one mapping.", false);
    }

    public static string Write(YamlMappingNode root) {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        writer.NewLine = "\n";
        var emitter = new Emitter(writer, Settings);
        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart());
        Emit(emitter, root);
        emitter.Emit(new DocumentEnd(true));
        emitter.Emit(new StreamEnd());
        return writer.ToString();
    }

    /// <summary>
    ///     Free text, quoted when a YAML reader would otherwise take it for a number, a boolean or null. Patina reads
    ///     every scalar as text, but these files also meet Git hooks and other people's scripts.
    /// </summary>
    public static YamlScalarNode Text(string value) =>
        new(value) { Style = LooksTyped().IsMatch(value) ? ScalarStyle.DoubleQuoted : ScalarStyle.Any };

    /// <summary>A scalar that is plain by construction: an id, a hash, a number.</summary>
    public static YamlScalarNode Plain(string value) => new(value) { Style = ScalarStyle.Plain };

    public static YamlScalarNode Plain(int value) => Plain(value.ToString(CultureInfo.InvariantCulture));

    public static YamlScalarNode Plain(double value) => Plain(value.ToString("R", CultureInfo.InvariantCulture));

    public static YamlSequenceNode Texts(IEnumerable<string> values) => new(values.Select(Text));

    static void Emit(Emitter emitter, YamlNode node) {
        switch (node) {
            case YamlScalarNode scalar:
                emitter.Emit(new Scalar(null, null, scalar.Value ?? string.Empty, scalar.Style, true, true));
                break;
            case YamlSequenceNode sequence:
                emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Block));
                foreach (var item in sequence) {
                    Emit(emitter, item);
                }

                emitter.Emit(new SequenceEnd());
                break;
            case YamlMappingNode mapping:
                emitter.Emit(new MappingStart(null, null, true, MappingStyle.Block));
                foreach (var (key, value) in mapping) {
                    Emit(emitter, key);
                    Emit(emitter, value);
                }

                emitter.Emit(new MappingEnd());
                break;
            default:
                throw new InvalidOperationException($"Patina writes no {node.GetType().Name}.");
        }
    }

    [GeneratedRegex(@"^(~|null|true|false|yes|no|on|off|y|n|[-+.]?\d.*|[-+]?\.(inf|nan))$", RegexOptions.IgnoreCase)]
    private static partial Regex LooksTyped();
}
