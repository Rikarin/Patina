using Patina.Contracts;
using System.Net;
using Vixen.Core.Imaging;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Tests;

/// <summary>
///     Bringing a material file's materials into a project as graphs (#157): MaterialX and USD's text form, which the
///     Server reads itself, each material a graph that compiles with nothing to say and reads its pictures from the
///     project.
/// </summary>
public sealed class MaterialImportApiTests : IAsyncLifetime {
    const string Usda = """
                        #usda 1.0
                        (
                            defaultPrim = "Brick"
                        )

                        def Material "Brick"
                        {
                            token outputs:surface.connect = </Brick/Surface.outputs:surface>

                            def Shader "Surface"
                            {
                                uniform token info:id = "UsdPreviewSurface"
                                color3f inputs:diffuseColor.connect = </Brick/Colour.outputs:rgb>
                                float inputs:roughness.connect = </Brick/Packed.outputs:g>
                                float inputs:metallic = 0.25
                                normal3f inputs:normal.connect = </Brick/Normal.outputs:rgb>
                                token outputs:surface
                            }

                            def Shader "Colour"
                            {
                                uniform token info:id = "UsdUVTexture"
                                asset inputs:file = @textures/colour.png@
                                token inputs:sourceColorSpace = "sRGB"
                                float3 outputs:rgb
                            }

                            def Shader "Packed"
                            {
                                uniform token info:id = "UsdUVTexture"
                                asset inputs:file = @textures/packed.png@ ( doc = "occlusion, roughness, metallic" )
                                float4 inputs:scale = (-1, -1, -1, -1)
                                float4 inputs:bias = (1, 1, 1, 1)
                                float outputs:g
                            }

                            def Shader "Normal"
                            {
                                uniform token info:id = "UsdUVTexture"
                                asset inputs:file = @textures/normal.png@
                                float4 inputs:scale = (2, 2, 2, 1)
                                float4 inputs:bias = (-1, -1, -1, 0)
                                float3 outputs:rgb
                            }
                        }
                        """;

    const string Mtlx = """
                        <?xml version="1.0" encoding="utf-8"?>
                        <materialx version="1.38">
                          <nodegraph name="NG_Tile">
                            <image name="colour" type="color3">
                              <input name="file" type="filename" value="textures/colour.png" colorspace="srgb_texture" />
                            </image>
                            <image name="packed" type="vector3">
                              <input name="file" type="filename" value="textures/packed.png" />
                            </image>
                            <extract name="rough" type="float">
                              <input name="in" type="vector3" nodename="packed" />
                              <input name="index" type="integer" value="1" />
                            </extract>
                            <noise2d name="noise" type="float" />
                            <output name="out_base" type="color3" nodename="colour" />
                            <output name="out_rough" type="float" nodename="rough" />
                            <output name="out_coat" type="float" nodename="noise" />
                          </nodegraph>
                          <standard_surface name="SR_Tile" type="surfaceshader">
                            <input name="base_color" type="color3" nodegraph="NG_Tile" output="out_base" />
                            <input name="specular_roughness" type="float" nodegraph="NG_Tile" output="out_rough" />
                            <input name="metalness" type="float" value="1" />
                            <input name="opacity" type="color3" nodegraph="NG_Tile" output="out_coat" />
                          </standard_surface>
                          <surfacematerial name="M_Tile" type="material">
                            <input name="surfaceshader" type="surfaceshader" nodename="SR_Tile" />
                          </surfacematerial>
                        </materialx>
                        """;

