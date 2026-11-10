using Patina.Contracts;

// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Graphics;
using Vixen.Interchange;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>One range of indices and the material that shades it.</summary>
/// <param name="FirstIndex">Where it starts in <see cref="ViewportMesh.Indices" />.</param>
/// <param name="IndexCount">How many indices.</param>
/// <param name="Material">Which material in <see cref="ViewportMesh.Materials" />.</param>
/// <param name="Name">The name of the mesh in the file.</param>
readonly record struct ViewportPart(int FirstIndex, int IndexCount, int Material, string Name);

/// <summary>What a material in the file asks for.</summary>
/// <param name="Factors">Its numbers.</param>
/// <param name="Textures">Its images.</param>
/// <param name="Name">What it is called, unique in the file.</param>
readonly record struct ViewportMaterial(ModelMaterialFactors Factors, ModelMaterialTextures Textures, string Name);

/// <summary>
///     A model's triangles in the space the viewport draws it, so that a ray from its camera and a stroke's stamps mean
///     the same thing as the picture.
/// </summary>
/// <param name="Positions">The corners.</param>
/// <param name="Coordinates">Their texture coordinates.</param>
/// <param name="Indices">Three corners per triangle.</param>
/// <param name="Bounds">The box round it, which the orbit camera frames.</param>
/// <param name="HasCoordinates">Whether it has a layout at all.</param>
public sealed record ViewportGeometry(
    Vector3[] Positions,
    Vector2[] Coordinates,
    int[] Indices,
    BoundingBox Bounds,
    bool HasCoordinates
) {
    /// <summary>Reads a model file.</summary>
    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public static ViewportGeometry Read(string path) => ViewportMesh.Read(path).Geometry();

    /// <summary>One material's triangles, laid out in one of the file's coordinate sets (#210).</summary>
    /// <exception cref="ModelFormatException">The file is not a readable model, or a part has no such set.</exception>
    public static ViewportGeometry Read(string path, string material, int uvSet = 0) =>
        ViewportMesh.Read(path, uvSet == 0 ? null : new Dictionary<string, int> { [material] = uvSet })
            .Geometry(material);

    /// <summary>The named meshes in a file, in one texture set if it is one of the file's materials.</summary>
    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public static IReadOnlyList<string> Parts(string path, string set) => ViewportMesh.Read(path).PartsOf(set);

    /// <summary>The layout of these named meshes, three coordinates to a triangle.</summary>
    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public static IReadOnlyList<Vector2> Layout(string path, IReadOnlyCollection<string> parts) =>
        ViewportMesh.Read(path).LayoutOf(parts);

    /// <summary>The materials the file's triangles use, which is its texture sets.</summary>
    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public static IReadOnlyList<string> Materials(string path) => ViewportMesh.Read(path).MaterialsInUse();

    /// <summary>
    ///     The triangles an isolation draws (#136), with the model's own number for each, so a pick through them names
    ///     the triangle it would have named on the whole model. Both lists empty is the whole model.
    /// </summary>
    /// <exception cref="ModelFormatException">The file is not a readable model.</exception>
    public static (ViewportGeometry Geometry, int[] Triangles) Shown(string path, string[] parts, string[] materials) =>
        ViewportMesh.Read(path).ShownGeometry(parts, materials);
}

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

    /// <summary>
    ///     How a shader reads these vertices. Raven numbers a shader's inputs, so the attributes are found by name, and
    ///     a shader that reads only some of them, as the shadow and the wire passes do, gets only those.
    /// </summary>
    public static VertexBufferLayout Layout(Effect effect) {
        (string Name, VertexFormat Format, int Offset)[] attributes = [
            ("position", VertexFormat.Float32X3, 0),
            ("normal", VertexFormat.Float32X3, 3 * sizeof(float)),
            ("texCoord", VertexFormat.Float32X2, 6 * sizeof(float)),
            ("tangent", VertexFormat.Float32X4, 8 * sizeof(float))
        ];
        List<VertexElement> elements = [];
        foreach (var (name, format, offset) in attributes) {
            foreach (var input in effect.VertexInputs) {
                if (input.Name.EndsWith(name, StringComparison.Ordinal)) {
                    elements.Add(new((uint)input.Location, format, offset));
                    break;
                }
            }
        }

        return new(FloatsPerVertex * sizeof(float), [..elements]);
    }

    /// <summary>
    ///     Each edge of the triangles once, as pairs of corners, for a wireframe. A mesh with none gives one empty
    ///     line, so there is always something to bind.
    /// </summary>
    public uint[] Edges() {
        HashSet<ulong> seen = [];
        List<uint> edges = [];
        for (var corner = 0; corner + 2 < Indices.Length; corner += 3) {
            Add(Indices[corner], Indices[corner + 1]);
            Add(Indices[corner + 1], Indices[corner + 2]);
            Add(Indices[corner + 2], Indices[corner]);
        }

        return edges.Count == 0 ? [0u, 0u] : [..edges];

        void Add(uint from, uint to) {
            var (low, high) = from < to ? (from, to) : (to, from);
            if (seen.Add(((ulong)low << 32) | high)) {
                edges.Add(low);
                edges.Add(high);
            }
        }
    }

    /// <summary>Reads a model, each material laid out in the coordinate set its texture set chose (#210).</summary>
    /// <param name="path">The file.</param>
    /// <param name="uvSets">The coordinate set of each material, by name. One it does not name has the first.</param>
    /// <exception cref="ModelFormatException">
    ///     The file is not a readable model, or a part has no coordinate set of the number its material asks for.
    /// </exception>
    public static ViewportMesh Read(string path, IReadOnlyDictionary<string, int>? uvSets = null) {
        var other = uvSets?.Values.Any(static set => set != 0) ?? false;
        var options = new ModelReadOptions {
            GenerateTangents = false, ImportAnimations = false, ImportBlendShapes = false, ImportAllTexCoords = other
        };
        var read = ModelReader.Read(
            SharedFile.ReadAllBytes(path),
            Path.GetExtension(path),
            Path.GetFileNameWithoutExtension(path),
            options,
            sideFiles: SharedFile.Beside(path)
        );
        var world = WorldTransforms(read.Model.Nodes);
        var meshes = read.Meshes.Select(static (mesh, at) => (mesh, at))
            .ToDictionary(static pair => pair.mesh.Name, StringComparer.Ordinal);
        var materials = ReadMaterials(read);
        var vertices = new List<float>();
        var indices = new List<uint>();
        var parts = new List<ViewportPart>();
        var bounds = BoundingBox.Empty;
        var hasCoordinates = false;

        foreach (var part in read.Model.Parts) {
            var (mesh, at) = meshes[part.Mesh];
            var first = indices.Count;
            var offset = (uint)(vertices.Count / FloatsPerVertex);
            var name = part.Material >= 0 && part.Material < materials.Length
                ? materials[part.Material].Name
                : string.Empty;
            var coordinates = Layout(read, mesh, at, uvSets?.GetValueOrDefault(name) ?? 0, name);
            bounds = Append(mesh, coordinates, world[part.Node], vertices, bounds);
            hasCoordinates |= coordinates.Length == mesh.Positions.Length;
            foreach (var index in mesh.Indices) {
                indices.Add(offset + (uint)index);
            }

            parts.Add(new(first, mesh.Indices.Length, part.Material, part.Mesh));
        }

        return new([..vertices], [..indices], [..parts], materials, bounds, hasCoordinates, false);
    }

    /// <summary>A mesh's coordinates in one of its sets: the first as the mesh has it, or another one.</summary>
    /// <exception cref="ModelFormatException">The mesh has no such set.</exception>
    static Vector2[] Layout(ReadModel read, MeshData mesh, int at, int uvSet, string material) {
        if (uvSet == 0) {
            return mesh.TexCoords;
        }

        var sets = at < read.TexCoordSets.Length ? read.TexCoordSets[at] : [];
        return uvSet < sets.Length && sets[uvSet].Length == mesh.Positions.Length
            ? sets[uvSet]
            : throw new ModelFormatException(
                $"Texture set '{material}' is laid out in UV set {uvSet + 1}, and its part '{mesh.Name}' has "
                + $"{sets.Count(static set => set.Length > 0)}. Choose a UV set every part of it has."
            );
    }

    /// <summary>The materials some part is drawn with.</summary>
    public string[] MaterialsInUse() =>
        [..Parts.Select(static part => part.Material).Distinct().Order().Select(material => Materials[material].Name)];

    /// <summary>The named meshes of a texture set, or of the whole file for a set that is no material here.</summary>
    public string[] PartsOf(string set) {
        var material = MaterialOf(set);
        return [
            ..Parts.Where(part => material < 0 || part.Material == material).Select(static part => part.Name).Distinct()
        ];
    }

    /// <summary>The layout of the named meshes, three coordinates to a triangle.</summary>
    public Vector2[] LayoutOf(IReadOnlyCollection<string> names) {
        var coordinates = Geometry().Coordinates;
        return [
            ..Parts.Where(part => names.Contains(part.Name))
                .SelectMany(part => Indices.Skip(part.FirstIndex).Take(part.IndexCount))
                .Select(corner => coordinates[corner])
        ];
    }

    /// <summary>Which material a texture set paints, by name.</summary>
    /// <returns>Its index, or -1 for one that is no material here.</returns>
    public int MaterialOf(string name) => Array.FindIndex(Materials, material => material.Name == name);

    /// <summary>The parts named, or whose material is named, or every part when nothing is (#136).</summary>
    public ViewportPart[] Shown(string[] parts, string[] materials) =>
        parts.Length == 0 && materials.Length == 0
            ? Parts
            : [
                ..Parts.Where(part => parts.Contains(part.Name)
                    || (part.Material >= 0
                        && part.Material < Materials.Length
                        && materials.Contains(Materials[part.Material].Name))
                )
            ];

    /// <summary>The triangles of the parts an isolation shows, and the model's number for each of them.</summary>
    public (ViewportGeometry Geometry, int[] Triangles) ShownGeometry(string[] parts, string[] materials) {
        var whole = Geometry();
        List<int> indices = [];
        List<int> triangles = [];
        foreach (var part in Shown(parts, materials)) {
            for (var corner = part.FirstIndex; corner + 2 < part.FirstIndex + part.IndexCount; corner += 3) {
                indices.AddRange(whole.Indices[corner..(corner + 3)]);
                triangles.Add(corner / 3);
            }
        }

        return (whole with { Indices = [..indices] }, [..triangles]);
    }

    /// <summary>One material's triangles as a brush reaches them, or all of them for an unknown name.</summary>
    public ViewportGeometry Geometry(string material) {
        var index = MaterialOf(material);
        var whole = Geometry();
        return index < 0
            ? whole
            : whole with {
                Indices = [
                    ..Parts.Where(part => part.Material == index)
                        .SelectMany(part => Indices.Skip(part.FirstIndex).Take(part.IndexCount))
                        .Select(static corner => (int)corner)
                ]
            };
    }

    /// <summary>The triangles as a brush reaches them, in the space the viewport draws them in.</summary>
    public ViewportGeometry Geometry() {
        var count = Vertices.Length / FloatsPerVertex;
        var positions = new Vector3[count];
        var coordinates = new Vector2[count];
        for (var vertex = 0; vertex < count; vertex++) {
            var at = vertex * FloatsPerVertex;
            positions[vertex] = new(Vertices[at], Vertices[at + 1], Vertices[at + 2]);
            coordinates[vertex] = new(Vertices[at + CoordinateOffset], Vertices[at + CoordinateOffset + 1]);
        }

        return new(positions, coordinates, [..Indices.Select(static index => (int)index)], Bounds, HasCoordinates);
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
        string[] raw = [
            ..Enumerable.Range(0, materials.Length)
                .Select(at => at < read.Model.Materials.Length ? read.Model.Materials[at] : string.Empty)
        ];
        var names = MaterialNames.Unique(raw);
        for (var at = 0; at < materials.Length; at++) {
            materials[at] = at < read.MaterialFactors.Length
                ? new(read.MaterialFactors[at], read.MaterialTextures[at], names[at])
                : new(ModelMaterialFactors.Default, new(), names[at]);
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

    static BoundingBox Append(
        MeshData mesh,
        Vector2[] coordinates,
        Matrix4x4 world,
        List<float> vertices,
        BoundingBox bounds
    ) {
        var normals = Matrix4x4.Invert(world, out var inverse) ? Matrix4x4.Transpose(inverse) : world;
        var hasCoordinates = coordinates.Length == mesh.Positions.Length;
        for (var i = 0; i < mesh.Positions.Length; i++) {
            var position = Matrix4x4.TransformPosition(mesh.Positions[i], world);
            var normal = mesh.Normals.Length > i
                ? Vector3.Normalize(Matrix4x4.TransformDirection(mesh.Normals[i], normals))
                : Vector3.UnitY;
            var coordinate = hasCoordinates ? coordinates[i] : default;
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
