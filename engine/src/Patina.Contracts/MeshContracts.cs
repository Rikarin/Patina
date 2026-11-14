namespace Patina.Contracts;

// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005

/// <summary>What a mesh file is, as the engine reads it (#146): the answer to <c>GET /library/mesh</c>.</summary>
/// <param name="File">The file's name.</param>
/// <param name="Format">What the format is called, for a person.</param>
/// <param name="Triangles">How many triangles, every part counted where it stands.</param>
/// <param name="Vertices">How many vertices, after welding.</param>
/// <param name="Parts">Each named mesh where the file places it.</param>
/// <param name="Materials">Each material a triangle uses, which is each texture set the mesh has.</param>
/// <param name="UvSets">The most texture coordinate sets any part has. Zero is a mesh with no layout.</param>
/// <param name="Minimum">The least corner of the box round the model, in the file's units.</param>
/// <param name="Maximum">The greatest corner of the box.</param>
/// <param name="MetresPerUnit">
///     How long the file's unit is, in metres, when the file or its format says: glTF is in metres, and an FBX states
///     its unit. Null for a file that does not say, as OBJ, PLY and STL never do.
/// </param>
/// <param name="VertexColours">Whether any part carries vertex colours.</param>
public sealed record MeshFacts(
    string File,
    string Format,
    int Triangles,
    int Vertices,
    IReadOnlyList<MeshPartFacts> Parts,
    IReadOnlyList<MeshMaterialFacts> Materials,
    int UvSets,
    IReadOnlyList<double> Minimum,
    IReadOnlyList<double> Maximum,
    double? MetresPerUnit,
    bool VertexColours
);

/// <summary>One named mesh where the file places it.</summary>
/// <param name="Name">Its name in the file.</param>
/// <param name="Material">The material, or texture set, it is drawn with.</param>
/// <param name="Triangles">How many triangles.</param>
/// <param name="UvSets">How many texture coordinate sets it has.</param>
public sealed record MeshPartFacts(string Name, string Material, int Triangles, int UvSets);
#pragma warning restore SK7005

/// <summary>One material of a mesh, which is one texture set.</summary>
/// <param name="Name">Its name, unique in the file.</param>
/// <param name="Triangles">How many triangles it is on.</param>
/// <param name="Parts">How many parts it is on.</param>
/// <param name="UvSets">
///     The texture coordinate sets every one of its parts has, so a texture set can paint and bake on any of them.
/// </param>
public sealed record MeshMaterialFacts(string Name, int Triangles, int Parts, int UvSets);

/// <summary>
///     What the Server needs of a mesh file's texture sets, read by the Worker (#285): the Server never parses a mesh,
///     since a bad file may crash the native parser. It travels between the processes only.
/// </summary>
/// <param name="Sets">
///     Each material, and the whole mesh as the empty name, in each UV set it can be laid out in. One that cannot be
///     read so is missing.
/// </param>
/// <param name="Pictures">The base colour pictures beside the file that its materials name, by full path.</param>
public sealed record MeshSetsRead(IReadOnlyList<MeshSetRead> Sets, IReadOnlyList<string> Pictures) {
    /// <summary>What a file that will not read has: nothing.</summary>
    public static MeshSetsRead None { get; } = new([], []);
}

/// <summary>One texture set's faces in one UV set, as a bake and an export read them.</summary>
/// <param name="Material">The material, or empty for the whole mesh.</param>
/// <param name="UvSet">The UV set, by the file's number.</param>
/// <param name="Udim">The UDIM tile the layout sits on: 1001 when it is the first, or when it reaches several.</param>
/// <param name="Geometry">The revision of the faces, as a bake records it in its manifest.</param>
public sealed record MeshSetRead(string Material, int UvSet, int Udim, string Geometry);
