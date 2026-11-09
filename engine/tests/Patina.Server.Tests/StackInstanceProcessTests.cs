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
}
