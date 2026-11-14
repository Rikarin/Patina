using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Patina.Server.Api;

/// <summary>
///     Makes the OpenAPI document say what the Server actually sends: every property that is not nullable is always
///     present, so it is required; nullable ones are left out when null (<c>WhenWritingNull</c>), so they are not.
///     Generated clients (editor/libs/api-client, clients/python) then get exact types instead of everything-optional
///     ones.
/// </summary>
/// <remarks>
///     ⚠ <b>A setting a request may leave out says what it starts as.</b> A contract made with nothing, such as a
///     bake's options, gives its numbers, words and switches a <c>default</c>, which a generated Python client takes
///     for its argument (#294): a new setting added to the options then breaks no script written before it.
/// </remarks>
static class OpenApiSchemas {
    public static void MarkNonNullablePropertiesRequired(this OpenApiOptions options) =>
        options.AddSchemaTransformer(static (schema, context, _) => {
                if (schema.Properties is { Count: > 0 } properties) {
                    var nullable = context.JsonTypeInfo.Properties.Where(static p => p.IsGetNullable)
                        .Select(static p => p.Name)
                        .ToHashSet();
                    foreach (var name in properties.Keys) {
                        // By the declaration: a nullable object is a reference at this point and an enum is not, so
                        // the schema's own shape would call the first required and the second optional.
                        if (nullable.Contains(name)) {
                            schema.Required?.Remove(name);
                        } else {
                            (schema.Required ??= new HashSet<string>()).Add(name);
                        }
                    }

                    Defaults(properties, context.JsonTypeInfo);
                }

                return Task.CompletedTask;
            }
        );

    /// <summary>The starting value of each plain setting of a contract that can be made with nothing.</summary>
    static void Defaults(IDictionary<string, IOpenApiSchema> properties, JsonTypeInfo type) {
        if (type.CreateObject is not { } create || type.Type.Assembly != typeof(Contracts.BakeOptions).Assembly) {
            return;
        }

        var made = create();
        foreach (var property in type.Properties) {
            if (property.Get is { } get
                && Plain(property.PropertyType)
                && properties.TryGetValue(property.Name, out var found)
                && found is OpenApiSchema { Default: null } schema
                && get(made) is { } value) {
                schema.Default = JsonSerializer.SerializeToNode(value, property.PropertyType, type.Options);
            }
        }
    }

    static bool Plain(Type type) =>
        type == typeof(string)
        || type == typeof(bool)
        || type == typeof(int)
        || type == typeof(double)
        || type == typeof(float)
        || type == typeof(long);
}
