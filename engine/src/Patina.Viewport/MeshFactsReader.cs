using Patina.Contracts;

// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>Says what a mesh file is (#146): its parts, materials, coordinate sets, triangles, box and units.</summary>
public static class MeshFactsReader {
    /// <summary>Reads a mesh file for its facts.</summary>
    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public static MeshFacts Read(string path) {
        var read = ModelReader.Read(
            SharedFile.ReadAllBytes(path),
            Path.GetExtension(path),
            Path.GetFileNameWithoutExtension(path),
            new ModelReadOptions {
                GenerateTangents = false,
                ImportAnimations = false,
                ImportBlendShapes = false,
                ImportVertexColours = true,
                ImportAllTexCoords = true
            },
            sideFiles: SharedFile.Beside(path)
        );
        var names = MaterialNames.Unique(read.Model.Materials);
        var meshes = read.Meshes.Select(static (mesh, index) => (mesh, index))
            .ToDictionary(static pair => pair.mesh.Name, StringComparer.Ordinal);

        List<MeshPartFacts> parts = [];
        var vertices = 0;
        var colours = false;
        foreach (var part in read.Model.Parts) {
            var (mesh, index) = meshes[part.Mesh];
            var material = part.Material >= 0 && part.Material < names.Length ? names[part.Material] : string.Empty;
            parts.Add(new(part.Mesh, material, mesh.TriangleCount, Sets(read, index, mesh.TexCoords.Length)));
            vertices += mesh.VertexCount;
            colours |= mesh.Colours.Length > 0;
        }

        MeshMaterialFacts[] materials = [
            ..parts.GroupBy(static part => part.Material, StringComparer.Ordinal)
                .Select(static group => new MeshMaterialFacts(
                        group.Key,
                        group.Sum(static part => part.Triangles),
                        group.Count(),
                        group.Min(static part => part.UvSets)
                    )
                )
        ];
        var box = read.Model.Bounds;
        var extension = Path.GetExtension(path);
        return new(
            Path.GetFileName(path),
            MeshFormats.Name(extension),
            parts.Sum(static part => part.Triangles),
            vertices,
            parts,
            materials,
            parts.Count == 0 ? 0 : parts.Max(static part => part.UvSets),
            parts.Count == 0 ? [0, 0, 0] : [box.Minimum.X, box.Minimum.Y, box.Minimum.Z],
            parts.Count == 0 ? [0, 0, 0] : [box.Maximum.X, box.Maximum.Y, box.Maximum.Z],
            MetresPerUnit(extension, read.UnitScaleFactor),
            colours
        );
    }

    /// <summary>
    ///     How many sets a mesh has before the first it lacks: a set past a gap is unreachable by number, so it is not
    ///     counted, and a mesh read without them has its first set or none.
    /// </summary>
    static int Sets(ReadModel read, int mesh, int first) {
        if (mesh >= read.TexCoordSets.Length) {
            return first > 0 ? 1 : 0;
        }

        var sets = read.TexCoordSets[mesh];
        var count = 0;
        while (count < sets.Length && sets[count].Length > 0) {
            count++;
        }

        return count;
    }

    /// <summary>
    ///     glTF is in metres by its specification. An FBX states centimetres per unit, which Assimp reports as its
    ///     <c>UnitScaleFactor</c>. Nothing else is trusted to mean a length.
    /// </summary>
    static double? MetresPerUnit(string extension, double? stated) =>
        extension.ToLowerInvariant() switch {
            ".glb" or ".gltf" => 1,
            ".fbx" when stated is > 0 => stated.Value / 100,
            _ => null
        };
}
