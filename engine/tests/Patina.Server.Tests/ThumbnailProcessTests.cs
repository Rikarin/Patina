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

    /// <summary>
    ///     A shipped compound on the shelf has a picture at the shelf's size (#298): a noise as it makes itself, a
    ///     filter over the standard noise, and two compounds are two pictures. A source the shelf does not offer has
    ///     none.
    /// </summary>
    [Fact]
    public async Task Shipped_compounds_on_the_shelf_have_pictures() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var (_, unused) = await StartAsync("Unused.vxlayers", deadline.Token);
        await PixelsAsync(unused.Id, false, deadline.Token);
        var listed = await http.GetFromJsonAsync<JsonElement>("/api/v1/library/materials", deadline.Token);
        var compounds = listed.GetProperty("items")
            .EnumerateArray()
            .Where(static item => item.GetProperty("source").GetString()!.StartsWith("compound:", StringComparison.Ordinal))
            .ToList();
        // A noise draws from nothing; a generator reads a bake, which a shelf has not got, and shows its name.
        string SourceOf(string role, string prefix) =>
            compounds.Select(static item => item.GetProperty("source").GetString()!)
                .First(source => source.StartsWith(prefix, StringComparison.Ordinal)
                    && compounds.Any(item => item.GetProperty("source").GetString() == source
                        && item.GetProperty("role").GetString() == role
                    )
                );
        string[] sources = [
            SourceOf("mask", "compound:Noise/"), SourceOf("filter", "compound:"), "compound:Nowhere/Nothing"
        ];

        var drawn = await ThumbnailsAsync(sources, deadline.Token);

        Assert.All(drawn[..2], static hash => Assert.Matches("^[0-9a-f]{64}$", hash));
        Assert.NotEqual(drawn[0], drawn[1]);
        Assert.Null(drawn[2]);
        Assert.Equal((256, 256), await PictureSizeAsync(drawn[0]!, deadline.Token));
        Assert.Equal(drawn, await ThumbnailsAsync(sources, deadline.Token));
    }

    /// <summary>
    ///     A project's pictures are on its shelf as Pictures (#203), each by its path below the library, and drawn as
    ///     any shelf material is: a saved-over picture is another picture.
    /// </summary>
    [Fact]
    public async Task A_projects_pictures_are_on_its_shelf_with_pictures_of_their_own() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var (_, unused) = await StartAsync("Unused.vxlayers", deadline.Token);
        await PixelsAsync(unused.Id, false, deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Crate", kind = "layered-material" },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        var pictures = Directory.CreateDirectory(Path.Combine(Library, "projects", "crate", "pictures")).FullName;
        var file = Path.Combine(pictures, "rust.png");
        await File.WriteAllBytesAsync(file, Png([200, 80, 20, 255]), deadline.Token);

        var listed = await http.GetFromJsonAsync<JsonElement>(
            "/api/v1/library/materials?project=crate&search=rust",
            deadline.Token
        );
        var item = Assert.Single(
            listed.GetProperty("items").EnumerateArray(),
            static item => item.GetProperty("role").GetString() == "picture"
        );
        Assert.Equal("rust", item.GetProperty("name").GetString());
        var source = item.GetProperty("source").GetString()!;
        Assert.Equal("projects/crate/pictures/rust.png", source);
        Assert.Contains(
            listed.GetProperty("roles").EnumerateArray(),
            static role => role.GetProperty("value").GetString() == "picture"
        );

        var drawn = (await ThumbnailsAsync([source], deadline.Token))[0];
        Assert.Matches("^[0-9a-f]{64}$", drawn);
        Assert.Equal((256, 256), await PictureSizeAsync(drawn!, deadline.Token));

        await File.WriteAllBytesAsync(file, Png([20, 80, 200, 255]), deadline.Token);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        Assert.NotEqual(drawn, (await ThumbnailsAsync([source], deadline.Token))[0]);

        static byte[] Png(byte[] texel) =>
            Vixen.Core.Imaging.PngCodec.Encode(new(2, 2, [..texel, ..texel, ..texel, ..texel]));
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
