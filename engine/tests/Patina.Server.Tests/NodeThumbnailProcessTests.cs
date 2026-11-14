using Patina.Library;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     Pictures of node types for a palette and a Nodes list (#183, #38): each type drawn once at its defaults, its
///     picture inputs fed a noise, by hash in the same cache as every other picture.
/// </summary>
public sealed partial class StackProcessTests {
    [Fact]
    public async Task Node_types_have_pictures_by_hash_drawn_once_and_none_for_a_type_with_no_picture() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var (_, unused) = await StartAsync("Unused.vxlayers", deadline.Token);
        // A type that cannot draw has no picture, as a Worker with no device would answer, so the device is asked.
        await PixelsAsync(unused.Id, false, deadline.Token);
        var graph = await http.OpenAsync(GraphTesting.Write(Library, GraphTesting.Sample()), deadline.Token);
        // A shipped grunge is a compound of Patina's own content, and the shelf's pictures of it are drawn here (#169).
        string[] types = [
            "Source/Noise", "Filters/Blur", "Source/Shape", "Grunges/Grunge 01", "Output/Output", "Nobody/Here"
        ];

        var first = await NodePicturesAsync(graph.Id, types, deadline.Token);

        Assert.All(first[..4], static hash => Assert.Matches("^[0-9a-f]{64}$", hash));
        Assert.Equal(4, first[..4].Distinct().Count());
        Assert.Null(first[4]);
        Assert.Null(first[5]);
        Assert.Equal((64, 64), await PictureSizeAsync(first[0]!, deadline.Token));

        // Asked again, nothing is drawn; with the cache gone, the same pictures are drawn again.
        Assert.Equal(first, await NodePicturesAsync(graph.Id, types, deadline.Token));
        Directory.Delete(LibraryLayout.PreviewsPath(Library), true);
        Assert.Equal(first, await NodePicturesAsync(graph.Id, types, deadline.Token));

        using var missing = await http.PostAsJsonAsync(
            "/api/v1/graphs/nobody/node-types/thumbnails",
            new { sources = types },
            deadline.Token
        );
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    /// <summary>
    ///     A stack's node types are drawn as a graph's palette draws them, keyed the same, so the layer panel's lists can
    ///     show each (#290). A generator reads mesh maps, so a stack with none has no picture of it; the bake test
    ///     draws one from a stack's bake.
    /// </summary>
    [Fact]
    public async Task A_stacks_node_types_have_the_pictures_a_graphs_palette_has() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var (_, stack) = await StartAsync("Generators.vxlayers", deadline.Token);
        await PixelsAsync(stack.Id, false, deadline.Token);
        var graph = await http.OpenAsync(GraphTesting.Write(Library, GraphTesting.Sample()), deadline.Token);
        string[] types = ["Source/Noise", "Grunges/Grunge 01", Generator, "Output/Output", "Nobody/Here"];

        var drawn = await NodePicturesAsync(stack.Id, types, deadline.Token, "stacks");

        Assert.All(drawn[..2], static hash => Assert.Matches("^[0-9a-f]{64}$", hash));
        Assert.All(drawn[2..], Assert.Null);
        Assert.Equal(drawn, await NodePicturesAsync(graph.Id, types, deadline.Token));

        using var missing = await http.PostAsJsonAsync(
            "/api/v1/stacks/nobody/node-types/thumbnails",
            new { sources = types },
            deadline.Token
        );
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    const string Generator = "Generators/Dirt";

    async Task<string?[]> NodePicturesAsync(
        string document,
        IReadOnlyList<string> types,
        CancellationToken token,
        string kind = "graphs"
    ) {
        using var reply = await http.PostAsJsonAsync(
            $"/api/v1/{kind}/{document}/node-types/thumbnails",
            new { sources = types },
            token
        );
        var text = await reply.Content.ReadAsStringAsync(token);
        Assert.True(reply.IsSuccessStatusCode, text);
        var items = JsonElement.Parse(text).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(types, items.Select(static item => item.GetProperty("source").GetString()));
        return [
            ..items.Select(static item => item.GetProperty("hash") is { ValueKind: JsonValueKind.String } hash
                    ? hash.GetString()
                    : null
            )
        ];
    }
}
