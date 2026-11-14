using System.Text;
using System.Text.Json;

namespace Patina.Export.Presets;

/// <summary>
///     The comments of a hand-written preset, kept through an edit in the app (#155): the block at the top, the lines
///     above a top-level field, the lines above a texture, found again by its <c>file</c>, and the lines above a field
///     inside a texture, found again by the texture's <c>file</c> and the field's key. A comment after a value on its
///     line, or above a field the edit leaves out, has nothing to stay attached to and is lost.
/// </summary>
public sealed class PresetComments {
    readonly List<string> head = [];
    readonly Dictionary<string, List<string>> above = new(StringComparer.Ordinal);

    /// <summary>Whether the file had no comment that can be kept.</summary>
    public bool Empty => head.Count == 0 && above.Count == 0;

    /// <summary>The comments of a preset's text that can be kept.</summary>
    /// <param name="text">The preset file's text.</param>
    public static PresetComments Of(string text) {
        ArgumentNullException.ThrowIfNull(text);
        var comments = new PresetComments();
        List<string> pending = [];
        var first = true;
        string? texture = null;
        foreach (var line in Lines(text)) {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#')) {
                pending.Add(line);
                continue;
            }

            if (trimmed.Length == 0) {
                continue;
            }

            if (first) {
                comments.head.AddRange(pending);
                pending.Clear();
                first = false;
            }

            var anchor = Anchor(line, ref texture);
            if (pending.Count > 0 && anchor is not null) {
                comments.above[anchor] = [..pending];
            }

            pending.Clear();
        }

        return comments;
    }

    /// <summary>
    ///     A preset's text as <see cref="ExportPresetWriter" /> writes it, with these comments where they were: the top
    ///     block in place of the writer's own first line, and the rest above the field or texture they stood above.
    /// </summary>
    /// <param name="written">What the writer wrote, whose first line is its own comment.</param>
    public string Into(string written) {
        ArgumentNullException.ThrowIfNull(written);
        var text = new StringBuilder();
        var lines = Lines(written);
        var start = 0;
        if (head.Count > 0 && lines.Length > 0 && lines[0].StartsWith('#')) {
            foreach (var line in head) {
                text.Append(line).Append('\n');
            }

            start = 1;
        }

        string? texture = null;
        foreach (var line in lines.Skip(start)) {
            if (Anchor(line, ref texture) is { } anchor && above.GetValueOrDefault(anchor) is { } comments) {
                foreach (var comment in comments) {
                    text.Append(comment).Append('\n');
                }
            }

            text.Append(line).Append('\n');
        }

        return text.ToString();
    }

    static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    /// <summary>
    ///     What a line can hold a comment above, as a key: a top-level field by its name, a texture by its file, or a
    ///     field inside the texture being read by both. A top-level line ends the texture being read.
    /// </summary>
    static string? Anchor(string line, ref string? texture) {
        var at = line.IndexOf(':', StringComparison.Ordinal);
        if (line.Length > 0 && char.IsAsciiLetter(line[0]) && at > 0) {
            texture = null;
            return line[..at];
        }

        var trimmed = line.TrimStart();
        const string File = "- file:";
        if (trimmed.StartsWith(File, StringComparison.Ordinal)) {
            texture = Unquoted(trimmed[File.Length..].Trim());
            return $"- {texture}";
        }

        var inner = trimmed.IndexOf(':', StringComparison.Ordinal);
        return texture is not null && inner > 0 && char.IsAsciiLetter(trimmed[0])
            ? $"- {texture}\n{trimmed[..inner]}"
            : null;
    }

    /// <summary>A scalar as the file spells it, plain, in double quotes or in single quotes, as its text.</summary>
    static string Unquoted(string value) {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
            try {
                using var parsed = JsonDocument.Parse(value);
                return parsed.RootElement.GetString() ?? value;
            } catch (JsonException) {
                return value;
            }
        }

        return value.Length >= 2 && value[0] == '\'' && value[^1] == '\''
            ? value[1..^1].Replace("''", "'", StringComparison.Ordinal)
            : value;
    }
}
