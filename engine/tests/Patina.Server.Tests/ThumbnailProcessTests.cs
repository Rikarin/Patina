using Patina.Library;
using System.Buffers.Binary;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     Pictures of what a library holds, for the shelf and the Library's grid alike: drawn by the Worker into the
///     cache, named by hash, and only time lost with the cache.
/// </summary>
public sealed partial class StackProcessTests {
    [Fact]
    public async Task Materials_and_projects_have_pictures_by_hash_that_a_deleted_cache_only_draws_again() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var (_, unused) = await StartAsync("Unused.vxlayers", deadline.Token);
        // ⚠ A material that cannot draw has no picture, by design, so a Worker with no device answers as one with a
        // broken drawing would. A stack's pixels say which it is, and skip where there is no device.
        await PixelsAsync(unused.Id, false, deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Crate", kind = "layered-material" },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        var file = Path.Combine(Library, "projects", "crate", "cells.vxtexgraph");
        await File.WriteAllTextAsync(file, CellGraph("{ id: 1, type: Source/Noise }"), deadline.Token);

        var listed = await http.GetFromJsonAsync<JsonElement>(
            "/api/v1/library/materials?project=crate",
            deadline.Token
        );
        var items = listed.GetProperty("items").EnumerateArray().ToList();
        string SourceOf(Func<JsonElement, bool> wanted) => items.First(wanted).GetProperty("source").GetString()!;
        var graph = SourceOf(static item => item.GetProperty("source").GetString()!.EndsWith(
                "cells.vxtexgraph",
                StringComparison.Ordinal
            )
        );
        var starter = SourceOf(static item => item.GetProperty("source").GetString()!.StartsWith(
                "starter:",
                StringComparison.Ordinal
            )
        );
        var shipped = SourceOf(static item => item.GetProperty("source").GetString()!.StartsWith(
                "smart:",
                StringComparison.Ordinal
            )
        );
        string[] sources = [
            graph, starter, shipped, "projects/crate/missing.vxtexgraph", "projects/crate", "projects",
            "projects/crate/crate.vxlayers"
        ];

        var first = await ThumbnailsAsync(sources, deadline.Token);
        Assert.All(first[..3], static hash => Assert.Matches("^[0-9a-f]{64}$", hash));
        Assert.Null(first[3]);
        // A project is the picture of the document that stands for it: its layered material comes before its graph.
        Assert.Equal(first[6], first[4]);
        Assert.NotEqual(first[0], first[4]);
        Assert.Null(first[5]);
        foreach (var hash in first[..3]) {
            Assert.Equal((256, 256), await PictureSizeAsync(hash!, deadline.Token));
        }

        // Asked again, nothing is drawn: the answer is the same, and quick.
        Assert.Equal(first, await ThumbnailsAsync(sources, deadline.Token));

        // The cache goes, pictures and all: the same materials are drawn again, to the same pictures.
        Directory.Delete(LibraryLayout.PreviewsPath(Library), true);
        var again = await ThumbnailsAsync(sources, deadline.Token);
        Assert.Equal(first, again);
        Assert.Equal((256, 256), await PictureSizeAsync(again[0]!, deadline.Token));

        // A saved edit is another picture.
        await File.WriteAllTextAsync(
            file,
            CellGraph("{ id: 1, type: Source/Noise, values: { Scale: [31] } }"),
            deadline.Token
        );
        Assert.NotEqual(first[0], (await ThumbnailsAsync([graph], deadline.Token))[0]);
    }

    async Task<string?[]> ThumbnailsAsync(IReadOnlyList<string> sources, CancellationToken token) {
        using var reply = await http.PostAsJsonAsync("/api/v1/library/thumbnails", new { sources }, token);
        var text = await reply.Content.ReadAsStringAsync(token);
        Assert.True(reply.IsSuccessStatusCode, text);
        var items = JsonElement.Parse(text).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(sources, items.Select(static item => item.GetProperty("source").GetString()));
        return [
            ..items.Select(static item => item.GetProperty("hash") is { ValueKind: JsonValueKind.String } hash
                    ? hash.GetString()
                    : null
            )
        ];
    }

    async Task<(int Width, int Height)> PictureSizeAsync(string hash, CancellationToken token) {
        using var reply = await http.GetAsync($"/api/v1/previews/{hash}", token);
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        var png = await reply.Content.ReadAsByteArrayAsync(token);
        return (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)),
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
    }
}
