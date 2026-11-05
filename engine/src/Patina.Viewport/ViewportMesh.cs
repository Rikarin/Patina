// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>One range of indices and the material that shades it.</summary>
/// <param name="FirstIndex">Where it starts in <see cref="ViewportMesh.Indices" />.</param>
/// <param name="IndexCount">How many indices.</param>
/// <param name="Material">Which material in <see cref="ViewportMesh.Materials" />.</param>
readonly record struct ViewportPart(int FirstIndex, int IndexCount, int Material);

/// <summary>What a material in the file asks for.</summary>
/// <param name="Factors">Its numbers.</param>
/// <param name="Textures">Its images.</param>
readonly record struct ViewportMaterial(ModelMaterialFactors Factors, ModelMaterialTextures Textures);

/// <summary>
///     A model flattened for drawing: every part in world space in one vertex array of position, normal, texture
///     coordinate and tangent, so a frame binds one buffer. Rebuilt from the file whenever a Worker starts.
/// </summary>
/// <remarks>
///     ⚠ <b>Tangents are one per triangle corner and a vertex is what is left after a weld</b>, so
///     <see cref="WithTangents" /> rebuilds the indices. The cost is why it is not always done: it reads the whole
///     mesh, and only a normal map needs the answer. Positions and normals are made as
///     <c>ModelGeometry.Flatten</c> makes them, so the baker and this run <see cref="MikkTSpace" /> over the same
///     numbers.
/// </remarks>
sealed class ViewportMesh {
    /// <summary>Position 3, normal 3, texture coordinate 2, tangent 4.</summary>
    public const int FloatsPerVertex = 12;

    const int NormalOffset = 3;
    const int CoordinateOffset = 6;
    const int TangentOffset = 8;

    ViewportMesh(
        float[] vertices,
        uint[] indices,
        ViewportPart[] parts,
        ViewportMaterial[] materials,
        BoundingBox bounds,
        bool hasCoordinates,
        bool hasTangents
    ) {
        Vertices = vertices;
        Indices = indices;
        Parts = parts;
        Materials = materials;
        Bounds = bounds;
        HasCoordinates = hasCoordinates;
        HasTangents = hasTangents;
    }

    public float[] Vertices { get; }

    public uint[] Indices { get; }

    public ViewportPart[] Parts { get; }

    public ViewportMaterial[] Materials { get; }

    public BoundingBox Bounds { get; }

    /// <summary>Whether a texture has anywhere to go.</summary>
    public bool HasCoordinates { get; }

    /// <summary>Whether a normal map can be read.</summary>
    public bool HasTangents { get; }

    /// <summary>Whether a material in the file brings a normal map of its own.</summary>
    public bool WantsTangents => Materials.Any(static material => material.Textures.Normal is not null);

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
        var hasCoordinates = false;

        foreach (var part in read.Model.Parts) {
            var mesh = meshes[part.Mesh];
            var first = indices.Count;
            var offset = (uint)(vertices.Count / FloatsPerVertex);
            bounds = Append(mesh, world[part.Node], vertices, bounds);
            hasCoordinates |= mesh.TexCoords.Length == mesh.Positions.Length;
            foreach (var index in mesh.Indices) {
                indices.Add(offset + (uint)index);
            }

            parts.Add(new(first, mesh.Indices.Length, part.Material));
        }

        return new([..vertices], [..indices], [..parts], ReadMaterials(read), bounds, hasCoordinates, false);
    }

    /// <summary>This mesh with a tangent at every vertex, split wherever two corners do not agree on one.</summary>
    public ViewportMesh WithTangents() {
        if (HasTangents || !HasCoordinates) {
            return this;
        }

        var corners = Indices.Length;
        var positions = new Vector3[corners];
        var normals = new Vector3[corners];
        var coordinates = new Vector2[corners];

        for (var corner = 0; corner < corners; corner++) {
            var at = (int)Indices[corner] * FloatsPerVertex;
            positions[corner] = new(Vertices[at], Vertices[at + 1], Vertices[at + 2]);
            normals[corner] = new(
                Vertices[at + NormalOffset],
                Vertices[at + NormalOffset + 1],
                Vertices[at + NormalOffset + 2]
            );
            coordinates[corner] = new(Vertices[at + CoordinateOffset], Vertices[at + CoordinateOffset + 1]);
        }

        var tangents = MikkTSpace.Generate(positions, normals, coordinates);
        var welded = new Dictionary<(uint Vertex, Vector4 Tangent), uint>();
        var vertices = new List<float>(Vertices.Length);
        var indices = new uint[corners];

        for (var corner = 0; corner < corners; corner++) {
            var key = (Indices[corner], tangents[corner]);
            if (!welded.TryGetValue(key, out var index)) {
                index = (uint)(vertices.Count / FloatsPerVertex);
                welded.Add(key, index);
                vertices.AddRange(Vertices.AsSpan((int)Indices[corner] * FloatsPerVertex, TangentOffset));
                vertices.AddRange([tangents[corner].X, tangents[corner].Y, tangents[corner].Z, tangents[corner].W]);
            }

            indices[corner] = index;
        }

        return new([..vertices], indices, Parts, Materials, Bounds, true, true);
    }

    static ViewportMaterial[] ReadMaterials(ReadModel read) {
        var materials = new ViewportMaterial[Math.Max(read.MaterialFactors.Length, 1)];
        for (var at = 0; at < materials.Length; at++) {
            materials[at] = at < read.MaterialFactors.Length
                ? new(read.MaterialFactors[at], read.MaterialTextures[at])
                : new(ModelMaterialFactors.Default, new());
        }

        return materials;
    }

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
        var hasCoordinates = mesh.TexCoords.Length == mesh.Positions.Length;
        for (var i = 0; i < mesh.Positions.Length; i++) {
            var position = Matrix4x4.TransformPosition(mesh.Positions[i], world);
            var normal = mesh.Normals.Length > i
                ? Vector3.Normalize(Matrix4x4.TransformDirection(mesh.Normals[i], normals))
                : Vector3.UnitY;
            var coordinate = hasCoordinates ? mesh.TexCoords[i] : default;
            vertices.AddRange(
                [
                    position.X, position.Y, position.Z, normal.X, normal.Y, normal.Z, coordinate.X, coordinate.Y, 0f,
                    0f,
                    0f, 0f
                ]
            );
            bounds = BoundingBox.Merge(bounds, position);
        }

        return bounds;
    }
}
