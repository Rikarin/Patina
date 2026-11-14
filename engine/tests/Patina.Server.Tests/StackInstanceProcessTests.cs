using Patina.Contracts;
using Patina.Server.Stacks;

namespace Patina.Server.Tests;

/// <summary>
///     Instances across texture sets with a real Worker (#108): an edit to the source shows in the other set.
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A set whose only covering layer is an instance of another set's base makes that set's picture, and goes on
    ///     making it after the base changes, unsaved, in its own session.
    /// </summary>
    [Fact]
    public async Task An_edit_to_a_source_shows_in_its_instance_in_another_texture_set() {
        using var deadline = Deadline(300);
        var (_, body) = await StartAsync("Body.vxlayers", deadline.Token);
        var lidPath = Path.Combine(Library, "Lid.vxlayers");
        StackDocuments.Create(lidPath, Side, Side);
        StackDocuments.SetSetName(lidPath, "Lid");
        var lid = await OpenAsync(lidPath, deadline.Token);

        await EditAsync(lid.Id, deadline.Token, new StackAddInstance(new("base", "Body")));
        Assert.Equal(
            Digest(await PixelsAsync(body.Id, true, deadline.Token), "baseColor"),
            Digest(await PixelsAsync(lid.Id, true, deadline.Token), "baseColor")
        );
        var before = Digest(await PixelsAsync(lid.Id, true, deadline.Token), "baseColor");

        await EditAsync(body.Id, deadline.Token, new StackSetLayer("base", Values: Tint));

        var after = Digest(await PixelsAsync(lid.Id, true, deadline.Token), "baseColor");
        Assert.NotEqual(before, after);
        Assert.Equal(Digest(await PixelsAsync(body.Id, true, deadline.Token), "baseColor"), after);
    }

    /// <summary>
    ///     An instance of a paint layer in another set shows the source's strokes before they are saved (#108): the
    ///     Worker's session of the instance's set reads the canvas the source's session paints. They stay through a
    ///     Worker lost and replaced, which replays the source's strokes, and go with an undo there. Before, the
    ///     instance read the source's file and showed only what was saved.
    /// </summary>
    [Fact]
    public async Task Unsaved_strokes_on_a_source_show_in_its_instance_in_another_texture_set() {
        using var deadline = Deadline(300);
        var (_, opened) = await StartAsync("Painted.vxlayers", deadline.Token, out var worker);
        var lidPath = Path.Combine(Library, "Cap.vxlayers");
        StackDocuments.Create(lidPath, Side, Side);
        StackDocuments.SetSetName(lidPath, "Cap");
        var lid = await OpenAsync(lidPath, deadline.Token);
        await EditAsync(lid.Id, deadline.Token, new StackAddInstance(new("paint", "Body")));

        // Opened again after the instance's set, so the evaluation loop comes to the instance first.
        using (var closed = await http.DeleteAsync($"/api/v1/stacks/{opened.Id}", deadline.Token)) {
            Assert.True(closed.IsSuccessStatusCode);
        }

        var body = await OpenAsync(opened.Path, deadline.Token);
        var before = Digest(await PixelsAsync(lid.Id, false, deadline.Token), "baseColor");
        var previewed = await BaseColorPreviewAsync(lid.Id, deadline.Token);

        await EditAsync(
            body.Id,
            deadline.Token,
            new StackStroke("unsaved", "paint", "baseColor", new(30f, 0.3f), Red, Line(100, 100, 10))
        );
        // Asked for first, the instance's set is evaluated before the source's stroke reaches the Worker, and again
        // once it has.
        await PixelsAsync(lid.Id, false, deadline.Token);
        await PixelsAsync(body.Id, false, deadline.Token);
        var shown = Digest(await PixelsAsync(lid.Id, false, deadline.Token), "baseColor");
        Assert.NotEqual(before, shown);
        Assert.Equal(Digest(await PixelsAsync(lid.Id, true, deadline.Token), "baseColor"), shown);
        // ⚠ A read evaluates afresh, and what a client is shown is what the evaluation loop last published.
        var moved = await PreviewMovedAsync(lid.Id, previewed, deadline.Token);
        Assert.NotEqual(previewed, moved);

        worker.Kill();
        await worker.WaitForExitAsync(deadline.Token);
        StartWorker();
        // Asked for first, the instance's set may be evaluated before the source's set is loaded again, and refused
        // for a canvas never saved, then evaluated again once the source's strokes are replayed.
        using (await http.GetAsync($"/api/v1/stacks/{lid.Id}/pixels?clean=false", deadline.Token)) { }

        await PixelsAsync(body.Id, false, deadline.Token);
        Assert.Equal(shown, Digest(await PixelsAsync(lid.Id, false, deadline.Token), "baseColor"));

        await ActAsync(body.Id, "undo", deadline.Token);
        await PixelsAsync(body.Id, false, deadline.Token);
        Assert.Equal(before, Digest(await PixelsAsync(lid.Id, false, deadline.Token), "baseColor"));
        Assert.Equal(previewed, await PreviewMovedAsync(lid.Id, moved, deadline.Token));
    }

    async Task<string> BaseColorPreviewAsync(string id, CancellationToken token) =>
        (await StateAsync(id, token)).Previews.Single(static preview => preview.Usage == "baseColor").Hash;

    /// <summary>
    ///     The set's published base color once it is another than <paramref name="from" />, or after a while.
    /// </summary>
    async Task<string> PreviewMovedAsync(string id, string from, CancellationToken token) {
        for (var tries = 0; tries < 100; tries++) {
            if (await BaseColorPreviewAsync(id, token) is var hash && hash != from) {
                return hash;
            }

            await Task.Delay(100, token);
        }

        return from;
    }
}
