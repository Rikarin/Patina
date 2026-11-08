using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     A settings record kept in a document as a YAML mapping, by the shape its JSON has. Writing walks the JSON;
///     reading walks the shape of a default record and asks the document for exactly those keys, so a key the record
///     does not have is still reported as unread, and a value is read as the type the record gives it, not guessed
///     from text.
/// </summary>
/// <remarks>
///     ⚠ <b>The shape is a default record's JSON, so every member needs a value that is not null</b>: a null is
///     left out of the JSON, and a member missing from the shape is a member that is never read back.
/// </remarks>
static class YamlRecords {
    /// <summary>A record's JSON as a mapping, keys in the record's order.</summary>
    public static YamlMappingNode Write(JsonObject value) {
        var map = new YamlMappingNode();
        foreach (var (key, node) in value) {
            map.Add(new YamlScalarNode(key), Node(node));
        }

        return map;
    }

    /// <summary>
    ///     A mapping read back into JSON, taking only the keys <paramref name="shape" /> has, and the shape's own value
    ///     for a key the mapping leaves out.
    /// </summary>
    /// <exception cref="LibraryFormatException">A value is not of the type the shape gives it.</exception>
    /// <remarks>
    ///     ⚠ <b>A key left out takes the default here, not in the deserializer.</b> The source-generated reader sets
    ///     an init-only member it is not given to null rather than leaving the record's own default, so a list or a
    ///     nested record a hand-edited file left out came back null and the next read of it threw.
    /// </remarks>
    public static JsonObject Read(YamlFields fields, JsonObject shape) {
        var made = new JsonObject();
        foreach (var (key, like) in shape) {
            made[key] = like switch {
                JsonObject inner => fields.Map(key) is { } child ? Read(child, inner) : inner.DeepClone(),
                JsonArray list => fields.Texts(key) is { Count: > 0 } texts
                    ? new JsonArray([..texts.Select(static text => (JsonNode)JsonValue.Create(text))])
                    : TextsWritten(fields, key)
                        ? new JsonArray()
                        : list.DeepClone(),
                JsonValue value when fields.Optional(key) is { } text => Scalar(
                    fields,
                    key,
                    value.GetValueKind(),
                    text
                ),
                _ => like?.DeepClone()
            };
        }

        return made;
    }

    /// <summary>Whether a list was there and empty, rather than left out: an empty list is <c>[]</c>.</summary>
    static bool TextsWritten(YamlFields fields, string key) => fields.Has(key);

    static JsonValue Scalar(YamlFields fields, string key, JsonValueKind kind, string text) =>
        kind switch {
            JsonValueKind.Number when long.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var whole
                )
                => JsonValue.Create(whole),
            JsonValueKind.Number when double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var number
                )
                && double.IsFinite(number) => JsonValue.Create(number),
            JsonValueKind.Number => throw fields.Problem($"'{key}' is not a number."),
            JsonValueKind.True or JsonValueKind.False => text switch {
                "true" => JsonValue.Create(true),
                "false" => JsonValue.Create(false),
                _ => throw fields.Problem($"'{key}' is not true or false.")
            },
            _ => JsonValue.Create(text)
        };

    static YamlNode Node(JsonNode? node) =>
        node switch {
            JsonObject map => Write(map),
            JsonArray list => new YamlSequenceNode(list.Select(Node)),
            JsonValue value when value.GetValueKind() is JsonValueKind.String =>
                YamlText.Text(value.GetValue<string>()),
            JsonValue value => YamlText.Plain(value.ToJsonString()),
            _ => YamlText.Plain(string.Empty)
        };
}
