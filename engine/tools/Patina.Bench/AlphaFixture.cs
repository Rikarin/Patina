// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Imaging;

namespace Patina.Bench;

/// <summary>
///     The bitmap brush alphas Patina ships in <c>content/alphas</c> (#34), drawn here from seeds so that they are CC0
///     and can be drawn again: white where a stamp paints, black where it does not, and black at every border so a
///     stamp never shows its square.
/// </summary>
static class AlphaFixture {
    /// <summary>The side of each alpha, in texels.</summary>
    const int Side = 256;

    /// <summary>Each alpha by its name, and how much of the stamp each texel of it paints.</summary>
    static readonly (string Name, Func<float, float, float> Paint)[] Alphas = [
        ("Splatter", Splatter()),
        ("Speckle", Speckle()),
        ("Scratches", Scratches()),
        ("Cloud", static (x, y) => Fade(x, y, 0.95f) * Smooth(Noise(x * 4f, y * 4f, 5, 11), 0.35f, 0.75f)),
        ("Sponge",
            static (x, y) => Smooth(Noise(x * 9f, y * 9f, 3, 23) - (1f - Fade(x, y, 0.75f)) * 0.6f, 0.42f, 0.5f)),
        ("Bristles", Bristles()),
        ("Crackle", Crackle()),
        ("Ring", static (x, y) => Band(MathF.Sqrt(x * x + y * y), 0.62f, 0.12f)),
        ("Star", static (x, y) => Smooth(Star(x, y), -0.02f, 0.02f)),
        ("Leaf", static (x, y) => Smooth(Leaf(x, y), -0.02f, 0.02f))
    ];

