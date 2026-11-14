using Vixen.Core.Yaml;
using Vixen.Graphics.Vulkan;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using static Patina.Content.Tests.CompoundBench;

namespace Patina.Content.Tests;

/// <summary>
///     Patina #118, #119, #120, #177 and #178: the generators that read the mesh, the weathering filters, the stylised
///     ones and the photo ones, each held to drawing something, to its seed, and to the picture it changes, over a
///     stand-in bake.
/// </summary>
public sealed class MeshCompoundTests {
    /// <summary>The compounds the issues ask for by name, each of which has to ship.</summary>
    public static IReadOnlyList<string> Named { get; } = [
        "Generators/Cavity Select",
        "Generators/Direction",
        "Generators/Distance From Point",
        "Generators/Drips",
        "Generators/Edge Select",
        "Generators/Fibrous Edge Wear",
        "Generators/Frayed Edges",
        "Generators/Ground Grime",
        "Generators/ID Mask",
        "Generators/Light",
        "Generators/Linear Gradient",
        "Generators/Moss",
        "Generators/Paint Chips",
        "Generators/Splashes",
        "Generators/Sun Fade",
        "Generators/Surface Scratches",
        "Generators/Thin Parts",
        "Generators/Water Line",
        "Generators/Wrinkles",
        "Stylised/Baked Lighting",
        "Photo/Clone Patch",
        "Photo/Fix Seams",
        "Photo/Textured Fill",
        "Stylised/Comic",
        "Stylised/Environment Lighting",
        "Stylised/Oil Paint",
        "Stylised/Watercolour",
        "Weathering/Edge Damage",
        "Weathering/Peeling Paint",
        "Weathering/Rust",
        "Weathering/Shut Lines",
        "Weathering/Water Drops",
        "Weathering/Weathered Rock",
        "Weathering/Worn Edges",
        "Weathering/Worn Fabric",
        "Weathering/Worn Leather",
        "Weathering/Worn Metal"
    ];

    [Fact]
    public void Every_named_compound_ships() {
        FactoryCompounds.Register();

        Assert.Subset(
            FactoryCompounds.Paths.ToHashSet(StringComparer.Ordinal),
            Named.ToHashSet(StringComparer.Ordinal)
        );
    }

    /// <summary>
    ///     Not flat, and a filter's picture is not its subject's: each changes what it is handed, and each generator
    ///     draws a mask with something in it over the stand-in bake.
    /// </summary>
    [Fact]
    public void Every_named_compound_draws_and_a_filter_changes_its_subject() {
        if (Device() is not { } device) {
            return;
        }

        using (device) {
            List<string> problems = [];

            foreach (var path in Named) {
                var (compiler, graph) = Containing(path, out _, out var subject);
                var pictures = Bake(device, compiler, graph);
                var picture = pictures["baseColor"];
                var spread = Spread(picture);

                if (spread < 0.05) {
                    problems.Add($"{path} is flat: its values span {spread:0.000}");
                }

                if (subject is not null && Moved(picture, pictures["roughness"]) < 0.01) {
                    problems.Add($"{path} left its subject as it was");
                }
            }

            Assert.Empty(problems);
        }
    }

    /// <summary>
    ///     Patina #120's seed on every generator: every factory compound that declares one draws another picture under
    ///     another.
    /// </summary>
    [Fact]
    public void A_seed_draws_another_picture() {
        if (Device() is not { } device) {
            return;
        }

        using (device) {
            var seeded = FactoryCompounds.Paths.Where(static path => Declares(path, "seed")).ToArray();
            List<string> problems = [];

            // The instrument: the weathering filters and the generators that break up declare one.
            Assert.Contains("Weathering/Rust", seeded);
            Assert.Contains("Generators/Drips", seeded);

            foreach (var path in seeded) {
                var first = Drawn(device, path, "0");
                var second = Drawn(device, path, "7");

                if (Moved(first, second) < 0.01) {
                    problems.Add($"{path} drew the same picture under seeds 0 and 7");
                }
            }

            Assert.Empty(problems);
        }
    }

    /// <summary>
    ///     A direction picks the faces turned towards it: the stand-in's upper half faces up, its lower half sideways.
    /// </summary>
    [Fact]
    public void Direction_picks_the_faces_turned_towards_it() {
        if (Device() is not { } device) {
            return;
        }

        using (device) {
            var (compiler, graph) = Containing("Generators/Direction", out var used, out _);
            var up = Bake(device, compiler, graph)["baseColor"];

            Assert.True(Mean(up, .., ..Upward) > 0.95, "An upward face is not picked by a direction up.");
            Assert.True(Mean(up, .., Upward..) < 0.05, "A sideways face is picked by a direction up.");

            used.SetText("across", "1");
            used.SetText("up", "0");

            var across = Bake(device, compiler, graph)["baseColor"];

            Assert.True(Mean(across, .., Upward..) > 0.95, "A sideways face is not picked by a direction across.");
            Assert.True(Mean(across, .., ..Upward) < 0.05, "An upward face is picked by a direction across.");
        }
    }

