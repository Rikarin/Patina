using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;

namespace Patina.Viewport.Tests;

/// <summary>
///     A panorama's light reaches the model whole. These need no device: the environment is baked on the CPU, and its
///     irradiance has an answer on paper.
/// </summary>
public sealed class ViewportEnvironmentTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-environment-");

    public void Dispose() => folder.Delete(true);

    /// <summary>
    ///     A sun one texel of a 2K panorama wide, 45° up, lights a surface facing it with its radiance times its solid
    ///     angle. The environment used to clamp every texel at 64, which kept a seven-hundredth of this sun, and read
    ///     the panorama nearest into a 128 cube, which missed a one-texel sun most of the time. Second-order harmonics
    ///     overstate a point light facing it by a sixteenth, which the bounds allow for.
    /// </summary>
    [Fact]
    public void A_small_sun_in_a_panorama_keeps_its_light() {
        const int width = 2048;
        const int height = 1024;
        const float radiance = 50_000f;
        const int row = height / 4;
        const int column = width / 2 + width / 8;
        var samples = new float[width * height * 3];
        for (var lane = 0; lane < 3; lane++) {
            samples[(row * width + column) * 3 + lane] = radiance;
        }

        var path = Path.Combine(folder.FullName, "sun.exr");
        File.WriteAllBytes(path, ExrCodec.Encode(new(width, height, 3, samples)));

        var environment = ViewportEnvironment.Load(path);

        // The texel's solid angle: its share of a turn across, times the band of cosines it spans down.
        var top = MathF.PI * row / height;
        var bottom = MathF.PI * (row + 1) / height;
        var solidAngle = MathF.Tau / width * (MathF.Cos(top) - MathF.Cos(bottom));
        var middle = (top + bottom) * 0.5f;
        var across = ((column + 0.5f) / width - 0.5f) * MathF.Tau;
        var toward = new Vector3(MathF.Sin(middle) * MathF.Sin(across), MathF.Cos(middle), -MathF.Sin(middle) * MathF.Cos(across));
        var expected = radiance * solidAngle / MathF.PI;

        var received = environment.Irradiance.Irradiance(Vector3.Normalize(toward)).X;

        Assert.InRange(received / expected, 0.85f, 1.25f);
    }
}
