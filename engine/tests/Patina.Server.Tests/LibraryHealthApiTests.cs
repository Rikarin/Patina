using Patina.Contracts;
using Patina.Library;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>Check Library, cache prune and Clean over the API, as the CLI does them alone (#192, #162).</summary>
public sealed class LibraryHealthApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() {
        LibraryFiles.CreateDefaultLibrary(server.Library, "Game");
        client = server.AuthorisedClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    async Task MakeAsync(string name) =>
        (await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest(name, Kind: "graph"),
            Token
        )).EnsureSuccessStatusCode();

    [Fact]
    public async Task A_check_reports_by_kind_with_errors_first() {
        await MakeAsync("Crate");
        var healthy = await (await client.PostAsync("/api/v1/library/check", null, Token)).Content
            .ReadFromJsonAsync<LibraryHealth>(Token);
        Assert.Equal(1, healthy!.Projects);
        Assert.Equal(0, healthy.Errors);

        // A folder copied by hand: two projects with one GUID.
        var crate = Path.Combine(server.Library, LibraryLayout.ProjectsFolder, "crate");
        var copy = Path.Combine(server.Library, LibraryLayout.ProjectsFolder, "crate-copy");
        Directory.CreateDirectory(copy);
        File.Copy(Path.Combine(crate, LibraryLayout.ProjectFile), Path.Combine(copy, LibraryLayout.ProjectFile));

        var found = await (await client.PostAsync("/api/v1/library/check", null, Token)).Content
            .ReadFromJsonAsync<LibraryHealth>(Token);

        Assert.Equal(2, found!.Projects);
        var first = found.Groups[0];
        Assert.True(first.IsError);
        Assert.Equal(nameof(FindingKind.DuplicateId), first.Kind);
        Assert.True(first.Count >= 2); // the projects, and the documents they share
        Assert.Equal(found.Errors, found.Groups.Where(static group => group.IsError).Sum(static group => group.Count));
    }

    [Fact]
    public async Task Pruning_takes_only_what_nothing_has_touched_for_the_days_given() {
        var objects = LibraryLayout.ObjectsPath(server.Library);
        Directory.CreateDirectory(objects);
        var old = Path.Combine(objects, "old.bin");
        var fresh = Path.Combine(objects, "fresh.bin");
        await File.WriteAllBytesAsync(old, new byte[1000], Token);
        await File.WriteAllBytesAsync(fresh, new byte[10], Token);
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-40));

        var pruned = await client.PostAsJsonAsync("/api/v1/library/cache/prune", new CachePruneRequest(30), Token);
        var tidied = await pruned.Content.ReadFromJsonAsync<LibraryTidied>(Token);

        Assert.Equal(1, tidied!.Removed);
        Assert.True(tidied.Bytes >= 1000);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));

        var refused = await client.PostAsJsonAsync("/api/v1/library/cache/prune", new CachePruneRequest(-1), Token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task Clean_takes_the_copies_of_moved_paint_and_nothing_else() {
        await MakeAsync("Crate");
        var crate = Path.Combine(server.Library, LibraryLayout.ProjectsFolder, "crate");
        var backup = Path.Combine(crate, LibraryLayout.PaintBackupFolder, "20261114-120000");
        Directory.CreateDirectory(backup);
        await File.WriteAllBytesAsync(Path.Combine(backup, "base.png"), new byte[500], Token);

        var cleaned = await client.PostAsync("/api/v1/library/clean", null, Token);
        var tidied = await cleaned.Content.ReadFromJsonAsync<LibraryCleaned>(Token);

        Assert.Equal(new(1, 500, 0, 0, null), tidied);
        Assert.False(Directory.Exists(Path.Combine(crate, LibraryLayout.PaintBackupFolder)));
        Assert.True(File.Exists(Path.Combine(crate, LibraryLayout.ProjectFile)));
    }

    /// <summary>
    ///     Clean moves the pictures no document reads into the bin as one entry, and Restore puts them back (#162). A
    ///     document names its own project's picture by <c>pictures/…</c>, and another project's by that project's
    ///     folder too, so a name alike in another project keeps nothing.
    /// </summary>
    [Fact]
    public async Task Clean_bins_the_pictures_nothing_reads_and_restore_brings_them_back() {
        await MakeAsync("Crate");
        await MakeAsync("Barrel");
        var projects = Path.Combine(server.Library, LibraryLayout.ProjectsFolder);
        var pictures = Path.Combine(projects, "crate", "pictures");
        Directory.CreateDirectory(Path.Combine(pictures, "scan"));
        foreach (var name in new[] { "used.png", "shared.png", "alike.png", "scan/left.jpg" }) {
            await File.WriteAllBytesAsync(Path.Combine(pictures, name), new byte[100], Token);
        }

        await File.AppendAllTextAsync(Graph("crate"), "# pictures/used.png\n", Token);
        await File.AppendAllTextAsync(Graph("barrel"), "# ../crate/pictures/shared.png pictures/alike.png\n", Token);

        var cleaned = await CleanAsync();

        Assert.Equal((2, 200), (cleaned.Pictures, cleaned.PictureBytes));
        Assert.True(File.Exists(Path.Combine(pictures, "used.png")));
        Assert.True(File.Exists(Path.Combine(pictures, "shared.png")));
        Assert.False(File.Exists(Path.Combine(pictures, "alike.png")));
        Assert.False(File.Exists(Path.Combine(pictures, "scan", "left.jpg")));
        var bin = await client.GetFromJsonAsync<BinList>("/api/v1/library/bin", Token);
        Assert.Equal(LibraryBin.PicturesKind, Assert.Single(bin!.Items).Kind);

        var restored = await client.PostAsJsonAsync(
            "/api/v1/library/bin/restore",
            new BinRestoreRequest(cleaned.Bin!),
            Token
        );

        restored.EnsureSuccessStatusCode();
        Assert.True(File.Exists(Path.Combine(pictures, "alike.png")));
        Assert.True(File.Exists(Path.Combine(pictures, "scan", "left.jpg")));
        Assert.Empty((await client.GetFromJsonAsync<BinList>("/api/v1/library/bin", Token))!.Items);
        Assert.Equal(2, (await CleanAsync()).Pictures);

        string Graph(string project) => Directory.GetFiles(Path.Combine(projects, project), "*.vxtexgraph")[0];
    }

    /// <summary>
    ///     Library Settings edits the name and the texel density (#192): kept in library.yaml, read back by GET
    ///     /library, and refused when blank or not above zero, with the file as it was.
    /// </summary>
    [Fact]
    public async Task The_library_settings_are_kept_in_its_file_and_refused_out_of_range() {
        var set = await client.PutAsJsonAsync(
            "/api/v1/library/settings",
            new LibrarySettingsRequest("  Ember  ", 512),
            Token
        );

        var info = await set.Content.ReadFromJsonAsync<LibraryInfo>(Token);
        Assert.Equal(("Ember", 512d), (info!.Name, info.TexelDensity));
        var file = Path.Combine(server.Library, LibraryLayout.LibraryFile);
        var kept = await File.ReadAllTextAsync(file, Token);
        Assert.Contains("texelDensity: 512", kept, StringComparison.Ordinal);
        foreach (var refused in new LibrarySettingsRequest[] { new(" ", 512), new("Ember", 0), new("Ember", -1) }) {
            using var answer = await client.PutAsJsonAsync("/api/v1/library/settings", refused, Token);
            Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        }

        Assert.Equal(kept, await File.ReadAllTextAsync(file, Token));
        Assert.Equal("Ember", (await client.GetFromJsonAsync<LibraryInfo>("/api/v1/library", Token))!.Name);
    }

    async Task<LibraryCleaned> CleanAsync() {
        using var cleaned = await client.PostAsync("/api/v1/library/clean", null, Token);
        return (await cleaned.Content.ReadFromJsonAsync<LibraryCleaned>(Token))!;
    }
}
