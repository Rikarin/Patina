using Patina.Bench;
using Patina.Jobs;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>
///     The Shut Lines demo pair (#118): its id map tells the panels apart inside the one chart its low has, which is
///     what Shut Lines draws a groove along. The housing's id map changes only where a chart ends, and Shut Lines keeps
///     off those on purpose.
/// </summary>
public sealed class PanelFixtureTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-panels-");

    public void Dispose() => folder.Delete(true);

    [Fact]
    public void The_panel_pair_bakes_every_panel_apart_inside_one_chart() {
        PanelFixture.Write(folder.FullName);
        var into = Path.Combine(folder.FullName, "bake");

        MeshMapBakeRun.Run(
            new() {
                High = [Path.Combine(folder.FullName, PanelFixture.HighFile)],
                Low = Path.Combine(folder.FullName, PanelFixture.LowFile),
                Output = into,
                Options = new() { Resolution = 128, Format = BakeInputs.Png16, Maps = ["id"] }
            },
            into,
            0,
            null,
            TestContext.Current.CancellationToken
        );

        var picture = PngCodec.Load(Path.Combine(into, "id.preview.png"));

        int Colour(int x, int y) {
            var at = (y * picture.Width + x) * 4;
            return picture.Pixels[at] << 16 | picture.Pixels[at + 1] << 8 | picture.Pixels[at + 2];
        }

        // Inside the chart, clear of its margin: every texel is some panel, and every panel is there.
        var inset = (int)Math.Ceiling(PanelFixture.Margin * picture.Width) + 2;
        HashSet<int> seen = [];
        for (var y = inset; y < picture.Height - inset; y++) {
            for (var x = inset; x < picture.Width - inset; x++) {
                Assert.NotEqual(0, Colour(x, y));
                seen.Add(Colour(x, y));
            }
        }

        Assert.Equal(PanelFixture.Columns * PanelFixture.Rows, seen.Count);

        // And across one row of the chart the id changes once between each pair of neighbouring panels.
        var changes = 0;
        var row = picture.Height / 4;
        for (var x = inset + 1; x < picture.Width - inset; x++) {
            changes += Colour(x, row) != Colour(x - 1, row) ? 1 : 0;
        }

        Assert.Equal(PanelFixture.Columns - 1, changes);
    }
}