    /// <summary>Writes every alpha into a folder as a PNG named after it.</summary>
    /// <param name="folder">The folder, made if it is not there.</param>
    /// <returns>Zero.</returns>
    public static int Write(string folder) {
        Directory.CreateDirectory(folder);
        foreach (var (name, paint) in Alphas) {
            var pixels = new byte[Side * Side * 4];
            for (var row = 0; row < Side; row++) {
                for (var column = 0; column < Side; column++) {
                    var x = (column + 0.5f) / Side * 2f - 1f;
                    var y = (row + 0.5f) / Side * 2f - 1f;
                    var grey = (byte)MathF.Round(Math.Clamp(paint(x, y), 0f, 1f) * 255f);
                    var at = (row * Side + column) * 4;
                    (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (grey, grey, grey, 255);
                }
            }

            File.WriteAllBytes(Path.Combine(folder, name + ".png"), PngCodec.Encode(new(Side, Side, pixels)));
            Console.WriteLine(name);
        }

        return 0;
    }

    /// <summary>A blob with droplets thrown round it, fewer and smaller further out.</summary>
    static Func<float, float, float> Splatter() {
        var random = new Random(7);
        var drops = Enumerable.Range(0, 40)
            .Select(_ => {
                    var angle = random.NextSingle() * MathF.Tau;
                    var reach = 0.25f + random.NextSingle() * 0.6f;
                    return (X: MathF.Cos(angle) * reach, Y: MathF.Sin(angle) * reach,
                        Radius: 0.015f + random.NextSingle() * 0.07f * (1f - reach));
                }
            )
            .ToArray();
        return (x, y) => {
            var wobble = Noise(x * 3f, y * 3f, 3, 5) * 0.25f;
            var paint = Smooth(0.38f + wobble - MathF.Sqrt(x * x + y * y), -0.02f, 0.02f);
            foreach (var drop in drops) {
                var distance = MathF.Sqrt((x - drop.X) * (x - drop.X) + (y - drop.Y) * (y - drop.Y));
                paint = MathF.Max(paint, Smooth(drop.Radius - distance, -0.01f, 0.01f));
            }

            return paint * Fade(x, y, 0.98f);
        };
    }

    /// <summary>Dots of several sizes, thinning out towards the edge.</summary>
    static Func<float, float, float> Speckle() {
        var random = new Random(13);
        var dots = Enumerable.Range(0, 220)
            .Select(_ => (X: random.NextSingle() * 1.8f - 0.9f, Y: random.NextSingle() * 1.8f - 0.9f,
                    Radius: 0.008f + random.NextSingle() * random.NextSingle() * 0.04f,
                    Grey: 0.5f + random.NextSingle() * 0.5f)
            )
            .Where(dot => dot.X * dot.X + dot.Y * dot.Y < 0.8f * 0.8f * random.NextSingle() + 0.1f)
            .ToArray();
        return (x, y) => {
            var paint = 0f;
            foreach (var dot in dots) {
                var distance = MathF.Sqrt((x - dot.X) * (x - dot.X) + (y - dot.Y) * (y - dot.Y));
                paint = MathF.Max(paint, dot.Grey * Smooth(dot.Radius - distance, -0.006f, 0.006f));
            }

            return paint * Fade(x, y, 0.95f);
        };
    }

    /// <summary>Thin strokes across the stamp, mostly one way, of several lengths and strengths.</summary>
    static Func<float, float, float> Scratches() {
        var random = new Random(29);
        var strokes = Enumerable.Range(0, 26)
            .Select(_ => {
                    var angle = 0.5f + (random.NextSingle() - 0.5f) * 0.7f;
                    var length = 0.3f + random.NextSingle() * 0.9f;
                    var (cx, cy) = (random.NextSingle() * 1.2f - 0.6f, random.NextSingle() * 1.2f - 0.6f);
                    var (dx, dy) = (MathF.Cos(angle) * length / 2f, MathF.Sin(angle) * length / 2f);
                    return (Ax: cx - dx, Ay: cy - dy, Bx: cx + dx, By: cy + dy,
                        Width: 0.003f + random.NextSingle() * 0.008f, Grey: 0.4f + random.NextSingle() * 0.6f);
                }
            )
            .ToArray();
        return (x, y) => {
            var paint = 0f;
            foreach (var stroke in strokes) {
                var (distance, along) = Segment(x, y, stroke.Ax, stroke.Ay, stroke.Bx, stroke.By);
                var taper = MathF.Sin(along * MathF.PI);
                paint = MathF.Max(
                    paint,
                    stroke.Grey * taper * Smooth(stroke.Width * taper - distance, -0.004f, 0.004f)
                );
            }

            return paint * Fade(x, y, 0.95f);
        };
    }

    /// <summary>A dry brush's mark: streaks along x of uneven strength, ragged at both ends.</summary>
    static Func<float, float, float> Bristles() {
        var random = new Random(31);
        var hairs = Enumerable.Range(0, 64).Select(_ => 0.3f + random.NextSingle() * 0.7f).ToArray();
        return (x, y) => {
            var lane = (y * 0.5f + 0.5f) * hairs.Length;
            var hair = hairs[Math.Clamp((int)lane, 0, hairs.Length - 1)];
            var across = 1f - MathF.Abs(lane - MathF.Floor(lane) - 0.5f) * 2f;
            var ragged = 0.55f + Noise(0f, y * 30f, 2, 41) * 0.3f;
            var ends = Smooth(ragged - MathF.Abs(x), 0f, 0.15f);
            var height = Smooth(0.75f - MathF.Abs(y), 0f, 0.1f);
            return hair * Smooth(across, 0.1f, 0.5f) * ends * height;
        };
    }

    /// <summary>The edges of cells, as paint cracks, inside a soft circle.</summary>
    static Func<float, float, float> Crackle() {
        var random = new Random(37);
        // On a jittered grid, so no two cells start so close that the crack between them reads as a fill.
        var seeds = Enumerable.Range(0, 49)
            .Select(at => {
                    var (row, column) = Math.DivRem(at, 7);
                    return (X: (column + 0.2f + random.NextSingle() * 0.6f) / 7f * 2.2f - 1.1f,
                        Y: (row + 0.2f + random.NextSingle() * 0.6f) / 7f * 2.2f - 1.1f);
                }
            )
            .ToArray();
        return (x, y) => {
            var (nearest, second) = (float.MaxValue, float.MaxValue);
            foreach (var seed in seeds) {
                var distance = MathF.Sqrt((x - seed.X) * (x - seed.X) + (y - seed.Y) * (y - seed.Y));
                if (distance < nearest) {
                    (nearest, second) = (distance, nearest);
                } else if (distance < second) {
                    second = distance;
                }
            }

            return Smooth(0.018f - (second - nearest), -0.008f, 0.008f) * Fade(x, y, 0.9f);
        };
    }

    /// <summary>A five-pointed star: positive inside, by how far in.</summary>
    static float Star(float x, float y) {
        var angle = MathF.Atan2(x, -y);
        var sector = MathF.Tau / 5f;
        var turn = MathF.Abs((angle % sector + sector) % sector - sector / 2f) / (sector / 2f);
        return 0.42f + 0.4f * turn * turn - MathF.Sqrt(x * x + y * y) * (1f + 0.6f * (1f - turn));
    }

    /// <summary>A leaf along y with a midrib: positive inside, by how far in.</summary>
    static float Leaf(float x, float y) {
        var width = 0.45f * MathF.Pow(MathF.Max(0f, 1f - y * y / 0.8f), 0.8f) * (1f - 0.25f * y);
        var rib = 0.06f - MathF.Abs(x) * 6f;
        return MathF.Min(width - MathF.Abs(x), 0.85f - MathF.Abs(y)) - MathF.Max(0f, rib) * 0.05f;
    }

    /// <summary>The distance from a point to a segment, and how far along the segment the nearest point is.</summary>
    static (float Distance, float Along) Segment(float x, float y, float ax, float ay, float bx, float by) {
        var (dx, dy) = (bx - ax, by - ay);
        var along = Math.Clamp(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy), 0f, 1f);
        var (px, py) = (ax + dx * along - x, ay + dy * along - y);
        return (MathF.Sqrt(px * px + py * py), along);
    }

