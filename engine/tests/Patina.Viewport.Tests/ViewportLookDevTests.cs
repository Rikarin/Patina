using Vixen.Core.Imaging;

namespace Patina.Viewport.Tests;

/// <summary>
///     The look-dev settings of waves six (#31, #137, #138, #159), judged by the numbers in the frames they make:
///     occlusion on the light, the grid, the background colour, bloom, the grade, the texel density view, the camera
///     over the poles, and the picture's lanes. Skipped where there is no Vulkan.
/// </summary>
public sealed partial class ViewportDisplayTests {
    static double Linear(int level) {
        var stored = level / 255.0;
        return stored <= 0.04045 ? stored / 12.92 : Math.Pow((stored + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    ///     Occlusion closes the light as well as the environment. With the map at zero there is no ambient light, and
    ///     what is left is the light through an opening as narrow as it gets, which goes as the cube of its cosine: the
    ///     light at rotations 0 and -0.6 meets the square at cosines 0.477 and 0.717, so the two pixels are about 0.29
    ///     apart and not the 0.66 the cosine alone gives, which is what occlusion used to leave of the light.
    /// </summary>
    [Fact]
    public void A_closed_occlusion_takes_the_light_down_as_the_cube_of_its_cosine() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        renderer.SetLive(ViewportSlot.Occlusion, Map, Map, Solid(0, 0, 0));
        var camera = new OrbitCamera(0f, 0f, 0.5f);

        double Lit(float rotation) {
            renderer.Shading = new() {
                View = ViewportView.Clay, ToneMapping = ViewportToneMapping.None, LightRotation = rotation, Shadows = 0f
            };
            var (r, _, _) = At(Render(renderer, camera, 64, 64), 64, 32, 32);
            return Linear(r);
        }

        var ratio = Lit(0f) / Lit(-0.6f);

        Assert.InRange(ratio, 0.22, 0.4);
    }

    /// <summary>
    ///     The grid lies on the floor the model stands on and draws round it, and leaves the model as it was: the
    ///     square's middle is the same pixel with it and without.
    /// </summary>
    [Fact]
    public void The_grid_draws_round_the_model_and_leaves_it_alone() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        var camera = new OrbitCamera(0.3f, 0.6f, 1.6f);
        renderer.Shading = new();
        var plain = Render(renderer, camera, 96, 96);
        renderer.Shading = new() { Grid = true };
        var grid = Render(renderer, camera, 96, 96);

        var changed = Enumerable.Range(0, 96 * 96).Count(i => Math.Abs(plain[i * 4] - grid[i * 4]) > 4);
        Assert.InRange(changed, 96, 96 * 96 / 2);
        Assert.Equal(At(plain, 96, 48, 48), At(grid, 96, 48, 48));
    }

    /// <summary>
    ///     With no environment behind the model the background is the colour asked for, which a view of stored values
    ///     shows as the number given, since no curve touches it. It used to be black.
    /// </summary>
    [Fact]
    public void With_no_environment_the_background_is_its_colour() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        renderer.Shading = new() {
            View = ViewportView.BaseColor,
            BackgroundOpacity = 0f,
            BackgroundColour = new((float)Linear(0x33), (float)Linear(0x66), (float)Linear(0x99))
        };

        var (r, g, b) = At(Render(renderer, new(0f, 0f, 2f), 64, 64), 64, 1, 1);

        Assert.True(
            Math.Abs(r - 0x33) <= 1 && Math.Abs(g - 0x66) <= 1 && Math.Abs(b - 0x99) <= 1,
            $"The background was ({r}, {g}, {b})."
        );
    }

    /// <summary>
    ///     Blur softens the background and not the light on the model (#30): the frames at no blur and at full blur
    ///     part
    ///     on the background, and no pixel of the model, which the frame with no background shows, moves.
    /// </summary>
    [Fact]
    public void Blur_softens_the_background_and_not_the_light_on_the_model() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        renderer.SetEnvironment("sunset");
        renderer.Shading = new() { BackgroundOpacity = 0f };
        var model = Render(renderer, OrbitCamera.Default, 160, 100);
        renderer.Shading = new() { BackgroundBlur = 0f };
        var sharp = Render(renderer, OrbitCamera.Default, 160, 100);
        renderer.Shading = new() { BackgroundBlur = 1f };
        var hazy = Render(renderer, OrbitCamera.Default, 160, 100);

        bool On(int x, int y) =>
            x is >= 0 and < 160 && y is >= 0 and < 100 && At(model, 160, x, y) is var (r, g, b) && r + g + b > 6;

