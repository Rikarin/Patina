using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>
///     <c>POST /previews/{hash}/save</c> (#184): Save Image in the 2D View writes a picture the Server has, by its hash,
///     to a file, with no GPU and no pixels in JSON.
/// </summary>
public sealed class PreviewSaveApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    readonly DirectoryInfo outside = Directory.CreateTempSubdirectory("patina-saved-");
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abd0
            name: Game
            texelDensity: 1024
            channels: []
            defaultShader: pbr
            exportTargets: []
            """
        );
        client = server.AuthorisedClient();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
        outside.Delete(true);
    }

    [Fact]
    public async Task A_picture_the_server_has_is_saved_to_a_file_and_one_it_lacks_or_a_bad_place_is_refused() {
        var hash = new string('a', 64);
        var cached = GraphPreviewFiles.PathOf(LibraryLayout.PreviewsPath(server.Library), hash);
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        await File.WriteAllBytesAsync(cached, [137, 80, 78, 71], Token);
        var file = Path.Combine(outside.FullName, "Blur.png");
        await File.WriteAllBytesAsync(file, [0], Token);

        var saved = await client.PostAsJsonAsync($"/api/v1/previews/{hash}/save", new PreviewSaveRequest(file), Token);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal([137, 80, 78, 71], await File.ReadAllBytesAsync(file, Token));

        var missing = await client.PostAsJsonAsync(
            $"/api/v1/previews/{new string('b', 64)}/save",
            new PreviewSaveRequest(file),
            Token
        );
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        foreach (var place in new[] { "Blur.png", Path.Combine(outside.FullName, "no", "Blur.png") }) {
            var refused = await client.PostAsJsonAsync(
                $"/api/v1/previews/{hash}/save",
                new PreviewSaveRequest(place),
                Token
            );
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        }
    }
}
