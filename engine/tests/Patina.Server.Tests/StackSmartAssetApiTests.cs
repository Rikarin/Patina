using Patina.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace Patina.Server.Tests;

/// <summary>
///     Layers and a mask saved from a stack as a smart material and a smart mask, and applied to another layer (#135),
///     over the API a client uses. Nothing here needs the Worker: saving and applying are edits to documents.
/// </summary>
public sealed class StackSmartAssetApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;
    string stack = string.Empty;

    CancellationToken Token => TestContext.Current.CancellationToken;

    static readonly Dictionary<string, IReadOnlyList<float>> Rust = new() { ["baseColor"] = [0.6f, 0.2f, 0.1f, 1f] };

    public async ValueTask InitializeAsync() {
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abcf
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

    Task<HttpResponseMessage> EditAsync(params StackCommand[] commands) =>
        client.PostAsJsonAsync($"/api/v1/stacks/{stack}/commands", new StackCommandBatch(commands), Token);

    async Task<StackState> StateAsync() =>
        (await client.GetFromJsonAsync<StackState>($"/api/v1/stacks/{stack}", Token))!;

    async Task<StackLayer> LayerAsync(string name) => (await StateAsync()).Layers.Single(layer => layer.Name == name);

    Task<HttpResponseMessage> SaveAsync(string name, IReadOnlyList<string> layers, bool mask = false) =>
        client.PostAsJsonAsync(
            $"/api/v1/stacks/{stack}/save-material",
            new StackSaveMaterialRequest("crate", name, layers, mask),
            Token
        );

    async Task<IReadOnlyList<MaterialInfo>> ShelfAsync() =>
        (await client.GetFromJsonAsync<MaterialList>("/api/v1/library/materials?project=crate", Token))!.Items;

    /// <summary>
    ///     A layer with a curvature mask and a blur on it saves as a smart material and its mask as a smart mask; the
    ///     shelf files the two apart; the material unfolds into the stack again; and the mask, merged onto a layer that
    ///     has one, narrows it by one more entry and one more effect, and undoes to what was there.
    /// </summary>
    [Fact]
    public async Task Layers_and_a_mask_save_to_the_shelf_and_apply_again() {
        using (var added = await EditAsync(new StackAddLayer("fill", "Rust", Rust))) {
            Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync(Token));
        }

        var rust = await LayerAsync("Rust");
        StackMask edge = new(
            "Bake",
            Map: "curvature",
            Effects: [new("Filters/Blur", true, new Dictionary<string, IReadOnlyList<float>> { ["Radius"] = [3f] })]
        );
        using (var masked = await EditAsync(new StackSetLayer(rust.Id, Mask: edge))) {
            Assert.True(masked.IsSuccessStatusCode, await masked.Content.ReadAsStringAsync(Token));
        }

        using var material = await SaveAsync("Rusty", [rust.Id]);
        Assert.True(material.IsSuccessStatusCode, await material.Content.ReadAsStringAsync(Token));
        var savedMaterial = (await material.Content.ReadFromJsonAsync<StackSavedMaterial>(Token))!;
        using var mask = await SaveAsync("Edges", [rust.Id], true);
        Assert.True(mask.IsSuccessStatusCode, await mask.Content.ReadAsStringAsync(Token));
        var savedMask = (await mask.Content.ReadFromJsonAsync<StackSavedMaterial>(Token))!;

        Assert.Equal("material", savedMaterial.Role);
        Assert.Equal("mask", savedMask.Role);
        var shelf = await ShelfAsync();
        Assert.Contains(
            shelf,
            item => item.Name == "Rusty" && item.Role == "material" && item.Source == savedMaterial.Path
        );
        Assert.Contains(shelf, item => item.Name == "Edges" && item.Role == "mask" && item.Source == savedMask.Path);

        // The material unfolds as a layer again, its mask with it.
        using (var applied = await EditAsync(new StackAddLayer("fill", string.Empty, Material: savedMaterial.Path))) {
            Assert.True(applied.IsSuccessStatusCode, await applied.Content.ReadAsStringAsync(Token));
        }

        var unfolded = (await StateAsync()).Layers
            .Where(static layer => layer.Name.StartsWith("Rust", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, unfolded.Length);
        Assert.All(unfolded, static layer => Assert.Equal("Bake", layer.Mask.Source));

        // The mask, merged onto a layer masked by a constant half: its base multiplies over as one more entry.
        using (var other = await EditAsync(
                   new StackAddLayer("fill", "Paint", Rust),
                   new StackSetLayer("paint", Mask: new("Constant", 0.5f))
               )) {
            Assert.True(other.IsSuccessStatusCode, await other.Content.ReadAsStringAsync(Token));
        }

        var before = (await LayerAsync("Paint")).Mask;
        using (var merged = await EditAsync(new StackApplyMask("paint", savedMask.Path))) {
            Assert.True(merged.IsSuccessStatusCode, await merged.Content.ReadAsStringAsync(Token));
        }

        var after = (await LayerAsync("Paint")).Mask;
        Assert.Equal("Constant", after.Source);
        Assert.Equal(0.5f, after.Value);
        var entry = Assert.Single(after.Layers!);
        Assert.Equal("Bake", entry.Source);
        Assert.Equal("Multiply", entry.Blend);
        Assert.Equal("curvature", entry.Map);
        Assert.Equal("Filters/Blur", Assert.Single(after.Effects!).Node);
        Assert.Equal("Apply mask", (await StateAsync()).History.UndoLabel);

        using (var undone = await client.PostAsync($"/api/v1/stacks/{stack}/undo", null, Token)) {
            undone.EnsureSuccessStatusCode();
        }

        var back = (await LayerAsync("Paint")).Mask;
        Assert.Equal(before.Source, back.Source);
        Assert.Empty(back.Layers!);

        // Not merged, it replaces the mask whole.
        using (var replaced = await EditAsync(new StackApplyMask("paint", savedMask.Path, false))) {
            Assert.True(replaced.IsSuccessStatusCode, await replaced.Content.ReadAsStringAsync(Token));
        }

        Assert.Equal("Bake", (await LayerAsync("Paint")).Mask.Source);
    }

    /// <summary>A painted layer cannot come, a bare layer has no smart mask, and an empty choice is refused.</summary>
    [Fact]
    public async Task What_cannot_come_is_refused_with_its_reason() {
        using (var added = await EditAsync(new StackAddLayer("paint", "Strokes"))) {
            Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync(Token));
        }

        var painted = await LayerAsync("Strokes");
        using var onlyPaint = await SaveAsync("Strokes", [painted.Id]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, onlyPaint.StatusCode);
        Assert.Contains("painted", await onlyPaint.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        using var noMask = await SaveAsync("Bare", [painted.Id], true);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noMask.StatusCode);

        using var none = await SaveAsync("None", []);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, none.StatusCode);
    }
}
