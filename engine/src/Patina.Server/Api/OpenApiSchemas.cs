using Microsoft.AspNetCore.OpenApi;

namespace Patina.Server.Api;

/// <summary>
///     Makes the OpenAPI document say what the Server actually sends: every property that is not nullable is always
///     present, so it is required; nullable ones are left out when null (<c>WhenWritingNull</c>), so they are not.
///     Generated clients (editor/libs/api-client, clients/python) then get exact types instead of everything-optional
///     ones.
/// </summary>
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
                }

                return Task.CompletedTask;
            }
        );
}
