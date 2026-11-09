using Patina.Bench;
using Patina.Contracts;
using Patina.Jobs;
using Patina.Library;
using System.Globalization;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Export.Tests;

/// <summary>
///     The colour map and a picture moved between layouts (#45), rounded edges (#214), the distance map (#150) and the
///     per-set key a mesh change keeps (#151), each from files to files.
/// </summary>
public sealed class BakeCorrectionTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-corrections-");

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    /// <summary>A unit quad, its corners at the given coordinates and, when asked, all one vertex colour.</summary>
    string Quad(string name, float lift, (float U, float V)[] uvs, float? grey = null) {
        static string Number(float value) => value.ToString(CultureInfo.InvariantCulture);

        var colour = grey is { } g ? $" {Number(g)} {Number(g)} {Number(g)}" : string.Empty;
        var path = Into(name);
        File.WriteAllText(
            path,
            $"""
             v 0 0 {Number(lift)}{colour}
             v 1 0 {Number(lift)}{colour}
             v 1 1 {Number(lift)}{colour}
             v 0 1 {Number(lift)}{colour}
             {string.Join('\n', uvs.Select(uv => $"vt {Number(uv.U)} {Number(uv.V)}"))}
             vn 0 0 1
             f 1/1/1 2/2/1 3/3/1
             f 1/1/1 3/3/1 4/4/1

             """
        );
        return path;
    }

    static readonly (float, float)[] Straight = [(0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f)];

    /// <summary>The same quad laid out mirrored left to right inside the square.</summary>
    static readonly (float, float)[] Mirrored = [(0.9f, 0.1f), (0.1f, 0.1f), (0.1f, 0.9f), (0.9f, 0.9f)];

    BakeRequest Request(string high, string low, Func<BakeOptions, BakeOptions> change) =>
        new() {
            High = [high],
            Low = low,
            Output = Into("out"),
            Options = change(new() { Resolution = 64, Format = BakeInputs.Png8, Gutter = 0, Maps = ["colour"] })
        };

    BakeManifest Bake(BakeRequest request, string into = "bake") =>
        MeshMapBakeRun.Run(request, Into(into), 0, null, TestContext.Current.CancellationToken);

    /// <summary>The red byte of a written map at a pixel.</summary>
    static int Red(Bitmap picture, int x, int y) => picture.Pixels[(y * picture.Width + x) * 4];

    /// <summary>
    ///     A picture made for the high mesh's layout lands on the low mesh's, which here is the same quad mirrored: a
    ///     red ramp rising left to right falls left to right on the low mesh, and its numbers are carried, not
    ///     converted.
    /// </summary>
    [Fact]
    public void A_picture_moves_from_the_high_meshes_layout_to_the_low_meshes() {
        var high = Quad("high.obj", 0f, Straight);
        var low = Quad("low.obj", 0f, Mirrored);
        var ramp = new byte[64 * 64 * 4];
        for (var at = 0; at < 64 * 64; at++) {
            (ramp[at * 4], ramp[at * 4 + 3]) = ((byte)(at % 64 * 4), 255);
        }

        var picture = Into("ramp.png");
        File.WriteAllBytes(picture, PngCodec.Encode(new(64, 64, ramp)));

        var manifest = Bake(
            Request(high, low, options => options with { Colour = new() { Source = "texture", Texture = picture } })
        );
        var baked = PngCodec.Load(Path.Combine(Into("bake"), "colour.png"));

        Assert.Contains(manifest.Result.Maps, static map => map.Map == "colour");
        // The low's u of 0.15 is the high's 0.9375, and its 0.85 is the high's 0.0625: the ramp, the other way round.
        Assert.InRange(Red(baked, 9, 32), 225, 245);
        Assert.InRange(Red(baked, 54, 32), 10, 30);
    }

    /// <summary>Vertex colours are carried in linear light and written in sRGB, as a base colour is stored.</summary>
    [Fact]
    public void Vertex_colours_are_written_in_srgb() {
        var high = Quad("high.obj", 0f, Straight, 0.2f);
        var low = Quad("low.obj", 0f, Straight);

        Bake(Request(high, low, static options => options with { Colour = new() { Source = "vertex-colour" } }));
        var baked = PngCodec.Load(Path.Combine(Into("bake"), "colour.png"));

        // The sRGB encoding of a linear 0.2 is 0.4845, which is 124 of 255; written as it is, it would be 51.
        Assert.InRange(Red(baked, 32, 32), 122, 126);
    }

    /// <summary>Asked for colour with nothing to carry, the map is white and the bake says why.</summary>
    [Fact]
    public void Vertex_colours_a_mesh_does_not_have_bake_white_with_a_warning() {
        var high = Quad("high.obj", 0f, Straight);
        var low = Quad("low.obj", 0f, Straight);

        var manifest = Bake(
            Request(high, low, static options => options with { Colour = new() { Source = "vertex-colour" } })
        );

        Assert.Contains(
            manifest.Result.Warnings,
            static warning => warning.Contains("vertex colours", StringComparison.Ordinal)
        );
        Assert.Equal(255, Red(PngCodec.Load(Path.Combine(Into("bake"), "colour.png")), 32, 32));
    }

    /// <summary>
    ///     A material's colour bakes when the high mesh has no vertex colours, which is what auto falls
    ///     to.
    /// </summary>
    [Fact]
    public void Auto_colour_falls_to_the_materials() {
        // A GLB, because an OBJ read from its bytes cannot find the material library beside it.
        static MeshData Half(string name, float left, int material) =>
            new() {
                Name = name,
                Positions = [new(left, 0, 0), new(left + 1, 0, 0), new(left + 1, 1, 0), new(left, 1, 0)],
                Normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
                TexCoords = [
                    new(left / 2 + 0.5f, 0), new(left / 2 + 1, 0), new(left / 2 + 1, 1), new(left / 2 + 0.5f, 1)
                ],
                Indices = [0, 1, 2, 0, 2, 3],
                MaterialIndex = material,
                Bounds = new(new(left, 0, 0), new(left + 1, 1, 0))
            };

        var high = Into("two.glb");
        ModelWriter.Write(
            high,
            [Half("Body", -1, 0), Half("Strap", 0, 1)],
            "two",
            [
                new("Body", ModelMaterialFactors.Default with { BaseColor = new(1, 0, 0, 1) }),
                new("Strap", ModelMaterialFactors.Default with { BaseColor = new(0, 0, 1, 1) })
            ]
        );

        Bake(Request(high, high, static options => options));
        var baked = PngCodec.Load(Path.Combine(Into("bake"), "colour.png"));

        // Body is red and has the left half of the atlas, Strap is blue and has the right.
        Assert.Equal(255, Red(baked, 16, 32));
        Assert.Equal(0, Red(baked, 48, 32));
        Assert.Equal(255, baked.Pixels[(32 * 64 + 48) * 4 + 2]);
    }

    /// <summary>A bake of the hard-edged pair onto its low mesh, with or without its edges rounded.</summary>
    byte[] Normals(bool rounded, string into) {
        BakeFixture.Write(folder.FullName, 0.02f);
        var low = Into(BakeFixture.LowFile);
        Bake(
            Request(
                low,
                low,
                options => options with {
                    Maps = ["normal"],
                    Resolution = 128,
                    Rounding = new() { Enabled = rounded, Width = 0.05, Edges = "angle", Angle = 30 }
                }
            ),
            into
        );
        return File.ReadAllBytes(Path.Combine(Into(into), "normal.png"));
    }

    /// <summary>
    ///     Rounding reaches the bake: the low mesh baked onto itself is flat without it and bevelled with
    ///     it.
    /// </summary>
    [Fact]
    public void Rounded_edges_reach_the_bake_of_a_low_mesh_onto_itself() {
        var flat = PngCodec.Decode(Normals(false, "flat"));
        var round = PngCodec.Decode(Normals(true, "round"));
        var differ = 0;
        for (var at = 0; at < flat.Pixels.Length; at += 4) {
            differ += flat.Pixels[at] != round.Pixels[at] || flat.Pixels[at + 1] != round.Pixels[at + 1] ? 1 : 0;
        }

        Assert.True(differ > 100, $"{differ} texels changed.");
        Assert.True(differ < flat.Pixels.Length / 4 / 2, $"{differ} texels changed: the bevel reached everywhere.");
    }

    /// <summary>
    ///     A distance map that is not square has no texel for every texel of the atlas, and is refused by
    ///     name.
    /// </summary>
    [Fact]
    public void A_distance_map_that_is_not_square_is_refused_by_name() {
        var quad = Quad("quad.obj", 0f, Straight);
        var map = Into("distance.exr");
        File.WriteAllBytes(map, ExrCodec.Encode(new(2, 1, 1, [0.5f, 0.5f])));

        var refused = Assert.Throws<InvalidDataException>(() =>
            Bake(
                Request(
                    quad,
                    quad,
                    options => options with { Maps = ["normal"], Rays = options.Rays with { DistanceMap = map } }
                )
            )
        );

        Assert.Contains("distance map", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The two-material low mesh with its strap lifted, so one texture set changed and the other did
    ///     not.
    /// </summary>
    string TwoSets(string name, float strap, string body = "1 0 0") {
        var path = Into(name);
        var lifted = strap.ToString(CultureInfo.InvariantCulture);
        File.WriteAllText(Path.ChangeExtension(path, ".mtl"), $"newmtl Body\nKd {body}\nnewmtl Strap\nKd 0 0 1\n");
        File.WriteAllText(
            path,
            $"""
             mtllib {Path.GetFileName(Path.ChangeExtension(path, ".mtl"))}
             v -1 -1 0
             v 0 -1 0
             v 0 1 0
             v -1 1 0
             v 0 -1 {lifted}
             v 1 -1 {lifted}
             v 1 1 {lifted}
             v 0 1 {lifted}
             vt 0 0
             vt 0.5 0
             vt 0.5 1
             vt 0 1
             vt 0.5 0
             vt 1 0
             vt 1 1
             vt 0.5 1
             vn 0 0 1
             usemtl Body
             f 1/1/1 2/2/1 3/3/1
             f 1/1/1 3/3/1 4/4/1
             usemtl Strap
             f 5/5/1 6/6/1 7/7/1
             f 5/5/1 7/7/1 8/8/1

             """
        );
        return path;
    }

    string Key(string low, string set, bool bytes = false) {
        var high = Quad("high.obj", 0f, Straight);
        var request = Request(high, low, static options => options with { Maps = ["normal"] }) with { Material = set };
        return (bytes
                ? BakeInputs.KeyOf(request, ToolCatalog.Default)
                : BakeKeyReads.KeyOf(request, ToolCatalog.Default))
            .Hex;
    }

    /// <summary>
    ///     A change to one texture set's faces changes that set's key and keeps the other's, so a rebake after it bakes
    ///     the one set and takes the other from the cache. Read as bytes, the same change rebaked both.
    /// </summary>
    [Fact]
    public void A_mesh_change_rebakes_only_the_texture_set_it_touched() {
        var before = TwoSets("before.obj", 0f);
        var after = TwoSets("after.obj", 0.25f);

        Assert.Equal(Key(before, "Body"), Key(after, "Body"));
        Assert.NotEqual(Key(before, "Strap"), Key(after, "Strap"));
        Assert.NotEqual(Key(before, "Body", true), Key(after, "Body", true));
    }

    /// <summary>A low mesh's material colour is read by no map of it, so changing it rebakes nothing.</summary>
    [Fact]
    public void A_recoloured_material_on_the_low_mesh_rebakes_nothing() {
        var red = TwoSets("red.obj", 0f);
        var green = TwoSets("green.obj", 0f, "0 1 0");

        Assert.Equal(Key(red, "Body"), Key(green, "Body"));
        Assert.Equal(Key(red, "Strap"), Key(green, "Strap"));
    }

    /// <summary>
    ///     A low mesh that is its own high mesh is struck by its own rays, so its bytes decide, as
    ///     before.
    /// </summary>
    [Fact]
    public void A_low_mesh_that_is_also_the_high_mesh_is_keyed_by_its_bytes() {
        var before = TwoSets("before.obj", 0f);
        var after = TwoSets("after.obj", 0.25f);

        BakeRequest Own(string low) =>
            Request(low, low, static options => options with { Maps = ["normal"] }) with { Material = "Body" };

        Assert.NotEqual(
            BakeKeyReads.KeyOf(Own(before), ToolCatalog.Default).Hex,
            BakeKeyReads.KeyOf(Own(after), ToolCatalog.Default).Hex
        );
    }

    /// <summary>
    ///     A bake taken from the cache says it was made on the mesh as it is now, so it is not out of
    ///     date.
    /// </summary>
    [Fact]
    public void A_cached_bake_delivered_onto_a_changed_mesh_names_the_mesh_as_it_is() {
        var high = Quad("high.obj", 0f, Straight);
        var before = TwoSets("before.obj", 0f);
        var request = Request(high, before, static options => options with { Maps = ["normal"] }) with {
            Material = "Body"
        };
        var cache = new BakeCache(Into("cache"));
        var key = BakeKeyReads.KeyOf(request, ToolCatalog.Default).Hex;
        var manifest = MeshMapBakeRun.Run(request, cache.FolderOf(key), 0, null, TestContext.Current.CancellationToken);

        File.WriteAllText(
            before,
            File.ReadAllText(before).Replace("v 1 -1 0", "v 1 -1 0.25", StringComparison.Ordinal)
        );
        Assert.Equal(key, BakeKeyReads.KeyOf(request, ToolCatalog.Default).Hex);
        cache.Deliver(key, manifest, request, true);

        var written = BakeCache.ReadManifestFile(Path.Combine(request.Output, $"before.{BakeCache.ManifestFile}"));
        Assert.Equal(LibraryFiles.Digest(before), written!.Mesh);
    }
}
