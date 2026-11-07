using Patina.Contracts;
using Vixen.Geometry;
using Vixen.Geometry.Uv;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>Gives a mesh a UV layout of its own, in a new file beside it.</summary>
/// <remarks>
///     ⚠ <b>Only the coordinates change.</b> Every corner keeps its normal and vertex colour, and every part its name and
///     material, because a smooth mesh written with a normal per face renders faceted, a lost material is a lost texture
///     set, a lost colour is a lost id bake, and a lost name is a lost match by name and mesh mask.
/// </remarks>
public static class MeshUnwrapRun {
    /// <summary>
    ///     Unwraps each material of a mesh that has no layout into an atlas of its own. A material whose parts all have
    ///     coordinates keeps them. The mesh it came from is not touched.
    /// </summary>
    /// <param name="source">The mesh file.</param>
    /// <param name="output">Where the unwrapped mesh goes, ending in <c>.glb</c> or <c>.gltf</c>.</param>
    /// <param name="options">How to lay it out, or null for the defaults.</param>
    /// <returns>What each unwrapped set came to, or null when every material had a layout and nothing was written.</returns>
    /// <exception cref="ModelFormatException">It is not a readable model.</exception>
    /// <exception cref="InvalidOperationException">Its islands could not be fitted.</exception>
    /// <exception cref="ArgumentException">The options cannot be used.</exception>
    public static IReadOnlyList<UnwrapSet>? Run(string source, string output, UnwrapOptions? options = null) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        options ??= new();
        if (options.Problem() is { } problem) {
            throw new ArgumentException(problem, nameof(options));
        }

        var read = MeshMapBakeRun.Model(source);
        var meshes = read.Meshes.ToDictionary(static mesh => mesh.Name, StringComparer.Ordinal);
        var names = MaterialNames.Unique(read.Model.Materials);
        int[] used = [..read.Model.Parts.Select(static part => part.Material).Distinct().Order()];

        bool Bare(int material) =>
            read.Model.Parts.Where(part => part.Material == material)
                .Any(part => meshes[part.Mesh] is var mesh && mesh.TexCoords.Length != mesh.Positions.Length);

        if (!used.Any(Bare)) {
            return null;
        }

        List<MeshData> written = [];
        List<UnwrapSet> sets = [];
        foreach (var material in used) {
            var flat = ModelGeometry.Flatten(
                read with {
                    Model = read.Model with { Parts = [..read.Model.Parts.Where(part => part.Material == material)] }
                }
            );
            if (!Bare(material)) {
                written.AddRange(ModelGeometry.ToParts(flat, flat.Mesh.TexCoords.ToArray()));
                continue;
            }

            // ⚠ Groups that are vertex colours are not material boundaries, and cutting along each would fragment the atlas.
            var report = UvUnwrap.All(
                flat.Mesh,
                new() { KeepGroups = !flat.ColouredGroups },
                Packing(options),
                out var coordinates
            );
            written.AddRange(ModelGeometry.ToParts(flat, coordinates));
            sets.Add(Summary(material < names.Length ? names[material] : string.Empty, report));
        }

        Write(output, written, read);
        return sets;
    }

    static PackSettings Packing(UnwrapOptions options) =>
        new() {
            Resolution = options.Resolution,
            Margin = options.Margin,
            Quality = options.Quality switch {
                "boxes" => PackQuality.Rectangle,
                "groups" => PackQuality.SuperPatch,
                _ => PackQuality.Irregular
            },
            Rotations = options.Rotations,
            TexelDensity = (float)options.TexelDensity,
            Overflow = options.Overflow == "refuse" ? PackOverflow.Refuse : PackOverflow.Scale
        };

    static UnwrapSet Summary(string material, UvReport report) =>
        new(
            material,
            report.ChartCount,
            report.Distortion.Flipped,
            Math.Round(report.Distortion.StretchL2, 3),
            Math.Round(report.Distortion.StretchLInf, 3),
            Math.Round(report.EffectiveEfficiency, 3),
            report.Warnings
        );

    static void Write(string output, List<MeshData> meshes, ReadModel read) {
        ModelWriterMaterial[] materials = [
            ..read.Model.Materials.Select((name, at) => new ModelWriterMaterial(
                    name,
                    at < read.MaterialFactors.Length ? read.MaterialFactors[at] : ModelMaterialFactors.Default
                )
            )
        ];

        // ⚠ The extension says what to write, and half a file must never look like a mesh a project could list.
        var name = Path.GetFileNameWithoutExtension(output);
        var part = Path.Combine(
            Path.GetDirectoryName(output) ?? string.Empty,
            $"{name}.{Guid.NewGuid():N}{Path.GetExtension(output)}"
        );
        try {
            ModelWriter.Write(part, meshes, name, materials);
            File.Move(part, output, true);
        } finally {
            File.Delete(part);
        }
    }
}
