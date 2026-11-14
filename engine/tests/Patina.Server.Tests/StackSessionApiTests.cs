using Patina.Contracts;
using Patina.Server.Api;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>A stack session a Server does not have, as a client finds out after the Server restarted (#226).</summary>
public sealed class StackSessionApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() {
        client = server.AuthorisedClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    /// <summary>
    ///     A session the Server has never heard of is refused with a code, so a client can tell it apart, open the
    ///     document again from its file, and never show the session's id to a person.
    /// </summary>
    [Fact]
    public async Task A_stack_the_Server_does_not_have_is_refused_as_not_open() {
        using var state = await client.GetAsync("/api/v1/stacks/gone", Token);
        using var command = await client.PostAsJsonAsync(
            "/api/v1/stacks/gone/commands",
            new StackCommandBatch([]),
            Token
        );
        using var worn = await client.PutAsJsonAsync("/api/v1/viewport/stack", new ViewportStackRequest("gone"), Token);

        foreach (var refused in new[] { state, command, worn }) {
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
            Assert.Equal(Refusals.StackNotOpen, problem.RootElement.GetProperty("code").GetString());
            Assert.Equal("gone", problem.RootElement.GetProperty("values").GetProperty("id").GetString());
        }
    }

    /// <summary>
    ///     A stack's export in a format a file cannot be, or with a compression and no format, is refused with the
    ///     reason before anything is asked of a Worker (#47).
    /// </summary>
    [Theory]
    [InlineData("gif", null, null, "is not a format")]
    [InlineData("png8", "bc7", null, "cannot be compressed")]
    [InlineData("jpeg", null, "0", "1 to 100")]
    [InlineData(null, "bc7", null, "go with a format")]
    public async Task An_export_a_file_cannot_hold_is_refused_with_the_reason(
        string? format,
        string? compression,
        string? quality,
        string reason
    ) {
        using var refused = await client.PostAsJsonAsync(
            "/api/v1/stacks/gone/export",
            new StackExportRequest("out") { Format = format, Compression = compression, Quality = quality },
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(reason, await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A document from a newer build is refused with a code before a session holds it, since a save here would
    ///     write it back at this build's format and lose what is new (#162). One at this build's format opens.
    /// </summary>
    [Fact]
    public async Task A_document_from_a_newer_build_is_refused_and_left_as_it_is() {
        var file = Path.Combine(server.Library, "Newer.vxlayers");
        const string Newer = "version: 99\nname: Newer\nbaseWidth: 256\nbaseHeight: 256\nsets: []\n";
        await File.WriteAllTextAsync(file, Newer, Token);

        using var refused = await client.PostAsJsonAsync("/api/v1/stacks", new StackOpenRequest(file), Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
        Assert.Equal(Refusals.DocumentNewer, problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("99", problem.RootElement.GetProperty("values").GetProperty("written").GetString());
        Assert.Equal("1", problem.RootElement.GetProperty("values").GetProperty("reads").GetString());
        Assert.Equal(Newer, await File.ReadAllTextAsync(file, Token));
    }
}
