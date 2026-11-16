namespace Patina.Contracts;

/// <summary>
///     A material preset (#124): a material from the shelf with its parameters set. It is a document of a project, a
///     <c>.ptpreset</c> file, and the file is the truth; the shelf lists it among the materials, and using it fills a
///     layer with the material at these values.
/// </summary>
/// <param name="Material">The material as the shelf names it: a graph's path in the library or a shipped one's.</param>
/// <param name="Values">What it sets the material's parameters to; a parameter left out keeps its default.</param>
public sealed record MaterialPreset(string Material, IReadOnlyList<MaterialPresetValue> Values) {
    /// <summary>What is wrong with it, or null when it can be kept.</summary>
    public string? Problem() =>
        Material.Trim().Length == 0
            ? "A material preset names the material it sets."
            : Values.Any(static value => value.Name.Trim().Length == 0)
                ? "A material preset's value names its parameter."
                : Values.DistinctBy(static value => value.Name, StringComparer.Ordinal).Count() != Values.Count
                    ? "A material preset sets each parameter once."
                    : null;
}

/// <summary>One parameter of a material preset.</summary>
/// <param name="Name">The parameter, by name.</param>
/// <param name="Value">Its value, as text, as a graph fill's <c>parameters</c> hold it.</param>
public sealed record MaterialPresetValue(string Name, string Value);

/// <summary>
///     The body of <c>POST /api/v1/library/materials/presets</c>, which keeps a graph fill's material and parameters
///     as a new material preset of the project.
/// </summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Name">What to call it, which names its file.</param>
/// <param name="Graph">
///     The material, as a graph fill in the project reads it (its <c>graph</c>), which the Server takes back to where
///     the shelf has it.
/// </param>
/// <param name="Parameters">What the fill sets the material's parameters to (its <c>parameters</c>).</param>
public sealed record MaterialPresetCreateRequest(
    string Project,
    string Name,
    string Graph,
    IReadOnlyDictionary<string, string> Parameters
);
