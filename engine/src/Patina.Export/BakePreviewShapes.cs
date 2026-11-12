using Patina.Contracts;
using System.Globalization;
using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Geometry.Remeshing;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>What a bake's preview draws, as coloured vertices, and the key to its colours.</summary>
/// <param name="Triangles">
///     See-through triangles, three vertices each of position then red, green, blue and
///     opacity.
/// </param>
/// <param name="Lines">Lines, two vertices each, laid out the same.</param>
/// <param name="Parts">The parts by name, when parts are matched by name.</param>
/// <param name="Warnings">What the preview left out or could not read.</param>
public sealed record BakePreviewDrawing(
    float[] Triangles,
    float[] Lines,
    IReadOnlyList<BakePreviewPart> Parts,
    IReadOnlyList<string> Warnings
);

/// <summary>
///     A bake's setup as shapes over the viewport (#149): where its rays start and how far they reach, the high mesh
///     see-through, and which low part strikes which high part when they are matched by name.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>The reach is Vixen's own</b> (<c>MapBaker.Reach</c>), from the settings the bake would build from the
///         same options, so the shell drawn is the cage or the ray distances the bake will use and not a guess at
///         them.
///     </para>
///     <para>
///         ⚠ <b>A hard edge with no UV seam is drawn magenta.</b> The texels along it are shared by two faces whose
///         normals part there, so a tangent-space normal map cannot be right on both sides and shows a line, however
///         the rays run. A hard edge that is also a seam is not drawn: that is how it should be.
///     </para>
///     <para>
///         ⚠ <b>A low part that matched nothing is drawn red</b>, because its rays strike the whole high mesh, which is
///         the commonest reason a neighbour's detail turns up in a part's normal map. A high part that nothing matched
///         is drawn grey and named in the key.
///     </para>
/// </remarks>
public static class BakePreviewShapes {
    /// <summary>The most high triangles drawn; a denser high mesh is thinned evenly, and the preview says so.</summary>
    public const int MostHighTriangles = 1_000_000;

    /// <summary>Floats in one vertex: position, then colour with opacity.</summary>
    const int Stride = 7;

    static readonly Vector4 Unmatched = new(1f, 0.18f, 0.12f, 0.55f);
    static readonly Vector4 Leftover = new(0.55f, 0.55f, 0.55f, 0.2f);
    static readonly Vector4 Ghost = new(0.85f, 0.85f, 0.9f, 1f);
    static readonly Vector4 Shell = new(0.2f, 0.75f, 1f, 1f);
    static readonly Vector4 Seam = new(1f, 0.15f, 0.85f, 1f);

    /// <summary>How far two normals at one point may turn before their edge is hard: about two degrees.</summary>
    const float SameNormal = 0.9994f;

    /// <summary>How far a missing seam's band reaches into each face, as a share of the low mesh's diagonal.</summary>
    const float SeamWidth = 0.006f;

    static readonly Vector4 ShellEdge = new(0.2f, 0.75f, 1f, 0.5f);
    static readonly Vector4 Front = new(0.2f, 0.75f, 1f, 0.8f);
    static readonly Vector4 Rear = new(1f, 0.6f, 0.15f, 0.8f);
    static readonly Vector4 Lean = new(0.2f, 0.8f, 0.15f, 0.95f);

    /// <summary>
    ///     A mesh with more triangles than this gets one leaned ray a triangle, at its middle, rather than three.
    /// </summary>
    const int FewTriangles = 20_000;

    /// <summary>Reads the meshes and makes the shapes.</summary>
    /// <param name="preview">What to show.</param>
    /// <returns>The shapes and the key.</returns>
    /// <exception cref="ModelFormatException">A mesh is not a readable model.</exception>
    /// <exception cref="InvalidDataException">The low mesh has no such texture set, or a map is not square.</exception>
    public static BakePreviewDrawing Build(ViewportBakePreview preview) {
        ArgumentNullException.ThrowIfNull(preview);
        var files = preview.High.Select(static file => MeshMapBakeRun.Model(file)).ToList();
        var high = ModelGeometry.Flatten(files);
        var low = MeshMapBakeRun.Read(preview.Low, preview.Material);
        var cage = preview.Cage is { Length: > 0 } path ? MeshMapBakeRun.Read(path, preview.Material).Mesh : null;
        return Build(preview, high, low, cage);
    }

