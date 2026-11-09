using Patina.Contracts;
using Patina.Server.Api;
using System.Net;
using System.Net.Http.Json;
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
}
