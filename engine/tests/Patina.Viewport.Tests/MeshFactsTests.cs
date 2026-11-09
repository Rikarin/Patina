using Patina.Contracts;

namespace Patina.Viewport.Tests;

/// <summary>Every mesh format Patina offers reads, and a mesh says what it is (#146).</summary>
public sealed class MeshFactsTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-mesh-facts-");

    public void Dispose() => folder.Delete(true);

    /// <summary>One triangle in each format the list offers, written as text where the format allows.</summary>
    static readonly Dictionary<string, string> Triangles = new(StringComparer.Ordinal) {
        [".obj"] = "o Tri\nv 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nf 1/1 2/2 3/3\n",
        [".stl"] = "solid tri\n facet normal 0 0 1\n  outer loop\n   vertex 0 0 0\n   vertex 1 0 0\n   vertex 0 1 0\n"
            + "  endloop\n endfacet\nendsolid tri\n",
        [".ply"] = "ply\nformat ascii 1.0\nelement vertex 3\nproperty float x\nproperty float y\nproperty float z\n"
            + "property float s\nproperty float t\nelement face 1\nproperty list uchar int vertex_indices\nend_header\n"
            + "0 0 0 0 0\n1 0 0 1 0\n0 1 0 0 1\n3 0 1 2\n",
        [".dae"] = """
            <?xml version="1.0" encoding="utf-8"?>
            <COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
              <asset><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
              <library_geometries>
                <geometry id="tri" name="Tri">
                  <mesh>
                    <source id="pos">
                      <float_array id="pos-a" count="9">0 0 0 1 0 0 0 1 0</float_array>
                      <technique_common>
                        <accessor source="#pos-a" count="3" stride="3">
                          <param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/>
                        </accessor>
                      </technique_common>
                    </source>
                    <vertices id="v"><input semantic="POSITION" source="#pos"/></vertices>
                    <triangles count="1"><input semantic="VERTEX" source="#v" offset="0"/><p>0 1 2</p></triangles>
                  </mesh>
                </geometry>
              </library_geometries>
              <library_visual_scenes>
                <visual_scene id="s"><node id="n" name="Tri"><instance_geometry url="#tri"/></node></visual_scene>
              </library_visual_scenes>
              <scene><instance_visual_scene url="#s"/></scene>
            </COLLADA>
            """,
        [".fbx"] = """
            ; FBX 7.4.0 project file
            FBXHeaderExtension:  {
            	FBXHeaderVersion: 1003
            	FBXVersion: 7400
            }
            GlobalSettings:  {
            	Version: 1000
            	Properties70:  {
            		P: "UnitScaleFactor", "double", "Number", "",100
            	}
            }
            Objects:  {
            	Geometry: 1000, "Geometry::Tri", "Mesh" {
            		Vertices: *9 {
            			a: 0,0,0,1,0,0,0,1,0
            		}
            		PolygonVertexIndex: *3 {
            			a: 0,1,-3
            		}
            		GeometryVersion: 124
            	}
            	Model: 2000, "Model::Tri", "Mesh" {
            		Version: 232
            	}
            }
            Connections:  {
            	C: "OO",1000,2000
            	C: "OO",2000,0
            }
            """,
        [".gltf"] = Gltf("data:application/octet-stream;base64," + Convert.ToBase64String(Buffer())),
        [".glb"] = string.Empty
    };

    /// <summary>A format offered with no file here to prove it reads is a format offered on faith.</summary>
    [Fact]
    public void Every_offered_format_has_a_triangle_here() =>
        Assert.Equal(MeshFormats.Meshes.Order(StringComparer.Ordinal), Triangles.Keys.Order(StringComparer.Ordinal));

    [Theory]
    [InlineData(".obj")]
    [InlineData(".stl")]
    [InlineData(".ply")]
    [InlineData(".dae")]
    [InlineData(".fbx")]
    [InlineData(".gltf")]
    [InlineData(".glb")]
    public void Each_offered_format_reads_as_one_triangle(string extension) {
        var path = Write(extension);

        var facts = MeshFactsReader.Read(path);

        Assert.Equal(1, facts.Triangles);
        Assert.Equal(MeshFormats.Name(extension), facts.Format);
        Assert.Equal(1, facts.Maximum[0], 3);
        Assert.Equal(1, facts.Maximum[1], 3);
    }

    /// <summary>Units are said only where the file or its format means a length: glTF and a stating FBX.</summary>
    [Fact]
    public void Units_are_said_where_the_format_means_them() {
        Assert.Equal(1, MeshFactsReader.Read(Write(".gltf")).MetresPerUnit);
        Assert.Equal(1, MeshFactsReader.Read(Write(".fbx")).MetresPerUnit);
        Assert.Null(MeshFactsReader.Read(Write(".obj")).MetresPerUnit);
        Assert.Null(MeshFactsReader.Read(Write(".stl")).MetresPerUnit);
    }

    /// <summary>A mesh's parts, materials and coordinate sets: two parts on two materials, one with a second set.</summary>
    [Fact]
    public void A_mesh_says_its_parts_materials_and_coordinate_sets() {
        var path = Path.Combine(folder.FullName, "two.gltf");
        File.WriteAllText(path, TwoParts());

        var facts = MeshFactsReader.Read(path);

        Assert.Equal(2, facts.Parts.Count);
        Assert.Equal(["Body", "Strap"], facts.Materials.Select(static m => m.Name).Order(StringComparer.Ordinal));
        Assert.Equal(2, facts.UvSets);
        Assert.Equal(2, facts.Materials.Single(static m => m.Name == "Body").UvSets);
        Assert.Equal(1, facts.Materials.Single(static m => m.Name == "Strap").UvSets);
        Assert.Equal(2, facts.Triangles);
        Assert.False(facts.VertexColours);
    }

    string Write(string extension) {
        var path = Path.Combine(folder.FullName, "tri" + extension);
        if (extension == ".glb") {
            Vixen.Interchange.ModelWriter.Write(
                path,
                [
                    new Vixen.Geometry.MeshData {
                        Name = "Tri",
                        Positions = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)],
                        Normals = [Vixen.Core.Mathematics.Vector3.UnitZ, Vixen.Core.Mathematics.Vector3.UnitZ,
                            Vixen.Core.Mathematics.Vector3.UnitZ],
                        TexCoords = [new(0, 0), new(1, 0), new(0, 1)],
                        Indices = [0, 1, 2],
                        Bounds = new(new(0, 0, 0), new(1, 1, 0))
                    }
                ],
                "tri"
            );
        } else {
            File.WriteAllText(path, Triangles[extension]);
        }

        return path;
    }

    /// <summary>Positions, then the first layout, then a second, then the corners: one triangle.</summary>
    static byte[] Buffer() {
        float[] positions = [0, 0, 0, 1, 0, 0, 0, 1, 0];
        float[] first = [0, 0, 1, 0, 0, 1];
        float[] second = [0.5f, 0.25f, 0.75f, 0.25f, 0.5f, 0.5f];
        ushort[] indices = [0, 1, 2];
        var buffer = new byte[36 + 24 + 24 + 6];
        System.Buffer.BlockCopy(positions, 0, buffer, 0, 36);
        System.Buffer.BlockCopy(first, 0, buffer, 36, 24);
        System.Buffer.BlockCopy(second, 0, buffer, 60, 24);
        System.Buffer.BlockCopy(indices, 0, buffer, 84, 6);
        return buffer;
    }

    const string OneMesh =
        "\"meshes\": [{ \"name\": \"Tri\", \"primitives\": [{ \"attributes\": { \"POSITION\": 0, \"TEXCOORD_0\": 1 }, \"indices\": 3 }] }],";

    static string Gltf(string uri, string meshes = OneMesh) =>
        $$"""
          {
            "asset": { "version": "2.0" },
            "scene": 0,
            "scenes": [{ "nodes": [0] }],
            "nodes": [{ "mesh": 0 }],
            {{meshes}}
            "buffers": [{ "byteLength": 90, "uri": "{{uri}}" }],
            "bufferViews": [
              { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
              { "buffer": 0, "byteOffset": 36, "byteLength": 24 },
              { "buffer": 0, "byteOffset": 60, "byteLength": 24 },
              { "buffer": 0, "byteOffset": 84, "byteLength": 6 }
            ],
            "accessors": [
              { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0, 0, 0], "max": [1, 1, 0] },
              { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC2" },
              { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
              { "bufferView": 3, "componentType": 5123, "count": 3, "type": "SCALAR" }
            ]
          }
          """;

    /// <summary>The triangle twice, as Body with both layouts and Strap with only the first.</summary>
    static string TwoParts() =>
        Gltf(
            "data:application/octet-stream;base64," + Convert.ToBase64String(Buffer()),
            """
            "meshes": [{ "name": "Tri", "primitives": [
              { "attributes": { "POSITION": 0, "TEXCOORD_0": 1, "TEXCOORD_1": 2 }, "indices": 3, "material": 0 },
              { "attributes": { "POSITION": 0, "TEXCOORD_0": 1 }, "indices": 3, "material": 1 }
            ] }],
            "materials": [{ "name": "Body" }, { "name": "Strap" }],
            """
        );
}
