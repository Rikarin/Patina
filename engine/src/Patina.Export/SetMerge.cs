using Patina.Export.Presets;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>
///     Several texture sets written as one (#211): sets that share one UV square without overlapping, as the parts of
///     one model often do, become one atlas, each set's maps where its own layout puts them. A texel no layout covers
///     takes the first set's, which padding then fills from the islands around it. Where two layouts overlap, the set
///     listed first wins, and the export says how much overlaps.
/// </summary>
public static class SetMerge {
    /// <summary>
    ///     The sets as one set of no name, which a preset names as it would a project with one set, or null when they
    ///     cannot be one: fewer than two, or one with no layout on a mesh to say where its texels are.
    /// </summary>
    /// <param name="sets">The sets, the one that wins an overlap first.</param>
    /// <param name="warnings">Told why the sets were not merged, or what overlapped.</param>
    public static ExportSet? Merge(IReadOnlyList<ExportSet> sets, ICollection<string> warnings) {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(warnings);
        if (sets.Count < 2) {
            return null;
        }

        var unlaid = sets.Where(static set => !set.Maps.ContainsKey(ExportMaps.Coverage)).ToList();
        if (unlaid.Count > 0) {
            warnings.Add(
                "The texture sets were written one by one and not merged: "
                + string.Join(", ", unlaid.Select(static set => set.Name))
                + " is on no mesh, so nothing says where its texels go."
            );
            return null;
        }

        var pictures = sets.SelectMany(static (ExportSet set) => set.Maps.Values).ToList();
        var width = pictures.Max(static (MapPicture map) => map.Image.Width);
        var height = pictures.Max(static (MapPicture map) => map.Image.Height);
        var owner = Owners(sets, width, height, out var overlaps);
        if (overlaps > 0) {
            warnings.Add(
                $"{overlaps} texels lie in more than one texture set's layout; each was written from the first of "
                + string.Join(", ", sets.Select(static set => set.Name))
                + " that covers it."
            );
        }

        Dictionary<string, MapPicture> maps = new(StringComparer.Ordinal);
        foreach (var name in sets.SelectMany(static set => set.Maps.Keys)
                     .Where(static name => name != ExportMaps.Coverage)
                     .Distinct(StringComparer.Ordinal)) {
            maps[name] = Map(name, sets, owner, width, height);
        }

        var covered = new float[width * height];
        for (var at = 0; at < covered.Length; at++) {
            covered[at] = owner[at] >= 0 ? 1f : 0f;
        }

        maps[ExportMaps.Coverage] = new(ExportMaps.Coverage, new(width, height, 1, covered), GreenAxis.Up);
        return new(string.Empty, maps);
    }

    /// <summary>Which set each texel is written from, or -1 where no layout covers it.</summary>
    static int[] Owners(IReadOnlyList<ExportSet> sets, int width, int height, out int overlaps) {
        var owner = new int[width * height];
        Array.Fill(owner, -1);
        overlaps = 0;
        for (var index = 0; index < sets.Count; index++) {
            var coverage = Sized(sets[index].Maps[ExportMaps.Coverage].Image, width, height, ResizeFilter.Nearest);
            for (var at = 0; at < owner.Length; at++) {
                if (coverage.Samples[at * coverage.Channels] < 0.5f) {
                    continue;
                }

                if (owner[at] < 0) {
                    owner[at] = index;
                } else {
                    overlaps++;
                }
            }
        }

        return owner;
    }

    /// <summary>
    ///     One map of the atlas: as many lanes as the most any set has, its green the first set's, and a set without
    ///     the map giving what a surface without it would be.
    /// </summary>
    static MapPicture Map(string name, IReadOnlyList<ExportSet> sets, int[] owner, int width, int height) {
        var held = sets.Select(set => set.Maps.GetValueOrDefault(name)).ToList();
        var first = held.First(static map => map is not null)!;
        var lanes = held.Max(static map => map?.Image.Channels ?? 0);
        var images = held.Select(map => map is null ? null : Sized(map.Image, width, height, ResizeFilter.Bilinear))
            .ToList();
        var background = held.IndexOf(first);
        // A normal whose green runs the other way from the first set's is turned over.
        var turned = held.Select(map => name == ExportMaps.Normal && map is not null && map.Green != first.Green)
            .ToList();
        var samples = new float[width * height * lanes];
        Parallel.For(
            0,
            width * height,
            at => {
                var from = owner[at] >= 0 ? owner[at] : background;
                for (var lane = 0; lane < lanes; lane++) {
                    var value = Lane(images[from], at, lane, name);
                    samples[at * lanes + lane] = turned[from] && lane == 1 ? 1f - value : value;
                }
            }
        );
        return first with { Name = name, Image = new(width, height, lanes, samples) };
    }

    /// <summary>A lane of a texel, as a packer reads one: a grey map's one lane is every colour lane.</summary>
    static float Lane(FloatImage? image, int at, int lane, string name) {
        if (image is null) {
            return ExportMaps.Default(name, lane);
        }

        var start = at * image.Channels;
        return lane < image.Channels ? image.Samples[start + lane]
            : lane == 3 ? 1f
            : image.Channels == 1 ? image.Samples[start] : 0f;
    }

    static FloatImage Sized(FloatImage image, int width, int height, ResizeFilter filter) =>
        image.Width == width && image.Height == height ? image : ImageResize.Resize(image, width, height, filter);
}
