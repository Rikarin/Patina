using Patina.Jobs;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>A bake lands in the coordinate set its texture set is laid out in (#210).</summary>
public sealed class BakeUvSetTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-uv-sets-");

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    /// <summary>
    ///     One red triangle laid out twice: its first set is the lower-left half of the square, its second a small
    ///     triangle in the middle of it.
    /// </summary>
    string TwoLayouts() {
        float[] positions = [0, 0, 0, 1, 0, 0, 0, 1, 0];
        float[] first = [0, 0, 1, 0, 0, 1];
        float[] second = [0.5f, 0.25f, 0.75f, 0.25f, 0.5f, 0.5f];
        float[] normals = [0, 0, 1, 0, 0, 1, 0, 0, 1];
        ushort[] indices = [0, 1, 2];
        var buffer = new byte[36 + 24 + 24 + 36 + 6];
        Buffer.BlockCopy(positions, 0, buffer, 0, 36);
        Buffer.BlockCopy(first, 0, buffer, 36, 24);
        Buffer.BlockCopy(second, 0, buffer, 60, 24);
        Buffer.BlockCopy(normals, 0, buffer, 84, 36);
        Buffer.BlockCopy(indices, 0, buffer, 120, 6);
        var path = Into("two-layouts.gltf");
        File.WriteAllText(
            path,
            $$"""
              {
                "asset": { "version": "2.0" },
                "scene": 0,
                "scenes": [{ "nodes": [0] }],
                "nodes": [{ "mesh": 0 }],
                "materials": [{ "name": "Body", "pbrMetallicRoughness": { "baseColorFactor": [1, 0, 0, 1] } }],
                "meshes": [{ "name": "Tri", "primitives": [{
                  "attributes": { "POSITION": 0, "TEXCOORD_0": 1, "TEXCOORD_1": 2, "NORMAL": 3 },
                  "indices": 4,
                  "material": 0
                }] }],
                "buffers": [{
                  "byteLength": {{buffer.Length}},
                  "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String(buffer)}}"
                }],
                "bufferViews": [
                  { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                  { "buffer": 0, "byteOffset": 36, "byteLength": 24 },
                  { "buffer": 0, "byteOffset": 60, "byteLength": 24 },
                  { "buffer": 0, "byteOffset": 84, "byteLength": 36 },
                  { "buffer": 0, "byteOffset": 120, "byteLength": 6 }
                ],
                "accessors": [
                  {
                    "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3",
                    "min": [0, 0, 0], "max": [1, 1, 0]
                  },
                  { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC2" },
                  { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                  { "bufferView": 3, "componentType": 5126, "count": 3, "type": "VEC3" },
                  { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
                ]
              }
              """
        );
        return path;
    }

    Bitmap Baked(string mesh, int uvSet, string into) {
        MeshMapBakeRun.Run(
            new() {
                High = [mesh],
                Low = mesh,
                Material = "Body",
                UvSet = uvSet,
                Output = Into("out"),
                Options = new() { Resolution = 64, Format = BakeInputs.Png8, Gutter = 0, Maps = ["colour"] }
            },
            Into(into),
            0,
            null,
            TestContext.Current.CancellationToken
        );
        return PngCodec.Load(Path.Combine(Into(into), "colour.png"));
    }

    static int Red(Bitmap picture, int x, int y) => picture.Pixels[(y * picture.Width + x) * 4];

    [Fact]
    public void A_bake_lands_in_the_coordinate_set_it_is_asked_for() {
        var mesh = TwoLayouts();

        var first = Baked(mesh, 0, "first");
        var second = Baked(mesh, 1, "second");

        // (5, 5) is inside the first layout and outside the second; (34, 18) is inside the second.
        Assert.Equal(255, Red(first, 5, 5));
        Assert.Equal(255, Red(second, 34, 18));
        Assert.NotEqual(255, Red(second, 5, 5));
    }

    [Fact]
    public void A_coordinate_set_the_mesh_does_not_have_is_refused_by_name() {
        var mesh = TwoLayouts();

        var refused = Assert.Throws<InvalidDataException>(() => MeshMapBakeRun.Read(mesh, "Body", 2));

        Assert.Contains("UV set 3", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The Worker's one read of a file for the Server (#285) says, for each set in each UV set, what reading that
    ///     set alone says, and leaves out a UV set the mesh does not have.
    /// </summary>
    [Fact]
    public void One_read_of_every_set_says_what_reading_each_alone_does() {
        var mesh = TwoLayouts();

        var all = MeshFileReads.All(mesh);

        Assert.Equal(4, all.Sets.Count);
        foreach (var set in all.Sets) {
            var material = set.Material.Length == 0 ? null : set.Material;
            Assert.Equal(MeshFileReads.Instance.Geometry(mesh, material, set.UvSet), set.Geometry);
            Assert.Equal(MeshFileReads.Instance.Udim(mesh, material, set.UvSet), set.Udim);
        }

        Assert.NotEqual(
            Assert.Single(all.Sets, static set => set is { Material: "Body", UvSet: 0 }).Geometry,
            Assert.Single(all.Sets, static set => set is { Material: "Body", UvSet: 1 }).Geometry
        );
        Assert.DoesNotContain(all.Sets, static set => set.UvSet == 2);
    }
}
