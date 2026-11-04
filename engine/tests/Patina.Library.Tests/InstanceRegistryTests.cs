using Patina.Contracts;
using System.Diagnostics;

namespace Patina.Library.Tests;

public sealed class InstanceRegistryTests : IDisposable {
    /// <summary>A process id with nothing behind it: the test runner's own child, already finished.</summary>
    static readonly int DeadProcess = StartAndWait();

    readonly string folder = Directory.CreateTempSubdirectory("patina-registry-").FullName;

    string FilePath => Path.Combine(folder, "nested", "servers.json");

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(folder, true);

    InstanceRegistry Registry() => new(FilePath);

    static ServerInstance Instance(string library, int? processId = null) =>
        new(library, processId ?? Environment.ProcessId, 5000, "token", "1.0.0");

    static int StartAndWait() {
        using var process = Process.Start(
            new ProcessStartInfo(Environment.ProcessPath!, "--version") {
                RedirectStandardOutput = true, RedirectStandardError = true
            }
        )!;
        process.WaitForExit();
        return process.Id;
    }

    [Fact]
    public async Task An_empty_machine_lists_nothing() {
        Assert.Empty(await Registry().ListAsync(Token));
        Assert.Null(await Registry().FindAsync("/games/Ember.ptlib", Token));
    }

    [Fact]
    public async Task A_registered_server_is_found_by_its_library() {
        var instance = Instance("/games/Ember.ptlib");

        Assert.Null(await Registry().RegisterAsync(instance, Token));

        Assert.Equal(instance, await Registry().FindAsync("/games/Ember.ptlib/", Token));
    }

    [Fact]
    public async Task One_server_per_library() {
        var first = Instance("/games/Ember.ptlib");
        await Registry().RegisterAsync(first, Token);

        var owner = await Registry().RegisterAsync(first with { Port = 6000 }, Token);

        Assert.Equal(first, owner);
        Assert.Single(await Registry().ListAsync(Token));
    }

    [Fact]
    public async Task Two_libraries_have_two_servers() {
        await Registry().RegisterAsync(Instance("/games/Ember.ptlib"), Token);
        await Registry().RegisterAsync(Instance("/games/Ash.ptlib"), Token);

        Assert.Equal(2, (await Registry().ListAsync(Token)).Count);
    }

    [Fact]
    public async Task A_server_that_died_gives_up_its_library() {
        await Registry().RegisterAsync(Instance("/games/Ember.ptlib", DeadProcess), Token);

        Assert.Empty(await Registry().ListAsync(Token));
        Assert.Null(await Registry().RegisterAsync(Instance("/games/Ember.ptlib"), Token));
    }

    [Fact]
    public async Task Unregistering_keeps_the_others() {
        await Registry().RegisterAsync(Instance("/games/Ember.ptlib"), Token);

        await Registry().UnregisterAsync(Environment.ProcessId, Token);

        Assert.Empty(await Registry().ListAsync(Token));
    }

    [Fact]
    public async Task A_damaged_file_counts_as_empty() {
        await Registry().RegisterAsync(Instance("/games/Ember.ptlib"), Token);
        await File.WriteAllTextAsync(FilePath, "{ not json", Token);

        Assert.Empty(await Registry().ListAsync(Token));
    }

    [Fact]
    public async Task Servers_starting_together_lose_no_entries() {
        await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(i => Task.Run(() => Registry().RegisterAsync(Instance($"/games/{i}"), Token), Token))
        );

        Assert.Equal(20, (await Registry().ListAsync(Token)).Count);
    }

    [Fact]
    public async Task The_file_is_private_to_its_owner() {
        if (OperatingSystem.IsWindows()) {
            Assert.Skip("Windows has no Unix file modes; the folder is per-user there.");
            return;
        }

        await Registry().RegisterAsync(Instance("/games/Ember.ptlib"), Token);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
    }

    [Fact]
    public async Task A_client_in_another_language_can_read_the_file() {
        await Registry().RegisterAsync(Instance("/games/Ember.ptlib"), Token);

        var json = await File.ReadAllTextAsync(FilePath, Token);

        Assert.Contains("\"instances\"", json, StringComparison.Ordinal);
        Assert.Contains("\"port\": 5000", json, StringComparison.Ordinal);
        Assert.Contains("\"token\": \"token\"", json, StringComparison.Ordinal);
    }
}