    /// <summary>The shapes for meshes already read.</summary>
    /// <param name="preview">What to show and the options the bake would run with.</param>
    /// <param name="high">The high meshes, flattened.</param>
    /// <param name="low">The low mesh, cut to its texture set.</param>
    /// <param name="cage">The cage, cut the same way, or null.</param>
    /// <returns>The shapes and the key.</returns>
    public static BakePreviewDrawing Build(ViewportBakePreview preview, FlatModel high, FlatModel low, EditMesh? cage) {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(high);
        ArgumentNullException.ThrowIfNull(low);

        List<float> triangles = [];
        List<float> lines = [];
        List<string> warnings = [];
        var rays = preview.Options.Rays;
        var matching = rays.MatchByName && preview.Matches;
        var (parts, lowColours, highColours) = matching
            ? Matches(rays, high, low)
            : ([], null, null);
        if (preview.Matches && !rays.MatchByName) {
            warnings.Add("Parts are not matched by name, so every low part's rays may strike all of the high mesh.");
        }

        if (preview.HighMesh) {
            Ghosted(high.Mesh, highColours, (float)preview.HighOpacity, triangles, warnings);
        }

        if (lowColours is not null) {
            foreach (var (face, a, b, c) in Faces(low.Mesh)) {
                Triangle(triangles, low.Mesh, a, b, c, lowColours[face]);
            }
        }

        if (preview.Reach) {
            Reach(preview, high, low, cage, triangles, lines, warnings);
        }

        if (preview.Seams) {
            Seams(low.Mesh, triangles, lines, warnings);
        }

        return new([..triangles], [..lines], parts, warnings);
    }

