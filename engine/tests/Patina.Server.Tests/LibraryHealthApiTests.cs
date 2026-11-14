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
        var tidied = await cleaned.Content.ReadFromJsonAsync<LibraryTidied>(Token);

        Assert.Equal(new LibraryTidied(1, 500), tidied);
        Assert.False(Directory.Exists(Path.Combine(crate, LibraryLayout.PaintBackupFolder)));
        Assert.True(File.Exists(Path.Combine(crate, LibraryLayout.ProjectFile)));
    }
}