    /// <summary>One inside a circle of a radius, falling to nought at the stamp's edge.</summary>
    static float Fade(float x, float y, float radius) => Smooth(radius - MathF.Sqrt(x * x + y * y), 0f, 0.25f);

    /// <summary>A band of a width round a radius, soft at both sides.</summary>
    static float Band(float distance, float radius, float width) =>
        Smooth(width / 2f - MathF.Abs(distance - radius), -0.015f, 0.015f);

    /// <summary>Hermite step from nought at <paramref name="low" /> to one at <paramref name="high" />.</summary>
    static float Smooth(float value, float low, float high) {
        var t = Math.Clamp((value - low) / (high - low), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Octaves of value noise from nought to one, seeded.</summary>
    static float Noise(float x, float y, int octaves, int seed) {
        var (sum, weight, total) = (0f, 1f, 0f);
        for (var octave = 0; octave < octaves; octave++) {
            sum += Value(x, y, seed + octave) * weight;
            total += weight;
            (x, y, weight) = (x * 2.03f, y * 2.03f, weight * 0.5f);
        }

        return sum / total;
    }

    /// <summary>Smoothly interpolated hashed values on a unit grid.</summary>
    static float Value(float x, float y, int seed) {
        var (cx, cy) = ((int)MathF.Floor(x), (int)MathF.Floor(y));
        var (fx, fy) = (x - cx, y - cy);
        (fx, fy) = (fx * fx * (3f - 2f * fx), fy * fy * (3f - 2f * fy));
        var top = Lerp(Hash(cx, cy, seed), Hash(cx + 1, cy, seed), fx);
        var bottom = Lerp(Hash(cx, cy + 1, seed), Hash(cx + 1, cy + 1, seed), fx);
        return Lerp(top, bottom, fy);
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>A number from nought to one for a grid point.</summary>
    static float Hash(int x, int y, int seed) {
        var h = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 2147483647));
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }
}
