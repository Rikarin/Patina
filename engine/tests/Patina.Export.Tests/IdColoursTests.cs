using Patina.Contracts;
using Patina.Jobs;
using System.Text.Json;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>A bake's ID colours, which the Worker lists for a Server that decodes no picture (#293).</summary>
public sealed class IdColoursTests : IDisposable {
    readonly string folder = Directory.CreateTempSubdirectory("patina-id-").FullName;

    public void Dispose() => Patina.Testing.TempFolders.Delete(folder);

    [Fact]
    public void An_id_map_lists_its_colours_the_largest_first_and_the_one_at_a_place() {
        // Four texels: red twice, green once, and black where no chart is, which is not a colour to choose.
        byte[] texels = [255, 0, 0, 255, 0, 0, 0, 255, 255, 0, 0, 255, 0, 255, 0, 255];
        File.WriteAllBytes(Path.Combine(folder, "id.png"), PngCodec.Encode8(2, 2, 4, texels));
        BakeManifest manifest = new("crate", BakeInputs.Png8, new([new("id", "id.png", 2, 2)], 2, 0, [], false));
        File.WriteAllBytes(
            Path.Combine(folder, "crate." + BakeCache.ManifestFile),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.BakeManifest)
        );

        var colours = IdColours.Read(folder, 0.75f, 0.75f);

        Assert.NotNull(colours);
        Assert.Equal([[1f, 0f, 0f], [0f, 1f, 0f]], colours.Colours.Select(static c => c.Colour.ToArray()));
        Assert.Equal([2 / 3f, 1 / 3f], colours.Colours.Select(static c => c.Share));
        Assert.Equal([0f, 1f, 0f], colours.At);
        Assert.Null(IdColours.Read(Path.Combine(folder, "none"), null, null));
    }
}
