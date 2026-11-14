using Patina.Library;
using System.Net;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>A library picture, decoded by the real Worker and served by the Server, which decodes none (#293).</summary>
public sealed partial class StackProcessTests {
    [Fact]
    public async Task A_library_picture_is_decoded_by_the_Worker_and_a_broken_one_refused() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var pictures = Directory.CreateDirectory(Path.Combine(Library, "projects", "shared-assets", "pictures"));
        var wide = ExrCodec.Encode(new(64, 32, 1, new float[64 * 32]));
        await File.WriteAllBytesAsync(Path.Combine(pictures.FullName, "wide.exr"), wide, deadline.Token);
        await File.WriteAllTextAsync(Path.Combine(pictures.FullName, "scan.tga"), "not much", deadline.Token);
        await StartAsync("Unused.vxlayers", deadline.Token);

        const string Folder = "/api/v1/library/picture?path=projects/shared-assets/pictures/";
        using var shown = await http.GetAsync(Folder + "wide.exr&side=16", deadline.Token);
        using var broken = await http.GetAsync(Folder + "scan.tga", deadline.Token);

        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        var (width, height, _, _) = PngCodec.Decode16(await shown.Content.ReadAsByteArrayAsync(deadline.Token));
        Assert.Equal((16, 8), (width, height));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, broken.StatusCode);
        Assert.Contains(
            "picture-unreadable",
            await broken.Content.ReadAsStringAsync(deadline.Token),
            StringComparison.Ordinal
        );
    }
}
