using Patina.Contracts;
using System.Net;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>Brush presets and a project's painting settings, over the API a client uses (#123, #124).</summary>
public sealed class BrushApiTests : IAsyncLifetime {
    readonly ServerFixture server = new();
    HttpClient client = null!;

    CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        Directory.CreateDirectory(Path.Combine(server.Library, "projects"));
        File.WriteAllText(
            Path.Combine(server.Library, "library.yaml"),
            """
            schema: 1
            id: 0190a000-0000-7000-8000-00000000abce
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
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await server.DisposeAsync();
    }

    string ProjectYaml => File.ReadAllText(Path.Combine(server.Library, "projects", "crate", "project.yaml"));

    /// <summary>
    ///     Recent colours, a palette, the brush and the symmetry go in the project's file and come back with its
    ///     contents, so they survive a restart; a colour that is not one is refused, with a code, before anything is
    ///     kept.
    /// </summary>
    [Fact]
    public async Task A_project_keeps_its_colours_brush_and_symmetry() {
        var paint = new ProjectPaintSettings {
            RecentColours = ["#d9401a", "#203040"],
            Palette = ["#ffffff"],
            Brush = StackBrushes.Default with { Radius = 40f, Aspect = 3f, SizeSpace = "texture" },
            Symmetry = new("radial", "y", Count: 6)
        };

        using var kept = await client.PutAsJsonAsync(
            "/api/v1/library/project/paint",
            new ProjectPaintRequest("crate", paint),
            Token
        );
        using var refused = await client.PutAsJsonAsync(
            "/api/v1/library/project/paint",
            new ProjectPaintRequest("crate", paint with { Palette = ["red"] }),
            Token
        );

        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("stroke-brush-refused", await CodeAsync(refused));
        Assert.Contains("paint:", ProjectYaml, StringComparison.Ordinal);

        var back = (await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token))!
            .Paint!;
        Assert.Equal(paint.RecentColours, back.RecentColours);
        Assert.Equal(paint.Palette, back.Palette);
        Assert.Equal(paint.Brush, back.Brush);
        Assert.Equal(paint.Symmetry, back.Symmetry);
    }

    [Fact]
    public async Task The_shelf_offers_the_shipped_presets_and_each_is_a_brush_a_stroke_takes() {
        var shelf = await ShelfAsync();
        var shipped = shelf.Where(static preset => preset.Origin == "shipped").ToList();

        Assert.True(shipped.Count >= 8, $"{shipped.Count} presets ship.");
        Assert.All(shipped, static preset => Assert.Null(preset.Preset.Problem()));
        Assert.Contains(shipped, static preset => preset.Preset.Tool == "eraser");
        Assert.Equal(24f, shipped.Single(static preset => preset.Name == "Soft Round").Preset.Brush.Radius);
        Assert.Equal("model", shipped.Single(static preset => preset.Name == "Even Grime").Preset.Brush.SizeSpace);
    }

    /// <summary>
    ///     The material shelf has a type for presets (#124): the shipped ones and the project's, each named by the
    ///     source the brush routes take, tagged with its tool so a search for the tool finds it, and the type is
    ///     offered by name.
    /// </summary>
    [Fact]
    public async Task The_material_shelf_lists_the_presets_as_a_type_of_their_own() {
        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetCreateRequest("crate", "Scuff", new("smudge", StackBrushes.Default)),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var shelf = (await client.GetFromJsonAsync<MaterialList>("/api/v1/library/materials?project=crate", Token))!;
        var presets = shelf.Items.Where(static item => item.Role == "preset").ToList();
        var searched = (await client.GetFromJsonAsync<MaterialList>(
            "/api/v1/library/materials?project=crate&search=smudge",
            Token
        ))!.Items;

        Assert.Contains(shelf.Roles, static term => term is { Value: "preset", Label: "Brush Presets" });
        Assert.Contains(
            presets,
            static item => item is { Name: "Soft Round", Origin: "shipped", Kind: "brush-preset" }
        );
        var scuff = presets.Single(static item => item.Name == "Scuff");
        Assert.Equal("project", scuff.Origin);
        Assert.Equal(["smudge"], scuff.Tags!);
        Assert.Equal(
            (await ShelfAsync()).Single(static preset => preset.Name == "Scuff").Source,
            scuff.Source
        );
        Assert.Contains(searched, static item => item.Name == "Scuff");
        Assert.DoesNotContain(searched, static item => item.Name == "Soft Round");
    }

    /// <summary>
    ///     A saved preset is a document of the project and its file is the truth: an edit to the file is what the shelf
    ///     offers, and updating from the brush rewrites the file.
    /// </summary>
    [Fact]
    public async Task A_saved_preset_is_a_document_whose_file_is_the_truth() {
        var preset = new BrushPreset("brush", StackBrushes.Default with { Radius = 30f, FlowJitter = 0.5f }, "#336699");

        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetCreateRequest("crate", "Scuff", preset),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var made = (await created.Content.ReadFromJsonAsync<BrushPresetInfo>(Token))!;
        Assert.Equal(("Scuff", "project"), (made.Name, made.Origin));

        var contents = await client.GetFromJsonAsync<ProjectContents>("/api/v1/library/project?path=crate", Token);
        var document = contents!.Documents.Single(static document => document.Kind == "brush-preset");
        Assert.Equal(made.Source, document.Path);
        var file = Path.Combine(server.Library, made.Source);
        Assert.Contains("flowJitter: 0.5", File.ReadAllText(file), StringComparison.Ordinal);

        File.WriteAllText(file, File.ReadAllText(file).Replace("radius: 30", "radius: 12", StringComparison.Ordinal));
        Assert.Equal(12f, (await ShelfAsync()).Single(static p => p.Name == "Scuff").Preset.Brush.Radius);

        using var updated = await client.PutAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetUpdateRequest("crate", made.Source, preset with { Favourite = true }),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var first = (await ShelfAsync())[0];
        Assert.Equal(("Scuff", true, 30f), (first.Name, first.Preset.Favourite, first.Preset.Brush.Radius));

        using var duplicate = await client.PostAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetCreateRequest("crate", "Scuff", preset),
            Token
        );
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    /// <summary>
    ///     A shipped preset cannot change, so favouriting one keeps a copy in the project, and it is the copy that is
    ///     first.
    /// </summary>
    [Fact]
    public async Task Favouriting_a_shipped_preset_keeps_a_copy_in_the_project() {
        var chalk = (await ShelfAsync()).Single(static preset => preset.Source == "starter:Chalk");

        using var updated = await client.PutAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetUpdateRequest("crate", chalk.Source, chalk.Preset with { Favourite = true }),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var shelf = await ShelfAsync();
        Assert.Equal(("Chalk", "project", true), (shelf[0].Name, shelf[0].Origin, shelf[0].Preset.Favourite));
        Assert.False(shelf.Single(static preset => preset.Source == "starter:Chalk").Preset.Favourite);
    }

    [Fact]
    public async Task A_preset_no_stroke_could_take_is_refused_with_a_code() {
        using var refused = await client.PostAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetCreateRequest("crate", "Wide", new("brush", StackBrushes.Default with { Aspect = 40f })),
            Token
        );
        using var tool = await client.PostAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetCreateRequest("crate", "Pick", new("picker", StackBrushes.Default)),
            Token
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("stroke-brush-refused", await CodeAsync(refused));
        Assert.Equal(HttpStatusCode.BadRequest, tool.StatusCode);
        Assert.DoesNotContain("Wide", ProjectYaml, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Another project's preset is on the shelf, and the shelf keeps what it read of it only while the file stands
    ///     (#124): an edit to the file, or a new preset in that project, is on the next load.
    /// </summary>
    [Fact]
    public async Task Another_projects_presets_are_read_again_when_their_files_change() {
        using var barrel = await client.PostAsJsonAsync(
            "/api/v1/library/projects",
            new ProjectCreateRequest("Barrel", Kind: "layered-material"),
            Token
        );
        Assert.Equal(HttpStatusCode.Created, barrel.StatusCode);
        var preset = new BrushPreset("brush", StackBrushes.Default with { Radius = 30f });
        using var created = await client.PostAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetCreateRequest("barrel", "Dent", preset),
            Token
        );
        var made = (await created.Content.ReadFromJsonAsync<BrushPresetInfo>(Token))!;

        Assert.Equal(30f, (await ShelfAsync()).Single(static p => p.Name == "Dent").Preset.Brush.Radius);
        var file = Path.Combine(server.Library, made.Source);
        File.WriteAllText(file, File.ReadAllText(file).Replace("radius: 30", "radius: 12", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(5));
        Assert.Equal(12f, (await ShelfAsync()).Single(static p => p.Name == "Dent").Preset.Brush.Radius);

        using var second = await client.PostAsJsonAsync(
            "/api/v1/library/brushes",
            new BrushPresetCreateRequest("barrel", "Gouge", preset),
            Token
        );
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(await ShelfAsync(), static p => p is { Name: "Gouge", Origin: "library" });
    }

    async Task<IReadOnlyList<BrushPresetInfo>> ShelfAsync() =>
        (await client.GetFromJsonAsync<BrushPresetList>("/api/v1/library/brushes?project=crate", Token))!.Items;

    async Task<string?> CodeAsync(HttpResponseMessage response) {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return problem.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
