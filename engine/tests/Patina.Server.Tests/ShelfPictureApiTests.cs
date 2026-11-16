using Microsoft.Extensions.DependencyInjection;
using Patina.Contracts;
using System.Buffers.Binary;

namespace Patina.Server.Tests;

/// <summary>
///     The pictures of the library's other projects are on the shelf beside the open project's (#203), from Other
///     projects, and one brought into its project after the shelf was first read is listed the next time.
/// </summary>
public sealed class ShelfPictureApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    const string Library = """
                           schema: 1
                           id: 0190a000-0000-7000-8000-00000000abcf
                           name: Game
                           texelDensity: 1024
                           channels: []
                           defaultShader: pbr
                           exportTargets: []
                           """;

    public async ValueTask InitializeAsync() {
        File.WriteAllText(Path.Combine(server.Library, "library.yaml"), Library);
        foreach (var folder in new[] { "shared", "projects" }) {
            Directory.CreateDirectory(Path.Combine(server.Library, folder));
        }

        client = server.AuthorisedClient();
        foreach (var name in new[] { "Alpha", "Wear Kit" }) {
            (await client.PostAsJsonAsync("/api/v1/library/projects", new ProjectCreateRequest(name), Token))
                .EnsureSuccessStatusCode();
        }
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task Another_projects_pictures_are_on_the_shelf_and_a_search_finds_them() {
        var pictures = Path.Combine(server.Library, "projects", "wear-kit", "pictures");
        Directory.CreateDirectory(pictures);
        await File.WriteAllBytesAsync(Path.Combine(pictures, "Rust.png"), [1, 2, 3], Token);
        await File.WriteAllBytesAsync(Path.Combine(server.Library, "projects", "alpha", "Own.png"), [4], Token);
        server.Services.GetRequiredService<LibraryHost>().RequestRescan();

        var rust = Assert.Single(await PicturesAsync("alpha", "Rust", static found => found.Count > 0));
        Assert.Equal(
            ("Rust", "picture", "projects/wear-kit/pictures/Rust.png", "library", "Wear Kit"),
            (rust.Name, rust.Role, rust.Source, rust.Origin, rust.Project)
        );
        Assert.Equal(["PNG"], rust.Tags);
        var own = Assert.Single(await PicturesAsync("alpha", "Own", static found => found.Count > 0));
        Assert.Equal(("project", "Alpha"), (own.Origin, own.Project));
        Assert.Single(await PicturesAsync("alpha", "rust other png", static found => found.Count > 0));
        Assert.Empty(await PicturesAsync("alpha", "rust brush", static _ => true));

        // Copied in after the shelf read the project, two folders deep: the next scan lists it, though the project's
        // file did not change (#306).
        var deep = Directory.CreateDirectory(Path.Combine(pictures, "worn", "edges")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(deep, "Moss.png"), Png(2048, 512), Token);
        server.Services.GetRequiredService<LibraryHost>().RequestRescan();
        var moss = Assert.Single(await PicturesAsync("alpha", "Moss", static found => found.Count > 0));
        Assert.Equal(("library", "projects/wear-kit/pictures/worn/edges/Moss.png"), (moss.Origin, moss.Source));
        Assert.Equal(["PNG", "2048 × 512"], moss.Tags);
        Assert.Single(await PicturesAsync("alpha", "2048", static found => found.Count > 0));
    }

    /// <summary>The start of a PNG as wide and high as asked: its signature and its header chunk.</summary>
    static byte[] Png(int width, int height) {
        byte[] head = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, ..("IHDR"u8), ..new byte[8]];
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(20), height);
        return head;
    }

    /// <summary>
    ///     The pictures a project's shelf finds for a search, once the index lists every project: it catches up after a
    ///     project is made.
    /// </summary>
    async Task<IReadOnlyList<MaterialInfo>> PicturesAsync(
        string project,
        string search,
        Func<IReadOnlyList<MaterialInfo>, bool> done
    ) {
        for (var attempt = 0;; attempt++) {
            var list = await client.GetFromJsonAsync<MaterialList>(
                $"/api/v1/library/materials?project={project}&search={Uri.EscapeDataString(search)}",
                Token
            );
            var found = list!.Items.Where(static material => material.Role == "picture").ToList();
            if (done(found) || attempt > 100) {
                return found;
            }

            await Task.Delay(50, Token);
        }
    }
}
