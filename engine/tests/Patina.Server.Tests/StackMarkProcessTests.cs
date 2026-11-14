using Patina.Contracts;
using Patina.Library;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     The mark on a texture set as the app makes it (#274, #295): New Project on find-isolate's two quads, each set's
///     stack open and the front one worn, and the other set pointed at. The renderer's own test and the viewport's
///     process test draw the mark on Linux CI's software GPU, while that e2e saw no orange in the Server's picture; this
///     is the e2e's path with no window, so it fails in the engine's CI if the Worker draws no mark there.
/// </summary>
public sealed partial class StackProcessTests {
    [Fact]
    public async Task A_texture_set_pointed_at_is_marked_orange_while_another_set_is_worn() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = Path.Combine(folder.FullName, "sets.obj");
        await File.WriteAllLinesAsync(
            mesh,
            [
                "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0", "v 1.2 0 0", "v 2.2 0 0", "v 2.2 1 0", "v 1.2 1 0",
                "vt 0 0", "vt 1 0", "vt 1 1", "vt 0 1",
                "usemtl metal", "f 1/1 2/2 3/3 4/4",
                "usemtl wood", "f 5/1 6/2 7/3 8/4"
            ],
            deadline.Token
        );
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        await SplitIntoSetsAsync(project, deadline.Token);
        var contents = await http.GetFromJsonAsync<ProjectContents>(
            $"/api/v1/library/project?path={project}",
            deadline.Token
        );
        var sets = contents!.Documents.Where(static d => d.Kind == DocumentKinds.Texturing).ToList();
        var metal = await OpenAsync(sets.Single(static d => d.Material == "metal").Path, deadline.Token);
        await OpenAsync(sets.Single(static d => d.Material == "wood").Path, deadline.Token);
        using (var worn = await http.PutAsJsonAsync(
                   "/api/v1/viewport/stack",
                   new { stack = metal.Id },
                   deadline.Token
               )) {
            Assert.True(worn.IsSuccessStatusCode, await worn.Content.ReadAsStringAsync(deadline.Token));
        }

        var keep = Environment.GetEnvironmentVariable("PATINA_PICTURES") ?? folder.FullName;
        var whole = Orange(await SettledShotAsync(Path.Combine(keep, "sets-worn-whole.png"), deadline.Token));
        using (var marked = await http.PutAsJsonAsync(
                   "/api/v1/viewport/highlight",
                   new ViewportHighlight([], ["wood"]),
                   ContractsJsonContext.Default.ViewportHighlight,
                   deadline.Token
               )) {
            Assert.True(marked.IsSuccessStatusCode, await marked.Content.ReadAsStringAsync(deadline.Token));
        }

        var wood = Orange(await SettledShotAsync(Path.Combine(keep, "sets-worn-wood-marked.png"), deadline.Token));

        Assert.Equal(0, whole);
        Assert.True(wood > 1000, $"Only {wood} pixels of the Server's picture are orange with wood marked.");
    }

    /// <summary>The e2e's count: pixels far redder than blue, which neither grey quad nor the sky is.</summary>
    static int Orange(byte[] png) {
        var picture = PngCodec.Decode(png);
        var pixels = picture.Pixels;
        var count = 0;
        for (var at = 0; at + 3 < pixels.Length; at += 4) {
            count += pixels[at] > 120 && pixels[at] > pixels[at + 2] + 60 ? 1 : 0;
        }

        return count;
    }
}
