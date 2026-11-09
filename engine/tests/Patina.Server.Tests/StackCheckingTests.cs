using Patina.Contracts;
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
        Assert.Equal(bytes, StackChecking.BytesPerTexel(format, compression, lanes));

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
}
