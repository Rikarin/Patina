using System.Globalization;
using Vixen.Core.Imaging;

namespace Patina.Viewport.Tests;

/// <summary>
///     A model's materials as maps (#157), read through Vixen's reader: glTF's images where they are, an image inside
///     the file written out, the packed lanes glTF reads, its factors, its emission and alpha, and where its pictures
///     are placed.
/// </summary>
public sealed class ModelMaterialsTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-model-materials-");

    public void Dispose() => folder.Delete(true);

    static string Png() =>
        Convert.ToBase64String(PngCodec.Encode8(2, 2, 3, Enumerable.Repeat((byte)200, 12).ToArray()));

    /// <summary>One material with images, one inside the file, and one with factors.</summary>
    const string Painted = """
                           {
                             "name": "Painted",
                             "pbrMetallicRoughness": {
                               "baseColorTexture": { "index": 0 },
                               "metallicRoughnessTexture": { "index": 1 },
                               "roughnessFactor": 0.5,
                               "metallicFactor": 0.25
                             },
                             "normalTexture": { "index": 2 }
                           },
                           { "name": "Plain", "pbrMetallicRoughness": { "baseColorFactor": [0.2, 0.4, 0.6, 1] } }
                           """;

    /// <summary>A placed, glowing, see-through material, and one that glows with no picture.</summary>
    const string Glowing = """
                           {
                             "name": "Glow",
                             "pbrMetallicRoughness": {
                               "baseColorTexture": {
                                 "index": 0,
                                 "extensions": {
                                   "KHR_texture_transform": { "offset": [0.25, 0.5], "rotation": 0.5, "scale": [2, 4] }
                                 }
                               },
                               "baseColorFactor": [1, 1, 1, 0.5]
                             },
                             "emissiveTexture": { "index": 1, "texCoord": 1 },
                             "emissiveFactor": [1, 0.5, 0],
                             "alphaMode": "BLEND",
                             "extensions": { "KHR_materials_emissive_strength": { "emissiveStrength": 3 } }
                           },
                           { "name": "Lamp", "emissiveFactor": [0.5, 0.5, 0.5], "alphaMode": "MASK", "alphaCutoff": 0.25 }
                           """;

    /// <summary>Two triangles, each wearing one of two materials, with pictures beside the file and inside it.</summary>
    string Gltf(string materials = Painted) {
        var floats = new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1 };
        var bytes = new byte[floats.Length * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        var buffer = Convert.ToBase64String(bytes);
        var embedded = Png();
        File.WriteAllBytes(Path.Combine(folder.FullName, "base colour.png"), Convert.FromBase64String(Png()));
        File.WriteAllBytes(Path.Combine(folder.FullName, "orm.png"), Convert.FromBase64String(Png()));
        var text = $$"""
                     {
                       "asset": { "version": "2.0" },
                       "extensionsUsed": ["KHR_texture_transform", "KHR_materials_emissive_strength"],
                       "scene": 0,
                       "scenes": [{ "nodes": [0, 1] }],
                       "nodes": [{ "mesh": 0, "name": "A" }, { "mesh": 1, "name": "B" }],
                       "meshes": [
                         {
                           "name": "A",
                           "primitives": [{
                             "attributes": { "POSITION": 0, "TEXCOORD_0": 1, "TEXCOORD_1": 1 },
                             "material": 0
                           }]
                         },
                         {
                           "name": "B",
                           "primitives": [{ "attributes": { "POSITION": 0, "TEXCOORD_0": 1 }, "material": 1 }]
                         }
                       ],
                       "materials": [{{materials}}],
                       "textures": [{ "source": 0 }, { "source": 1 }, { "source": 2 }],
                       "images": [
                         { "uri": "base%20colour.png" },
                         { "uri": "orm.png" },
                         { "uri": "data:image/png;base64,{{embedded}}" }
                       ],
                       "buffers": [{
                         "byteLength": {{bytes.Length.ToString(CultureInfo.InvariantCulture)}},
                         "uri": "data:application/octet-stream;base64,{{buffer}}"
                       }],
                       "bufferViews": [
                         { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                         { "buffer": 0, "byteOffset": 36, "byteLength": 24 }
                       ],
                       "accessors": [
                         {
                           "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3",
                           "min": [0, 0, 0], "max": [1, 1, 0]
                         },
                         { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC2" }
                       ]
                     }
                     """;
        var path = Path.Combine(folder.FullName, "two.gltf");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_gltf_material_reads_as_its_images_lanes_and_factors() {
        var pictures = Path.Combine(folder.FullName, "pictures");

        var materials = ModelMaterials.Read(Gltf(), pictures);

        Assert.Equal(["Painted", "Plain"], materials.Select(static m => m.Name));
        var painted = materials[0].Slots.ToDictionary(static s => s.Usage);
        Assert.Equal(Path.Combine(folder.FullName, "base colour.png"), painted["baseColor"].Texture);
        Assert.Equal(
            (Path.Combine(folder.FullName, "orm.png"), 1),
            (painted["roughness"].Texture, painted["roughness"].Lane)
        );
        Assert.Equal(0.5f, painted["roughness"].Value[0], 3);
        Assert.Equal(
            (Path.Combine(folder.FullName, "orm.png"), 2),
            (painted["metalness"].Texture, painted["metalness"].Lane)
        );
        Assert.Equal(0.25f, painted["metalness"].Value[0], 3);
        var normal = painted["normal"].Texture!;
        Assert.StartsWith(pictures, normal, StringComparison.Ordinal);
        Assert.True(File.Exists(normal));
        Assert.Null(painted["baseColor"].Placement);

        var plain = materials[1].Slots.ToDictionary(static s => s.Usage);
        Assert.Null(plain["baseColor"].Texture);
        Assert.Equal([0.2f, 0.4f, 0.6f], plain["baseColor"].Value.Take(3).Select(static v => MathF.Round(v, 3)));
        Assert.DoesNotContain(ExportNormal, plain.Keys);
    }

    /// <summary>glTF's emission is black unless the file says otherwise, so a material that says nothing has none.</summary>
    [Fact]
    public void A_gltf_material_with_no_emissive_factor_and_an_opaque_alpha_has_neither_map() {
        var materials = ModelMaterials.Read(Gltf(), Path.Combine(folder.FullName, "pictures"));

        Assert.All(
            materials,
            static m => Assert.DoesNotContain(m.Slots, static s => s.Usage is "emissive" or "opacity")
        );
    }

    [Fact]
    public void A_gltfs_emission_alpha_and_placement_come_with_its_slots() {
        var materials = ModelMaterials.Read(Gltf(Glowing), Path.Combine(folder.FullName, "pictures"));

        var glow = materials[0].Slots.ToDictionary(static s => s.Usage);
        var placement = glow["baseColor"].Placement!;
        Assert.Equal(
            (0.25f, 0.5f, 0.5f, 2f, 4f),
            (Round(placement.OffsetX), Round(placement.OffsetY), Round(placement.Rotation), Round(placement.ScaleX),
                Round(placement.ScaleY))
        );
        Assert.Equal([3f, 1.5f, 0f], glow["emissive"].Value.Take(3).Select(Round));
        Assert.Equal(
            (glow["baseColor"].Texture, 3, 0.5f),
            (glow["opacity"].Texture, glow["opacity"].Lane, Round(glow["opacity"].Value[0]))
        );
        Assert.Equal(placement, glow["opacity"].Placement);
        Assert.Contains(materials[0].Warnings, static w => w.Contains("coordinate set 2", StringComparison.Ordinal));

        var lamp = materials[1].Slots.ToDictionary(static s => s.Usage);
        Assert.Equal((null, 0.5f), (lamp["emissive"].Texture, Round(lamp["emissive"].Value[0])));
        Assert.Null(lamp["opacity"].Texture);
        Assert.Contains(materials[1].Warnings, static w => w.Contains("0.25", StringComparison.Ordinal));
    }

    static float Round(float value) => MathF.Round(value, 4);

    const string ExportNormal = "normal";
}
