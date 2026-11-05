using Patina.Contracts;
using Patina.Library;

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

    BakeRequest Request() => new(File("high.glb", "high"), File("low.glb", "low"), folder.FullName);

    static string Key(BakeRequest request) => BakeInputs.KeyOf(request, ToolCatalog.Default).Hex;

    /// <summary>The file's content is what counts, so moving a mesh does not rebake it.</summary>
    [Fact]
    public void Renaming_a_mesh_or_moving_the_output_keeps_the_key() {
        var request = Request();
        var moved = request with { High = File("renamed.glb", "high"), Output = "/elsewhere", Name = "other" };

        Assert.Equal(Key(request), Key(moved));
    }

    [Fact]
    public void Changing_a_mesh_changes_the_key() {
        var request = Request();

        Assert.NotEqual(Key(request), Key(request with { High = File("edited.glb", "high!") }));
    }

    /// <summary>A role is part of a file's meaning: the same two files the other way round is another bake.</summary>
    [Fact]
    public void Swapping_the_high_and_the_low_changes_the_key() {
        var request = Request();

        Assert.NotEqual(Key(request), Key(request with { High = request.Low, Low = request.High }));
    }

    /// <summary>Every setting that changes a pixel has to reach the key, or the cache serves the wrong bake.</summary>
    [Theory]
    [InlineData("resolution")]
    [InlineData("maps")]
    [InlineData("format")]
    [InlineData("supersampling")]
    [InlineData("rays")]
    [InlineData("search")]
    [InlineData("reach")]
    [InlineData("skew")]
    [InlineData("match")]
    [InlineData("gutter")]
    [InlineData("space")]
    public void Each_setting_is_in_the_key(string setting) {
        var request = Request();
        var changed = setting switch {
            "resolution" => request with { Resolution = 1024 },
            "maps" => request with { Maps = ["ao"] },
            "format" => request with { Format = BakeInputs.Exr32 },
            "supersampling" => request with { Supersampling = 2 },
            "rays" => request with { OcclusionRays = 8 },
            "search" => request with { SearchRadius = 0.1 },
            "reach" => request with { OcclusionRadius = 0.1 },
            "skew" => request with { Skew = 0.5 },
            "match" => request with { MatchByName = true },
            "gutter" => request with { Gutter = 8 },
            _ => request with { Space = "object" }
        };

        Assert.NotEqual(Key(request), Key(changed));
    }

    [Fact]
    public void The_order_maps_are_asked_for_in_does_not_change_the_key() {
        var request = Request();

        Assert.Equal(Key(request with { Maps = ["ao", "id"] }), Key(request with { Maps = ["id", "ao"] }));
    }

    [Fact]
    public void A_cage_is_an_input() {
        var request = Request();

        Assert.NotEqual(Key(request), Key(request with { Cage = File("cage.glb", "cage") }));
    }

    [Theory]
    [InlineData(8, "png16", "tangent")]
    [InlineData(2048, "tga", "tangent")]
    [InlineData(2048, "png16", "sideways")]
    public void A_bake_no_baker_can_do_is_refused(int resolution, string format, string space) {
        var request = Request() with { Resolution = resolution, Format = format, Space = space };

        Assert.Throws<BakeRefusedException>(() => BakeInputs.Check(request));
    }

    [Fact]
    public void An_unknown_map_is_refused_and_says_what_the_maps_are() {
        var refusal = Assert.Throws<BakeRefusedException>(() => BakeInputs.Check(Request() with { Maps = ["occlusion"] }));

        Assert.Contains("ao", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_file_is_named() {
        var refusal = Assert.Throws<FileNotFoundException>(() => BakeInputs.Check(Request() with { High = "/nowhere.glb" }));

        Assert.Contains("/nowhere.glb", refusal.Message, StringComparison.Ordinal);
    }
}
