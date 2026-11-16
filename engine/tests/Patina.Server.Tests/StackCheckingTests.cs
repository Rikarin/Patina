using Patina.Contracts;
using Patina.Export;
using Patina.Export.Presets;
using Patina.Ipc;
using Patina.Server.Stacks;

namespace Patina.Server.Tests;

/// <summary>
///     How the Server judges what the Worker measured (#159), with no Worker: budgets and limits from the project, the
///     density from the project or the library, and the maps the export preset reads.
/// </summary>
public sealed class StackCheckingTests {
    const int Side = 1024;

    static readonly ExportPreset Unreal = ExportPresetCatalog.Read(null).Find("unreal")!;

    static StackState Set(params string[] usages) =>
        new(
            "s1",
            "/library/projects/crate/crate.vxlayers",
            "Crate",
            "Wood",
            Side,
            Side,
            1,
            0,
            new(false, false, null, null, false),
            [..usages.Select(static usage => new StackChannel(usage, [0f]))],
            [],
            [],
            null,
            1,
            "crate.glb"
        );

    /// <summary>
    ///     A set is measured against the preset its project chose, and one that chose none against the preset the
    ///     Export dialog starts from, which is not the first by id (#240).
    /// </summary>
    [Fact]
    public void A_project_that_chose_no_preset_is_checked_against_the_starter() {
        Assert.Equal("unreal", StackChecking.PresetOf(Project(new()), null)!.Id);
        Assert.Equal("gltf", StackChecking.PresetOf(null, null)!.Id);
        Assert.Equal("gltf", StackChecking.PresetOf(Project(new()) with { Export = null }, null)!.Id);
    }

    /// <summary>
    ///     A project whose chosen preset was renamed is measured with the starter, and the checks say so, pointing at
    ///     its export setting, rather than falling back without a word (#305). A preset that is there says nothing.
    /// </summary>
    [Fact]
    public void A_project_whose_preset_has_gone_is_told_which_one_its_checks_use() {
        var renamed = Project(new()) with { Export = new() { Preset = "unreal-old" } };

        var gone = Assert.Single(StackChecking.Platforms(renamed, null));

        Assert.Equal("gltf", StackChecking.PresetOf(renamed, null)!.Id);
        Assert.Equal(CheckCodes.PresetUnknown, gone.Code);
        Assert.Equal(("unreal-old", "gltf"), (gone.Values["preset"], gone.Values["fallback"]));
        Assert.Equal("export.preset", gone.Target.Setting);
        Assert.Empty(StackChecking.Platforms(Project(new()), null));
    }

    static ProjectContents Project(ProjectCheckSettings checks) =>
        new(Guid.Empty, "Crate", "crate", [], [], "bakes", [], []) {
            Checks = checks, Export = new() { Preset = Unreal.Id }
        };

    /// <summary>A square of two units over the whole tile: half the side, 512 texels, per unit.</summary>
    static readonly StackMeasured Square = new(Side, Side, new(2, 4, 1, 0, 0, 0, 0, []), []);

    /// <summary>
    ///     Three of Unreal's files read what the set makes: colour, normal and the packed ORM, each four bytes a texel
    ///     on a GPU since three lanes are held as four. The emissive and height files read nothing it makes and are not
    ///     written, so they cost nothing.
    /// </summary>
    [Fact]
    public void A_sets_memory_is_the_files_its_preset_writes_at_the_size_they_are_written() {
        var state = Set("baseColor", "roughness", "metalness", "normal");

        Assert.Equal(3L * 4 * Side * Side, StackChecking.Memory(Unreal, null, state, Side, Side));
        Assert.Equal(3L * 4 * 512 * 512, StackChecking.Memory(Unreal, new() { Size = 512 }, state, Side, Side));
        var off = new ProjectExportSettings { Files = [new(Unreal.Id, "T_{project}_ORM", false, "png8")] };
        Assert.Equal(2L * 4 * Side * Side, StackChecking.Memory(Unreal, off, state, Side, Side));
    }

