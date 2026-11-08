using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     A settings record kept in a document as a YAML mapping, by its JSON contract: the export, bake and viewport
///     blocks of a <c>project.yaml</c>. Writing walks the record's JSON, so a key is the name the API gives it and a
///     null is left out. Reading walks the contract's properties and asks the document for exactly those keys, so a
///     key the record does not have is still reported as unread, and a value is read as the type the record gives
///     it, not guessed from text.
/// </summary>
/// <remarks>
///     ⚠ <b>A key left out takes the default here, not in the deserializer.</b> The source-generated reader sets an
///     init-only member it is not given to null rather than leaving the record's own default, so a list or a nested
///     record a hand-edited file left out came back null and the next read of it threw. The defaults are a default
///     record's JSON; a member that is null there, and every member of a record in a list, has no default, so leaving
///     it out is null for a nullable member and a missing key for any other.
/// </remarks>
static class YamlRecords {
    /// <summary>A record as a mapping, keys in the record's order, nulls left out.</summary>
    public static YamlMappingNode Write<T>(T value, JsonTypeInfo<T> type) =>
        Write(JsonSerializer.SerializeToNode(value, type)!.AsObject());

    /// <summary>A mapping read back into a record, taking what it leaves out from <paramref name="defaults" />.</summary>
    /// <exception cref="LibraryFormatException">A value is not of the type the record gives it, or is missing.</exception>
    public static T Read<T>(YamlFields fields, JsonTypeInfo<T> type, T defaults) {
        var shape = JsonSerializer.SerializeToNode(defaults, type)!.AsObject();
        return Record(fields, type, shape).Deserialize(type)!;
    }

    static YamlMappingNode Write(JsonObject value) {
        var map = new YamlMappingNode();
        foreach (var (key, node) in value) {
            map.Add(new YamlScalarNode(key), Node(node));
        }

        return map;
    }

    static YamlNode Node(JsonNode? node) =>
        node switch {
            JsonObject map => Write(map),
            JsonArray list => new YamlSequenceNode(list.Select(Node)),
            JsonValue value when value.GetValueKind() is JsonValueKind.String =>
                YamlText.Text(value.GetValue<string>()),
            JsonValue value => YamlText.Plain(value.ToJsonString()),
            _ => YamlText.Plain(string.Empty)
        };

    /// <summary>The record's JSON from the mapping, with <paramref name="shape" />'s value for each key left out.</summary>
    static JsonObject Record(YamlFields fields, JsonTypeInfo type, JsonObject? shape) {
        var made = new JsonObject();
        foreach (var property in type.Properties) {
            var key = property.Name;
            var like = shape?[key];
            var info = type.Options.GetTypeInfo(property.PropertyType);
            if (Value(fields, key, info, like) is { } value) {
                made[key] = value;
            } else if (like is not null) {
                made[key] = like.DeepClone();
            } else if (info.Kind is JsonTypeInfoKind.Enumerable) {
                made[key] = new JsonArray(); // a list left out is empty, never null
            } else if (!CanBeNull(property)) {
                throw fields.Problem($"'{key}' is missing.");
            }
        }

        return made;
    }

    /// <summary>What the mapping holds under a key, as the JSON of its type, or null when it holds nothing.</summary>
    static JsonNode? Value(YamlFields fields, string key, JsonTypeInfo type, JsonNode? like) =>
        type.Kind switch {
            JsonTypeInfoKind.Object => fields.Map(key) is { } child
                ? Record(child, type, like as JsonObject)
                : null,
            JsonTypeInfoKind.Enumerable => List(fields, key, type.Options.GetTypeInfo(type.ElementType!)),
            _ => fields.Optional(key) is { } text ? Scalar(fields, key, type.Type, text) : null
        };

    /// <summary>A list, or null when it was left out: an empty one is written <c>[]</c>.</summary>
    static JsonArray? List(YamlFields fields, string key, JsonTypeInfo element) {
        if (!fields.Has(key)) {
            return null;
        }

        if (element.Kind is JsonTypeInfoKind.Object) {
            return [..fields.Maps(key).Select(item => (JsonNode)Record(item, element, null))];
        }

        return [..fields.Texts(key).Select(text => (JsonNode)Scalar(fields, key, element.Type, text))];
    }

    static JsonValue Scalar(YamlFields fields, string key, Type type, string text) {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        if (plain == typeof(bool)) {
            return text switch {
                "true" => JsonValue.Create(true),
                "false" => JsonValue.Create(false),
                _ => throw fields.Problem($"'{key}' is not true or false.")
            };
        }

        if (plain == typeof(int) || plain == typeof(long)) {
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)
                ? JsonValue.Create(whole)
                : throw fields.Problem($"'{key}' is not a whole number.");
        }

        if (plain == typeof(double) || plain == typeof(float)) {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && double.IsFinite(number)
                    ? JsonValue.Create(number)
                    : throw fields.Problem($"'{key}' is not a number.");
        }

        return JsonValue.Create(text);
    }

    /// <summary>Whether a member can be null, which is what leaving it out says when it has no default.</summary>
    static bool CanBeNull(JsonPropertyInfo property) =>
        Nullable.GetUnderlyingType(property.PropertyType) is not null
        || (!property.PropertyType.IsValueType && property.IsSetNullable);
}
