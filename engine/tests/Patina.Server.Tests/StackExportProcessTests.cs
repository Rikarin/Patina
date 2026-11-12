using Patina.Bench;
using Patina.Contracts;
using Patina.Library;
using System.Globalization;
using System.Net;
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
        var missing = report.Outputs.Select(static o => o.Error)
            .OfType<string>()
            .FirstOrDefault(static error => error.Contains("no vulkan", StringComparison.OrdinalIgnoreCase));
        if (missing is not null) {
            Gpu.Skip(missing);
        }
    }

    /// <summary>A file's lanes at a place, from 0 to 1.</summary>
    static float[] Texel(string path, float u, float v) {
        var (width, height, channels, samples) = PngCodec.Decode16(File.ReadAllBytes(path));
        var at = ((int)(v * height) * width + (int)(u * width)) * channels;
        return [..samples.AsSpan(at, channels).ToArray().Select(static sample => sample / (float)ushort.MaxValue)];
    }

    /// <summary>
    ///     Baking into the same folder again reaches an open stack, whose masks read the bake. The folder has not
    ///     changed, so nothing told the Worker to read it.
    /// </summary>
    [Fact]
    public async Task A_second_bake_into_the_same_folder_reaches_an_open_stack() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.05f);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(Path.Combine(meshes, BakeFixture.LowFile), deadline.Token);
        var low = Path.Combine(Library, "projects", project, BakeFixture.LowFile);
        var bakes = Path.Combine(Library, "projects", project, "bakes", "maps");

        BakeRequest Baking(double reach) =>
            new() {
                High = [Path.Combine(meshes, BakeFixture.HighFile)],
                Low = low,
                Output = bakes,
                Options = new() {
                    Resolution = ExportSide,
                    Maps = ["normal", "height", "ao"],
                    Format = "png16",
                    Occlusion = new() { Rays = 16, Distance = reach }
                }
            };

        await BakeAsync(Baking(2), deadline.Token);

        // Opening reconnects it, with no client saying which bake.
        var state = await OpenAsync($"projects/{project}/housing.vxlayers", deadline.Token);
        Assert.Equal(Path.GetFullPath(bakes), state.MeshMaps);
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(
                "base",
                Values: new Dictionary<string, IReadOnlyList<float>> { ["baseColor"] = Rust },
                Mask: new("Bake", Map: "ao")
            )
        );
        var first = Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor");

        await BakeAsync(Baking(0.01), deadline.Token);
        using (var again = await http.PutAsJsonAsync(
                   $"/api/v1/stacks/{state.Id}/mesh-maps",
                   new { folder = bakes },
                   deadline.Token
               )) {
            Assert.True(again.IsSuccessStatusCode, await again.Content.ReadAsStringAsync(deadline.Token));
        }

        Assert.NotEqual(first, Digest(await PixelsAsync(state.Id, true, deadline.Token), "baseColor"));
    }

    /// <summary>
    ///     A graph's normal and the same normal through a stack export as one map, on both sides of green. A stack's
    ///     output was declared green up, though what fills it runs down, so it reached one kind of engine turned over.
    /// </summary>
    [Theory]
    [InlineData("godot", "Crate_{0}_normal.png")]
    [InlineData("unreal", "T_Crate_{0}_N.png")]
    public async Task A_graphs_normal_keeps_its_green_through_a_stack(string preset, string file) {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Crate", kind = "layered-material", resolution = ExportSide },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        var listed = await http.PostAsJsonAsync(
            "/api/v1/library/project/documents",
            new { project = "crate", kind = "graph", name = "Slope" },
            deadline.Token
        );
        Assert.True(listed.IsSuccessStatusCode, await listed.Content.ReadAsStringAsync(deadline.Token));
        await File.WriteAllTextAsync(
            Path.Combine(Library, "projects", "crate", "slope.vxtexgraph"),
            SlopeGraph,
            deadline.Token
        );
        var used = await http.PostAsJsonAsync(
            "/api/v1/library/materials/use",
            new { project = "crate", source = "projects/crate/slope.vxtexgraph" },
            deadline.Token
        );
        var reply = await used.Content.ReadAsStringAsync(deadline.Token);
        Assert.True(used.IsSuccessStatusCode, reply);
        var graph = JsonElement.Parse(reply).GetProperty("graph").GetString();

        var state = await OpenAsync("projects/crate/crate.vxlayers", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddChannel("normal"));
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Slope"));
        var layer = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(state.Id, deadline.Token, new StackSetLayer(layer, Fill: "Graph", Graph: graph));
        await SaveAsync(state.Id, deadline.Token);

        var job = await BuildAsync(preset, deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.True(
            job.Report!.Failed == 0,
            string.Join(' ', report.Outputs.Select(static o => o.Error).OfType<string>())
        );

        var exported = Path.Combine(Library, "exports", preset, "crate");
        var alone = Texel(
            Path.Combine(exported, string.Format(CultureInfo.InvariantCulture, file, "Slope")),
            0.5f,
            0.5f
        );
        var stacked = Texel(
            Path.Combine(exported, string.Format(CultureInfo.InvariantCulture, file, "Crate")),
            0.5f,
            0.5f
        );

        Assert.True(Math.Abs(alone[1] - 0.5f) > 0.02f, "The slope has no green to compare.");
        Assert.Equal(alone[1], stacked[1], 0.02f);
    }

    /// <summary>A ramp down the picture, as a material: its normal leans in green and nowhere else.</summary>
    const string SlopeGraph = """
                              name: Slope
                              nodes:
                                - { id: 1, type: Source/Gradient, values: { Angle: [1.5708] } }
                                - { id: 2, type: Surface/Height to Normal, values: { Intensity: [4] } }
                                - { id: 3, type: Output/Output, texts: { Usage: normal } }
                                - { id: 4, type: Colour/Grayscale }
                              edges:
                                - { fromNode: 1, fromPort: Out, toNode: 4, toPort: Input }
                                - { fromNode: 4, fromPort: Out, toNode: 2, toPort: Height }
                                - { fromNode: 2, fromPort: Out, toNode: 3, toPort: Input }
                              """;

    /// <summary>
    ///     A glTF material brought into a project through the Worker is a graph that exports what the glTF meant
    ///     (#157): its base color as it was, its roughness the green of the packed image times the factor, and its
    ///     normal's green as Godot reads it, which is glTF's, after the graph turned it to its own and the export back.
    /// </summary>
    [Fact]
    public async Task A_gltf_material_brought_in_exports_what_the_gltf_meant() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);
        var made = await http.PostAsJsonAsync("/api/v1/library/projects", new { name = "Imported" }, deadline.Token);
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));
        var gltf = WriteGltf(folder.FullName, [200, 100, 50], [255, 160, 80], [128, 200, 255], 0.5f);

        using var imported = await http.PostAsJsonAsync(
            "/api/v1/library/project/materials",
            new MaterialImportRequest("imported", gltf),
            deadline.Token
        );
        Assert.True(imported.IsSuccessStatusCode, await imported.Content.ReadAsStringAsync(deadline.Token));
        var job = await BuildAsync("godot", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.Equal(0, job.Report!.Failed);

        var exports = Path.Combine(Library, "exports", "godot", "imported");
        byte[] Texel(string file) {
            var picture = Patina.Export.MapPictures.ReadFile(Path.Combine(exports, file));
            var middle = (picture.Height / 2 * picture.Width + picture.Width / 2) * picture.Channels;
            return [..Enumerable.Range(0, 3).Select(lane => (byte)MathF.Round(picture.Samples[middle + lane] * 255))];
        }

        var albedo = Texel("Imported_albedo.png");
        Assert.True(albedo.Zip((byte[])[200, 100, 50]).All(static p => Math.Abs(p.First - p.Second) <= 1), string.Join(",", albedo));
        var orm = Texel("Imported_orm.png");
        Assert.InRange(orm[1], 79, 81);
        Assert.InRange(orm[2], 79, 81);
        Assert.InRange(Texel("Imported_normal.png")[1], 199, 201);
    }

    /// <summary>A glTF of one triangle and one material whose three images are each one color, beside it.</summary>
    static string WriteGltf(string folder, byte[] colour, byte[] packed, byte[] normal, float roughness) {
        foreach (var (name, texel) in (IEnumerable<(string, byte[])>) [("colour", colour), ("packed", packed), ("normal", normal)]) {
            var pixels = Enumerable.Range(0, 16).SelectMany(_ => texel).ToArray();
            File.WriteAllBytes(Path.Combine(folder, $"{name}.png"), PngCodec.Encode8(4, 4, 3, pixels));
        }

        var floats = new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1 };
        var bytes = new byte[floats.Length * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        var factor = roughness.ToString(CultureInfo.InvariantCulture);
        var path = Path.Combine(folder, "painted.gltf");
        File.WriteAllText(
            path,
            $$"""
              {
                "asset": { "version": "2.0" },
                "scene": 0,
                "scenes": [{ "nodes": [0] }],
                "nodes": [{ "mesh": 0, "name": "Tri" }],
                "meshes": [{ "name": "Tri", "primitives": [{ "attributes": { "POSITION": 0, "TEXCOORD_0": 1 }, "material": 0 }] }],
                "materials": [{
                  "name": "Imported",
                  "pbrMetallicRoughness": {
                    "baseColorTexture": { "index": 0 },
                    "metallicRoughnessTexture": { "index": 1 },
                    "roughnessFactor": {{factor}},
                    "metallicFactor": 1
                  },
                  "normalTexture": { "index": 2 }
                }],
                "textures": [{ "source": 0 }, { "source": 1 }, { "source": 2 }],
                "images": [{ "uri": "colour.png" }, { "uri": "packed.png" }, { "uri": "normal.png" }],
                "buffers": [{ "byteLength": {{bytes.Length}}, "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String(bytes)}}" }],
                "bufferViews": [
                  { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                  { "buffer": 0, "byteOffset": 36, "byteLength": 24 }
                ],
                "accessors": [
                  { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0, 0, 0], "max": [1, 1, 0] },
                  { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC2" }
                ]
              }
              """
        );
        return path;
    }

    /// <summary>
    ///     A picture keeps its depth to the exported file. The ramp spans a few bytes' worth of height across a
    ///     thousand texels, so eight bits would leave a few terraces, and a file that could not be read would fail.
    /// </summary>
    [Theory]
    [InlineData(".png")]
    [InlineData(".exr")]
    public async Task An_image_material_keeps_the_depth_of_its_picture(string extension) {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        await StartAsync("Unused.vxlayers", deadline.Token);

        const int Wide = 1024;
        var ramp = Enumerable.Range(0, Wide * 2).Select(static at => 0.4f + at % Wide / (Wide * 100f)).ToArray();
        var picture = Path.Combine(folder.FullName, $"ramp{extension}");
        File.WriteAllBytes(
            picture,
            extension == ".exr"
                ? ExrCodec.Encode(new(Wide, 2, 1, ramp))
                : PngCodec.Encode16(
                    Wide,
                    2,
                    1,
                    [..ramp.Select(static value => (ushort)MathF.Round(value * ushort.MaxValue))]
                )
        );
        var made = await http.PostAsJsonAsync(
            "/api/v1/library/projects",
            new { name = "Ramp", kind = "image-material", image = picture, resolution = 1024 },
            deadline.Token
        );
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync(deadline.Token));

        var job = await BuildAsync("unreal", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.Equal(0, job.Report!.Failed);

        var height = Path.Combine(Library, "exports", "unreal", "ramp", "T_Ramp_H.png");
        var (width, _, lanes, samples) = PngCodec.Decode16(File.ReadAllBytes(height));
        var levels = Enumerable.Range(0, width).Select(x => samples[x * lanes]).Distinct().Count();
        // Half-floats hold about 40 steps in this span, and bytes about 3.
        Assert.True(levels > 20, $"{levels} levels came through: the picture was narrowed.");
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
                   new {
                       project, autoUnwrap = false
                   }, // what a new project sends, and a same file alone changes nothing
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

        static IReadOnlyList<float> Colour(string material) => material == "Body" ? Rust : [0.1f, 0.3f, 0.9f, 1f];

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
            await SaveAsync(state.Id, deadline.Token);
        }

        var low = Path.Combine(Library, "projects", project, "two.obj");
        await BakeAsync(
            new() {
                High = [low],
                Low = low,
                Material = "Body",
                Output = Path.Combine(Library, "projects", project, "bakes", "maps", "Body"),
                Options = new() {
                    Resolution = ExportSide, Maps = ["normal", "height", "ao"], Occlusion = new() { Rays = 4 }
                }
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
    ///     A draft leaves a half set layer out so that painting goes on. A build has no draft: maps short of a layer
    ///     would ship without a word, so the export fails and says why.
    /// </summary>
    [Fact]
    public async Task A_half_set_layer_stops_an_export_and_says_why() {
        using var deadline = Deadline(240);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var meshes = Path.Combine(folder.FullName, "meshes");
        BakeFixture.Write(meshes, 0.05f);
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(Path.Combine(meshes, BakeFixture.LowFile), deadline.Token);

        var state = await OpenAsync($"projects/{project}/housing.vxlayers", deadline.Token);
        await EditAsync(state.Id, deadline.Token, new StackAddLayer("fill", "Rough"));
        var fill = (await StateAsync(state.Id, deadline.Token)).Layers[^1].Id;
        await EditAsync(
            state.Id,
            deadline.Token,
            new StackSetLayer(fill, Mask: new("Generator"))
        );
        await SaveAsync(state.Id, deadline.Token);

        var job = await BuildAsync("unreal", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);

        Assert.True(job.Report!.Failed > 0, "A stack short of a layer exported as if it were whole.");
        Assert.Contains(report.Outputs, static o => !string.IsNullOrWhiteSpace(o.Error));
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
        await PaintAsync(state.Id, "stroke", Line(40, 40, 6), false, deadline.Token);
        await SaveAsync(state.Id, deadline.Token);

        var preview = await PreviewAsync("unreal", deadline.Token);
        Assert.Equal(1, preview.Projects);
        Assert.True(preview.Rebuilds >= 1, "A project made in the app has nothing to build.");
        Assert.Equal(1, preview.Exports);

        var job = await BuildAsync("unreal", deadline.Token);
        var report = await ReportedAsync(job.Id, deadline.Token);
        SkipWithoutDevice(report);
        Assert.Equal(JobStates.Done, job.State);
        Assert.True(
            job.Report!.Failed == 0,
            string.Join(' ', report.Outputs.Select(static o => o.Error).OfType<string>())
        );

        var exported = Path.Combine(Library, "exports", "unreal", project);
        var colour = Texel(Path.Combine(exported, "T_Housing_D.png"), 0.5f, 0.5f);
        Assert.Equal(Rust[0], colour[0], 0.01f);
        Assert.Equal(Rust[1], colour[1], 0.01f);
        Assert.Equal(Rust[2], colour[2], 0.01f);
        var stroke = Texel(Path.Combine(exported, "T_Housing_D.png"), 55f / ExportSide, 41f / ExportSide);
        Assert.Equal(Red[1], stroke[1], 0.03f);
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
                High = [Path.Combine(meshes, BakeFixture.HighFile)],
                Low = Path.Combine(Library, "projects", project, BakeFixture.LowFile),
                Output = bakes,
                Options = new() {
                    Resolution = ExportSide,
                    Maps = ["normal", "height", "ao"],
                    Format = "png16",
                    Occlusion = new() { Rays = 16 }
                }
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

    /// <summary>
    ///     Auto Unwrap keeps a two-material mesh two texture sets, says what each layout came to, and lays the mesh out
    ///     again from the file it came from when its options change. It once wrote one material, so one set.
    /// </summary>
    [Fact]
    public async Task An_unwrap_keeps_each_material_a_set_and_its_options_lay_it_out_again() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = Path.Combine(folder.FullName, "box.obj");
        File.WriteAllText(
            mesh,
            """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            v 0 0 1
            v 1 0 1
            v 1 1 1
            v 0 1 1
            usemtl lid
            f 5 6 7 8
            f 4 8 7 3
            usemtl walls
            f 1 4 3 2
            f 2 3 7 6
            f 1 5 8 4
            f 1 2 6 5
            """
        );
        await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);

        async Task<ProjectContents> SetAsync(object body) {
            using var set = await http.PatchAsJsonAsync("/api/v1/library/project/settings", body, deadline.Token);
            Assert.True(set.IsSuccessStatusCode, await set.Content.ReadAsStringAsync(deadline.Token));
            return (await set.Content.ReadFromJsonAsync<ProjectContents>(deadline.Token))!;
        }

        var unwrapped = await SetAsync(new { project, autoUnwrap = true });

        var held = Assert.Single(unwrapped.Meshes, static m => m.Unwrapped is not null);
        Assert.EndsWith("box_unwrapped.glb", held.Path, StringComparison.Ordinal);
        Assert.Equal("box.obj", held.Unwrapped!.From);
        Assert.Equal(["lid", "walls"], held.Unwrapped.Sets.Select(static s => s.Material).Order());
        Assert.All(held.Unwrapped.Sets, static s => Assert.True(s.Charts > 0 && s.Packed > 0));
        Assert.Equal(["lid", "walls"], unwrapped.Documents.Select(static d => d.Material ?? string.Empty).Order());

        var file = Path.Combine(Library, held.Path);
        var first = await File.ReadAllBytesAsync(file, deadline.Token);
        var again = await SetAsync(
            new {
                project, unwrap = new UnwrapOptions { Resolution = 512, Margin = 16, Quality = "boxes", Rotations = 1 }
            }
        );

        Assert.Equal(
            new() { Resolution = 512, Margin = 16, Quality = "boxes", Rotations = 1 },
            again.Unwrap
        );
        Assert.NotEqual(first, await File.ReadAllBytesAsync(file, deadline.Token));
        Assert.Equal("box.obj", Assert.Single(again.Meshes, static m => m.Unwrapped is not null).Unwrapped!.From);
        Assert.Equal(2, again.Documents.Count);

        using var refused = await http.PatchAsJsonAsync(
            "/api/v1/library/project/settings",
            new { project, unwrap = new { resolution = 2048, margin = 4, quality = "outlines", rotations = 3 } },
            deadline.Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    /// <summary>
    ///     A set laid out past UDIM tile 1001 says so in its problems as soon as it is open, because a stroke there
    ///     paints nothing and the viewport shows 1001's maps on it. It once said nothing until the export.
    /// </summary>
    [Fact]
    public async Task A_set_on_a_second_udim_tile_says_so_when_it_opens() {
        using var deadline = Deadline(180);
        LibraryFiles.CreateDefaultLibrary(Library, "Game");
        var mesh = Path.Combine(folder.FullName, "tiles.obj");
        File.WriteAllText(
            mesh,
            """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            v 2 0 0
            v 3 0 0
            v 3 1 0
            v 2 1 0
            vt 0.1 0.1
            vt 0.9 0.1
            vt 0.9 0.9
            vt 0.1 0.9
            vt 1.1 0.1
            vt 1.9 0.1
            vt 1.9 0.9
            vt 1.1 0.9
            f 1/1 2/2 3/3 4/4
            f 5/5 6/6 7/7 8/8
            """
        );
        var (instance, _) = await StartAsync("Unused.vxlayers", deadline.Token);
        var project = await MakeTexturingProjectAsync(mesh, deadline.Token);
        using var contents = JsonDocument.Parse(
            await http.GetStringAsync($"/api/v1/library/project?path={project}", deadline.Token)
        );
        var document = contents.RootElement.GetProperty("documents")[0].GetProperty("path").GetString()!;

        var state = await OpenAsync(document, deadline.Token);
        using var socket = await FollowAsync(instance, state, deadline.Token);
        var evaluation = await EvaluatedAsync(socket, state.Revision, deadline.Token);

        var said = Assert.Single(evaluation.Problems, static p => p.Message.Contains("UDIM", StringComparison.Ordinal));
        Assert.Equal("Warning", said.Severity);
        Assert.Contains("1001, 1002", said.Message, StringComparison.Ordinal);
    }
}
