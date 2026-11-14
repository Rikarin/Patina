using Patina.Bench;
using Patina.Contracts;
using Patina.Jobs;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>
///     The opacity, edges and UV islands maps, a rounded edge's profile and a custom curvature range (#148, #214,
///     #121), each from files to files on the test housing's low mesh baked onto itself.
/// </summary>
public sealed class BakeMapKindsTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-map-kinds-");

    public void Dispose() => folder.Delete(true);

    string Into(string name) => Path.Combine(folder.FullName, name);

    BakeManifest Bake(string into, Func<BakeOptions, BakeOptions> change, bool high = false) {
        BakeFixture.Write(folder.FullName, 0.02f);
        var low = Into(BakeFixture.LowFile);
        var request = new BakeRequest {
            High = [high ? Into(BakeFixture.HighFile) : low],
            Low = low,
            Output = Into("out"),
            Options = change(new() { Resolution = 128, Format = BakeInputs.Png8, Gutter = 0 })
        };
        return MeshMapBakeRun.Run(request, Into(into), 0, null, TestContext.Current.CancellationToken);
    }

    Bitmap Picture(string into, string map) =>
        PngCodec.Decode(File.ReadAllBytes(Path.Combine(Into(into), $"{map}.png")));

    /// <summary>The red byte of every texel a chart covers, as the islands map says: its background is black.</summary>
    static List<int> Covered(Bitmap picture, Bitmap islands) {
        List<int> values = [];
        for (var at = 0; at < islands.Pixels.Length; at += 4) {
            if (islands.Pixels[at] + islands.Pixels[at + 1] + islands.Pixels[at + 2] > 0) {
                values.Add(picture.Pixels[at]);
            }
        }

        return values;
    }

    /// <summary>
    ///     The three maps are written under their names. The low mesh baked onto itself has an edges map white along
    ///     its hard edges and black in the middle of its faces, and its islands are several colours. The housing's high
    ///     mesh lies over all of the low one, so a bake from it is opaque nearly everywhere a chart is.
    /// </summary>
    [Fact]
    public void Opacity_edges_and_islands_are_written_and_read_as_worked_out() {
        var manifest = Bake("maps", static options => options with { Maps = ["normal", "edges", "islands"] });
        Bake("opacity", static options => options with { Maps = ["opacity", "islands"] }, true);
        Assert.Equal(["normal", "edges", "islands"], manifest.Result.Maps.Select(static map => map.Map).ToArray());

        var islands = Picture("maps", "islands");
        var opacity = Covered(Picture("opacity", "opacity"), Picture("opacity", "islands"));
        var edges = Covered(Picture("maps", "edges"), islands);

        Assert.NotEmpty(opacity);
        Assert.True(opacity.Count(static value => value == 255) > opacity.Count * 0.95, "Rays missed the high mesh.");
        Assert.Contains(edges, static value => value > 200);
        Assert.Contains(edges, static value => value == 0);

        HashSet<int> colours = [];
        for (var at = 0; at < islands.Pixels.Length; at += 4) {
            colours.Add(islands.Pixels[at] << 16 | islands.Pixels[at + 1] << 8 | islands.Pixels[at + 2]);
        }

        Assert.True(colours.Count > 2, $"{colours.Count} colours: the background and one island at most.");
    }

    /// <summary>
    ///     The low mesh baked onto itself is struck everywhere: its rays start on the surface they strike, and they used
    ///     to meet it at zero distance, which did not count, so opacity read black and every texel was missed (#278).
    /// </summary>
    [Fact]
    public void The_low_mesh_baked_onto_itself_is_opaque_everywhere_it_is_charted() {
        var manifest = Bake("self", static options => options with { Maps = ["opacity", "islands"] });
        var opacity = Covered(Picture("self", "opacity"), Picture("self", "islands"));

        // Only the outline misses: a texel a chart claims past its triangle's edge stands off the mesh, at a corner.
        Assert.NotEmpty(opacity);
        Assert.True(opacity.Count(static value => value == 255) > opacity.Count * 0.98, "Rays missed the low mesh.");
        Assert.True(manifest.Result.Covered > 0);
        Assert.True(manifest.Result.Missed * 50 < manifest.Result.Covered, $"{manifest.Result.Missed} texels missed.");
    }

    /// <summary>
    ///     The edges map marks the low mesh's edges when asked, measured at the texel, so a bake from the high mesh
    ///     marks exactly what the low mesh baked onto itself does, and the high mesh's own edges are another map (#278).
    /// </summary>
    [Fact]
    public void The_edges_map_can_mark_the_low_meshs_edges_under_a_high_mesh() {
        static Func<BakeOptions, BakeOptions> With(string mesh) =>
            options => options with {
                Maps = ["edges"],
                Rays = options.Rays with { Units = "absolute", Front = 0.2, Rear = 0.2 },
                Edges = new() { Edges = "angle", Width = 0.05, Mesh = mesh }
            };

        Bake("self", With("low"));
        Bake("low", With("low"), true);
        Bake("high", With("high"), true);
        var self = Picture("self", "edges").Pixels;

        Assert.Equal(self, Picture("low", "edges").Pixels);
        Assert.NotEqual(self, Picture("high", "edges").Pixels);
        Assert.Contains("high or low", new BakeOptions { Edges = new() { Mesh = "both" } }.Problem());
    }

    /// <summary>
    ///     A chamfered band is white all the way across, so its edges map holds no grey; an even one ramps down through
    ///     the greys. The rounded normal turns differently too.
    /// </summary>
    [Fact]
    public void The_profile_reaches_the_edges_map_and_the_rounded_normal() {
        static BakeOptions With(BakeOptions options, string profile) =>
            options with {
                Maps = ["normal", "edges", "islands"],
                Edges = new() { Edges = "angle", Width = 0.05, Profile = profile },
                Rounding = new() { Enabled = true, Width = 0.05, Edges = "angle", Profile = profile }
            };

        Bake("even", static options => With(options, "even"));
        Bake("chamfer", static options => With(options, "chamfer"));
        var normal = Picture("even", "normal");
        var even = Covered(Picture("even", "edges"), Picture("even", "islands"));
        var chamfer = Covered(Picture("chamfer", "edges"), Picture("chamfer", "islands"));

        Assert.All(chamfer, static value => Assert.True(value is 0 or 255, $"A chamfer read {value}."));
        Assert.Contains(even, static value => value is > 20 and < 235);
        Assert.NotEqual(normal.Pixels, Picture("chamfer", "normal").Pixels);
    }

    /// <summary>
    ///     A custom curvature range stores the map with one over the radius as its scale, in model units: a share of
    ///     the high mesh's diagonal, here one in a hundred of it.
    /// </summary>
    [Fact]
    public void A_custom_curvature_range_is_one_over_its_radius() {
        var auto = Bake("auto", static options => options with { Maps = ["curvature"] });
        var custom = Bake(
            "custom",
            static options => options with {
                Maps = ["curvature"], Curvature = new() { Range = "custom", Radius = 0.01 }
            }
        );
        var low = MeshMapBakeRun.Read(Into(BakeFixture.LowFile)).Mesh.Bounds;
        var diagonal = Vixen.Core.Mathematics.Vector3.Distance(low.Minimum, low.Maximum);

        var scale = Assert.Single(custom.Result.Maps).Scale;
        Assert.Equal(1 / (0.01 * diagonal), scale, 1e-3 * scale);
        Assert.NotEqual(Assert.Single(auto.Result.Maps).Scale, scale);
    }

    /// <summary>The options refuse a profile, a curvature range and an edges width that are not ones.</summary>
    [Fact]
    public void Profiles_ranges_and_widths_that_are_not_ones_are_refused() {
        Assert.Contains("profile", new BakeOptions { Rounding = new() { Profile = "bevel" } }.Problem());
        Assert.Contains("profile", new BakeOptions { Edges = new() { Profile = "bevel" } }.Problem());
        Assert.Contains("width", new BakeOptions { Edges = new() { Width = 0 } }.Problem());
        Assert.Contains("curvature", new BakeOptions { Curvature = new() { Range = "loose" } }.Problem());
        Assert.Contains("radius", new BakeOptions { Curvature = new() { Range = "custom", Radius = 0 } }.Problem());
        Assert.Null(new BakeOptions { Maps = ["opacity", "edges", "islands"] }.Problem());
    }
}
