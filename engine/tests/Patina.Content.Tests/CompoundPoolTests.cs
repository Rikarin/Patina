using System.Globalization;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Content.Tests;

/// <summary>
///     What each factory compound allocates at the largest graph size, read off Vixen's pool schedule with no device,
///     from a plan emitted for that bake as the Worker emits one.
/// </summary>
/// <remarks>
///     ⚠ <b>Never a small plan's pool scaled by the square of the side.</b> A node whose middles are sized from the
///     bake's extent, Histogram Equalise's counts and table above all, is twice the side of a 128² bench plan and a
///     thirty-second of an 8K one, so scaling the bench reported Edge Select at 8 GiB at 8K (Vixen #1646) when it is
///     a quarter of one.
/// </remarks>
public sealed class CompoundPoolTests(ITestOutputHelper output) {
    /// <summary>The side every shipped compound is authored at.</summary>
    const int Authored = 1024;

    /// <summary>The largest side a graph is made at, Patina.Contracts' <c>TextureSizes.Largest</c>.</summary>
    const int Largest = 8192;

    /// <summary>
    ///     Every factory compound at an 8K bake makes no texture wider than the bake, and the curvature-ranked ones,
    ///     Edge Select and Cavity Select, under 1 GiB: their ranking is two small tables, not two images at twice the
    ///     side.
    /// </summary>
    [Fact]
    public void Every_compound_at_the_largest_size_fits_its_side_and_the_ranked_selections_are_small() {
        FactoryCompounds.Register();
        NodeTypeRegistry registry = new();
        NodeTypes.Register(registry);
        var library = TextureCompoundLibrary.Publish(registry, null, out _);
        const int bake = -3; // 1024 << 3 is Largest.
        var pools = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var path in FactoryCompounds.Paths) {
            var schedule = TexturePoolSchedule.For(Plan(registry, library, path, bake));

            foreach (var slot in schedule.Slots) {
                Assert.True(
                    Math.Max(slot.Width, slot.Height) <= Largest,
                    $"{path} makes a {slot.Width}×{slot.Height} {slot.Format} texture at an 8K bake."
                );
            }

            pools[path] = schedule.Bytes;
            output.WriteLine(
                $"{path}: {schedule.Peak} live, {schedule.Allocations} textures, "
                + $"{(schedule.Bytes / (1024.0 * 1024 * 1024)).ToString("0.00", CultureInfo.InvariantCulture)} GiB"
            );
        }

        Assert.InRange(pools["Generators/Edge Select"], 1, 1L << 30);
        Assert.InRange(pools["Generators/Cavity Select"], 1, 1L << 30);
    }

    /// <summary>A graph of one compound, every image input fed a noise, compiled for a bake.</summary>
    static TexturePlan Plan(NodeTypeRegistry registry, ISubGraphSource library, string path, int bake) {
        NodeGraphModel graph = new();
        var used = graph.Add(path);
        var type = registry.Get(path);
        var target = graph.Add("Output/Output");
        var picture = type.Ports.First(static port => port is { Direction: PortDirection.Output, Kind: PortKind.Image }
        );

        graph.Connect(new(used.Id, picture.Name), new(target.Id, "Input"));

        foreach (var port in type.Ports.Where(static port => port is {
                         Direction: PortDirection.Input, Kind: PortKind.Image
                     }
                 )) {
            var noise = graph.Add("Source/Noise");
            graph.Connect(new(noise.Id, "Out"), new(used.Id, port.Name));
        }

        TextureGraphCompiler compiler = new(registry) {
            BaseWidth = Authored,
            BaseHeight = Authored,
            BakeLevelOffset = bake,
            Seed = 1,
            SubGraphSource = library
        };
        var compilation = compiler.Compile(graph);

        Assert.True(
            compilation.Succeeded,
            path + ": " + string.Join("; ", compilation.Diagnostics.Select(static diagnostic => diagnostic.Message))
        );

        return compilation.Value;
    }
}