    /// <summary>
    ///     ⚠ The edge and cavity selections find a soft edge and a soft crease on a bake scaled by its sharpest
    ///     texel, as a real one is, and leave a flat alone although a flat ties most of the atlas at one value, which
    ///     ranks it half way up and within reach of an amount of one.
    /// </summary>
    [Theory]
    [InlineData("Generators/Edge Select", true, "0.3")]
    [InlineData("Generators/Edge Select", true, "1")]
    [InlineData("Generators/Cavity Select", false, "0.3")]
    [InlineData("Generators/Cavity Select", false, "1")]
    public void A_selection_finds_its_band_and_leaves_the_flat(string path, bool convex, string amount) {
        if (Device() is not { } device) {
            return;
        }

        using (device) {
            var (compiler, graph) = Containing(path, out var used, out _);
            used.SetText("amount", amount);
            var picture = Bake(device, compiler, graph)["baseColor"];
            var (own, other) = convex ? (Convex, Concave) : (Concave, Convex);
            var middle = new Range(own.Start.Value + 4, own.End.Value - 4);

            Assert.True(Mean(picture, middle, 8..^8) > 0.9, $"{path} misses its own band.");
            Assert.True(Mean(picture, other, 8..^8) < 0.02, $"{path} takes the other band.");
            Assert.True(Mean(picture, 100..120, 8..^8) < 0.02, $"{path} takes the flat.");
        }
    }

    /// <summary>
    ///     Wave 14's single generators pick what they say and leave the rest (#177): the stand-in's thickness climbs
    ///     from thin on the left to thick on the right, its position climbs up the picture, and its ID bake is red on
    ///     the left and blue on the right.
    /// </summary>
    [Theory]
    [InlineData("Generators/Thin Parts", "left")]
    [InlineData("Generators/Water Line", "bottom")]
    [InlineData("Generators/ID Mask", "left")]
    public void A_generator_picks_its_part_of_the_stand_in_and_leaves_the_rest(string path, string picked) {
        if (Device() is not { } device) {
            return;
        }

        using (device) {
            var (compiler, graph) = Containing(path, out _, out _);
            var picture = Bake(device, compiler, graph)["baseColor"];
            var (own, other) = picked == "left"
                ? (Mean(picture, ..(Side / 5), ..), Mean(picture, ^(Side / 5).., ..))
                : (Mean(picture, .., ^(Side / 8)..), Mean(picture, .., ..(Side / 2)));

            Assert.True(own > 0.3, $"{path} misses its own part: {own:0.000}");
            Assert.True(other < 0.02, $"{path} takes the rest: {other:0.000}");
        }
    }

    /// <summary>
    ///     Patina #120: fibrous wear keeps to the convex edges as the other wear does, and wrinkles gather in the
    ///     hollows, each far more on its own side of the stand-in's curvature than on the other.
    /// </summary>
    [Theory]
    [InlineData("Generators/Fibrous Edge Wear", true)]
    [InlineData("Generators/Wrinkles", false)]
    public void A_generator_keeps_to_its_side_of_the_curvature(string path, bool convex) {
        if (Device() is not { } device) {
            return;
        }

        using (device) {
            var (compiler, graph) = Containing(path, out _, out _);
            var picture = Bake(device, compiler, graph)["baseColor"];
            var (own, other) = convex ? (Convex, Concave) : (Concave, Convex);
            var (mine, theirs) = (Mean(picture, own, 8..^8), Mean(picture, other, 8..^8));

            Assert.True(mine > theirs * 2 + 0.05, $"{path} covers {mine:0.000} here, {theirs:0.000} there.");
        }
    }

    static Vixen.Core.Imaging.Bitmap Drawn(VulkanDevice device, string path, string seed) {
        var (compiler, graph) = Containing(path, out var used, out _);
        used.SetText("seed", seed);
        return Bake(device, compiler, graph)["baseColor"];
    }

    static bool Declares(string path, string parameter) =>
        TextureCompoundLibrary.Source(path) is { } text
        && YamlSerializer.Parse<NodeGraphAsset>(text).Parameters.Any(knob => knob.Name == parameter);

    static double Spread(Vixen.Core.Imaging.Bitmap picture) {
        var (lowest, highest) = (1.0, 0.0);

        for (var at = 0; at < picture.Width * picture.Height; at++) {
            var value = picture.Pixels[at * 4] / 255.0;
            lowest = Math.Min(lowest, value);
            highest = Math.Max(highest, value);
        }

        return highest - lowest;
    }

    static VulkanDevice? Device() {
        if (VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            return device;
        }

        Gpu.Skip($"No Vulkan device: {refusal}");
        return null;
    }
}