        var (background, moved, within) = (0, 0, 0);
        for (var y = 0; y < 100; y++) {
            for (var x = 0; x < 160; x++) {
                var ((r, _, b), (hr, _, hb)) = (At(sharp, 160, x, y), At(hazy, 160, x, y));
                var apart = Math.Abs(r - hr) + Math.Abs(b - hb) > 6;
                background += !On(x, y) && apart ? 1 : 0;
                // Inside the silhouette, which is antialiased over the background at its edge.
                var inside = On(x, y) && On(x - 1, y) && On(x + 1, y) && On(x, y - 1) && On(x, y + 1);
                moved += inside && apart ? 1 : 0;
                within += inside ? 1 : 0;
            }
        }

        Assert.InRange(background, 160, 160 * 100);
        // A few pixels between the knot's turns see the background past a thin edge.
        Assert.InRange(moved, 0, within / 100);
    }

    /// <summary>
    ///     Bloom adds the light past white back over the frame before the curve, so it brightens and never darkens a
    ///     pixel, and at zero it does not run.
    /// </summary>
    [Fact]
    public void Bloom_only_adds_light_and_at_zero_does_nothing() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        renderer.Shading = new() { Exposure = 2f };
        var plain = Render(renderer, OrbitCamera.Default, 160, 100);
        renderer.Shading = new() { Exposure = 2f, Bloom = 0f };
        Assert.Equal(plain, Render(renderer, OrbitCamera.Default, 160, 100));

        renderer.Shading = new() { Exposure = 2f, Bloom = 1f };
        var bloomed = Render(renderer, OrbitCamera.Default, 160, 100);

        var darker = Enumerable.Range(0, plain.Length).Count(i => (i & 3) != 3 && bloomed[i] + 1 < plain[i]);
        Assert.Equal(0, darker);
        Assert.True(Brightness(bloomed) > Brightness(plain) * 1.02, "Bloom added no light.");
    }

    /// <summary>
    ///     Warmth takes blue towards red and tint takes green out, and neither moves the frame much in all.
    /// </summary>
    [Fact]
    public void Warmth_and_tint_move_the_colour_and_not_the_brightness() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        renderer.Shading = new();
        var plain = Render(renderer, OrbitCamera.Default, 160, 100);
        renderer.Shading = new() { Warmth = 1f };
        var warm = Render(renderer, OrbitCamera.Default, 160, 100);
        renderer.Shading = new() { Tint = 1f };
        var tinted = Render(renderer, OrbitCamera.Default, 160, 100);

        static long Lane(byte[] pixels, int lane) {
            var sum = 0L;
            for (var at = lane; at < pixels.Length; at += 4) {
                sum += pixels[at];
            }

            return sum;
        }

        Assert.True(Lane(warm, 0) - Lane(warm, 2) > Lane(plain, 0) - Lane(plain, 2) + 160 * 100 * 10);
        Assert.True(Lane(tinted, 1) < Lane(plain, 1) - 160 * 100 * 5);
        Assert.InRange((double)Brightness(warm) / Brightness(plain), 0.9, 1.1);
    }

    /// <summary>
    ///     Texels per unit against the project's density: a square two units across laid over the whole tile at 1024 is
    ///     512 per unit, which is green at 512, red at a quarter of it and blue at four times it. A material that wears
    ///     a map is measured at the map's size: 64 texels is 32 per unit.
    /// </summary>
    [Fact]
    public void The_texel_density_view_measures_each_material_at_its_own_size() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        var camera = new OrbitCamera(0f, 0f, 0.5f);

        (int R, int G, int B) Middle(float target) {
            renderer.Shading = new() {
                View = ViewportView.TexelDensity, DensityTarget = target, DensityResolution = 1024f
            };
            return At(Render(renderer, camera, 64, 64), 64, 32, 32);
        }

        static bool Leads((int R, int G, int B) colour, int lane) =>
            lane switch {
                0 => colour.R > colour.G + 60 && colour.R > colour.B + 60,
                1 => colour.G > colour.R + 60 && colour.G > colour.B + 60,
                _ => colour.B > colour.R + 60 && colour.B > colour.G + 60
            };

        Assert.True(Leads(Middle(512f), 1), $"On its density the square was {Middle(512f)}.");
        Assert.True(Leads(Middle(128f), 0), $"At four times the density asked it was {Middle(128f)}.");
        Assert.True(Leads(Middle(2048f), 2), $"At a quarter of it it was {Middle(2048f)}.");

        renderer.SetLive(ViewportSlot.BaseColor, Map, Map, Solid(200, 200, 200));
        Assert.True(Leads(Middle(32f), 1), $"At its map's own size it was {Middle(32f)}.");
    }

    /// <summary>
    ///     The orbit is free over the poles. Past straight down the camera sits where the opposite yaw would put it
    ///     short
    ///     of the pole, upside down, so its picture is that one turned half round. With up held level it was that
    ///     picture the right way up.
    /// </summary>
    [Fact]
    public void Past_the_pole_the_picture_turns_over() {
        using var renderer = Open();
        renderer.OpenMesh(Fixture);
        renderer.Shading = new();
        const int width = 120;
        const int height = 80;
        var over = Render(renderer, new(0.6f, 2.2f, 1.05f), width, height);
        var short_ = Render(renderer, new(0.6f + MathF.PI, MathF.PI - 2.2f, 1.05f), width, height);

        var apart = 0;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var (r, g, b) = At(over, width, x, y);
                var (tr, tg, tb) = At(short_, width, width - 1 - x, height - 1 - y);
                apart += Math.Abs(r - tr) + Math.Abs(g - tg) + Math.Abs(b - tb) > 24 ? 1 : 0;
            }
        }

        Assert.InRange(apart, 0, width * height / 50);
    }

    /// <summary>A lane of the picture shows as a grey of its number, and the scale multiplies it first.</summary>
    [Fact]
    public void A_lane_of_the_picture_shows_as_a_grey_of_its_number() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        var picture = In("lanes.png");
        File.WriteAllBytes(picture, PngCodec.Encode(new(Map, Map, Solid(33, 166, 222))));
        renderer.SetPicture(new(picture, "lanes"));
        var camera = new OrbitCamera(0f, 0f, 0.5f);

        (int, int, int) Shown(int lane, float scale) {
            renderer.Shading = new() { View = ViewportView.Picture, PictureLane = lane, PictureScale = scale };
            return At(Render(renderer, camera, 64, 64), 64, 32, 32);
        }

        Assert.Equal((33, 166, 222), Shown(0, 1f));
        Assert.Equal((33, 33, 33), Shown(1, 1f));
        Assert.Equal((83, 83, 83), Shown(2, 0.5f));
        Assert.Equal((255, 255, 255), Shown(4, 1f));
        Assert.Equal((255, 255, 255), Shown(3, 2f));
    }

    /// <summary>
    ///     Depth of field (#138) keeps what the lens focuses on sharp and softens what it does not: a checker
    ///     square facing the camera stays as sharp as with none when the focus is on it, loses much of its edges'
    ///     contrast when the focus is a bounding radius behind it, and the setting at zero draws what it drew before.
    /// </summary>
    [Fact]
    public void Depth_of_field_softens_what_is_out_of_focus_and_leaves_the_focus_sharp() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        var checker = new byte[Map * Map * 4];
        for (var at = 0; at < Map * Map; at++) {
            var on = (at % Map / 8 + at / Map / 8) % 2 == 0;
            (checker[at * 4], checker[at * 4 + 1], checker[at * 4 + 2], checker[at * 4 + 3]) =
                on ? ((byte)230, (byte)230, (byte)230, (byte)255) : ((byte)20, (byte)20, (byte)20, (byte)255);
        }

        renderer.SetLive(ViewportSlot.BaseColor, Map, Map, checker);
        var camera = new OrbitCamera(0f, 0f, 0.5f);

        byte[] Seen(float depthOfField, float focus) {
            renderer.Shading = new() { DepthOfField = depthOfField, Focus = focus };
            return Render(renderer, camera, 128, 128);
        }

        // How much the picture changes from each pixel to the next, inside the square.
        static long Edges(byte[] pixels) {
            long sum = 0;
            for (var y = 16; y < 112; y++) {
                for (var x = 16; x < 111; x++) {
                    sum += Math.Abs(pixels[(y * 128 + x) * 4] - pixels[(y * 128 + x + 1) * 4]);
                }
            }

            return sum;
        }

        var none = Seen(0f, 0f);
        Assert.Equal(Render(renderer, camera, 128, 128), none);
        var focused = Edges(Seen(1f, 0f));
        var behind = Edges(Seen(1f, 1f));

        Assert.InRange(focused, Edges(none) * 9 / 10, Edges(none) * 11 / 10);
        Assert.True(behind < Edges(none) * 6 / 10, $"Out of focus kept {behind} of {Edges(none)}.");
    }

    /// <summary>
    ///     Neutral draws the lit view in clay, so a bake's preview reads against plain grey (#149), and leaves a view
    ///     of a stored channel as it is.
    /// </summary>
    [Fact]
    public void Neutral_draws_the_lit_view_in_clay_and_leaves_the_others() {
        using var renderer = Open();
        renderer.OpenMesh(Square());
        renderer.SetLive(ViewportSlot.BaseColor, Map, Map, Solid(200, 30, 30));
        var camera = new OrbitCamera(0f, 0f, 0.5f);

        byte[] Seen(ViewportView view, bool neutral) {
            renderer.Neutral = neutral;
            renderer.Shading = new() { View = view };
            return Render(renderer, camera, 64, 64);
        }

        var clay = Seen(ViewportView.Clay, false);
        var lit = Seen(ViewportView.Lit, false);

        Assert.NotEqual(clay, lit);
        Assert.Equal(clay, Seen(ViewportView.Lit, true));
        Assert.Equal(Seen(ViewportView.BaseColor, false), Seen(ViewportView.BaseColor, true));
    }
}
