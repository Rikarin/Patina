using Patina.Bench;
using Patina.Contracts;
using Patina.Library;

namespace Patina.Server.Tests;

/// <summary>An export whose files follow the depth each channel is painted at (#141), with a real Worker.</summary>
public sealed partial class StackProcessTests {
    /// <summary>Where a PNG's header says how many bits a sample has.</summary>
    const int PngDepthAt = 24;

    /// <summary>
    ///     A project preset of two PNGs that follow their maps: the height painted at 16-bit float is written as a
    ///     16-bit PNG holding a value eight bits round away, and the roughness painted at eight bits stays 8-bit.
    /// </summary>
    [Fact]
    public async Task A_png_that_follows_its_maps_is_as_deep_as_its_channel_is_painted() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.05f);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(Path.Combine(meshes, BakeFixture.LowFile), deadline.Token);
        var presets = Path.Combine(Library, "projects", project, "export-presets");
        Directory.CreateDirectory(presets);
        await File.WriteAllTextAsync(
            Path.Combine(presets, "deep.yaml"),
            """
            version: 1
            id: deep
            name: Deep
            normal: y-up
            textures:
              - file: "{project}_height"
                format: png
                space: linear
                channels: [height.r]
              - file: "{project}_rough"
                format: png
                space: linear
                channels: [roughness.r]
            """,
            deadline.Token
        );

        var state = await OpenAsync($"projects/{project}/housing.vxlayers", deadline.Token);
        var held = state.Channels.Select(static channel => channel.Usage).ToHashSet(StringComparer.Ordinal);
        foreach (var usage in ((string[])["height", "roughness"]).Where(usage => !held.Contains(usage))) {
            await EditAsync(state.Id, deadline.Token, new StackAddChannel(usage));
        }

        await EditAsync(state.Id, deadline.Token, new StackSetChannelFormat("height", "Rgba16Float"));
        await EditAsync(
            state.Id,
            deadline.Token,
            Dab("deep", "height", 46f, 41f),
            Dab("flat", "roughness", 46f, 41f)
        );
        await SaveAsync(state.Id, deadline.Token);

        var preview = await PreviewAsync("deep", deadline.Token);
        Assert.Equal(1, preview.Exports);
        var job = await BuildAsync("deep", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.True(
            job.Report!.Failed == 0,
            string.Join(' ', report.Outputs.Select(static o => o.Error).OfType<string>())
        );

        var exported = Path.Combine(Library, "exports", "deep", project);
        var height = Assert.Single(Directory.GetFiles(exported, "*_height.png"));
        var rough = Assert.Single(Directory.GetFiles(exported, "*_rough.png"));
        Assert.Equal(16, File.ReadAllBytes(height)[PngDepthAt]);
        Assert.Equal(8, File.ReadAllBytes(rough)[PngDepthAt]);
        Assert.Equal(Between[0], Texel(height, 48f / ExportSide, 41f / ExportSide)[0], 0.0005f);
        // Eight bits hold 76 or 77 of 255, either side of what was painted.
        var rounded = Texel(rough, 48f / ExportSide, 41f / ExportSide)[0];
        Assert.InRange(Math.Abs(rounded - Between[0]), 0.001f, 0.003f);
    }
}
