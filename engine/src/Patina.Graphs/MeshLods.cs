using Patina.Contracts;
using Patina.Export;
using System.Globalization;
using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Graphs;

/// <summary>
///     Lower levels of detail of a mesh, through Vixen's <see cref="MeshSimplifier" />. Each level is simplified from
///     the one before, as its errors say to, and stops where it would have to tear an open border. A level keeps the
///     source's vertices, so its coordinates and normals are unchanged and maps baked on the source still fit.
/// </summary>
static class MeshLods {
    /// <summary>The share of the source's triangles each level aims for, when an output does not say.</summary>
    const string DefaultRatios = "0.5,0.25,0.125";

    public static BuildManifest Build(OutputStep step, string folder, CancellationToken cancellationToken) {
        if (step.Files.Count == 0) {
            throw new InvalidDataException("A mesh-lod output names no mesh.");
        }

        var ratios = Ratios(step.Settings.GetValueOrDefault("ratios", DefaultRatios));
        var mesh = MeshMapBakeRun.Read(step.Files[0]).Mesh;
        var source = ModelGeometry.ToMeshData(mesh, "lod", mesh.TexCoords.ToArray());
        var positions = source.Positions.ToArray();
        var welded = Weld(positions);
        var seam = Seams(source, welded);
        var error = new float[positions.Length];
        var corners = source.Indices.ToArray();
        var whole = corners.Length / 3;

        List<string> files = [];
        List<string> warnings = [];
        for (var level = 0; level < ratios.Count; level++) {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Math.Max(1, (int)(whole * ratios[level]));
            var result = MeshSimplifier.Simplify(
                positions,
                welded,
                seam,
                error,
                corners,
                Border(corners, welded),
                target
            );
            foreach (var (vertex, moved) in result.Updates) {
                error[vertex] = moved;
            }

            corners = result.Corners;
            if (corners.Length / 3 > target * 3 / 2) {
                warnings.Add($"LOD {level + 1} stopped at {corners.Length / 3} triangles, aiming for {target}.");
            }

            var name = $"lod{level + 1}.glb";
            ModelWriter.Write(Path.Combine(folder, name), [Compact(source, corners)], $"lod{level + 1}");
            files.Add(name);
        }

        return new(step.Tool, [], files, warnings);
    }

    static List<float> Ratios(string text) {
        List<float> ratios = [];
        foreach (var part in text.Split(',')) {
            if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio)
                || ratio is <= 0 or >= 1) {
                throw new InvalidDataException($"A ratio is between nothing and one, and '{part}' is not.");
            }

            ratios.Add(ratio);
        }

        return ratios;
    }

    /// <summary>Which vertex stands for each: the first at its exact position.</summary>
    static int[] Weld(Vector3[] positions) {
        var first = new Dictionary<Vector3, int>();
        var welded = new int[positions.Length];
        for (var vertex = 0; vertex < positions.Length; vertex++) {
            welded[vertex] = first.TryGetValue(positions[vertex], out var known)
                ? known
                : first[positions[vertex]] = vertex;
        }

        return welded;
    }

    /// <summary>
    ///     Welded vertices with more than one coordinate: an atlas seam, which must not close. Normals do not count,
    ///     since a flat-shaded mesh has a normal per face, and a seam on every vertex simplifies nothing.
    /// </summary>
    static bool[] Seams(MeshData mesh, int[] welded) {
        var seam = new bool[welded.Length];
        var coordinates = mesh.TexCoords.ToArray();
        for (var vertex = 0; coordinates.Length > 0 && vertex < welded.Length; vertex++) {
            var other = welded[vertex];
            if (other != vertex && coordinates[vertex] != coordinates[other]) {
                seam[other] = true;
            }
        }

        return seam;
    }

    /// <summary>Edges with one face: moving them opens a hole.</summary>
    static HashSet<long> Border(int[] corners, int[] welded) {
        var uses = new Dictionary<long, int>();
        for (var at = 0; at < corners.Length; at += 3) {
            for (var side = 0; side < 3; side++) {
                var key = MeshSimplifier.EdgeKey(welded[corners[at + side]], welded[corners[at + (side + 1) % 3]]);
                uses[key] = uses.GetValueOrDefault(key) + 1;
            }
        }

        return [..uses.Where(static edge => edge.Value == 1).Select(static edge => edge.Key)];
    }

    /// <summary>The vertices a level uses, renumbered.</summary>
    static MeshData Compact(MeshData source, int[] corners) {
        var renumbered = new Dictionary<int, int>();
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var coordinates = new List<Vector2>();
        var indices = new int[corners.Length];
        for (var at = 0; at < corners.Length; at++) {
            if (!renumbered.TryGetValue(corners[at], out var index)) {
                index = renumbered[corners[at]] = positions.Count;
                positions.Add(source.Positions[corners[at]]);
                if (source.Normals.Length > 0) {
                    normals.Add(source.Normals[corners[at]]);
                }

                if (source.TexCoords.Length > 0) {
                    coordinates.Add(source.TexCoords[corners[at]]);
                }
            }

            indices[at] = index;
        }

        return source with {
            Positions = [..positions], Normals = [..normals], TexCoords = [..coordinates], Indices = indices
        };
    }
}
