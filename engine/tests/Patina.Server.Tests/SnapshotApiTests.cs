using Patina.Contracts;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>Named checkpoints of a document, over the API a client uses (#209).</summary>
public sealed partial class LibraryTreeApiTests {
    /// <summary>
    ///     A snapshot is of what is saved, so one of a document with unsaved edits is refused with a code; with the
    ///     edits gone, it is kept and listed.
    /// </summary>
    [Fact]
    public async Task A_snapshot_of_unsaved_edits_is_refused_and_one_of_the_saved_file_is_listed() {
        var (document, _, stack) = await OpenStackAsync();
        await EditAsync(stack);

        using var refused = await client.PostAsJsonAsync(
            "/api/v1/library/snapshots",
            new SnapshotTakeRequest(document, "Edited"),
            Token
        );
        using var closed = await client.DeleteAsync($"/api/v1/stacks/{stack}?discard=true", Token);
        using var taken = await client.PostAsJsonAsync(
            "/api/v1/library/snapshots",
            new SnapshotTakeRequest(document, "Saved"),
            Token
        );
        var list = await client.GetFromJsonAsync<DocumentSnapshotList>(
            $"/api/v1/library/snapshots?path={Uri.EscapeDataString(document)}",
            Token
        );

        Assert.Equal("snapshot-unsaved", await CodeOfAsync(refused));
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        Assert.Equal("Saved", Assert.Single(list!.Items).Name);
    }

    /// <summary>
    ///     A snapshot goes back only into a closed document, whose session would otherwise write over it; closed, the
    ///     file is what the snapshot holds, and how it stood is kept as a snapshot of its own.
    /// </summary>
    [Fact]
    public async Task A_snapshot_goes_back_only_into_a_closed_document_and_keeps_how_it_stood() {
        var (document, file, stack) = await OpenStackAsync();
        var original = await File.ReadAllTextAsync(file, Token);
        using var taken = await client.PostAsJsonAsync(
            "/api/v1/library/snapshots",
            new SnapshotTakeRequest(document, "Start"),
            Token
        );
        var snapshot = await taken.Content.ReadFromJsonAsync<DocumentSnapshot>(Token);

        using var open = await client.PostAsJsonAsync(
            "/api/v1/library/snapshots/restore",
            new SnapshotRequest(document, snapshot!.Id),
            Token
        );
        using var closed = await client.DeleteAsync($"/api/v1/stacks/{stack}", Token);
        await File.WriteAllTextAsync(file, original + "# written since\n", Token);
        using var restored = await client.PostAsJsonAsync(
            "/api/v1/library/snapshots/restore",
            new SnapshotRequest(document, snapshot.Id),
            Token
        );
        var before = await restored.Content.ReadFromJsonAsync<DocumentSnapshot>(Token);

        Assert.Equal("snapshot-document-open", await CodeOfAsync(open));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(original, await File.ReadAllTextAsync(file, Token));
        Assert.Equal("Before restoring Start", before!.Name);
    }

    /// <summary>A path outside the library is no document of it.</summary>
    [Fact]
    public async Task A_snapshot_of_a_file_outside_the_library_is_refused() {
        using var refused = await client.PostAsJsonAsync(
            "/api/v1/library/snapshots",
            new SnapshotTakeRequest("../outside.vxlayers", null),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    async Task<string?> CodeOfAsync(HttpResponseMessage response) {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return problem.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
