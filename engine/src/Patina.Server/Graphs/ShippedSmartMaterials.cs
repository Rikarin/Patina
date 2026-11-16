using Vixen.Texturing.Layers;

namespace Patina.Server.Graphs;

/// <summary>
///     The layered materials the shelf offers as shipped (#34): Patina's own, <c>content/smart-materials</c> under CC0,
///     and Vixen's, whose files are Vixen's. A name both ship is Patina's, so the CC0 one is what a project gets.
/// </summary>
static class ShippedSmartMaterials {
    const string Resource = "Smart/";

    /// <summary>Patina's own, in order.</summary>
    public static IReadOnlyList<string> Own { get; } = [
        ..typeof(ShippedSmartMaterials).Assembly.GetManifestResourceNames()
            .Where(static resource => resource.StartsWith(Resource, StringComparison.Ordinal))
            .Select(static resource => resource[Resource.Length..])
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>Every shipped one, Patina's first.</summary>
    public static IReadOnlyList<string> Names { get; } =
        [..Own, ..SmartMaterial.Shipped.Where(static name => !Own.Contains(name, StringComparer.Ordinal))];

    /// <summary>The text of a shipped layered material, or null when none has that name.</summary>
    /// <param name="name">Its name, as <see cref="Names" /> holds it.</param>
    public static string? Source(string name) {
        using var stream = typeof(ShippedSmartMaterials).Assembly.GetManifestResourceStream(Resource + name);
        if (stream is null) {
            return name.Length > 0 ? SmartMaterial.Source(name) : null;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
