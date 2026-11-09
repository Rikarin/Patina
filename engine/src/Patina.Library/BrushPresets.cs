using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     A brush or tool preset's file: a <c>.ptbrush</c>, YAML keyed as the API names a <see cref="BrushPreset" />. The
///     file is the truth, as for every document; the project lists it so that it has a name and an identity.
/// </summary>
public static class BrushPresets {
    /// <summary>A preset's file extension.</summary>
    public const string Extension = ".ptbrush";

    /// <summary>A preset as text.</summary>
    public static string Text(BrushPreset preset) =>
        YamlText.Write(YamlRecords.Write(preset, LibraryTreeJsonContext.Default.BrushPreset));

    /// <summary>A preset read from text.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="path">Where it came from, for a problem to name.</param>
    /// <exception cref="LibraryFormatException">It is not a preset, or holds one no brush can be.</exception>
    public static BrushPreset Parse(string text, string path) {
        var fields = new YamlFields(YamlText.Parse(text, path), path);
        var preset = YamlRecords.Read(
            fields,
            LibraryTreeJsonContext.Default.BrushPreset,
            new("brush", StackBrushes.Default)
        );
        return preset.Problem() is { } problem ? throw fields.Problem(problem) : preset;
    }

    /// <summary>Reads a preset's file.</summary>
    /// <exception cref="LibraryFormatException">It is not a preset.</exception>
    public static BrushPreset Read(string path) => Parse(SharedFile.ReadAllText(path), path);

    /// <summary>Writes a preset's file whole, so a reader never sees half of one.</summary>
    public static void Write(string path, BrushPreset preset) => LibraryFiles.Replace(path, Text(preset));
}
