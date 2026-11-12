using Patina.Bench;
using Patina.Contracts;
using Patina.Export;
using Patina.Graphs;
using Patina.Library;
using Patina.Server.Viewport;
using System.Numerics;
using Vixen.Geometry;

namespace Patina.Server.Tests;

/// <summary>
///     Every mesh Patina makes turns its faces the way its normals say, and shows a picture the right way round
///     (#242). The preview sphere did neither (#184): it was drawn inside out, and a tangent-space normal map on it lit
///     the wrong way because its layout was mirrored. Each is read the way the viewport and the bake read a file.
///     ⚠ Auto Unwrap's mesh is one of them: Vixen's unwrapper flattens each chart anticlockwise with v up, which a file
///     whose v runs down shows mirrored on every face, so Patina turns the atlas over as it writes it.
/// </summary>
public sealed class GeneratedMeshTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-generated-");

    public void Dispose() => folder.Delete(true);

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> Meshes => [
        ..ViewportShapes.All.Select(static shape => $"preview {shape}"),
        "viewport fixture",
        "housing low",
        "housing high",
        "two materials",
        "two materials cage",
        "knot low",
        "knot high",
        "level of detail",
        "unwrapped"
    ];

    [Theory]
    [MemberData(nameof(Meshes))]
    public void Every_face_turns_the_way_its_normals_say_and_no_picture_is_mirrored(string name) {
        var mesh = MeshMapBakeRun.Read(Made(name)).Mesh;
        Assert.True(mesh.FaceCount > 0, $"The {name} has no faces.");
        List<string> faults = [];
        var normals = mesh.Normals;
        var uvs = mesh.TexCoords;
        for (var face = 0; face < mesh.FaceCount; face++) {
            var corners = mesh.CornersOf(face);
            var first = mesh.Faces[face].Start;
            for (var at = 2; at < corners.Length; at++) {
                int[] fan = [first, first + at - 1, first + at];
                var (a, b, c) = (Point(mesh, fan[0]), Point(mesh, fan[1]), Point(mesh, fan[2]));
                var turned = Vector3.Cross(b - a, c - a);
                if (turned.LengthSquared() < 1e-14f) {
                    continue; // a sliver has no direction
                }

                if (normals.Length > 0
                    && Vector3.Dot(turned, normals[fan[0]] + normals[fan[1]] + normals[fan[2]]) <= 0) {
                    faults.Add($"face {face} turns against its normals");
                }

                // Down the picture is down the texture, so a triangle seen from the front turns the other way in it.
                if (uvs.Length > 0) {
                    var (u, v, w) = (uvs[fan[0]], uvs[fan[1]], uvs[fan[2]]);
                    if ((v.X - u.X) * (w.Y - u.Y) - (v.Y - u.Y) * (w.X - u.X) > 1e-9f) {
                        faults.Add($"face {face} shows the picture mirrored");
                    }
                }
            }
        }

        Assert.True(
            faults.Count == 0,
            $"The {name} has {faults.Count} faults, the first {string.Join("; ", faults.Take(5))}."
        );
        if (Closed(name)) {
            Assert.True(Volume(mesh) > 0, $"The {name} is inside out: its faces enclose less than nothing.");
        }
    }

    /// <summary>
    ///     A bake's preview draws its triangles as the faces under them turn, so a see-through shell and the colours of
    ///     matched parts face out of the model as the low mesh does.
    /// </summary>
    [Fact]
    public void A_bake_preview_turns_its_triangles_as_the_low_mesh_does() {
        var meshes = Path.Combine(folder.FullName, "housing");
        BakeFixture.Write(meshes, 0.1f);
        var (high, low) = (Path.Combine(meshes, BakeFixture.HighFile), Path.Combine(meshes, BakeFixture.LowFile));
        var drawing = BakePreviewShapes.Build(
            new() {
                High = [high],
                Low = low,
                HighMesh = false,
                Options = new() { Rays = new() { MatchByName = true, Units = "absolute", Front = 0.05, Rear = 0.05 } }
            }
        );

        var triangles = drawing.Triangles;
        var stride = BakePreviewShapes.FloatsPerVertex * 3;
        Assert.True(triangles.Length > 0);
        var outward = 0;
        for (var at = 0; at < triangles.Length; at += stride) {
            var (a, b, c) = (Corner(triangles, at, 0), Corner(triangles, at, 1), Corner(triangles, at, 2));
            var turned = Vector3.Cross(b - a, c - a);
            // The housing is convex round its middle, so a face that turns out faces away from it.
            outward += Vector3.Dot(turned, (a + b + c) / 3) > 0 ? 1 : 0;
        }

        Assert.Equal(triangles.Length / stride, outward);
    }

    static Vector3 Corner(float[] triangles, int at, int corner) {
        var from = at + corner * BakePreviewShapes.FloatsPerVertex;
        return new(triangles[from], triangles[from + 1], triangles[from + 2]);
    }

    static Vector3 Point(EditMesh mesh, int corner) => mesh.Positions[mesh.Corners[corner]];

    /// <summary>The volume a closed mesh's faces enclose, which is negative when they all face in.</summary>
    static float Volume(EditMesh mesh) {
        var volume = 0f;
        for (var face = 0; face < mesh.FaceCount; face++) {
            var first = mesh.Faces[face].Start;
            for (var at = 2; at < mesh.Faces[face].Count; at++) {
                var (a, b, c) = (Point(mesh, first), Point(mesh, first + at - 1), Point(mesh, first + at));
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
            }
        }

        return volume;
    }

    static bool Closed(string name) =>
        name is "preview sphere"
            or "preview cube"
            or "preview cylinder"
            or "housing high"
            or "knot high"
            or "knot low"
            or "unwrapped";

    /// <summary>Makes the mesh of that name, as the code that makes it for real does.</summary>
    string Made(string name) {
        var at = folder.FullName;
        if (name.StartsWith("preview ", StringComparison.Ordinal)) {
            return PreviewShape.File(at, name["preview ".Length..]);
        }

        switch (name) {
            case "viewport fixture":
                var fixture = Path.Combine(at, "viewport-test.glb");
                MeshFixture.Write(fixture);
                return fixture;
            case "housing low" or "housing high":
                BakeFixture.Write(at, 0.1f);
                return Path.Combine(at, name == "housing low" ? BakeFixture.LowFile : BakeFixture.HighFile);
            case "two materials" or "two materials cage":
                var quads = Path.Combine(at, "quads.obj");
                BakeFixture.WriteTwoMaterials(quads, name.EndsWith("cage", StringComparison.Ordinal) ? 0.1f : 0f);
                return quads;
            case "knot low" or "knot high":
                var (high, low) = BakeFixture.WriteKnot(at, 60);
                return name == "knot low" ? low : high;
            case "level of detail":
                BakeFixture.Write(at, 0.1f);
                var made = Path.Combine(at, "made");
                var built = new OutputBuilders(
                    static () => throw new InvalidOperationException("This step needs no device."),
                    static work => work()
                )
                    .Build(
                        new(
                            BuildTools.MeshLod,
                            [Path.Combine(at, BakeFixture.HighFile)],
                            [],
                            new Dictionary<string, string> { ["ratios"] = "0.25" },
                            made,
                            null
                        ),
                        1,
                        Token
                    );
                return Path.Combine(made, Assert.Single(built.Files));
            case "unwrapped":
                // A box with no layout, as Auto Unwrap is given one.
                var bare = Path.Combine(at, "bare.obj");
                File.WriteAllText(
                    bare,
                    """
                    v -1 -1 -1
                    v 1 -1 -1
                    v 1 1 -1
                    v -1 1 -1
                    v -1 -1 1
                    v 1 -1 1
                    v 1 1 1
                    v -1 1 1
                    f 1 4 3 2
                    f 5 6 7 8
                    f 1 2 6 5
                    f 2 3 7 6
                    f 3 4 8 7
                    f 4 1 5 8

                    """
                );
                var unwrapped = Path.Combine(at, "bare_unwrapped.glb");
                Assert.NotNull(MeshUnwrapRun.Run(bare, unwrapped));
                return unwrapped;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "No such mesh.");
        }
    }
}
