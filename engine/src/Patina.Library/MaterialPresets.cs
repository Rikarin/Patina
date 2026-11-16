using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     A material preset's file (#124): a <c>.ptpreset</c>, YAML keyed as the API names a
///     <see cref="MaterialPreset" />. The file is the truth, as for every document; the project lists it so that it
///     has a name and an identity.
/// </summary>
public static class MaterialPresets {
    /// <summary>A preset's file extension.</summary>
    public const string Extension = ".ptpreset";

    /// <summary>A preset as text.</summary>
    public static string Text(MaterialPreset preset) =>
        YamlText.Write(YamlRecords.Write(preset, LibraryQueryJsonContext.Default.MaterialPreset));

    /// <summary>A preset read from text.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="path">Where it came from, for a problem to name.</param>
    /// <exception cref="LibraryFormatException">It is not a material preset.</exception>
    public static MaterialPreset Parse(string text, string path) {
        var fields = new YamlFields(YamlText.Parse(text, path), path);
        var preset = YamlRecords.Read(fields, LibraryQueryJsonContext.Default.MaterialPreset, new(string.Empty, []));
        return preset.Problem() is { } problem ? throw fields.Problem(problem) : preset;
    }

    /// <summary>Reads a preset's file.</summary>
    /// <exception cref="LibraryFormatException">It is not a material preset.</exception>
    public static MaterialPreset Read(string path) => Parse(SharedFile.ReadAllText(path), path);

    /// <summary>Whether a source the shelf gave names a material preset's file.</summary>
    public static bool Names(string source) => source.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Writes a preset's file whole, so a reader never sees half of one.</summary>
    public static void Write(string path, MaterialPreset preset) => LibraryFiles.Replace(path, Text(preset));
}
