using System.Text;

namespace Patina.Export.Presets;

/// <summary>What a file's name can be built from.</summary>
/// <param name="Project">The project's name.</param>
/// <param name="Preset">The preset's id.</param>
public sealed record NamingValues(string Project, string Preset);

/// <summary>
///     Turns <c>T_{project}_D</c> into a relative path. Forward slashes make folders on every platform, and a value can
///     neither make a folder nor climb out of where exports go, because it arrives from a project's name.
/// </summary>
public static class NamingTemplate {
    static readonly HashSet<string> Known = new(["project", "preset"], StringComparer.Ordinal);

    /// <summary>Refuses a template that could not make a file name.</summary>
    /// <exception cref="ExportPresetException">It names something unknown or leaves the folder.</exception>
    public static void Check(string template, string where) {
        ArgumentNullException.ThrowIfNull(template);
        foreach (var (name, _) in Holes(template, where)) {
            if (!Known.Contains(name)) {
                throw new ExportPresetException(
                    $"{where}: '{{{name}}}' is not a name. The names are {string.Join(", ", Known.Order())}."
                );
            }
        }

        if (template.Contains('\\', StringComparison.Ordinal)
            || template.StartsWith('/')
            || template.Split('/').Any(static part => part is "" or "." or "..")) {
            throw new ExportPresetException($"{where}: '{template}' must stay inside the export folder.");
        }
    }

    /// <summary>The path a template gives, relative to the export folder, in this platform's separators.</summary>
    public static string Resolve(string template, NamingValues values, string extension) {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        var text = new StringBuilder();
        var from = 0;
        foreach (var (name, at) in Holes(template, template)) {
            text.Append(template, from, at - from);
            text.Append(Clean(name == "project" ? values.Project : values.Preset));
            from = at + name.Length + 2;
        }

        text.Append(template, from, template.Length - from).Append(extension);
        return text.ToString().Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>A value as part of one file's name. Spaces go too, since engines' importers often refuse them.</summary>
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
