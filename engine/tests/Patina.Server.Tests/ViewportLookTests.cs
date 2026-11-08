using Microsoft.Extensions.DependencyInjection;
using Patina.Contracts;
using Patina.Server.Viewport;
using System.Net;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>
///     The display settings belong to the view, so nothing laid on the mesh takes them away, and a client is told what
///     it asked for that cannot be shown before any of it is.
/// </summary>
public sealed class ViewportLookTests {
    const string Table = "/looks/warm.cube";

    [Fact]
    public async Task A_stack_put_on_the_mesh_keeps_the_display() {
        await using var server = new ServerFixture();
        var session = server.Services.GetRequiredService<ViewportSession>();
        session.SetDisplay(new(new() { Look = Table, ToneMapping = "agx" }, new(Table, "hash"), null));

        session.Wear("session", "/meshes/prop.glb", [], []);

        Assert.Equal(Table, session.State.Display.Look);
        Assert.Equal("agx", session.State.Display.ToneMapping);
    }

    [Fact]
    public async Task Pictures_laid_over_a_stack_keep_the_display() {
        await using var server = new ServerFixture();
        var session = server.Services.GetRequiredService<ViewportSession>();
        session.SetDisplay(new(new() { Look = Table }, new(Table, "hash"), null));
        session.Wear("session", "/meshes/prop.glb", [], []);

        session.SetMaps(new());

        Assert.Equal(Table, session.State.Display.Look);
        Assert.Null(session.State.Stack);
    }

    [Fact]
    public async Task Display_settings_come_back_as_they_were_sent() {
        await using var server = new ServerFixture();
        using var client = server.AuthorisedClient();
        var table = Path.Combine(server.Library, "warm.cube");
        await File.WriteAllTextAsync(
            table,
            "LUT_3D_SIZE 2\n0 0 0\n1 0 0\n0 1 0\n1 1 0\n0 0 1\n1 0 1\n0 1 1\n1 1 1\n",
            TestContext.Current.CancellationToken
        );
        var asked = new ViewportDisplay {
            Environment = "sunset",
            EnvironmentRotation = 1.5,
            LightRotation = -0.5,
            Exposure = 1.5,
            ToneMapping = "filmic",
            Look = table,
            View = "roughness",
            Wireframe = true,
            Orthographic = true,
            FieldOfView = 0.4,
            Vignette = 0.3
        };

        using var reply = await client.PutAsJsonAsync(
            "/api/v1/viewport/display",
            asked,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        var state = await reply.Content.ReadFromJsonAsync<ViewportState>(TestContext.Current.CancellationToken);
        Assert.Equal(asked, state!.Display);
    }

    [Theory]
    [InlineData("""{ "view": "x-ray" }""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("""{ "toneMapping": "kodak" }""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("""{ "fieldOfView": 3.5 }""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("""{ "view": "picture" }""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("""{ "look": "/nowhere/warm.cube" }""", HttpStatusCode.NotFound)]
    [InlineData("""{ "look": "/nowhere/warm.png" }""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("""{ "environment": "/nowhere/sky.exr" }""", HttpStatusCode.NotFound)]
    [InlineData("""{ "environment": "cathedral" }""", HttpStatusCode.UnprocessableEntity)]
    public async Task Settings_that_cannot_be_shown_are_refused_and_change_nothing(string body, HttpStatusCode status) {
        await using var server = new ServerFixture();
        using var client = server.AuthorisedClient();
        var before = server.Services.GetRequiredService<ViewportSession>().State.Display;

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var reply = await client.PutAsync(
            "/api/v1/viewport/display",
            content,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(status, reply.StatusCode);
        Assert.Equal(before, server.Services.GetRequiredService<ViewportSession>().State.Display);
    }
}
