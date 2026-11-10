using Patina.Contracts;
using Patina.Export.Presets;
using System.Text;

namespace Patina.Export.Materials;

/// <summary>
///     The material files a preset writes beside a texture set's files (<see cref="ExportMaterials" />): a glTF
///     material through Vixen's writer, a USD preview surface and a MaterialX standard surface. Each names the files by
///     the slots <see cref="MaterialSlots" /> finds, and says what it had to leave out rather than wiring a lane to the
///     wrong input.
/// </summary>
public static class MaterialFiles {
    /// <summary>What was written and what a person should know.</summary>
    /// <param name="Files">The material files, absolute.</param>
    /// <param name="Warnings">A slot left out, and why.</param>
    public sealed record Written(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

    /// <summary>
    ///     The formats USD's and MaterialX's own image readers open without OpenImageIO: PNG, JPEG, TGA, Radiance HDR
    ///     and OpenEXR.
    /// </summary>
    static readonly HashSet<string> SceneFormats = new(
        [
            ExportFormats.Png8, ExportFormats.Png16, ExportFormats.Jpeg, ExportFormats.Tga, ExportFormats.Hdr,
            ExportFormats.Exr16, ExportFormats.Exr32
        ],
        StringComparer.Ordinal
    );

    /// <summary>Whether USD's and MaterialX's own image readers open a format.</summary>
    internal static bool SceneReads(string format) => SceneFormats.Contains(format);

    /// <summary>Writes a set's material files.</summary>
    /// <param name="preset">The preset, which names the kinds.</param>
    /// <param name="files">The set's files and where each was delivered.</param>
    /// <param name="folder">Where the material files go.</param>
    /// <param name="stem">Their name without an extension, which is also the material's.</param>
    /// <param name="material">The material's name, or nothing for <paramref name="stem" />.</param>
    public static Written Write(
        ExportPreset preset,
        IReadOnlyList<(ExportedFile File, string Path)> files,
        string folder,
        string stem,
        string material
    ) {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(material);
        var slots = MaterialSlots.Find(preset, files, folder);
        var name = material.Length > 0 ? material : stem;
        List<string> written = [];
        List<string> warnings = [];
        foreach (var kind in preset.Materials) {
            var path = Path.Combine(folder, $"{stem}.{kind}");
            var text = kind switch {
                ExportMaterials.Gltf => GltfMaterialFile.Write(slots, name, warnings),
                ExportMaterials.Usd => new UsdMaterialFile(name, warnings).Write(slots),
                _ => new MaterialXFile(name, warnings).Write(slots)
            };
            File.WriteAllText(path, text, new UTF8Encoding(false));
            written.Add(path);
        }

        return new(written, [..warnings.Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>A name as a USD prim or MaterialX element may be called: letters, digits and underscores.</summary>
    internal static string Identifier(string name) {
        var cleaned = NamingTemplate.Clean(name).Replace('-', '_');
        return char.IsAsciiLetter(cleaned[0]) || cleaned[0] == '_' ? cleaned : $"_{cleaned}";
    }
}
