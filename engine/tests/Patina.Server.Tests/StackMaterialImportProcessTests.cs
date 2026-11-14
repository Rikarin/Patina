using Patina.Contracts;
using Patina.Export;
using Patina.Library;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

public sealed partial class StackProcessTests {
    /// <summary>
    ///     What glTF says beyond a material's pictures, brought in through the Worker and drawn on the GPU (#157): a
    ///     picture moved by its texture transform is drawn moved and clamped where its sampler clamps it (#280), a
    ///     blended material's alpha is the exported alpha, and
    ///     an emissive factor times its strength is the emission. A node input spelled wrong would be dropped silently,
    ///     so only the exported pixels prove the graph says what was meant.
    /// </summary>
    [Fact]
    public async Task A_gltfs_placement_alpha_and_emission_are_drawn_as_the_gltf_meant() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync("/api/v1/library/projects", new { name = "Placed" }, deadline.Token);
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        var gltf = WritePlacedGltf(folder.FullName);

        using var imported = await http.PostAsJsonAsync(
            "/api/v1/library/project/materials",
            new MaterialImportRequest("placed", gltf),
            deadline.Token
        );
        Assert.True(imported.IsSuccessStatusCode, await imported.Content.ReadAsStringAsync(deadline.Token));
        var job = await BuildAsync("godot", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.Equal(0, job.Report!.Failed);

        var exports = Path.Combine(Library, "exports", "godot", "placed");

        byte[] Texel(string file, float across) {
            var picture = MapPictures.ReadFile(Path.Combine(exports, file));
            var at = (picture.Height / 2 * picture.Width + (int)(picture.Width * across)) * picture.Channels;
            return [
                ..Enumerable.Range(0, picture.Channels)
                    .Select(lane => (byte)MathF.Round(picture.Samples[at + lane] * 255))
            ];
        }

        // The picture is red on its left half and blue on its right; moved by half, the left reads the right, and
        // the right reads past the edge, which its sampler clamps. Repeated, it would read red. ⚠ The bitmap clamps
        // too while it resamples the picture (#289): a bitmap that wrapped as the graph does had a last column half
        // blue and half the first column's red, and the clamp held that half.
        var left = Texel("Placed_albedo.png", 0.25f);
        var right = Texel("Placed_albedo.png", 0.75f);
        Assert.True(left[2] > 200 && left[0] < 50, string.Join(",", left));
        Assert.True(right[2] > 200 && right[0] < 50, string.Join(",", right));
        Assert.InRange(left[3], 126, 130);
        // Linear (0.5, 0.25, 0) times a strength of 2 is (1, 0.5, 0), which is sRGB (255, 188, 0).
        var emission = Texel("Placed_emission.png", 0.5f);
        Assert.True(
            emission.Take(3).Zip((byte[])[255, 188, 0]).All(static p => Math.Abs(p.First - p.Second) <= 2),
            string.Join(",", emission)
        );
    }

    /// <summary>
    ///     A glTF of one triangle and one material: a picture red on the left and blue on the right, moved by half its
    ///     width and clamped at its edges, at half alpha and blended, and an emission of numbers alone made brighter by
    ///     its strength.
    /// </summary>
    static string WritePlacedGltf(string folder) {
        const int Side = 8;
        var pixels = new byte[Side * Side * 3];
        for (var texel = 0; texel < Side * Side; texel++) {
            pixels[texel * 3 + (texel % Side < Side / 2 ? 0 : 2)] = 255;
        }

        File.WriteAllBytes(Path.Combine(folder, "halves.png"), PngCodec.Encode8(Side, Side, 3, pixels));
        var gltf = TriangleGltf(
            Path.Combine(folder, "placed.gltf"),
            """
            {
              "name": "Glow",
              "pbrMetallicRoughness": {
                "baseColorTexture": {
                  "index": 0,
                  "extensions": { "KHR_texture_transform": { "offset": [0.5, 0] } }
                },
                "baseColorFactor": [1, 1, 1, 0.5]
              },
              "alphaMode": "BLEND",
              "emissiveFactor": [0.5, 0.25, 0],
              "extensions": { "KHR_materials_emissive_strength": { "emissiveStrength": 2 } }
            }
            """,
            ["halves.png"]
        );
        // CLAMP_TO_EDGE both ways.
        File.WriteAllText(
            gltf,
            File.ReadAllText(gltf)
                .Replace(
                    "\"textures\": [{ \"source\": 0 }]",
                    "\"samplers\": [{ \"wrapS\": 33071, \"wrapT\": 33071 }], "
                    + "\"textures\": [{ \"source\": 0, \"sampler\": 0 }]",
                    StringComparison.Ordinal
                )
        );
        return gltf;
    }
}
