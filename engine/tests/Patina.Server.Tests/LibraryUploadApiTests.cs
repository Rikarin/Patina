using Patina.Contracts;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>Files a client sends from its own computer (#4, ADR 0041).</summary>
public sealed partial class LibraryTreeApiTests {
    async Task<HttpResponseMessage> UploadAsync(string name, byte[] bytes) {
        using var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new("application/octet-stream");
        return await client.PostAsync($"/api/v1/library/uploads?name={Uri.EscapeDataString(name)}", body, Token);
    }

    /// <summary>
    ///     A mesh sent from a client's computer is kept in the library's uploads folder under its own name, the same
    ///     file sent twice once, and a project takes it as its mesh by the path the Server answered with.
    /// </summary>
    [Fact]
    public async Task A_mesh_sent_from_another_computer_becomes_a_project_s_mesh() {
        using var sent = await UploadAsync("crate.glb", [.."mesh"u8]);
        using var again = await UploadAsync("crate.glb", [.."mesh"u8]);
        using var other = await UploadAsync("crate.glb", [.."other mesh"u8]);
        sent.EnsureSuccessStatusCode();
        var path = (await sent.Content.ReadFromJsonAsync<UploadedFile>(Token))!.Path;
        var second = (await again.Content.ReadFromJsonAsync<UploadedFile>(Token))!.Path;
        var third = (await other.Content.ReadFromJsonAsync<UploadedFile>(Token))!.Path;

        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Mesh: path),
            Token
        );

        Assert.Equal(path, second);
        Assert.NotEqual(path, third);
        Assert.Equal("crate.glb", Path.GetFileName(path));
        Assert.StartsWith(
            Path.Combine(Path.GetFullPath(server.Library), "uploads") + Path.DirectorySeparatorChar,
            path,
            StringComparison.Ordinal
        );
        Assert.Equal("mesh", await File.ReadAllTextAsync(path, Token));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(File.Exists(Path.Combine(server.Library, "projects", "crate", "crate.glb")));
        Assert.Empty(Directory.GetFiles(Path.Combine(server.Library, "uploads"), ".partial-*"));
    }

    /// <summary>
    ///     A file over the limit is refused with a code before any of it is kept (#297): the Server here takes 1 MiB, the
    ///     product 4 GiB, by the same check.
    /// </summary>
    [Fact]
    public async Task A_file_over_the_limit_is_refused_and_nothing_is_kept() {
        using var refused = await UploadAsync("huge.png", new byte[ServerFixture.UploadMostBytes + 1]);
        using var taken = await UploadAsync("small.png", new byte[ServerFixture.UploadMostBytes]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, refused.StatusCode);
        Assert.Contains("upload-too-large", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        taken.EnsureSuccessStatusCode();
        var kept = Directory.EnumerateFiles(Path.Combine(server.Library, "uploads"), "*", SearchOption.AllDirectories);
        Assert.Equal(["small.png"], kept.Select(Path.GetFileName));
    }

    /// <summary>
    ///     An upload nothing names any more is listed and cleared into the bin (#297); one a project's file names
    ///     stays, and so does one sent within the hour, which its sender may be about to use.
    /// </summary>
    [Fact]
    public async Task Uploads_nothing_names_are_listed_and_cleared() {
        using var crate = await UploadAsync("crate.glb", [.."crate"u8]);
        using var scan = await UploadAsync("scan.png", [.."scan"u8]);
        using var fresh = await UploadAsync("fresh.png", [.."fresh"u8]);
        var mesh = (await crate.Content.ReadFromJsonAsync<UploadedFile>(Token))!.Path;
        var picture = (await scan.Content.ReadFromJsonAsync<UploadedFile>(Token))!.Path;
        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Mesh: mesh),
            Token
        );
        // The project's mesh names its upload as where it came from, which keeps the upload.
        created.EnsureSuccessStatusCode();
        File.SetLastWriteTimeUtc(mesh, DateTime.UtcNow.AddDays(-1));
        File.SetLastWriteTimeUtc(picture, DateTime.UtcNow.AddDays(-1));

        var unused = await client.GetFromJsonAsync<UnusedUploadList>("/api/v1/library/uploads/unused", Token);
        using var cleared = await client.PostAsync("/api/v1/library/uploads/clear", null, Token);

        Assert.Equal(["scan.png"], unused!.Files.Select(static file => Path.GetFileName(file.Path)));
        Assert.Equal(1, (await cleared.Content.ReadFromJsonAsync<UploadsCleared>(Token))!.Count);
        Assert.False(File.Exists(picture));
        Assert.True(File.Exists(mesh));
    }

    /// <summary>A name that is no picture or mesh, or is more than a file's name, is refused with a code.</summary>
    [Theory]
    [InlineData("notes.txt")]
    [InlineData("../escape.png")]
    [InlineData("sub/scan.png")]
    [InlineData(".hidden.png")]
    public async Task A_file_no_project_reads_is_not_kept(string name) {
        using var refused = await UploadAsync(name, [.."bytes"u8]);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("upload-kind", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(server.Library, "uploads")));
    }
}
