// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Graphics;

namespace Patina.Viewport;

/// <summary>
///     The built-in light: a neutral studio, written as a cube and baked by Vixen's own
///     <see cref="EnvironmentPrefilter" /> and <see cref="SphericalHarmonicsL2" />. A gradient room with two soft
///     boxes, because a metal shows what surrounds it, and a uniform environment would make every metal a flat disc.
/// </summary>
sealed class ViewportEnvironment {
    const int Size = 128;
    const int Levels = 7;
    const int Samples = 64;

    ViewportEnvironment(TextureData prefiltered, SphericalHarmonicsL2 irradiance) {
        Prefiltered = prefiltered;
        Irradiance = irradiance;
    }

    public TextureData Prefiltered { get; }

    public SphericalHarmonicsL2 Irradiance { get; }

    public static ViewportEnvironment Studio() {
        var cube = new TextureData(PixelFormat.Rgba16Float, Size, Size, 1, faceCount: CubeMap.Faces);
        for (var face = 0; face < CubeMap.Faces; face++) {
            for (var y = 0; y < Size; y++) {
                for (var x = 0; x < Size; x++) {
                    CubeMap.WriteTexel(cube, 0, face, x, y, Radiance(CubeMap.DirectionOfTexel(face, x, y, Size)));
                }
            }
        }

        return new(EnvironmentPrefilter.Specular(cube, Levels, Samples), SphericalHarmonicsL2.Project(cube));
    }

    static Vector3 Radiance(Vector3 direction) {
        var height = direction.Y * 0.5f + 0.5f;
        var room = Vector3.Lerp(new(0.09f, 0.085f, 0.08f), new(0.42f, 0.44f, 0.48f), height * height);
        var key = SoftBox(direction, Vector3.Normalize(new(0.6f, 0.7f, 0.5f)), 0.86f) * new Vector3(9f, 8.6f, 8f);
        var fill = SoftBox(direction, Vector3.Normalize(new(-0.8f, 0.2f, 0.3f)), 0.92f) * new Vector3(1.6f, 1.9f, 2.4f);
        return room + key + fill;
    }

    static float SoftBox(Vector3 direction, Vector3 axis, float edge) =>
        Math.Clamp((Vector3.Dot(direction, axis) - edge) / ((1f - edge) * 0.25f), 0f, 1f);
}
