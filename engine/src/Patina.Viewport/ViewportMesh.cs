// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>One range of indices and the material that shades it.</summary>
/// <param name="FirstIndex">Where it starts in <see cref="ViewportMesh.Indices" />.</param>
/// <param name="IndexCount">How many indices.</param>
/// <param name="Material">The factors, from the file.</param>
readonly record struct ViewportPart(int FirstIndex, int IndexCount, ModelMaterialFactors Material);

/// <summary>
///     A model flattened for drawing: every part in world space in one vertex array of position then normal, so a frame
///     binds one buffer. Rebuilt from the file whenever a Worker starts.
/// </summary>
sealed class ViewportMesh {
    /// <summary>Position and normal, three floats each.</summary>
    public const int FloatsPerVertex = 6;

    ViewportMesh(float[] vertices, uint[] indices, ViewportPart[] parts, BoundingBox bounds) {
        Vertices = vertices;
        Indices = indices;
        Parts = parts;
        Bounds = bounds;
    }

    public float[] Vertices { get; }

    public uint[] Indices { get; }

    public ViewportPart[] Parts { get; }

    public BoundingBox Bounds { get; }

    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public static ViewportMesh Read(string path) {
        var options = new ModelReadOptions {
            GenerateTangents = false, ImportAnimations = false, ImportBlendShapes = false
        };
        var read = ModelReader.Read(
            File.ReadAllBytes(path),
            Path.GetExtension(path),
            Path.GetFileNameWithoutExtension(path),
            options
        );
        var world = WorldTransforms(read.Model.Nodes);
        var meshes = read.Meshes.ToDictionary(static mesh => mesh.Name, StringComparer.Ordinal);
        var vertices = new List<float>();
        var indices = new List<uint>();
        var parts = new List<ViewportPart>();
        var bounds = BoundingBox.Empty;

        foreach (var part in read.Model.Parts) {
            var mesh = meshes[part.Mesh];
            var first = indices.Count;
            var offset = (uint)(vertices.Count / FloatsPerVertex);
            bounds = Append(mesh, world[part.Node], vertices, bounds);
            indices.AddRange(mesh.Indices.Select(index => offset + (uint)index));
            parts.Add(new(first, mesh.Indices.Length, Factors(read, part.Material)));
        }

        return new([..vertices], [..indices], [..parts], bounds);
    }

    static ModelMaterialFactors Factors(ReadModel read, int material) =>
        material >= 0 && material < read.MaterialFactors.Length
            ? read.MaterialFactors[material]
            : ModelMaterialFactors.Default;

    static Matrix4x4[] WorldTransforms(ModelNode[] nodes) {
        var world = new Matrix4x4[nodes.Length];
        for (var i = 0; i < nodes.Length; i++) {
            var parent = nodes[i].Parent;
            world[i] = parent < 0 ? nodes[i].Transform : nodes[i].Transform * world[parent];
        }

        return world;
    }

    static BoundingBox Append(MeshData mesh, Matrix4x4 world, List<float> vertices, BoundingBox bounds) {
        var normals = Matrix4x4.Invert(world, out var inverse) ? Matrix4x4.Transpose(inverse) : world;
        for (var i = 0; i < mesh.Positions.Length; i++) {
            var position = Matrix4x4.TransformPosition(mesh.Positions[i], world);
            var normal = mesh.Normals.Length > i
                ? Vector3.Normalize(Matrix4x4.TransformDirection(mesh.Normals[i], normals))
                : Vector3.UnitY;
            vertices.AddRange([position.X, position.Y, position.Z, normal.X, normal.Y, normal.Z]);
            bounds = BoundingBox.Merge(bounds, position);
        }

        return bounds;
    }
}