    /// <summary>
    ///     The high mesh see-through, in its parts' colours or neutral, thinned evenly past the most
    ///     drawn.
    /// </summary>
    static void Ghosted(
        EditMesh mesh,
        Vector4[]? colours,
        float opacity,
        List<float> triangles,
        List<string> warnings
    ) {
        var count = Faces(mesh).Count();
        var step = Math.Max(1, (count + MostHighTriangles - 1) / MostHighTriangles);
        if (step > 1) {
            warnings.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The high mesh has {count} triangles, so one in {step} is drawn."
                )
            );
        }

        var at = 0;
        foreach (var (face, a, b, c) in Faces(mesh)) {
            if (at++ % step == 0) {
                Triangle(triangles, mesh, a, b, c, Seen(colours?[face] ?? Ghost, opacity));
            }
        }
    }

    /// <summary>
    ///     The cage or the ray distances: the outer shell, see-through with its edges, and each point's
    ///     ray.
    /// </summary>
    static void Reach(
        ViewportBakePreview preview,
        FlatModel high,
        FlatModel low,
        EditMesh? cage,
        List<float> triangles,
        List<float> lines,
        List<string> warnings
    ) {
        BakeReachShell shell;
        IReadOnlyList<BakeLeanRay> leans;
        try {
            var request = new BakeRequest {
                High = preview.High, Low = preview.Low, Output = string.Empty, Options = preview.Options
            };
            var settings = MeshMapBakeRun.Settings(request, high, low, cage, 1);
            shell = MapBaker.Reach(high.Mesh, low.Mesh, settings);
            // The skew leans rays inside a face, which the corners cannot show: Vixen's rays from inside, if any lean.
            var divisions = Faces(low.Mesh).Count() > FewTriangles ? 3 : 4;
            leans = MapBaker.Leans(high.Mesh, low.Mesh, settings, divisions);
        } catch (ArgumentException e) {
            warnings.Add($"The reach cannot be drawn: {e.Message}");
            return;
        }

        var mesh = low.Mesh;
        var fill = Seen(Shell, (float)preview.ShellOpacity);
        HashSet<Vector3> rayed = [];
        for (var face = 0; face < mesh.FaceCount; face++) {
            var entry = mesh.Faces[face];
            for (var at = 0; at < entry.Count; at++) {
                var corner = entry.Start + at;
                var next = entry.Start + (at + 1) % entry.Count;
                Line(lines, shell.Outer[corner], shell.Outer[next], ShellEdge);
                if (at >= 2 && fill.W > 0f) {
                    Vertex(triangles, shell.Outer[entry.Start], fill);
                    Vertex(triangles, shell.Outer[corner - 1], fill);
                    Vertex(triangles, shell.Outer[corner], fill);
                }

                // One ray a point, not one a corner: a smooth point's corners all cast the same ray.
                var point = mesh.Positions[mesh.Corners[corner]];
                if (rayed.Add(point)) {
                    Line(lines, point, shell.Outer[corner], Front);
                    Line(lines, point, shell.Inner[corner], Rear);
                }
            }
        }

        foreach (var ray in leans) {
            Line(lines, ray.Point, ray.Outer, Lean);
        }

        if (leans.Count > 0) {
            warnings.Add("Green rays inside the faces are where the skew leans them, its map applied.");
        }
    }

    /// <summary>
    ///     The low mesh's hard edges that are no UV seam: an edge two faces share whose normals part at either end
    ///     while its coordinates do not. A mesh with no normals of its own is smooth and has none; one with no
    ///     coordinates is not drawn, since every edge would count.
    /// </summary>
    static void Seams(EditMesh mesh, List<float> triangles, List<float> lines, List<string> warnings) {
        if (mesh.Normals.Length != mesh.CornerCount || mesh.TexCoords.Length != mesh.CornerCount) {
            return;
        }

        var missing = Unseamed(mesh);
        if (missing.Count == 0) {
            return;
        }

        // A line is a pixel wide, so each side of the edge also gets a band on its face, as wide as a small share of
        // the model, which reads at any distance.
        var bounds = mesh.Bounds;
        var width = Vector3.Distance(bounds.Minimum, bounds.Maximum) * SeamWidth;
        foreach (var ((a, b), face, other) in missing) {
            Line(lines, a, b, Seam);
            Band(triangles, mesh, a, b, face, width);
            Band(triangles, mesh, a, b, other, width);
        }

        var said = missing.Count == 1 ? "edge has" : "edges have";
        const string Why = "a tangent-space normal map shows a line there.";
        warnings.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{missing.Count} hard {said} no UV seam, drawn in magenta: {Why}"
            )
        );
    }

    /// <summary>
    ///     Each edge two faces share whose normals part at either end and whose coordinates do not, with the two faces.
    /// </summary>
    static List<((Vector3 A, Vector3 B) Edge, int Face, int Other)> Unseamed(EditMesh mesh) {
        // Each edge by where its ends are, lower first, with the corners at each end of the first face to reach it.
        // ⚠ By place and not by point: a file may split a point for each normal, which is exactly a hard edge.
        Dictionary<(Vector3, Vector3), (int From, int To, int Face)> first = [];
        List<((Vector3 A, Vector3 B) Edge, int Face, int Other)> missing = [];
        for (var face = 0; face < mesh.FaceCount; face++) {
            var entry = mesh.Faces[face];
            for (var at = 0; at < entry.Count; at++) {
                var (here, next) = (entry.Start + at, entry.Start + (at + 1) % entry.Count);
                var (a, b) = (mesh.Positions[mesh.Corners[here]], mesh.Positions[mesh.Corners[next]]);
                var lower = Lower(a, b);
                var key = lower ? (a, b) : (b, a);
                var ends = lower ? (here, next) : (next, here);
                if (!first.TryAdd(key, (ends.Item1, ends.Item2, face))
                    && first[key] is var (from, to, other)
                    && Missing(mesh, ends, (from, to))) {
                    missing.Add((key, face, other));
                }
            }
        }

        return missing;
    }

    /// <summary>Whether an edge's corners on two faces part in their normals and not in their coordinates.</summary>
    static bool Missing(EditMesh mesh, (int From, int To) one, (int From, int To) another) {
        var hard = Vector3.Dot(Unit(mesh.Normals[one.From]), Unit(mesh.Normals[another.From])) < SameNormal
            || Vector3.Dot(Unit(mesh.Normals[one.To]), Unit(mesh.Normals[another.To])) < SameNormal;
        var seam = mesh.TexCoords[one.From] != mesh.TexCoords[another.From]
            || mesh.TexCoords[one.To] != mesh.TexCoords[another.To];
        return hard && !seam;
    }

    /// <summary>A strip on a face along one of its edges, reaching in by the width or half the face at most.</summary>
    static void Band(List<float> triangles, EditMesh mesh, Vector3 a, Vector3 b, int face, float width) {
        var middle = Vector3.Zero;
        var loop = mesh.CornersOf(face);
        foreach (var point in loop) {
            middle += mesh.Positions[point];
        }

        middle /= Math.Max(loop.Length, 1);
        var along = Unit(b - a);
        var toward = middle - (a + b) * 0.5f;
        toward -= along * Vector3.Dot(toward, along);
        var depth = toward.Length();
        if (depth <= 0f) {
            return;
        }

        var inward = toward / depth * MathF.Min(width, depth * 0.5f);
        Vertex(triangles, a, Seam);
        Vertex(triangles, b, Seam);
        Vertex(triangles, b + inward, Seam);
        Vertex(triangles, a, Seam);
        Vertex(triangles, b + inward, Seam);
        Vertex(triangles, a + inward, Seam);
    }

    static Vector3 Unit(Vector3 normal) => normal.LengthSquared() > 0f ? Vector3.Normalize(normal) : normal;

    static Vector4 Seen(Vector4 colour, float opacity) => new(colour.X, colour.Y, colour.Z, opacity);

    static bool Lower(Vector3 a, Vector3 b) => a.X != b.X ? a.X < b.X : a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;

    /// <summary>A colour per name, a colour per face of each side, and the key.</summary>
    static (List<BakePreviewPart> Parts, Vector4[] Low, Vector4[] High) Matches(
        BakeRayOptions rays,
        FlatModel high,
        FlatModel low
    ) {
        var lowSuffixes = MeshMapBakeRun.Suffixes(rays.LowSuffix);
        var highSuffixes = MeshMapBakeRun.Suffixes(rays.HighSuffix);
        var lowFaces = Count(low.Parts, lowSuffixes);
        var highFaces = Count(high.Parts, highSuffixes);
        Dictionary<string, Vector4> colours = new(StringComparer.Ordinal);
        List<BakePreviewPart> parts = [];
        foreach (var name in lowFaces.Keys.Concat(highFaces.Keys.Where(key => !lowFaces.ContainsKey(key)))) {
            var matched = lowFaces.ContainsKey(name) && highFaces.ContainsKey(name);
            var colour = matched
                ? new Vector4(MapBaker.IdColour(colours.Count + 1), 0.55f)
                : lowFaces.ContainsKey(name)
                    ? Unmatched
                    : Leftover;
            colours[name] = colour;
            parts.Add(new(name, lowFaces.GetValueOrDefault(name), highFaces.GetValueOrDefault(name), Hex(colour)));
        }

        return (parts, PerFace(low, lowSuffixes, colours), PerFace(high, highSuffixes, colours));
    }

    static Dictionary<string, int> Count(
        IReadOnlyList<(string Name, int Faces)> parts,
        IReadOnlyList<string> suffixes
    ) {
        Dictionary<string, int> faces = new(StringComparer.Ordinal);
        foreach (var (name, count) in parts) {
            var key = BakeParts.Key(name, suffixes);
            faces[key] = faces.GetValueOrDefault(key) + count;
        }

        return faces;
    }

    static Vector4[] PerFace(FlatModel model, IReadOnlyList<string> suffixes, Dictionary<string, Vector4> colours) {
        var faces = new Vector4[model.Mesh.FaceCount];
        var face = 0;
        foreach (var (name, count) in model.Parts) {
            faces.AsSpan(face, Math.Min(count, faces.Length - face)).Fill(colours[BakeParts.Key(name, suffixes)]);
            face += count;
        }

        return faces;
    }

    /// <summary>Each face as a fan of triangles, by position.</summary>
    static IEnumerable<(int Face, int A, int B, int C)> Faces(EditMesh mesh) {
        for (var face = 0; face < mesh.FaceCount; face++) {
            var entry = mesh.Faces[face];
            var first = mesh.Corners[entry.Start];
            for (var at = 2; at < entry.Count; at++) {
                yield return (face, first, mesh.Corners[entry.Start + at - 1], mesh.Corners[entry.Start + at]);
            }
        }
    }

    static void Triangle(List<float> into, EditMesh mesh, int a, int b, int c, Vector4 colour) {
        Vertex(into, mesh.Positions[a], colour);
        Vertex(into, mesh.Positions[b], colour);
        Vertex(into, mesh.Positions[c], colour);
    }

    static void Line(List<float> into, Vector3 from, Vector3 to, Vector4 colour) {
        Vertex(into, from, colour);
        Vertex(into, to, colour);
    }

    static void Vertex(List<float> into, Vector3 point, Vector4 colour) {
        into.Add(point.X);
        into.Add(point.Y);
        into.Add(point.Z);
        into.Add(colour.X);
        into.Add(colour.Y);
        into.Add(colour.Z);
        into.Add(colour.W);
    }

    static string Hex(Vector4 colour) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"#{Byte(colour.X):x2}{Byte(colour.Y):x2}{Byte(colour.Z):x2}"
        );

    static int Byte(float value) => (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);

    /// <summary>How many floats a vertex is, for whoever uploads these.</summary>
    public static int FloatsPerVertex => Stride;
}
