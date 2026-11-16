namespace Patina.Viewport.Tests;

/// <summary>
///     OpenPBR's coat, sheen and transmission in the lit view (#142): each changes the picture as it should when
///     painted, and painted at OpenPBR's default it draws the base exactly as no map does. Skipped where there is no
///     Vulkan.
/// </summary>
public sealed partial class ViewportDisplayTests {
    static readonly OrbitCamera Angled = new(0.3f, 0.2f, 0.6f);

    static readonly byte[] Red = Solid(200, 40, 30);

    /// <summary>A lit frame of the square in rough plastic of this colour, with these layers painted over it.</summary>
    byte[] WithLayers(byte[] colour, params (ViewportSlot Slot, byte Value)[] layers) {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        renderer.SetLive(ViewportSlot.BaseColor, Map, Map, colour);
        renderer.SetLive(ViewportSlot.Roughness, Map, Map, Solid(0, 200, 0));
        renderer.SetLive(ViewportSlot.Metallic, Map, Map, Solid(0, 0, 0));
        foreach (var (slot, value) in layers) {
            renderer.SetLive(slot, Map, Map, Solid(value, value, value));
        }

        renderer.Shading = new() { ToneMapping = ViewportToneMapping.None };
        return Render(renderer, Angled, 96, 96);
    }

    /// <summary>
    ///     A stack that adds a coat, sheen or transmission channel starts it at OpenPBR's default (ADR 0050): none, a
    ///     smooth coat and a white sheen. The model must then look as it did, to the byte, or adding a channel would
    ///     change a material nobody has painted.
    /// </summary>
    [Fact]
    public void Layers_at_their_defaults_draw_the_base_as_no_layer_does() {
        var bare = WithLayers(Red);

        var untouched = WithLayers(
            Red,
            (ViewportSlot.Coat, 0),
            (ViewportSlot.CoatRoughness, 0),
            (ViewportSlot.Sheen, 0),
            (ViewportSlot.SheenColor, 255),
            (ViewportSlot.SheenRoughness, 128),
            (ViewportSlot.Transmission, 0)
        );

        Assert.Equal(bare, untouched);
    }

    /// <summary>
    ///     Each layer painted on changes the lit picture as it should. Over a black base only a coat's own reflection
    ///     can add light, so a coat brightens it; a white sheen brightens red plastic; and a transmitting base shows
    ///     the environment behind it in place of its diffuse red, so the picture loses red against green.
    /// </summary>
    [Fact]
    public void Coat_sheen_and_transmission_each_change_the_lit_picture() {
        var black = Solid(0, 0, 0);
        var dark = WithLayers(black);
        var coated = WithLayers(black, (ViewportSlot.Coat, 255));
        var bare = WithLayers(Red);
        var sheened = WithLayers(Red, (ViewportSlot.Sheen, 255), (ViewportSlot.SheenRoughness, 200));
        var through = WithLayers(Red, (ViewportSlot.Transmission, 255));

        Assert.True(Brightness(coated) > Brightness(dark), "A coat adds no reflection over a black base.");
        Assert.True(Brightness(sheened) > Brightness(bare), "A white sheen does not brighten the surface.");
        Assert.True(Redness(through) < Redness(bare) - 0.1, "Transmission leaves the diffuse red as it was.");
    }

    /// <summary>Clay shows the shape alone, so it draws no layer.</summary>
    [Fact]
    public void Clay_draws_no_layer() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        renderer.Shading = new() { View = ViewportView.Clay };
        var bare = Render(renderer, Angled, 96, 96);
        renderer.SetLive(ViewportSlot.Coat, Map, Map, Solid(255, 255, 255));
        renderer.SetLive(ViewportSlot.Sheen, Map, Map, Solid(255, 255, 255));
        renderer.SetLive(ViewportSlot.Transmission, Map, Map, Solid(255, 255, 255));

        Assert.Equal(bare, Render(renderer, Angled, 96, 96));
    }

    /// <summary>The share of red in the picture's red and green.</summary>
    static double Redness(byte[] pixels) {
        long red = 0, green = 0;
        for (var at = 0; at < pixels.Length; at += 4) {
            red += pixels[at];
            green += pixels[at + 1];
        }

        return (double)red / Math.Max(red + green, 1);
    }
}
