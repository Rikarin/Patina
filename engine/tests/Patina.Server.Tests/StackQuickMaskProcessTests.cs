using Patina.Contracts;

namespace Patina.Server.Tests;

/// <summary>
///     A quick mask painted, kept to inside and outside, cleared and left out of a save, with a real Worker (#127).
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A dab on the quick mask paints no layer. A larger dab kept inside it reaches only the quick mask's disc, and
    ///     kept outside only the ring round it; together they are the larger dab alone. Cleared, the next quick mask is
    ///     empty and keeps nothing back. A save writes no quick mask beside the stack.
    /// </summary>
    [Fact]
    public async Task A_quick_mask_keeps_a_stroke_inside_or_outside_what_was_painted_on_it() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("Quick.vxlayers", deadline.Token);

        static StackStroke Dab(string name, float radius, string? quick) =>
            new(name, "paint", "baseColor", new(radius, Falloff: 0f), Red, [new(400, 400)], QuickMask: quick);

        var small = await CountAfterAsync(state.Id, "small", Dab("small", 30f, null), Reddish, deadline.Token);
        var large = await CountAfterAsync(state.Id, "large", Dab("large", 60f, null), Reddish, deadline.Token);
        Assert.True(large > small * 3, $"A dab of 60 took {large} texels against {small} for one of 30.");

        Assert.False((await QuickMaskAsync(state.Id, null, deadline.Token)).Painted);
        var mask = Dab("mask", 30f, StackQuickMasks.Paint) with { Colour = [1f, 1f, 1f, 1f] };
        await EditAsync(state.Id, deadline.Token, mask);
        Assert.Equal(0, await CountAsync(state.Id, "masked", Reddish, deadline.Token));
        var shown = await QuickMaskAsync(state.Id, new(true), deadline.Token);
        Assert.True(shown is { Shown: true, Painted: true }, $"The quick mask is {shown}.");

        var inside = await CountAfterAsync(
            state.Id,
            "inside",
            Dab("inside", 60f, StackQuickMasks.Inside),
            Reddish,
            deadline.Token
        );
        var outside = await CountAfterAsync(
            state.Id,
            "outside",
            Dab("outside", 60f, StackQuickMasks.Outside),
            Reddish,
            deadline.Token
        );
        Assert.InRange(inside, small * 0.9, small * 1.1);
        Assert.InRange(outside, (large - small) * 0.9, (large - small) * 1.1);

        var cleared = await QuickMaskAsync(state.Id, new(false, true), deadline.Token);
        Assert.True(cleared is { Shown: false, Painted: false }, $"The quick mask is {cleared}.");
        var unkept = Dab("free", 60f, StackQuickMasks.Inside);
        var free = await CountAfterAsync(state.Id, "free", unkept, Reddish, deadline.Token);
        Assert.InRange(free, large * 0.98, large * 1.02);

        await SaveAsync(state.Id, deadline.Token);
        var beside = Directory.EnumerateFiles(Path.GetDirectoryName(state.Path)!)
            .Where(static file => file.Contains(StackQuickMasks.FileMarker, StringComparison.Ordinal));
        Assert.Empty(beside);
    }

    /// <summary>
    ///     A fill is kept to the quick mask by its mask (#282): kept inside, it covers the quick mask's disc, as a dab
    ///     of that size would; kept outside, the rest; and the two cover the fill's whole map. A quick mask nothing has
    ///     painted refuses to keep anything.
    /// </summary>
    [Fact]
    public async Task A_fill_is_kept_inside_or_outside_the_quick_mask() {
        using var deadline = Deadline(300);
        var (_, state) = await StartAsync("QuickFill.vxlayers", deadline.Token);
        var disc = await CountAfterAsync(
            state.Id,
            "disc",
            new("disc", "paint", "baseColor", new(30f, Falloff: 0f), Red, [new(400, 400)]),
            Reddish,
            deadline.Token
        );

        await EditAsync(
            state.Id,
            deadline.Token,
            new StackAddLayer("fill", "Red", new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = Red })
        );
        var fill = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        var whole = await CountAsync(state.Id, "whole", Reddish, deadline.Token);
        Assert.True(whole > disc * 20, $"The fill covers {whole} texels against {disc} for the disc.");

        using (var refused = await http.PostAsJsonAsync(
                   $"/api/v1/stacks/{state.Id}/commands",
                   new StackCommandBatch([new StackKeepQuickMask(fill)]),
                   ContractsJsonContext.Default.StackCommandBatch,
                   deadline.Token
               )) {
            Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Contains(Api.Refusals.QuickMaskUnpainted, await refused.Content.ReadAsStringAsync(deadline.Token));
        }

        StackStroke quick = new(
            "quick",
            "paint",
            "baseColor",
            new(30f, Falloff: 0f),
            [1f, 1f, 1f, 1f],
            [new(400, 400)],
            QuickMask: StackQuickMasks.Paint
        );
        await EditAsync(state.Id, deadline.Token, quick);

        await EditAsync(state.Id, deadline.Token, new StackKeepQuickMask(fill));
        var inside = await CountAsync(state.Id, "inside", Reddish, deadline.Token);
        await ActAsync(state.Id, "undo", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackKeepQuickMask(fill, true));
        var outside = await CountAsync(state.Id, "outside", Reddish, deadline.Token);

        Assert.InRange(inside, disc * 0.9, disc * 1.1);
        Assert.InRange(inside + outside, whole * 0.98, whole * 1.01);
    }

    /// <summary>A stroke on the quick mask may not paint a layer's mask, and a quick mask takes three words.</summary>
    [Fact]
    public async Task A_quick_mask_stroke_is_refused_what_it_cannot_paint() {
        using var deadline = Deadline(120);
        var (_, state) = await StartAsync("QuickRefused.vxlayers", deadline.Token);
        StackStroke stroke = new(
            "mask",
            "paint",
            "baseColor",
            new(10f),
            Red,
            [new(10, 10)],
            Mask: true,
            QuickMask: StackQuickMasks.Paint
        );

        foreach (var refused in new[] { stroke, stroke with { Mask = false, QuickMask = "around" } }) {
            using var response = await http.PostAsJsonAsync(
                $"/api/v1/stacks/{state.Id}/commands",
                new StackCommandBatch([refused]),
                ContractsJsonContext.Default.StackCommandBatch,
                deadline.Token
            );
            Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains(Api.Refusals.BrushRefused, await response.Content.ReadAsStringAsync(deadline.Token));
        }
    }

    /// <summary>Reads the quick mask, or changes it and reads what it became.</summary>
    async Task<StackQuickMaskState> QuickMaskAsync(string id, StackQuickMaskRequest? request, CancellationToken token) {
        var route = $"/api/v1/stacks/{id}/quick-mask";
        if (request is null) {
            return (await http.GetFromJsonAsync(route, ContractsJsonContext.Default.StackQuickMaskState, token))!;
        }

        using var response = await http.PutAsJsonAsync(
            route,
            request,
            ContractsJsonContext.Default.StackQuickMaskRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.StackQuickMaskState, token))!;
    }
}
