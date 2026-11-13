using Patina.Contracts;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     A Color Selection of two colors with a brush on it, and kept to some meshes (#245): the selection is made first,
///     and then painted and limited, whichever color a texel was selected by.
/// </summary>
public sealed partial class StackProcessTests {
    static readonly IReadOnlyList<float> SelectRed = [1f, 0f, 0f];
    static readonly IReadOnlyList<float> SelectBlue = [0f, 0f, 1f];

    /// <summary>
    ///     A stroke adds to a selection and takes from it, over either color. ⚠ Before #245 the brush lay under the
    ///     selection: the first color's step read the stroke as a color and the second never saw it, so white added
    ///     nothing and black took nothing from blue.
    /// </summary>
    [Fact]
    public async Task A_stroke_adds_to_and_takes_from_a_selection_of_two_colors() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Selected.vxlayers", deadline.Token);
        IdBands();
        var fill = await MaskedFillAsync(
            state.Id,
            new("Texture", Asset: "ids.png", Colours: [SelectRed, SelectBlue]),
            deadline.Token
        );
        var before = await TexelsAsync(state.Id, "before", deadline.Token);
        var (shown, hidden) = (Texel(before, 100, 100), Texel(before, 800, 100));
        Assert.NotEqual(shown, hidden);
        Near(shown, Texel(before, 400, 300));

        await EditAsync(state.Id, deadline.Token, MaskStroke("add", fill, 600f, 300f, [1f, 1f, 1f, 1f]));
        await EditAsync(state.Id, deadline.Token, MaskStroke("off-blue", fill, 300f, 300f, [0f, 0f, 0f, 1f]));
        await EditAsync(state.Id, deadline.Token, MaskStroke("off-red", fill, 40f, 600f, [0f, 0f, 0f, 1f]));

        var after = await TexelsAsync(state.Id, "after", deadline.Token);
        Near(shown, Texel(after, 650, 300));
        Near(hidden, Texel(after, 350, 300));
        Near(hidden, Texel(after, 100, 600));
        Near(shown, Texel(after, 100, 100));
        Near(shown, Texel(after, 400, 800));
        Near(hidden, Texel(after, 800, 800));
    }

    /// <summary>
    ///     A layer kept to some meshes stays off the others for every color it selects. ⚠ Before #245 the limit lay
    ///     under the selection, and only the first color's step read it: blue showed where the layer was kept away.
    /// </summary>
    [Fact]
    public async Task Keep_to_meshes_holds_for_every_color_of_a_selection() {
        using var deadline = Deadline(240);
        var (_, state) = await StartAsync("Kept.vxlayers", deadline.Token);
        IdBands();
        var keep = new byte[Side * Side * 4];
        for (var at = 0; at < Side * Side; at++) {
            var lit = at / Side < Side / 2 ? (byte)255 : (byte)0;
            keep.AsSpan(at * 4, 4).Fill(lit);
            keep[at * 4 + 3] = 255;
        }

        PngCodec.Save(Path.Combine(Library, "keep.png"), new(Side, Side, keep));
        var fill = await MaskedFillAsync(
            state.Id,
            new("Texture", Asset: "ids.png", Colours: [SelectRed, SelectBlue]),
            deadline.Token
        );
        var free = await TexelsAsync(state.Id, "free", deadline.Token);
        var (shown, hidden) = (Texel(free, 100, 100), Texel(free, 800, 100));
        Near(shown, Texel(free, 400, 800));

        await EditAsync(state.Id, deadline.Token, new StackSetLayer(fill, MeshMask: new(["top"], "keep.png")));

        var kept = await TexelsAsync(state.Id, "kept", deadline.Token);
        Near(shown, Texel(kept, 100, 100));
        Near(shown, Texel(kept, 400, 100));
        Near(hidden, Texel(kept, 100, 800));
        Near(hidden, Texel(kept, 400, 800));
        Near(hidden, Texel(kept, 800, 100));
    }

    /// <summary>An id map in the library: red, then blue, then green across, each a band from top to bottom.</summary>
    void IdBands() {
        var pixels = new byte[Side * Side * 4];
        for (var at = 0; at < Side * Side; at++) {
            var column = at % Side;
            var lane = column < Side / 4 ? 0 : column < Side / 2 ? 2 : 1;
            pixels[at * 4 + lane] = 255;
            pixels[at * 4 + 3] = 255;
        }

        PngCodec.Save(Path.Combine(Library, "ids.png"), new(Side, Side, pixels));
    }
}
