using Patina.Contracts;
using System.Net;
using System.Text.Json;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     The library's pictures and meshes, listed for a client to pick from and brought into a project (#4).
/// </summary>
public sealed partial class LibraryTreeApiTests {
    /// <summary>
    ///     The pictures and meshes every project holds are listed for a client that cannot see the Server's disk, the
    ///     asked project's first and none of what a bake or a hidden folder wrote; a kind it does not list is refused
    ///     with a code (#4).
    /// </summary>
    [Fact]
    public async Task The_library_lists_its_pictures_and_meshes_for_a_client_to_pick_from() {
        await MakeCrateAsync();
        using var barrel = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Barrel", Kind: "layered-material"),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, barrel.StatusCode);
        foreach (var (project, file) in new[] {
                     ("barrel", "pictures/wood.png"), ("crate", "pictures/rust.jpg"), ("crate", "bakes/ao.png"),
                     ("crate", ".paint-backup/old.png"), ("crate", "notes.txt"), ("crate", "crate_low.glb")
                 }) {
            var full = Path.Combine(server.Library, "projects", project, file);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, "bytes", Token);
        }

        var pictures = await client.GetFromJsonAsync<LibraryFileList>(
            "/api/v1/library/files?kind=picture&project=crate",
            Token
        );
        var meshes = await client.GetFromJsonAsync<LibraryFileList>("/api/v1/library/files?kind=mesh", Token);
        using var unknown = await client.GetAsync("/api/v1/library/files?kind=sound", Token);

        Assert.Equal(
            [
                new("projects/crate/pictures/rust.jpg", "rust.jpg", "crate"),
                new LibraryFile("projects/barrel/pictures/wood.png", "wood.png", "barrel")
            ],
            pictures!.Files
        );
        Assert.False(pictures.More);
        Assert.Equal([new LibraryFile("projects/crate/crate_low.glb", "crate_low.glb", "crate")], meshes!.Files);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("file-kind-unknown", await unknown.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A picture another project holds comes into this one as a copy, by its path below the library; one already in
    ///     the project stays where it is, and a path that climbs out of the library is refused (#4).
    /// </summary>
    [Fact]
    public async Task A_picture_the_library_holds_comes_into_a_project_by_its_path() {
        await MakeCrateAsync();
        var other = Directory.CreateDirectory(Path.Combine(server.Library, "projects/barrel/pictures"));
        var wood = PngCodec.Encode8(1, 1, 3, [120, 80, 40]);
        await File.WriteAllBytesAsync(Path.Combine(other.FullName, "wood.png"), wood, Token);
        var own = Directory.CreateDirectory(Path.Combine(server.Library, "projects/crate/pictures"));
        var rust = PngCodec.Encode8(1, 1, 3, [150, 60, 20]);
        await File.WriteAllBytesAsync(Path.Combine(own.FullName, "rust.png"), rust, Token);
        await File.WriteAllTextAsync(Path.Combine(files.FullName, "outside.png"), "outside", Token);
        var outside = Path.GetRelativePath(server.Library, Path.Combine(files.FullName, "outside.png"));

        using var copied = await client.PostAsJsonAsync(
            "/api/v1/library/project/pictures",
            new ProjectPictureRequest("crate", "projects/barrel/pictures/wood.png"),
            Token
        );
        using var kept = await client.PostAsJsonAsync(
            "/api/v1/library/project/pictures",
            new ProjectPictureRequest("crate", "projects/crate/pictures/rust.png"),
            Token
        );
        using var climbed = await client.PostAsJsonAsync(
            "/api/v1/library/project/pictures",
            new ProjectPictureRequest("crate", outside),
            Token
        );

        Assert.Equal(
            "projects/crate/pictures/wood.png",
            (await copied.Content.ReadFromJsonAsync<ProjectPicture>(Token))!.Path
        );
        Assert.Equal(wood, await File.ReadAllBytesAsync(Path.Combine(own.FullName, "wood.png"), Token));
        Assert.Equal(
            "projects/crate/pictures/rust.png",
            (await kept.Content.ReadFromJsonAsync<ProjectPicture>(Token))!.Path
        );
        Assert.Equal(HttpStatusCode.BadRequest, climbed.StatusCode);
        Assert.False(File.Exists(Path.Combine(own.FullName, "outside.png")));
    }

    /// <summary>
    ///     A file named as a picture that does not read as one is refused on every route that brings one in, and
    ///     nothing is copied (#260): before, only its extension was looked at, and the layer that read it failed.
    /// </summary>
    [Theory]
    [InlineData("sky.hdr")]
    [InlineData("paint.psd")]
    [InlineData("scan.tif")]
    [InlineData("rust.png")]
    public async Task A_broken_picture_is_refused_on_every_route_and_copies_nothing(string name) {
        await MakeCrateAsync();
        var file = Path.Combine(files.FullName, name);
        File.WriteAllText(file, "not much");

        using var imported = await client.PostAsJsonAsync(
            "/api/v1/library/project/files",
            new ProjectFileRequest("crate", file),
            Token
        );
        using var added = await client.PostAsJsonAsync(
            "/api/v1/library/project/pictures",
            new ProjectPictureRequest("crate", file),
            Token
        );

        foreach (var refused in new[] { imported, added }) {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
            Assert.Equal("picture-unreadable", problem.RootElement.GetProperty("code").GetString());
            Assert.Equal(name, problem.RootElement.GetProperty("values").GetProperty("file").GetString());
        }

        Assert.False(Directory.Exists(Path.Combine(server.Library, "projects/crate/pictures")));
    }

    /// <summary>A broken picture already in the library is refused by name when a client asks to see it.</summary>
    [Fact]
    public async Task A_broken_picture_in_the_library_is_refused_when_shown() {
        await MakeCrateAsync();
        var pictures = Directory.CreateDirectory(Path.Combine(server.Library, "projects/crate/pictures"));
        await File.WriteAllTextAsync(Path.Combine(pictures.FullName, "scan.tga"), "not much", Token);

        using var served = await client.GetAsync(
            "/api/v1/library/picture?path=projects/crate/pictures/scan.tga",
            Token
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, served.StatusCode);
        Assert.Contains("picture-unreadable", await served.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }
}