    /// <summary>
    ///     A set's size is what it is authored at, and need not be square (ADR 0020). An export's size for one set is
    ///     the longest side its files are written at (ADR 0019): it changes the memory they take, keeping their shape,
    ///     and not the size the checks say the set is. Another set's size changes nothing here.
    /// </summary>
    [Fact]
    public void A_sets_own_export_size_changes_what_its_files_take_and_not_its_size() {
        var state = Set("baseColor", "roughness", "metalness", "normal") with { Width = 2048, Height = 512 };
        StackMeasured wide = new(2048, 512, null, []);
        var own = Project(new()) with { Export = new() { Preset = Unreal.Id, PerSet = [new("Wood") { Size = 1024 }] } };
        var other = Project(new()) with {
            Export = new() { Preset = Unreal.Id, PerSet = [new("Metal") { Size = 1024 }] }
        };

        var checks = StackChecking.Judge(state, wide, own, 0, Unreal);

        Assert.Equal((2048, 512), (checks.Width, checks.Height));
        Assert.Equal(3L * 4 * 1024 * 256, checks.MemoryBytes);
        Assert.Equal(3L * 4 * 2048 * 512, StackChecking.Judge(state, wide, other, 0, Unreal).MemoryBytes);
    }

    [Theory]
    [InlineData("png8", "none", 3, 4)]
    [InlineData("png8", "none", 1, 1)]
    [InlineData("png16", "none", 2, 4)]
    [InlineData("exr32", "none", 4, 16)]
    [InlineData("ktx2", "bc7", 4, 1)]
    [InlineData("dds", "bc1", 3, 0.5)]
    [InlineData("ktx2", "half", 3, 8)]
    public void A_texel_takes_what_its_format_holds_on_a_gpu(
        string format,
        string compression,
        int lanes,
        double bytes
    ) =>
        Assert.Equal(bytes, ExportMemory.BytesPerTexel(format, compression, lanes));

