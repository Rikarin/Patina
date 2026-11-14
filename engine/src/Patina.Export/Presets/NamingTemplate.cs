using System.Globalization;
using System.Text;

namespace Patina.Export.Presets;

/// <summary>What a file's name can be built from.</summary>
/// <param name="Project">The project's name.</param>
/// <param name="Preset">The preset's id.</param>
/// <param name="Set">
///     The texture set, or nothing for a project with none named. <c>{set}</c> is then the project.
/// </param>
public sealed record NamingValues(string Project, string Preset, string Set = "") {
    /// <summary>The name of the mesh the set is painted on, without its extension, or nothing for none.</summary>
    public string Mesh { get; init; } = string.Empty;

    /// <summary>The material of the mesh the set paints, or nothing when the set names none.</summary>
    public string Material { get; init; } = string.Empty;

    /// <summary>The file's numbers: srgb or linear.</summary>
    public string Space { get; init; } = string.Empty;

    /// <summary>The file's longest side in texels, or zero when it is not known.</summary>
    public int Size { get; init; }

    /// <summary>
    ///     The UDIM tile the set's layout sits on (#20), which <c>{udim}</c> and <c>{tile}</c> name: 1001 until a
    ///     layout says otherwise.
    /// </summary>
    public int Udim { get; init; } = OneTile.First;

    /// <summary>Each texture set's mesh and material, by the set's name, for a project that knows them.</summary>
    public IReadOnlyDictionary<string, SetNaming> Sets { get; init; } =
        new Dictionary<string, SetNaming>(StringComparer.Ordinal);

    /// <summary>
    ///     These values for one texture set: its name, and its mesh, material and tile when they are known.
    /// </summary>
    public NamingValues Of(string set) =>
        Sets.TryGetValue(set, out var known)
            ? this with { Set = set, Mesh = known.Mesh, Material = known.Material, Udim = known.Udim }
            : this with { Set = set };
}

/// <summary>What a texture set's files can be named by beyond the set's own name.</summary>
/// <param name="Mesh">The mesh it paints, without its extension.</param>
/// <param name="Material">The material of the mesh it paints, or nothing.</param>
public sealed record SetNaming(string Mesh, string Material) {
    /// <summary>The UDIM tile its layout sits on (#281): 1001 until its layout says otherwise.</summary>
    public int Udim { get; init; } = OneTile.First;
}

/// <summary>
///     Turns <c>T_{project}_D</c> into a relative path. Forward slashes make folders on every platform, and a value can
///     neither make a folder nor climb out of where exports go, because it arrives from a project's name.
/// </summary>
public static class NamingTemplate {
    const string ProjectHole = "{project}";
    const string SetHole = "{set}";

    /// <summary>Every name a template can hold, in the order the ADR lists them.</summary>
    public static IReadOnlyList<string> Names { get; } =
        ["project", "preset", "set", "mesh", "material", "udim", "tile", "space", "size"];

    /// <summary>Refuses a template that could not make a file name.</summary>
    /// <exception cref="ExportPresetException">It names something unknown or leaves the folder.</exception>
    public static void Check(string template, string where) {
        ArgumentNullException.ThrowIfNull(template);
        foreach (var (name, _) in Holes(template, where)) {
            if (!Names.Contains(name, StringComparer.Ordinal)) {
                throw new ExportPresetException(
                    $"{where}: '{{{name}}}' is not a name. The names are {string.Join(", ", Names)}."
                );
            }
        }

        if (template.Contains('\\', StringComparison.Ordinal)
            || template.StartsWith('/')
            || template.Split('/').Any(static part => part is "" or "." or "..")) {
            throw new ExportPresetException($"{where}: '{template}' must stay inside the export folder.");
        }
    }

    /// <summary>
    ///     The template of a file in a project of several texture sets, which has to say which set it is. A template
    ///     that does not gets the set's name after the project's, which is where a person would have written it.
    /// </summary>
    public static string PerSet(string template) {
        ArgumentNullException.ThrowIfNull(template);
        return template.Contains(SetHole, StringComparison.Ordinal)
            ? template
            : template.Contains(ProjectHole, StringComparison.Ordinal)
                ? template.Replace(ProjectHole, $"{ProjectHole}_{SetHole}", StringComparison.Ordinal)
                : $"{template}_{SetHole}";
    }

    /// <summary>The path a template gives, relative to the export folder, in this platform's separators.</summary>
    public static string Resolve(string template, NamingValues values, string extension) {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        var text = new StringBuilder();
        var from = 0;
        foreach (var (name, at) in Holes(template, template)) {
            text.Append(template, from, at - from);
            text.Append(Clean(ValueOf(name, values)));
            from = at + name.Length + 2;
        }

        text.Append(template, from, template.Length - from).Append(extension);
        return text.ToString().Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>What a name stands for. A value a project does not have falls back to the one that holds it.</summary>
    static string ValueOf(string name, NamingValues values) {
        var set = values.Set.Length > 0 ? values.Set : values.Project;
        return name switch {
            "project" => values.Project,
            "set" => set,
            "mesh" => values.Mesh.Length > 0 ? values.Mesh : values.Project,
            "material" => values.Material.Length > 0 ? values.Material : set,
            "udim" => values.Udim.ToString(CultureInfo.InvariantCulture),
            "tile" => OneTile.MariName(values.Udim),
            "space" => values.Space,
            "size" => values.Size > 0 ? values.Size.ToString(CultureInfo.InvariantCulture) : string.Empty,
            _ => values.Preset
        };
    }

    /// <summary>
    ///     A value as part of one file's name. Spaces go too, since engines' importers often refuse
    ///     them.
    /// </summary>
    public static string Clean(string value) {
        ArgumentNullException.ThrowIfNull(value);
        var cleaned = new StringBuilder(value.Length);
        foreach (var c in value) {
            cleaned.Append(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_');
        }

        return cleaned.Length == 0 ? "_" : cleaned.ToString();
    }

    static List<(string Name, int At)> Holes(string template, string where) {
        List<(string, int)> holes = [];
        for (var at = 0; at < template.Length; at++) {
            if (template[at] == '}') {
                throw new ExportPresetException($"{where}: '{template}' has a stray closing brace.");
            }

            if (template[at] != '{') {
                continue;
            }

            var end = template.IndexOf('}', at);
            if (end < 0) {
                throw new ExportPresetException($"{where}: '{template}' has an open brace with no end.");
            }

            holes.Add((template[(at + 1)..end], at));
            at = end;
        }

        return holes;
    }
}
