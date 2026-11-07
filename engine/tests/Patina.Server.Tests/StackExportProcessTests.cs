using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     Export as a person meets it: a project made the way New Project makes one, with no output written by hand, and
///     the pixels of the files that came out. The bench's libraries declare outputs, which is how a test of a library
///     from a bench tool once passed while nothing made in the app exported at all.
/// </summary>
public sealed partial class StackProcessTests {
    const int ExportSide = 256;

    static readonly IReadOnlyList<float> Rust = [0.8f, 0.25f, 0.1f, 1f];

    /// <summary>New Project, as the dialog sends it, with a mesh and nothing else.</summary>
    async Task<string> MakeTexturingProjectAsync(string mesh, CancellationToken token) {
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Housing", kind = "texturing", mesh, resolution = ExportSide },
            token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(token));
        return "housing";
    }

    async Task<BuildPreview> PreviewAsync(string preset, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/builds/preview",
            new() { Presets = [preset] },
            ContractsJsonContext.Default.BuildRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.BuildPreview, token))!;
    }

    async Task<JobState> BuildAsync(string preset, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/builds",
            new() { Presets = [preset] },
            ContractsJsonContext.Default.BuildRequest,
            token
        );
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.JobState, token))!;
        return await FinishedAsync(job.Id, token);
    }

    async Task<JobState> BakeAsync(BakeRequest request, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/jobs/bakes",
            request,
            ContractsJsonContext.Default.BakeRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        var job = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.JobState, token))!;
        var done = await FinishedAsync(job.Id, token);
        Assert.True(done.State == JobStates.Done, done.Error);
        return done;
    }

    async Task<JobState> FinishedAsync(string id, CancellationToken token) {
        while (true) {
            var job = (await http.GetFromJsonAsync(
                $"/api/v1/jobs/{id}",
                ContractsJsonContext.Default.JobState,
                token
            ))!;
            if (job.State is JobStates.Done or JobStates.Failed or JobStates.Cancelled) {
                return job;
            }

            await Task.Delay(25, token);
        }
    }

    async Task<ProjectReport> ReportedAsync(string job, CancellationToken token) {
        var page = (await http.GetFromJsonAsync(
            $"/api/v1/builds/{job}/report/projects",
            ContractsJsonContext.Default.ProjectReportPage,
            token
        ))!;
        return Assert.Single(page.Items);
    }

    static void SkipWithoutDevice(ProjectReport report) {
        foreach (var error in report.Outputs.Select(static o => o.Error).OfType<string>()) {
            if (error.Contains("no vulkan", StringComparison.OrdinalIgnoreCase)) {
                Gpu.Skip(error);
            }
        }
    }

    /// <summary>A file's lanes at a place, from 0 to 1.</summary>
    static float[] Texel(string path, float u, float v) {
        var (width, height, channels, samples) = PngCodec.Decode16(File.ReadAllBytes(path));
        var at = ((int)(v * height) * width + (int)(u * width)) * channels;
        return [..samples.AsSpan(at, channels).ToArray().Select(static sample => sample / (float)ushort.MaxValue)];
    }

    /// <summary>
    ///     A model of two materials is two texture sets. Later outputs once replaced earlier ones, because maps were
    ///     kept by name alone, so the second set's file went out under the project's one name.
    /// </summary>
    [Fact]
    public async Task Each_set_of_a_model_exports_its_own_maps_and_its_own_bake() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = Path.Combine(folder.FullName, "two.obj");
        BakeFixture.WriteTwoMaterials(mesh);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        using (var set = await http.PatchAsJsonAsync(
                   "/api/v1/library/project/settings",
                   new { project, mesh },
                   deadline.Token
               )) {
            Assert.True(set.IsSuccessStatusCode, await set.Content.ReadAsStringAsync(deadline.Token));
        }

        using var contents = JsonDocument.Parse(
            await http.GetStringAsync($"/api/v1/library/project?path={project}", deadline.Token)
        );
        (string Path, string Material)[] documents = [
            ..contents.RootElement.GetProperty("documents")
                .EnumerateArray()
                .Select(static d => (d.GetProperty("path").GetString()!, d.GetProperty("material").GetString()!))
        ];
        Assert.Equal(["Body", "Strap"], documents.Select(static d => d.Material).Order());

        IReadOnlyList<float> Colour(string material) => material == "Body" ? Rust : [0.1f, 0.3f, 0.9f, 1f];

        foreach (var (path, material) in documents) {
            var state = await OpenAsync(path, deadline.Token);
            await EditAsync(
                state.Id,
                deadline.Token,
                new StackSetLayer(
                    "base",
                    Values: new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = Colour(material) }
                )
            );
            using var saved = await http.PostAsync($"/api/v1/stacks/{state.Id}/save", null, deadline.Token);
            Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync(deadline.Token));
        }

        var low = Path.Combine(Library, "projects", project, "two.obj");
        await BakeAsync(
            new() {
                High = low,
                Low = low,
                Material = "Body",
                Output = Path.Combine(Library, "projects", project, "bakes", "maps", "Body"),
                Resolution = ExportSide,
                Maps = ["ao"],
                OcclusionRays = 4
            },
            deadline.Token
        );

        var job = await BuildAsync("unreal", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.Equal(0, job.Report!.Failed);

        var exported = Path.Combine(Library, "exports", "unreal", project);
        foreach (var set in new[] { "Body", "Strap" }) {
            var texel = Texel(Path.Combine(exported, $"T_Housing_{set}_D.png"), 0.25f, 0.5f);
            Assert.Equal(Colour(set)[2], texel[2], 0.01f);
        }

        Assert.True(File.Exists(Path.Combine(exported, "T_Housing_Body_N.png")));
        Assert.False(File.Exists(Path.Combine(exported, "T_Housing_Strap_N.png")), "Strap took Body's bake.");
        Assert.False(File.Exists(Path.Combine(exported, "T_Housing_D.png")));
        Assert.Contains(report.Exports[0].Skipped, static file => file.StartsWith("Strap: ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The blocker in #71. Make a project in the app, change it, save, and export: the files appear and hold what
    ///     was on the layer, with no output declared by anyone.
    /// </summary>
    [Fact]
    public async Task A_project_from_New_Project_exports_what_was_saved_in_it() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.05f);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(Path.Combine(meshes, BakeFixture.LowFile), deadline.Token);

        var state = await OpenAsync($"projects/{project}/housing.vxlayers", deadline.Token);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer("base", Values: new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = Rust })
        );
        using (var saved = await http.PostAsync($"/api/v1/stacks/{state.Id}/save", null, deadline.Token)) {
            Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync(deadline.Token));
        }

        var preview = await PreviewAsync("unreal", deadline.Token);
        Assert.Equal(1, preview.Projects);
        Assert.True(preview.Rebuilds >= 1, "A project made in the app has nothing to build.");
        Assert.Equal(1, preview.Exports);

        var job = await BuildAsync("unreal", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.Equal(JobStates.Done, job.State);
        Assert.Equal(0, job.Report!.Failed);

        var exported = Path.Combine(Library, "exports", "unreal", project);
        var colour = Texel(Path.Combine(exported, "T_Housing_D.png"), 0.5f, 0.5f);
        Assert.Equal(Rust[0], colour[0], 0.01f);
        Assert.Equal(Rust[1], colour[1], 0.01f);
        Assert.Equal(Rust[2], colour[2], 0.01f);
        var orm = Texel(Path.Combine(exported, "T_Housing_ORM.png"), 0.5f, 0.5f);
        Assert.Equal(1f, orm[0], 0.01f);
        Assert.Equal(0.5f, orm[1], 0.01f);
        Assert.Equal(0f, orm[2], 0.01f);

        // Nothing changed, so a second build touches nothing and says the files are there.
        var again = await PreviewAsync("unreal", deadline.Token);
        Assert.Equal(0, again.Rebuilds);
        Assert.Equal(0, again.Exports);
        Assert.Equal(1, again.ExportsFresh);
        Assert.NotNull(again.Nothing);
        Assert.False(File.Exists(Path.Combine(exported, "T_Housing_N.png")), "Nothing has baked a normal yet.");

        // A bake into the folder the Bake panel fills in reaches the export with nothing else to say.
        var bakes = Path.Combine(Library, "projects", project, "bakes", "maps");
        await BakeAsync(
            new() {
                High = Path.Combine(meshes, BakeFixture.HighFile),
                Low = Path.Combine(Library, "projects", project, BakeFixture.LowFile),
                Output = bakes,
                Resolution = ExportSide,
                Maps = ["ao"],
                Format = "png16",
                OcclusionRays = 16
            },
            deadline.Token
        );
        var baked = await BuildAsync("unreal", deadline.Token);
        Assert.Equal(0, baked.Report!.Failed);
        var normal = Path.Combine(exported, "T_Housing_N.png");
        Assert.True(File.Exists(normal), "A bake never reached the export.");
        var (width, height, lanes, samples) = PngCodec.Decode16(
            File.ReadAllBytes(Path.Combine(exported, "T_Housing_ORM.png"))
        );
        var occluded = Enumerable.Range(0, width * height).Count(at => samples[at * lanes] < 60000);
        Assert.True(occluded > 0 && height == ExportSide, "Baked occlusion never reached the ORM file.");
    }
}
