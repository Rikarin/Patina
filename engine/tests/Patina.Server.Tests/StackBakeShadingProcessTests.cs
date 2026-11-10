using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Net.Http.Json;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     A bake reaches a texture set's shading and what it exports (#86), and a set whose bake no longer fits it says
///     so (#238): from New Project, through the Worker, to the files an export writes.
/// </summary>
public sealed partial class StackProcessTests {
    /// <summary>
    ///     A set with a height channel writes a normal, and with an occlusion channel painted at a half writes an
    ///     occlusion. Before #86 the bake reached neither: the exported normal was the height's flat slope and the
    ///     occlusion the painted half. Now the normal is the baked one, as the preset turns it (y-down), and the
    ///     occlusion is the painted half times the baked.
    /// </summary>
    [Fact]
    public async Task A_bake_lies_beneath_the_sets_normal_and_occlusion_in_what_it_exports() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.05f);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(Path.Combine(meshes, BakeFixture.LowFile), deadline.Token);
        var projectFolder = Path.Combine(Library, "projects", project);
        var bake = await BakeAsync(
            new() {
                High = [Path.Combine(meshes, BakeFixture.HighFile)],
                Low = Path.Combine(projectFolder, BakeFixture.LowFile),
                Output = Path.Combine(projectFolder, "bakes", "maps"),
                Options = new() {
                    Resolution = ExportSide, Maps = ["normal", "ao"], Occlusion = new() { Rays = 16 }, Format = "png16"
                }
            },
            deadline.Token
        );

        var state = await OpenAsync($"projects/{project}/housing.vxlayers", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddChannel("height"), new StackAddChannel("occlusion"));
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(
                "base",
                Values: new Dictionary<string, IReadOnlyList<float>> { ["occlusion"] = [0.5f, 0.5f, 0.5f, 1f] }
            )
        );
        await SaveAsync(state.Id, deadline.Token);

        var job = await BuildAsync("unreal", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.Equal(0, job.Report!.Failed);

        var exported = Path.Combine(Library, "exports", "unreal", project);
        var bakedNormal = bake.Result!.Maps.Single(static m => m.Map == "normal").Path;
        var bakedOcclusion = bake.Result.Maps.Single(static m => m.Map == "ao").Path;
        var (normals, leaning) = (0, 0);
        var (shaded, darker) = (0, 0);
        for (var v = 0.05f; v < 1f; v += 0.05f) {
            for (var u = 0.05f; u < 1f; u += 0.05f) {
                var baked = Texel(bakedNormal, u, v);
                if (baked[2] is > 0.995f or < 0.55f) {
                    continue; // flat, where nothing tells the two apart, or the background, a grey the padding replaces
                }

                normals++;
                var written = Texel(Path.Combine(exported, "T_Housing_N.png"), u, v);
                if (Math.Abs(written[0] - baked[0]) < 0.03f && Math.Abs(written[1] - (1f - baked[1])) < 0.03f) {
                    leaning++;
                }

                var occlusion = Texel(bakedOcclusion, u, v)[0];
                var orm = Texel(Path.Combine(exported, "T_Housing_ORM.png"), u, v)[0];
                if (occlusion < 0.9f) {
                    shaded++;
                    if (Math.Abs(orm - (0.5f * occlusion)) < 0.03f) {
                        darker++;
                    }
                }
            }
        }

        Assert.True(normals > 20, $"Only {normals} places lean in the bake.");
        Assert.True(leaning >= normals * 9 / 10, $"{leaning} of {normals} leaning places kept the bake's normal.");
        Assert.True(shaded > 0, "Nothing in the bake is occluded.");
        Assert.True(darker >= shaded * 9 / 10, $"{darker} of {shaded} occluded places multiplied the bake.");
    }

    /// <summary>
    ///     Changing the set's UV set leaves its bake in the old one (#238). The set, its project and its checks say so,
    ///     and baking again in the set's UV set clears it.
    /// </summary>
    [Fact]
    public async Task A_set_moved_to_another_uv_set_says_its_bake_is_stale_until_it_is_baked_again() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = TwoLayoutTriangle(Path.Combine(folder.FullName, "meshes"));
        _ = await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        var projectFolder = Path.Combine(Library, "projects", project);
        var stack = Path.GetRelativePath(Library, Directory.EnumerateFiles(projectFolder, "*.vxlayers").Single())
            .Replace('\\', '/');
        BakeRequest request = new() {
            High = [Path.Combine(projectFolder, "ramp.gltf")],
            Low = Path.Combine(projectFolder, "ramp.gltf"),
            Material = "Body",
            Output = Path.Combine(projectFolder, "bakes", "maps"),
            Options = new() { Resolution = ExportSide, Maps = ["position"], Format = "png16" }
        };
        await BakeAsync(request, deadline.Token);

        var state = await OpenAsync(stack, deadline.Token);
        Assert.Null(state.BakeStale);
        Assert.Null(await ProjectBakeStaleAsync(project, deadline.Token));

        await EditAsync(state.Id, deadline.Token, new StackSetSettings(UvSet: 1));
        Assert.Equal(BakeFit.UvSet, (await StateAsync(state.Id, deadline.Token)).BakeStale);
        await SaveAsync(state.Id, deadline.Token);
        Assert.Equal(BakeFit.UvSet, await ProjectBakeStaleAsync(project, deadline.Token));
        var checks = await ChecksAsync(state.Id, deadline.Token);
        var problem = Assert.Single(checks.Problems, static p => p.Code == CheckCodes.BakeStale);
        Assert.Equal("2", problem.Values["uvSet"]);

        // Left to choose, the bake takes the set's UV set, and fits it again.
        request.Material = "Body";
        await BakeAsync(request, deadline.Token);
        Assert.Null((await StateAsync(state.Id, deadline.Token)).BakeStale);
        Assert.Null(await ProjectBakeStaleAsync(project, deadline.Token));
        Assert.DoesNotContain(
            (await ChecksAsync(state.Id, deadline.Token)).Problems,
            static p => p.Code == CheckCodes.BakeStale
        );
    }

    /// <summary>
    ///     What the viewport draws of a set with no channel of its own for either map: with the bake beneath it, the
    ///     bevels light and the corners darken; with the bake switched off, which is how every set looked before #86,
    ///     the low mesh is flat. <c>PATINA_PICTURES</c> names a folder to keep the two frames in.
    /// </summary>
    [Fact]
    public async Task The_viewport_shades_a_set_with_its_bake() {
        using var deadline = Deadline(300);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.05f);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(Path.Combine(meshes, BakeFixture.LowFile), deadline.Token);
        var projectFolder = Path.Combine(Library, "projects", project);
        await BakeAsync(
            new() {
                High = [Path.Combine(meshes, BakeFixture.HighFile)],
                Low = Path.Combine(projectFolder, BakeFixture.LowFile),
                Output = Path.Combine(projectFolder, "bakes", "maps"),
                Options = new() {
                    Resolution = 512, Maps = ["normal", "ao"], Occlusion = new() { Rays = 32 }, Format = "png16"
                }
            },
            deadline.Token
        );

        var state = await OpenAsync($"projects/{project}/housing.vxlayers", deadline.Token);
        using (var worn = await http.PutAsJsonAsync("/api/v1/viewport/stack", new { stack = state.Id }, deadline.Token)) {
            Assert.True(worn.IsSuccessStatusCode, await worn.Content.ReadAsStringAsync(deadline.Token));
        }

        var keep = Environment.GetEnvironmentVariable("PATINA_PICTURES") ?? folder.FullName;
        var shaded = await SettledShotAsync(Path.Combine(keep, "bake-shading-combined.png"), deadline.Token);
        var off = await EditAsync(state.Id, deadline.Token, new StackSetSettings(BakedNormal: "Off", BakedOcclusion: "Off"));
        while ((await StateAsync(state.Id, deadline.Token)).Settled < off.Revision) {
            await Task.Delay(100, deadline.Token);
        }

        var flat = await SettledShotAsync(Path.Combine(keep, "bake-shading-off.png"), deadline.Token);

        Assert.NotEqual(Convert.ToHexString(shaded), Convert.ToHexString(flat));
    }

    /// <summary>A screenshot once two in a row agree, so the Worker's feed has reached the frame.</summary>
    async Task<byte[]> SettledShotAsync(string path, CancellationToken token) {
        byte[] last = [];
        for (var attempt = 0; attempt < 40; attempt++) {
            await Task.Delay(500, token);
            using var response = await http.PostAsJsonAsync(
                "/api/v1/viewport/screenshot",
                new ViewportScreenshotRequest(path, 640, 480),
                ContractsJsonContext.Default.ViewportScreenshotRequest,
                token
            );
            if (!response.IsSuccessStatusCode) {
                Gpu.Skip(await response.Content.ReadAsStringAsync(token));
            }

            var shot = await File.ReadAllBytesAsync(path, token);
            if (shot.AsSpan().SequenceEqual(last)) {
                return shot;
            }

            last = shot;
        }

        return last;
    }

    async Task<string?> ProjectBakeStaleAsync(string project, CancellationToken token) {
        var contents = await http.GetFromJsonAsync<ProjectContents>($"/api/v1/library/project?path={project}", token);
        return contents!.Documents.Single(static d => d.Kind == DocumentKinds.Texturing).BakeStale;
    }
}
