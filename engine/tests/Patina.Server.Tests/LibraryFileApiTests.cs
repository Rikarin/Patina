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
    ///     The fonts a graph's Text draws with and the SVGs its Vector draws are listed for a client to pick from, as
    ///     pictures are (#139), and the system says which files they are, for a dialog of the client's own machine.
    /// </summary>
    [Fact]
    public async Task The_library_lists_its_fonts_and_drawings_for_a_client_to_pick_from() {
        await MakeCrateAsync();
        foreach (var file in new[] { "fonts/Plate.ttf", "fonts/Sign.OTF", "logos/logo.svg", "pictures/rust.jpg" }) {
            var full = Path.Combine(server.Library, "projects", "crate", file);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, "bytes", Token);
        }

        var fonts = await client.GetFromJsonAsync<LibraryFileList>("/api/v1/library/files?kind=font", Token);
        var vectors = await client.GetFromJsonAsync<LibraryFileList>("/api/v1/library/files?kind=vector", Token);
        var system = await client.GetFromJsonAsync<SystemInfo>("/api/v1/system", Token);

        var named = fonts!.Files.Select(static file => file.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["Plate.ttf", "Sign.OTF"], named);
        Assert.Equal([new LibraryFile("projects/crate/logos/logo.svg", "logo.svg", "crate")], vectors!.Files);
        Assert.Equal([".ttf", ".otf"], system!.Formats.Fonts);
        Assert.Equal([".svg"], system.Formats.Vectors);
    }

    /// <summary>
    ///     The folders a client can write an export or a bake into are listed for one that cannot see the disk of the
    ///     Server: every project's and those in it, the asked project's first, then the exports folder's; a hidden
    ///     folder and a project inside a project are not (#4).
    /// </summary>
    [Fact]
    public async Task The_library_lists_the_folders_a_client_can_write_into() {
        await MakeCrateAsync();
        using var barrel = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Barrel", Kind: "layered-material"),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, barrel.StatusCode);
        foreach (var folder in new[] {
                     "projects/crate/out/maps", "projects/crate/.paint-backup", "projects/barrel/Pictures",
                     "exports/crate"
                 }) {
            Directory.CreateDirectory(Path.Combine(server.Library, folder));
        }

        var listed = await client.GetFromJsonAsync<LibraryFileList>(
            "/api/v1/library/files?kind=folder&project=crate",
            Token
        );

        Assert.Equal(
            [
                new("projects/crate", "crate", "crate"), new("projects/crate/out", "out", "crate"),
                new("projects/crate/out/maps", "maps", "crate"), new("projects/barrel", "barrel", "barrel"),
                new("projects/barrel/Pictures", "Pictures", "barrel"),
                new("projects/shared-assets", "shared-assets", "shared-assets"),
                new("exports", "exports", string.Empty),
                new LibraryFile("exports/crate", "crate", string.Empty)
            ],
            listed!.Files
        );
        Assert.False(listed.More);
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

    /// <summary>
    ///     A picture asked for as a thumbnail comes sized down, and is decoded once: its PNG is kept in the cache by
    ///     content, and a picture shown once is known to read when it is brought in (#269).
    /// </summary>
    [Fact]
    public async Task A_thumbnail_is_sized_down_and_kept_by_content() {
        await MakeCrateAsync();
        var pictures = Directory.CreateDirectory(Path.Combine(server.Library, "projects/crate/pictures"));
        var wide = Path.Combine(pictures.FullName, "wide.exr");
        await File.WriteAllBytesAsync(wide, ExrCodec.Encode(new(64, 32, 1, new float[64 * 32])), Token);

        const string Thumbnail = "/api/v1/library/picture?path=projects/crate/pictures/wide.exr&side=16";
        using var first = await client.GetAsync(Thumbnail, Token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("image/png", first.Content.Headers.ContentType?.MediaType);
        var bytes = await first.Content.ReadAsByteArrayAsync(Token);
        var (width, height, _, _) = PngCodec.Decode16(bytes);
        Assert.Equal((16, 8), (width, height));

        var kept = Path.Combine(server.Library, ".ptcache", "pictures");
        Assert.Single(Directory.EnumerateFiles(kept, "*.png"));
        Assert.Single(Directory.EnumerateFiles(kept, "*.read"));
        using var again = await client.GetAsync(Thumbnail, Token);
        Assert.Equal(bytes, await again.Content.ReadAsByteArrayAsync(Token));
        Assert.Single(Directory.EnumerateFiles(kept, "*.png"));

        // The same bytes from outside the library are known to read already.
        var copy = Path.Combine(files.FullName, "copied.exr");
        File.Copy(wide, copy);
        using var imported = await client.PostAsJsonAsync(
            "/api/v1/library/project/pictures",
            new ProjectPictureRequest("crate", copy),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Single(Directory.EnumerateFiles(kept, "*.read"));
    }

    /// <summary>
    ///     A picture that took the Worker down is refused from then on and never handed to the next Worker, which it
    ///     would take down too (#293); the next Worker still reads the others.
    /// </summary>
    [Fact]
    public async Task A_picture_that_took_the_Worker_down_is_not_asked_about_again() {
        await MakeCrateAsync();
        var shared = Directory.CreateDirectory(Path.Combine(server.Library, "shared"));
        await File.WriteAllBytesAsync(
            Path.Combine(shared.FullName, "bomb.exr"),
            ExrCodec.Encode(new(4, 4, 1, new float[16])),
            Token
        );
        await File.WriteAllBytesAsync(
            Path.Combine(shared.FullName, "fine.exr"),
            ExrCodec.Encode(new(2, 2, 1, new float[4])),
            Token
        );
        await pictures.DisposeAsync();
        pictures = await PictureWorker.ConnectAsync(
            server.SocketPath,
            Token,
            static file => file.EndsWith("bomb.exr", StringComparison.Ordinal)
        );

        using var first = await client.GetAsync("/api/v1/library/picture?path=shared/bomb.exr", Token);
        await pictures.DisposeAsync();
        pictures = await PictureWorker.ConnectAsync(server.SocketPath, Token);
        using var again = await client.GetAsync("/api/v1/library/picture?path=shared/bomb.exr", Token);
        using var fine = await client.GetAsync("/api/v1/library/picture?path=shared/fine.exr", Token);

        foreach (var refused in new[] { first, again }) {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Contains(
                "picture-unreadable",
                await refused.Content.ReadAsStringAsync(Token),
                StringComparison.Ordinal
            );
        }

        Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
        Assert.Equal(1, pictures.Reads);
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

    /// <summary>
    ///     A texturing project made at a size bakes at that size until someone says otherwise (#144), where every
    ///     project baked at 2048 whatever it was made at; one made at the default size keeps nothing in its file.
    /// </summary>
    [Fact]
    public async Task A_project_bakes_at_the_size_it_was_made_at() {
        var mesh = Path.Combine(files.FullName, "crate.glb");
        File.WriteAllText(mesh, "mesh");

        foreach (var (name, side) in new[] { ("Small", 1024), ("Large", 4096), ("Plain", 2048) }) {
            using var created = await client.PostAsJsonAsync(
                "/api/v1/library/projects",
                new ProjectCreateRequest(name, Mesh: mesh, Resolution: side),
                Token
            );
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        async Task<int> BakeSide(string path) =>
            (await client.GetFromJsonAsync<ProjectContents>($"/api/v1/library/project?path={path}", Token))!
            .Bake.Options.Resolution;

        Assert.Equal((1024, 4096, 2048), (await BakeSide("small"), await BakeSide("large"), await BakeSide("plain")));
        var plain = await File.ReadAllTextAsync(Path.Combine(server.Library, "projects/plain/project.yaml"), Token);
        Assert.DoesNotContain("bake", plain, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A new project bakes with the options the person made their default (#189), a texturing one still at the size
    ///     it was made at, and options no bake could use are refused rather than kept.
    /// </summary>
    [Fact]
    public async Task A_new_project_bakes_with_the_default_options() {
        var mesh = Path.Combine(files.FullName, "crate.glb");
        File.WriteAllText(mesh, "mesh");
        var preferred = new BakeOptions { Resolution = 512, Supersampling = 4, Maps = ["normal", "ao"] };
        (await client.PutAsJsonAsync("/api/v1/preferences", new ServerPreferences(new()) { Bake = preferred }, Token))
            .EnsureSuccessStatusCode();
        var unusable = new ServerPreferences(new()) { Bake = new() { Maps = [] } };
        using var refused = await client.PutAsJsonAsync("/api/v1/preferences", unusable, Token);

        foreach (var request in new[] {
                     new ProjectCreateRequest("Crate", Mesh: mesh, Resolution: 1024), new ProjectCreateRequest("Empty")
                 }) {
            (await client.PostAsJsonAsync("/api/v1/library/projects", request, Token)).EnsureSuccessStatusCode();
        }

        async Task<BakeOptions> OptionsOf(string path) =>
            (await client.GetFromJsonAsync<ProjectContents>($"/api/v1/library/project?path={path}", Token))!
            .Bake.Options;

        var (crate, empty) = (await OptionsOf("crate"), await OptionsOf("empty"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal((1024, 4), (crate.Resolution, crate.Supersampling));
        Assert.Equal(["normal", "ao"], crate.Maps);
        Assert.Equal((512, 4), (empty.Resolution, empty.Supersampling));
    }
}
