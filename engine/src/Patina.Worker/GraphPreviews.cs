using Patina.Ipc;
using System.Collections.Immutable;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>An image of a compiled graph that a client can look at.</summary>
readonly record struct GraphPreviewSource(int Node, string Port, int Image);

/// <summary>A picture in an extended plan, and whose it is.</summary>
readonly record struct GraphPreviewImage(int Node, string Port, int Image);

/// <summary>A compiled plan with a small copy of every node's image on the end.</summary>
readonly record struct GraphPreviewPlan(TexturePlan Plan, IReadOnlyList<GraphPreviewImage> Images);

/// <summary>
///     Turns images a graph computed into thumbnails. Reading a 2K image back costs tens of milliseconds even before it
///     is encoded, so each one is first reduced on the GPU, by Vixen's own Resample kernel, as ordinary ops at the end
///     of the plan. Only the reductions are the plan's outputs: they are what stays in the result cache for certain,
///     and the 2K images stay there while they fit.
/// </summary>
static class GraphPreviews {
    const string Resample = "Resample";

    /// <summary>The filter a reduction uses: the box, the only correct choice going down (Resample.rvn).</summary>
    const float Box = 2f;

    /// <summary>Writes a file under its hash: whoever reads it sees all of it or no file.</summary>
    public static void Store(string folder, string hash, byte[] png) {
        var path = GraphPreviewFiles.PathOf(folder, hash);
        if (File.Exists(path)) {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // A name of its own: two nodes with one picture reach here at once, under one hash.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(temporary, png);
        File.Move(temporary, path, true);
    }

    /// <summary>
    ///     What a compilation has to show: the last image of each output port, since a compound's inner nodes all
    ///     answer to the compound node, and what each Output node writes, under its usage.
    /// </summary>
    public static IReadOnlyList<GraphPreviewSource> Sources(TextureGraphCompiler compiler) {
        Dictionary<(int Node, string Port), int> images = [];
        foreach (var written in compiler.NodeImages) {
            images[(written.Node.Value, written.Port)] = written.Image;
        }

        foreach (var output in compiler.Outputs) {
            images[(output.Node.Value, output.Usage)] = output.Image;
        }

        return [..images.Select(static image => new GraphPreviewSource(image.Key.Node, image.Key.Port, image.Value))];
    }

    /// <summary>Adds a reduction of every written image to a plan, leaving the ops before them as they were.</summary>
    public static GraphPreviewPlan Extend(TexturePlan plan, IReadOnlyList<GraphPreviewSource> sources, int side) {
        HashSet<int> written = [..plan.Ops.Select(static op => op.Output)];
        var images = plan.Images.ToBuilder();
        var ops = plan.Ops.ToBuilder();
        Dictionary<int, int> reduced = [];
        List<GraphPreviewImage> previews = [];
        foreach (var source in sources) {
            // An image nothing writes is a node with an empty input, which has nothing to show.
            if (!written.Contains(source.Image)) {
                continue;
            }

            if (!reduced.TryGetValue(source.Image, out var image)) {
                image = Reduction(plan, source.Image, side, images, ops);
                reduced[source.Image] = image;
            }

            previews.Add(new(source.Node, source.Port, image));
        }

        var extended = new TexturePlan {
            BaseWidth = plan.BaseWidth,
            BaseHeight = plan.BaseHeight,
            BakeLevelOffset = plan.BakeLevelOffset,
            Images = images.ToImmutable(),
            Ops = ops.ToImmutable(),
            Outputs = [..reduced.Values.Distinct()],
            Seed = plan.Seed,
            WrapX = plan.WrapX,
            WrapY = plan.WrapY,
            Kernels = plan.Kernels
        };
        return new(extended, previews);
    }

    /// <summary>A plan whose one output is an image at the size the graph computes it.</summary>
    public static TexturePlan Full(TexturePlan plan, int image) =>
        new() {
            BaseWidth = plan.BaseWidth,
            BaseHeight = plan.BaseHeight,
            BakeLevelOffset = plan.BakeLevelOffset,
            Images = plan.Images,
            Ops = plan.Ops,
            Outputs = [image],
            Seed = plan.Seed,
            WrapX = plan.WrapX,
            WrapY = plan.WrapY,
            Kernels = plan.Kernels
        };

    static int Reduction(
        TexturePlan plan,
        int image,
        int side,
        ImmutableArray<TextureImage>.Builder images,
        ImmutableArray<TextureOp>.Builder ops
    ) {
        var size = plan.SizeOf(image);
        var longest = Math.Max(size.X, size.Y);
        if (longest <= side) {
            return image;
        }

        var steps = 0;
        while (longest >> steps > side) {
            steps++;
        }

        var source = plan.Images[image];
        images.Add(source with { LevelOffset = source.LevelOffset + steps });
        ops.Add(
            new() {
                Kernel = Resample,
                Output = images.Count - 1,
                Inputs = [image],
                ReadsOtherExtents = true,
                Parameters = [new("filter", Box)]
            }
        );
        return images.Count - 1;
    }
}
