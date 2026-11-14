using Patina.Server.Api;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     A request refused as asked goes out with a code and its values, and names a file by its name alone, so the app
///     says it in its words and never shows a path (#198). A check the Server makes itself, such as the viewport having
///     no mesh, is not told as a failure of the GPU engine.
/// </summary>
public sealed class RequestRefusalTests {
    static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/api/v1/viewport/pick", """{ "x": 0.5, "y": 0.5 }""")]
    [InlineData("/api/v1/viewport/marquee", """{ "x": 0, "y": 0, "toX": 1, "toY": 1 }""")]
    [InlineData("/api/v1/viewport/cast", """{ "origins": [0, 0, 0], "direction": [0, 0, 1] }""")]
    public async Task A_question_of_a_viewport_with_no_mesh_is_not_an_engine_failure(string route, string body) {
        await using var server = new ServerFixture();
        using var client = server.AuthorisedClient();

        using var reply = await PostAsync(client, route, body);

        Assert.Equal(HttpStatusCode.Conflict, reply.StatusCode);
        Assert.Equal(Refusals.ViewportNoMesh, (await RefusalAsync(reply)).Code);
    }

    [Fact]
    public async Task A_screenshot_of_a_viewport_with_no_mesh_is_not_an_engine_failure() {
        await using var server = new ServerFixture();
        using var client = server.AuthorisedClient();
        var file = JsonSerializer.Serialize(Path.Combine(server.Library, "shot.png"));

        using var reply = await PostAsync(client, "/api/v1/viewport/screenshot", $$"""{ "path": {{file}} }""");

        Assert.Equal(Refusals.ViewportNoMesh, (await RefusalAsync(reply)).Code);
    }

    [Theory]
    [InlineData("""{ "look": "C:/nowhere/deep/warm.png" }""", "file-kind-wrong", "warm.png")]
    [InlineData("""{ "look": "C:/nowhere/deep/warm.cube" }""", "file-missing", "warm.cube")]
    [InlineData("""{ "environment": "C:/nowhere/deep/sky.tga" }""", "file-kind-wrong", "sky.tga")]
    public async Task A_file_refused_is_named_without_where_it_is(string body, string code, string file) {
        await using var server = new ServerFixture();
        using var client = server.AuthorisedClient();

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var reply = await client.PutAsync("/api/v1/viewport/display", content, Token);
        var (sent, values, detail) = await RefusalAsync(reply);

        Assert.Equal(code, sent);
        Assert.Equal(file, values.GetProperty("file").GetString());
        Assert.DoesNotContain("nowhere", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_screenshot_too_large_names_the_sizes_it_takes() {
        await using var server = new ServerFixture();
        using var client = server.AuthorisedClient();
        var file = JsonSerializer.Serialize(Path.Combine(server.Library, "shot.png"));

        using var reply = await PostAsync(
            client,
            "/api/v1/viewport/screenshot",
            $$"""{ "path": {{file}}, "width": 1 }"""
        );
        var (code, values, _) = await RefusalAsync(reply);

        Assert.Equal(Refusals.SizeRefused, code);
        Assert.Equal("2 × 2", values.GetProperty("least").GetString());
    }

    [Theory]
    [InlineData(@"Access to the path 'C:\Users\Ann\Library\T_Crate.png' is denied.", "'T_Crate.png'")]
    [InlineData("""Could not find a part of the path "/home/ann/library/out/".""", "out")]
    [InlineData(@"The share '\\studio\library\crate.ptstack' is busy.", "'crate.ptstack'")]
    public void A_reason_names_a_file_without_where_it_is(string sentence, string named) {
        var reason = Refusals.Reason(sentence)["reason"];

        Assert.Contains(named, reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Users", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("home", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("studio", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reason_with_no_full_path_is_kept_as_it_is() {
        const string Sentence = "'Projects/Crate' is not a project's path, and 'x' is not a port kind.";

        Assert.Equal(Sentence, Refusals.Reason(Sentence)["reason"]);
    }

    static async Task<HttpResponseMessage> PostAsync(HttpClient client, string route, string body) {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(route, content, Token);
    }

    static async Task<(string? Code, JsonElement Values, string Detail)> RefusalAsync(HttpResponseMessage reply) {
        using var problem = JsonDocument.Parse(await reply.Content.ReadAsStringAsync(Token));
        var root = problem.RootElement;
        return (root.GetProperty("code").GetString(), root.GetProperty("values").Clone(),
            root.GetProperty("detail").GetString() ?? string.Empty);
    }
}