    readonly ServerFixture server = new();
    readonly DirectoryInfo source = Directory.CreateTempSubdirectory("patina-materials-");
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abcf
            name: Game
            texelDensity: 1024
            channels: []
            defaultShader: pbr
            exportTargets: []
            """
        );
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        client = server.AuthorisedClient();
        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Walls"),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var textures = Directory.CreateDirectory(Path.Combine(source.FullName, "textures")).FullName;
        foreach (var name in (string[])["colour", "packed", "normal"]) {
            var pixels = Enumerable.Repeat((byte)128, 4 * 4 * 3).ToArray();
            File.WriteAllBytes(Path.Combine(textures, $"{name}.png"), PngCodec.Encode8(4, 4, 3, pixels));
        }

        File.WriteAllText(Path.Combine(source.FullName, "brick.usda"), Usda);
        File.WriteAllText(Path.Combine(source.FullName, "tile.mtlx"), Mtlx);
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
        source.Delete(true);
    }

    async Task<MaterialImportResult> ImportAsync(string file) {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/library/project/materials",
            new MaterialImportRequest("walls", Path.Combine(source.FullName, file)),
            Token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Token));
        return (await response.Content.ReadFromJsonAsync<MaterialImportResult>(Token))!;
    }

    /// <summary>
    ///     Each material is a graph of the project that compiles with nothing to say, its pictures copied into the
    ///     project, and each input the file wires is an output of the graph.
    /// </summary>
    [Theory]
    [InlineData("brick.usda", "Brick", new[] { "baseColor", "roughness", "metalness", "normal" })]
    [InlineData("tile.mtlx", "Tile", new[] { "baseColor", "roughness", "metalness" })]
    public async Task A_material_file_becomes_a_graph_that_reads_its_pictures(
        string file,
        string name,
        string[] usages
    ) {
        var imported = await ImportAsync(file);

        var path = Assert.Single(imported.Documents);
        var graph = Path.Combine(server.Library, path);
        var asset = YamlSerializer.Parse<NodeGraphAsset>(await File.ReadAllTextAsync(graph, Token));
        Assert.Equal(name, asset.Name);
        Assert.Equal(
            usages.Order(StringComparer.Ordinal),
            asset.Nodes.Where(static n => n.Type == "Output/Output")
                .Select(static n => n.Texts["Usage"])
                .Order(StringComparer.Ordinal)
        );
        var stem = Path.GetFileNameWithoutExtension(file);
        Assert.All(
            asset.Nodes.Where(static n => n.Type == "Source/Bitmap"),
            node => {
                Assert.StartsWith($"pictures/{stem}/", node.Texts["Source"], StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(server.Library, "projects/walls", node.Texts["Source"])));
            }
        );

        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        var model = NodeGraphDocument.Load(asset, out var repairs);
        var diagnostics = new TextureGraphCompiler(registry).Compile(model).Diagnostics;
        Assert.Empty(repairs);
        Assert.True(diagnostics.IsEmpty, string.Join("; ", diagnostics.Select(static d => d.Message)));

        var project = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=walls", Token);
        Assert.Contains(project!.Documents, d => d.Kind == "graph" && d.Name == name);
    }

    /// <summary>USD's roughness is one minus a lane and its normal runs up, so both are turned in the graph.</summary>
    [Fact]
    public async Task One_minus_a_lane_and_a_green_that_runs_up_are_turned_in_the_graph() {
        var imported = await ImportAsync("brick.usda");

        var asset = YamlSerializer.Parse<NodeGraphAsset>(
            await File.ReadAllTextAsync(Path.Combine(server.Library, imported.Documents[0]), Token)
        );
        var mixes = asset.Nodes.Where(static n => n.Type == "Colour/Channel Mix").ToList();
        Assert.Contains(mixes, static m => m.Values["Red from Red"][0] < 0 && m.Values["Red Offset"][0] > 0);
        // The normal's own mix turns green alone, and leaves red and blue as they are.
        Assert.Contains(
            mixes,
            static m => !m.Values.ContainsKey("Red from Red")
                && m.Values["Green from Green"][0] < 0
                && m.Values["Green Offset"][0] > 0
        );
        Assert.Single(asset.Nodes, static n => n.Type == "Colour/Channel Shuffle");
    }

    /// <summary>An input a graph would have to compute is left out and said, and the rest comes in.</summary>
    [Fact]
    public async Task An_input_made_by_a_node_is_left_out_and_said() {
        var imported = await ImportAsync("tile.mtlx");

        Assert.Contains(imported.Warnings, static w => w.Contains("'noise2d'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_file_that_is_no_material_file_is_refused() {
        File.WriteAllText(Path.Combine(source.FullName, "notes.txt"), "no");
        using var response = await client.PostAsJsonAsync(
            "/api/v1/library/project/materials",
            new MaterialImportRequest("walls", Path.Combine(source.FullName, "notes.txt")),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
