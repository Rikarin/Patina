using Patina.Contracts;
using Patina.Library;
using System.Globalization;

namespace Patina.Jobs.Tests;

/// <summary>What a bake's key does and does not depend on, which is what the cache's honesty rests on.</summary>
public sealed class BakeInputsTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-bake-");

    public void Dispose() => folder.Delete(true);

    string File(string name, string content) {
        var path = Path.Combine(folder.FullName, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    BakeRequest Request() =>
        new() { High = [File("high.glb", "high")], Low = File("low.glb", "low"), Output = folder.FullName };

    static string Key(BakeRequest request) => BakeInputs.KeyOf(request, ToolCatalog.Default).Hex;

    static BakeRequest With(BakeRequest request, Func<BakeOptions, BakeOptions> change) =>
        request with { Options = change(request.Options) };

    /// <summary>The file's content is what counts, so moving a mesh does not rebake it.</summary>
    [Fact]
    public void Renaming_a_mesh_or_moving_the_output_keeps_the_key() {
        var request = Request();
        var moved = request with { High = [File("renamed.glb", "high")], Output = "/elsewhere", Name = "other" };

        Assert.Equal(Key(request), Key(moved));
    }

    [Fact]
    public void Changing_a_mesh_changes_the_key() {
        var request = Request();

        Assert.NotEqual(Key(request), Key(request with { High = [File("edited.glb", "high!")] }));
    }

    /// <summary>A role is part of a file's meaning: the same two files the other way round is another bake.</summary>
    [Fact]
    public void Swapping_the_high_and_the_low_changes_the_key() {
        var request = Request();

        Assert.NotEqual(Key(request), Key(request with { High = [request.Low], Low = request.High[0] }));
    }

    /// <summary>A second high mesh is an input, and the order of the high meshes is part of the bake.</summary>
    [Fact]
    public void A_second_high_mesh_and_the_order_of_the_high_meshes_change_the_key() {
        var request = Request();
        var second = File("second.glb", "second");
        var both = request with { High = [request.High[0], second] };

        Assert.NotEqual(Key(request), Key(both));
        Assert.NotEqual(Key(both), Key(request with { High = [second, request.High[0]] }));
    }

    /// <summary>Every setting that changes a pixel has to reach the key, or the cache serves the wrong bake.</summary>
    [Theory]
    [InlineData("resolution")]
    [InlineData("maps")]
    [InlineData("format")]
    [InlineData("supersampling")]
    [InlineData("gutter")]
    [InlineData("space")]
    [InlineData("front")]
    [InlineData("rear")]
    [InlineData("units")]
    [InlineData("backfaces")]
    [InlineData("skew")]
    [InlineData("match")]
    [InlineData("suffix")]
    [InlineData("rays")]
    [InlineData("reach")]
    [InlineData("spread")]
    [InlineData("falloff")]
    [InlineData("distribution")]
    [InlineData("minimum")]
    [InlineData("ao-backfaces")]
    [InlineData("within-part")]
    [InlineData("ground")]
    [InlineData("ground-offset")]
    [InlineData("thickness")]
    [InlineData("height")]
    [InlineData("id-source")]
    [InlineData("id-palette")]
    [InlineData("rounding")]
    [InlineData("rounding-width")]
    [InlineData("rounding-edges")]
    [InlineData("colour-source")]
    [InlineData("edges-mesh")]
    [InlineData("borders-width")]
    [InlineData("diffusion")]
    public void Each_setting_is_in_the_key(string setting) {
        var request = Request();
        var changed = With(
            request,
            options => setting switch {
                "resolution" => options with { Resolution = 1024 },
                "maps" => options with { Maps = ["ao"] },
                "format" => options with { Format = BakeInputs.Exr32 },
                "supersampling" => options with { Supersampling = 2 },
                "gutter" => options with { Gutter = 8 },
                "space" => options with { Space = "object" },
                "front" => options with { Rays = options.Rays with { Front = 0.1 } },
                "rear" => options with { Rays = options.Rays with { Rear = 0.1 } },
                "units" => options with { Rays = options.Rays with { Units = "absolute" } },
                "backfaces" => options with { Rays = options.Rays with { IgnoreBackfaces = true } },
                "skew" => options with { Rays = options.Rays with { Skew = 0.5 } },
                "match" => options with { Rays = options.Rays with { MatchByName = true } },
                "suffix" => options with { Rays = options.Rays with { LowSuffix = "_lp" } },
                "rays" => options with { Occlusion = options.Occlusion with { Rays = 8 } },
                "reach" => options with { Occlusion = options.Occlusion with { Distance = 0.1 } },
                "spread" => options with { Occlusion = options.Occlusion with { Spread = 45 } },
                "falloff" => options with { Occlusion = options.Occlusion with { Falloff = "smooth" } },
                "distribution" => options with { Occlusion = options.Occlusion with { Distribution = "uniform" } },
                "minimum" => options with { Occlusion = options.Occlusion with { Minimum = 0.01 } },
                "ao-backfaces" => options with { Occlusion = options.Occlusion with { IgnoreBackfaces = true } },
                "within-part" => options with { Occlusion = options.Occlusion with { WithinPart = true } },
                "ground" => options with { Occlusion = options.Occlusion with { GroundPlane = true } },
                "ground-offset" => options with { Occlusion = options.Occlusion with { GroundOffset = 0.2 } },
                "thickness" => options with { Thickness = new() { Normalised = false } },
                "height" => options with { Height = new() { Range = "rays" } },
                "id-source" => options with { Id = options.Id with { Source = "mesh" } },
                "rounding" => options with { Rounding = options.Rounding with { Enabled = true } },
                "rounding-width" => options with { Rounding = options.Rounding with { Width = 0.5 } },
                "rounding-edges" => options with { Rounding = options.Rounding with { Edges = "hard" } },
                "colour-source" => options with { Colour = options.Colour with { Source = "material" } },
                "edges-mesh" => options with { Edges = options.Edges with { Mesh = "low" } },
                "borders-width" => options with { Borders = new() { Width = 0.1 } },
                "diffusion" => options with { Diffusion = true },
                _ => options with { Id = options.Id with { Palette = "grey" } }
            }
        );

        Assert.NotEqual(Key(request), Key(changed));
    }

    [Fact]
    public void The_order_maps_are_asked_for_in_does_not_change_the_key() {
        var request = Request();

        Assert.Equal(
            Key(With(request, options => options with { Maps = ["ao", "id"] })),
            Key(With(request, options => options with { Maps = ["id", "ao"] }))
        );
    }

    [Fact]
    public void A_cage_is_an_input() {
        var request = Request();

        Assert.NotEqual(Key(request), Key(request with { Cage = File("cage.glb", "cage") }));
    }

    /// <summary>The skew map's bytes are in the key, and where it lies is not.</summary>
    [Fact]
    public void A_skew_map_is_an_input_by_its_content() {
        var request = Request();
        var one = With(request, options => options with { Rays = options.Rays with { SkewMap = File("a.png", "a") } });
        var moved = With(
            request,
            options => options with { Rays = options.Rays with { SkewMap = File("b.png", "a") } }
        );
        var other = With(
            request,
            options => options with { Rays = options.Rays with { SkewMap = File("c.png", "c") } }
        );

        Assert.NotEqual(Key(request), Key(one));
        Assert.Equal(Key(one), Key(moved));
        Assert.NotEqual(Key(one), Key(other));
    }

    /// <summary>
    ///     The distance map and the picture a colour map carries are inputs by their bytes, as the skew map
    ///     is.
    /// </summary>
    [Theory]
    [InlineData("distance")]
    [InlineData("colour")]
    public void A_distance_map_and_a_carried_picture_are_inputs_by_their_content(string which) {
        var request = Request();

        BakeRequest Using(string path) =>
            With(
                request,
                options => which == "distance"
                    ? options with { Rays = options.Rays with { DistanceMap = path } }
                    : options with { Maps = ["colour"], Colour = new() { Source = "texture", Texture = path } }
            );

        Assert.Equal(Key(Using(File("a.png", "a"))), Key(Using(File("b.png", "a"))));
        Assert.NotEqual(Key(Using(File("a.png", "a"))), Key(Using(File("c.png", "c"))));
    }

    [Theory]
    [InlineData(8, "png16", "tangent")]
    [InlineData(2048, "tga", "tangent")]
    [InlineData(2048, "png16", "sideways")]
    public void A_bake_no_baker_can_do_is_refused(int resolution, string format, string space) {
        var request = With(
            Request(),
            options => options with { Resolution = resolution, Format = format, Space = space }
        );

        Assert.Throws<BakeRefusedException>(() => BakeInputs.Check(request));
    }

    [Theory]
    [InlineData("rays")]
    [InlineData("units")]
    [InlineData("spread")]
    [InlineData("falloff")]
    [InlineData("height")]
    [InlineData("id")]
    [InlineData("no-maps")]
    [InlineData("rounding-width")]
    [InlineData("rounding-edges")]
    [InlineData("colour-source")]
    [InlineData("colour-texture")]
    [InlineData("distance-picture")]
    public void A_map_setting_out_of_range_is_refused(string setting) {
        var request = With(
            Request(),
            options => setting switch {
                "rays" => options with { Rays = options.Rays with { Front = 0, Rear = 0 } },
                "units" => options with { Rays = options.Rays with { Units = "metres" } },
                "spread" => options with { Occlusion = options.Occlusion with { Spread = 120 } },
                "falloff" => options with { Occlusion = options.Occlusion with { Falloff = "cubic" } },
                "height" => options with { Height = new() { Range = "custom", Scale = 0 } },
                "id" => options with { Id = options.Id with { Source = "uv" } },
                "rounding-width" => options with { Rounding = new() { Enabled = true, Width = 0 } },
                "rounding-edges" => options with { Rounding = new() { Edges = "weights" } },
                "colour-source" => options with { Colour = new() { Source = "paint" } },
                "colour-texture" => options with { Maps = ["colour"], Colour = new() { Source = "texture" } },
                "distance-picture" => options with { Rays = options.Rays with { DistanceMap = "distance.txt" } },
                _ => options with { Maps = [] }
            }
        );

        Assert.Throws<BakeRefusedException>(() => BakeInputs.Check(request));
    }

    /// <summary>
    ///     A minimum occluder distance at or past the occlusion distance starts every ray past its reach, which bakes
    ///     white occlusion and a bent normal that is the normal, so it is refused with a code (#228). Thickness reads
    ///     the distance only, so it may bake with one.
    /// </summary>
    [Theory]
    [InlineData("ao", 0.5, 0.5)]
    [InlineData("ao", 0.75, 0.5)]
    [InlineData("ao", 0, 0)]
    [InlineData("bent", 0.5, 0.25)]
    public void A_minimum_occluder_distance_at_or_past_the_distance_is_refused_with_a_code(
        string map,
        double minimum,
        double distance
    ) {
        var refusal = Assert.Throws<BakeRefusedException>(() =>
            BakeInputs.Check(
                With(
                    Request(),
                    options => options with {
                        Maps = ["normal", map],
                        Occlusion = options.Occlusion with { Minimum = minimum, Distance = distance }
                    }
                )
            )
        );

        Assert.Equal(BakeOptions.MinimumPastDistance, refusal.Code);
        Assert.Equal(minimum.ToString(CultureInfo.InvariantCulture), refusal.Values["minimum"]);
        Assert.Equal(distance.ToString(CultureInfo.InvariantCulture), refusal.Values["distance"]);
        Assert.Null(
            With(
                    Request(),
                    options => options with {
                        Maps = ["normal", "thickness"],
                        Occlusion = options.Occlusion with { Minimum = minimum, Distance = distance }
                    }
                )
                .Options.Problem()
        );
    }

    [Fact]
    public void An_unknown_map_is_refused_and_says_what_the_maps_are() {
        var refusal = Assert.Throws<BakeRefusedException>(() =>
            BakeInputs.Check(With(Request(), options => options with { Maps = ["occlusion"] }))
        );

        Assert.Contains("ao", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>A height outside what a bake makes, or a position range that is not one, is refused by name.</summary>
    [Fact]
    public void A_height_or_a_position_range_that_cannot_be_baked_is_refused() {
        Assert.Contains(
            "height",
            With(Request(), static options => options with { Rows = 8 }).Options.Problem(),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "position range",
            With(Request(), static options => options with { Position = new() { Range = "ball" } }).Options.Problem(),
            StringComparison.Ordinal
        );
        Assert.Null(
            With(Request(), static options => options with { Rows = 512, Position = new() { Range = "sphere" } })
                .Options.Problem()
        );
    }

    /// <summary>A client that leaves the options out, or a part of them, bakes with the defaults.</summary>
    [Fact]
    public void A_request_that_leaves_options_out_takes_the_defaults() {
        var request = System.Text.Json.JsonSerializer.Deserialize(
            """{ "high": ["a.glb"], "low": "b.glb", "output": "out", "options": { "rays": { "front": 0.2 } } }""",
            ContractsJsonContext.Default.BakeRequest
        )!;

        Assert.Equal(0.2, request.Options.Rays.Front);
        Assert.Equal(0.05, request.Options.Rays.Rear);
        Assert.NotNull(request.Options.Occlusion);
        Assert.Equal(new BakeOptions().Maps, request.Options.Maps);
        Assert.Null(request.Options.Problem());
    }

    [Fact]
    public void A_missing_file_is_named() {
        var refusal = Assert.Throws<FileNotFoundException>(() =>
            BakeInputs.Check(Request() with { High = ["/nowhere.glb"] })
        );

        Assert.Contains("/nowhere.glb", refusal.Message, StringComparison.Ordinal);
    }
}