    /// <summary>
    ///     A missing channel's sentence and values name the file as the export writes it, never its template, and the
    ///     channel by its label, never its stored value (#198).
    /// </summary>
    [Fact]
    public void A_missing_channel_names_the_file_as_the_export_writes_it_and_the_channel_as_the_app_calls_it() {
        var checks = StackChecking.Judge(
            Set("baseColor", "roughness", "metalness", "normal"),
            Square,
            Project(new()),
            512,
            Unreal
        );

        var missing = checks.Problems.Where(static p => p.Code == CheckCodes.ChannelMissing).ToArray();
        Assert.NotEmpty(missing);
        foreach (var problem in missing) {
            Assert.DoesNotContain('{', problem.Detail);
            Assert.DoesNotContain('{', problem.Values["file"]);
            Assert.StartsWith("T_Crate_", problem.Values["file"], StringComparison.Ordinal);
        }

        Assert.Equal(["Opacity", "Ambient Occlusion"], missing.Select(static p => p.Values["channel"]));
        Assert.Contains("reads Ambient Occlusion into T_Crate_ORM,", missing[1].Detail, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Over budget and over the size limit are errors and come first; the maps the preset reads that the set does
    ///     not make, and a density half the library's, are warnings after them, each once and in the order of the
    ///     codes.
    /// </summary>
    [Fact]
    public void The_projects_limits_come_first_and_each_problem_has_its_code() {
        var checks = StackChecking.Judge(
            Set("baseColor", "roughness", "metalness", "normal"),
            Square,
            Project(new() { MemoryBudget = 8, MaxSize = 512 }),
            1024,
            Unreal
        );

        Assert.Equal(
            [
                CheckCodes.OverBudget, CheckCodes.TooLarge, CheckCodes.ChannelMissing, CheckCodes.ChannelMissing,
                CheckCodes.DensityLow
            ],
            checks.Problems.Select(static problem => problem.Code)
        );
        Assert.Equal(
            ["error", "error", "warning", "warning", "warning"],
            checks.Problems.Select(static p => p.Severity)
        );
        Assert.Equal(
            ["opacity", "occlusion"],
            checks.Problems.Where(static p => p.Code == CheckCodes.ChannelMissing).Select(static p => p.Values["map"])
        );
        Assert.Equal(512, checks.TexelDensity, 1e-9);
        Assert.Equal(1024, checks.TargetDensity);
        Assert.Equal(8L * 1024 * 1024, checks.MemoryBudget);
        Assert.Equal(ViewportViews.TexelDensity, checks.Problems[^1].Target.View);
    }

    /// <summary>
    ///     A project's own density wins over the library's, and a density within the tolerance is no problem.
    /// </summary>
    [Theory]
    [InlineData(512, 0.25, null)]
    [InlineData(600, 0.25, null)]
    [InlineData(700, 0.25, CheckCodes.DensityLow)]
    [InlineData(700, 0.5, null)]
    [InlineData(300, 0.25, CheckCodes.DensityHigh)]
    public void A_density_is_held_to_the_projects_own_within_its_tolerance(
        double target,
        double tolerance,
        string? code
    ) {
        var checks = StackChecking.Judge(
            Set("baseColor", "opacity", "roughness", "metalness", "normal", "occlusion"),
            Square,
            Project(new() { TexelDensity = target, DensityTolerance = tolerance }),
            4096,
            Unreal
        );

        Assert.Equal(code is null ? [] : [code], checks.Problems.Select(static problem => problem.Code));
    }

    /// <summary>
    ///     A density is texels per metre, as the project's is (#307): the same layout on a mesh in centimetres covers a
    ///     hundredth of the metres, so it has a hundred times the texels per metre. What the file says is used unless
    ///     the project says otherwise, and a file that says nothing is in metres.
    /// </summary>
    [Theory]
    [InlineData(null, null, 512)]
    [InlineData(null, 1.0, 512)]
    [InlineData(null, 0.01, 51200)]
    [InlineData(0.01, null, 51200)]
    [InlineData(0.001, 0.01, 512000)]
    public void A_density_is_measured_in_texels_per_metre(double? project, double? file, double expected) {
        var state = Set("baseColor", "opacity", "roughness", "metalness", "normal", "occlusion");
        var centimetres = Square with { MetresPerUnit = file };

        var checks = StackChecking.Judge(
            state,
            centimetres,
            Project(new() { TexelDensity = 51200 }) with { MetresPerUnit = project },
            0,
            Unreal
        );

        Assert.Equal(expected, checks.TexelDensity, 1e-6);
        Assert.Equal(expected == 51200 ? [] : [expected < 51200 ? CheckCodes.DensityLow : CheckCodes.DensityHigh],
            checks.Problems.Select(static problem => problem.Code));
    }

    /// <summary>
    ///     A platform's limits hold a set measured with its preset, each in place of the project's where it sets one,
    ///     and a set measured with another preset is held to the project's. Nothing measured has no density, layout or
    ///     values to judge, and says so.
    /// </summary>
    [Fact]
    public void A_platforms_limits_hold_a_set_measured_with_its_preset() {
        var state = Set("baseColor", "opacity", "roughness", "metalness", "normal", "occlusion");
        var project = Project(
            new() { MaxSize = 4096, TexelDensity = 512, Platforms = [new(Unreal.Id) { MaxSize = 512 }] }
        );
        var gltf = ExportPresetCatalog.Read(null).Find("gltf")!;

        var forUnreal = StackChecking.Judge(state, null, project, 0, Unreal);
        var forGltf = StackChecking.Judge(state, null, project, 0, gltf);

        Assert.Equal([CheckCodes.TooLarge], forUnreal.Problems.Select(static problem => problem.Code));
        Assert.Equal("512", forUnreal.Problems[0].Values["limit"]);
        Assert.Equal(512, forUnreal.TargetDensity);
        Assert.DoesNotContain(forGltf.Problems, static problem => problem.Code == CheckCodes.TooLarge);
        Assert.False(forUnreal.Measured);
        Assert.Equal(0, forUnreal.TexelDensity);
        Assert.True(StackChecking.Judge(state, Square, project, 0, Unreal).Measured);
    }

    /// <summary>A platform names its preset once, and its limits are held to what the project's are.</summary>
    [Fact]
    public void A_platforms_limits_are_refused_where_the_projects_would_be() {
        Assert.Null(new ProjectCheckSettings { Platforms = [new("unreal") { MaxSize = 1024 }] }.Problem());
        Assert.NotNull(new ProjectCheckSettings { Platforms = [new(" ")] }.Problem());
        Assert.NotNull(new ProjectCheckSettings { Platforms = [new("unreal"), new("unreal")] }.Problem());
        Assert.NotNull(new ProjectCheckSettings { Platforms = [new("unreal") { MaxSize = -1 }] }.Problem());
    }

    /// <summary>
    ///     A value on a few stray texels is not worth a line; on more it is, with the place it was found.
    /// </summary>
    [Fact]
    public void A_few_stray_texels_say_nothing() {
        CheckSpot spot = new(CheckCodes.AlbedoDark, 3, 0.25f, 0.75f, new(1, 0.5, 0.3));
        StackMeasured Of(double share) => Square with { Maps = [new(CheckCodes.AlbedoDark, share, spot)] };
        var state = Set("baseColor", "opacity", "roughness", "metalness", "normal", "occlusion");

        Assert.Empty(StackChecking.Judge(state, Of(0.001), null, 0, Unreal).Problems);
        var found = Assert.Single(StackChecking.Judge(state, Of(0.2), null, 0, Unreal).Problems);
        Assert.Equal((0.25f, 0.75f, 1d), (found.Target.U, found.Target.V, found.Target.Camera!.Yaw));
        Assert.Equal("0.2", found.Values["share"]);
    }

    /// <summary>
    ///     Every file the export writes is listed with its name as written, its size and what it takes, and the set's
    ///     memory is their sum: Unreal's emissive and height files read nothing the set makes, so they are not listed.
    /// </summary>
    [Fact]
    public void Each_file_written_is_listed_with_the_memory_it_takes() {
        var checks = StackChecking.Judge(
            Set("baseColor", "roughness", "metalness", "normal"),
            Square,
            Project(new()) with { Export = new() { Preset = Unreal.Id, Size = 512 } },
            0,
            Unreal
        );

        Assert.Equal(["T_Crate_D.png", "T_Crate_N.png", "T_Crate_ORM.png"], checks.Files.Select(static f => f.File));
        Assert.All(
            checks.Files,
            static file => Assert.Equal((512, 512, 4L * 512 * 512), (file.Width, file.Height, file.Bytes))
        );
        Assert.Equal(checks.Files.Sum(static file => file.Bytes), checks.MemoryBytes);
    }

    static ChannelSource[] Lanes(string map, int count) => [..Enumerable.Range(0, count).Select(l => Lane(map, l))];

    static ChannelSource Lane(string map, int lane = 0) => new(map, lane, false, 0);

    /// <summary>A file of a preset: its name, format, compression, space, then mips.</summary>
    static ExportTexture Texture(
        (string Name, string Format, string Compression, string Space, bool Mips) file,
        params ChannelSource[] lanes
    ) =>
        new(file.Name, file.Format, file.Compression, "balanced", file.Space, file.Mips, lanes);

    /// <summary>
    ///     A preset's files are held to what an engine reads well (#159): a colour and a data map packed in one file's
    ///     colour lanes, data in sRGB, 8-bit colour in linear, a normal in BC1 or BC3 blocks, and a GPU container
    ///     without mips. Each names the file as written and goes to the export's settings. Emission may be linear, and
    ///     a file reading nothing the set makes is not judged.
    /// </summary>
    [Fact]
    public void A_presets_files_are_held_to_their_packing_space_compression_and_mips() {
        ExportPreset preset = new(
            "bad",
            "Bad",
            "test",
            GreenAxis.Up,
            [
                Texture(("{set}_mixed", "png8", "none", "srgb", false), [..Lanes("baseColor", 2), Lane("roughness")]),
                Texture(("{set}_normal", "png8", "none", "srgb", false), Lanes("normal", 3)),
                Texture(("{set}_colour", "ktx2", "bc7", "linear", true), Lanes("baseColor", 3)),
                Texture(("{set}_n", "dds", "bc1", "linear", false), Lanes("normal", 3)),
                Texture(("{set}_glow", "png8", "none", "linear", false), Lanes("emissive", 3)),
                Texture(("{set}_opacity", "dds", "bc4", "linear", true), Lane("opacity"))
            ],
            "r",
            null
        );

        var checks = StackChecking.Judge(Set("baseColor", "roughness", "normal", "emissive"), Square, null, 0, preset);

        Assert.Equal(
            [
                (CheckCodes.PackingMixed, "Wood_mixed"), (CheckCodes.SpaceDataSrgb, "Wood_normal"),
                (CheckCodes.SpaceColourLinear, "Wood_colour"), (CheckCodes.CompressionNormal, "Wood_n"),
                (CheckCodes.MipsMissing, "Wood_n")
            ],
            checks.Problems.Select(static p => (p.Code, p.Values["file"]))
        );
        Assert.All(
            checks.Problems,
            static p => Assert.Equal(("warning", "export.preset"), (p.Severity, p.Target.Setting))
        );
        var mixed = checks.Problems[0].Values;
        Assert.Equal(("Base Color", "Roughness"), (mixed["color"], mixed["data"]));
        Assert.Equal("BC1 (RGB)", checks.Problems[3].Values["compression"]);
    }

    /// <summary>
    ///     A linear file of a fixed depth that reads a channel painted deeper says it rounds it away and that a file
    ///     following its maps keeps it (ADR 0044): an 8-bit PNG of a 16-bit roughness, a half EXR of a 32-bit height
    ///     and the normal worked out from it. A following PNG, an sRGB file and a GPU container are not judged on
    ///     depth, and neither is a file as deep as what it reads.
    /// </summary>
    [Fact]
    public void A_fixed_file_shallower_than_the_channel_it_reads_says_so() {
        ExportPreset preset = new(
            "deep",
            "Deep",
            "test",
            GreenAxis.Up,
            [
                Texture(("{set}_rough", "png8", "none", "linear", false), Lane("roughness")),
                Texture(("{set}_rough_follows", "png", "none", "linear", false), Lane("roughness")),
                Texture(("{set}_height", "exr16", "none", "linear", false), Lane("height")),
                Texture(("{set}_normal", "png16", "none", "linear", false), Lanes("normal", 3)),
                Texture(("{set}_colour", "png8", "none", "srgb", false), Lanes("baseColor", 3)),
                Texture(("{set}_rough_bc4", "dds", "bc4", "linear", true), Lane("roughness")),
                Texture(("{set}_metal", "png8", "none", "linear", false), Lane("metalness"))
            ],
            "r",
            null
        );
        var state = Set("baseColor", "roughness", "height", "metalness") with {
            Channels = [
                new("baseColor", [0f], "Rgba16Float"), new("roughness", [0f], "Rgba16Float"),
                new("height", [0f], "Rgba32Float"), new("metalness", [0f])
            ]
        };

        var lost = StackChecking.Judge(state, Square, null, 0, preset)
            .Problems.Where(static p => p.Code == CheckCodes.DepthLost)
            .ToArray();

        Assert.Equal(
            [
                ("Wood_rough", "Roughness", "16", "8"), ("Wood_height", "Height", "32", "16"),
                ("Wood_normal", "Normal", "32", "16")
            ],
            lost.Select(static p => (p.Values["file"], p.Values["channel"], p.Values["painted"], p.Values["bits"]))
        );
        Assert.All(lost, static p => Assert.Equal(("warning", "export.preset"), (p.Severity, p.Target.Setting)));
        Assert.Contains("png, tiff or exr", lost[0].Detail, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A sheen's roughness without the sheen's weight writes no sheen file (#310), so the checks neither count one
    ///     nor say it has no Sheen Tint; a coat's roughness without the coat writes no clear coat file either.
    /// </summary>
    [Fact]
    public void A_layers_roughness_without_its_weight_is_no_file_to_check() {
        var gltf = ExportPresetCatalog.Read(null).Find("gltf")!;
        var state = Set(
            "baseColor", "opacity", "roughness", "metalness", "normal", "occlusion", "sheenRoughness", "coatRoughness"
        );

        var checks = StackChecking.Judge(state, Square, null, 0, gltf);

        Assert.DoesNotContain(checks.Problems, static p => p.Code == CheckCodes.ChannelMissing);
        Assert.DoesNotContain(checks.Files, static f => f.File.Contains("sheen", StringComparison.Ordinal));
        Assert.DoesNotContain(checks.Files, static f => f.File.Contains("clearcoat", StringComparison.Ordinal));
        var layered = StackChecking.Judge(Set("baseColor", "sheen", "sheenRoughness", "coat"), Square, null, 0, gltf);
        Assert.Contains(layered.Files, static f => f.File.Contains("sheen", StringComparison.Ordinal));
        Assert.Contains(layered.Files, static f => f.File.Contains("clearcoat", StringComparison.Ordinal));
    }

    /// <summary>Every preset Patina ships passes its own file checks, for a set that makes every map.</summary>
    [Fact]
    public void No_shipped_preset_trips_the_file_checks() {
        var state = Set([..ExportMaps.All.Except(ExportMaps.Derived)]);
        foreach (var preset in ExportPresetCatalog.Read(null).Presets) {
            var found = StackChecking.Judge(state, Square, null, 0, preset).Problems.Select(static p => p.Detail);
            Assert.True(!found.Any(), $"{preset.Id}: {string.Join(" ", found)}");
        }
    }

    /// <summary>
    ///     The export works out specular, diffuse and glossiness from a set's metal-roughness maps, so a
    ///     specular-glossiness preset says none of them is missing, and counts the files that read them.
    /// </summary>
    [Fact]
    public void A_specular_glossiness_preset_misses_no_map_the_export_works_out() {
        var specGloss = ExportPresetCatalog.Read(null).Find("spec-gloss")!;

        var checks = StackChecking.Judge(
            Set("baseColor", "opacity", "roughness", "metalness", "normal", "occlusion"),
            Square,
            null,
            0,
            specGloss
        );

        Assert.DoesNotContain(checks.Problems, static p => p.Code == CheckCodes.ChannelMissing);
        Assert.Equal(
            [
                "Crate_Wood_Diffuse.png", "Crate_Wood_SpecularGlossiness.png", "Crate_Wood_Normal.png",
                "Crate_Wood_AmbientOcclusion.png"
            ],
            checks.Files.Select(static file => file.File)
        );
    }
}
