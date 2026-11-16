using Patina.Contracts;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>
///     A normal map laid as a layer's picture is read as the project's normal format says (#310, ADR 0054), unless the
///     change says how; a layer laid before keeps how it was read. Nothing here needs the Worker.
/// </summary>
public sealed class StackNormalFormatApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;
    string stack = string.Empty;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abd1
            name: Game
            texelDensity: 1024
            channels: []
            defaultShader: pbr
            exportTargets: []
            """
        );
        client = server.AuthorisedClient();
        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Crate", Kind: "layered-material"),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var file = Directory.GetFiles(Path.Combine(server.Library, "projects", "crate"), "*.vxlayers").Single();
        using var opened = await client.PostAsJsonAsync("/api/v1/stacks", new StackOpenRequest(file), Token);
        Assert.True(opened.IsSuccessStatusCode, await opened.Content.ReadAsStringAsync(Token));
        stack = (await opened.Content.ReadFromJsonAsync<StackState>(Token))!.Id;
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    async Task EditAsync(params StackCommand[] commands) {
        using var edited = await client.PostAsJsonAsync(
            $"/api/v1/stacks/{stack}/commands",
            new StackCommandBatch(commands),
            Token
        );
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync(Token));
    }

    async Task<StackLayer> LayerAsync(string name) =>
        (await client.GetFromJsonAsync<StackState>($"/api/v1/stacks/{stack}", Token))!.Layers.Single(layer =>
            layer.Name == name
        );

    async Task<string> PictureAsync(string name, string usage, StackSetLayer? change = null) {
        await EditAsync(new StackAddLayer("fill", name));
        var layer = await LayerAsync(name);
        var laid = (change ?? new StackSetLayer(layer.Id)) with {
            Layer = layer.Id, Fill = "Texture", Textures = new Dictionary<string, string> { [usage] = "pictures/a.png" }
        };
        await EditAsync(laid);
        return (await LayerAsync(name)).NormalFormat;
    }

    [Fact]
    public async Task A_normal_picture_is_read_as_the_project_says_unless_the_change_says() {
        // A picture that is no normal says nothing of green, and a layer reads its pictures as they are, as before.
        Assert.Equal(NormalFormats.Down, await PictureAsync("Colour", "baseColor"));
        Assert.Equal(NormalFormats.Up, await PictureAsync("Bumps", "normal"));

        using var settings = await client.PatchAsJsonAsync(
            "/api/v1/library/project/settings",
            new ProjectSettingsRequest("crate", null, null, null) { NormalFormat = NormalFormats.Down },
            Token
        );
        Assert.True(settings.IsSuccessStatusCode, await settings.Content.ReadAsStringAsync(Token));

        Assert.Equal(NormalFormats.Down, await PictureAsync("Coat", "coatNormal"));
        Assert.Equal(NormalFormats.Up, (await LayerAsync("Bumps")).NormalFormat);
        var told = new StackSetLayer("") { NormalFormat = NormalFormats.Up };
        Assert.Equal(NormalFormats.Up, await PictureAsync("Told", "normal", told));

        var bumps = await LayerAsync("Bumps");
        await EditAsync(new StackSetLayer(bumps.Id) { NormalFormat = NormalFormats.Down });
        Assert.Equal(NormalFormats.Down, (await LayerAsync("Bumps")).NormalFormat);
        using var refused = await client.PostAsJsonAsync(
            $"/api/v1/stacks/{stack}/commands",
            new StackCommandBatch([new StackSetLayer(bumps.Id) { NormalFormat = "sideways" }]),
            Token
        );
        Assert.False(refused.IsSuccessStatusCode);
    }
}
