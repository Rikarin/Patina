using Microsoft.Extensions.DependencyInjection;
using Patina.Server.Viewport;

namespace Patina.Server.Tests;

/// <summary>A viewing table belongs to the view, so nothing laid on the mesh takes it away.</summary>
public sealed class ViewportLookTests {
    const string Table = "/looks/warm.cube";

    [Fact]
    public async Task A_stack_put_on_the_mesh_keeps_the_table() {
        await using var server = new ServerFixture();
        var session = server.Services.GetRequiredService<ViewportSession>();
        session.SetLook(new(Table, "hash"));

        session.Wear("session", "/meshes/prop.glb");

        Assert.Equal(Table, session.State.Maps.Look);
    }

    [Fact]
    public async Task Pictures_laid_over_a_stack_keep_the_table() {
        await using var server = new ServerFixture();
        var session = server.Services.GetRequiredService<ViewportSession>();
        session.SetLook(new(Table, "hash"));
        session.Wear("session", "/meshes/prop.glb");

        session.SetMaps(new());

        Assert.Equal(Table, session.State.Maps.Look);
        Assert.Null(session.State.Stack);
    }
}
