namespace Patina.Contracts;

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

/// <summary>One material of a mesh, which is one texture set.</summary>
/// <param name="Name">Its name, unique in the file.</param>
/// <param name="Triangles">How many triangles it is on.</param>
/// <param name="Parts">How many parts it is on.</param>
/// <param name="UvSets">
///     The texture coordinate sets every one of its parts has, so a texture set can paint and bake on any of them.
/// </param>
public sealed record MeshMaterialFacts(string Name, int Triangles, int Parts, int UvSets);
