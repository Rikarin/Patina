using CsCheck;
using Patina.Contracts;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace Patina.Library.Tests;

public sealed class DocumentCodecTests {
    const string File = "test.yaml";

    /// <summary>Text built to trip a YAML writer: everything here looks like syntax or like another type.</summary>
    static readonly Gen<string> Text = Gen.OneOf(
        Gen.String.AlphaNumeric,
        Gen.OneOf(
            string.Empty,
            " ",
            " leading",
            "trailing ",
            "true",
            "null",
            "~",
            "123",
            "1.5e3",
            "0x10",
            "- item",
            "key: value",
            "# comment",
            "'quoted'",
            "\"double\"",
            "line\nbreak",
            "tab\there",
            "[flow]",
            "{flow}",
            "&anchor",
            "*alias",
            "!tag",
            "%directive",
            "---",
            "...",
            "Žluťoučký",
            "日本語",
            "🦀 crab"
        )
    );

    static readonly Gen<Revision> Revisions = Gen.Byte.Array[32].Select(static bytes => Revision.Of(bytes));

    static readonly Gen<AssetReference> References = Gen.OneOf(
        Gen.Guid.Where(static g => g != Guid.Empty).Select(static g => AssetReference.Floating(new(g))),
        Gen.Guid.Where(static g => g != Guid.Empty)
            .Select(Revisions)
            .Select(static pair => AssetReference.Pinned(new(pair.Item1), pair.Item2))
    );

    static readonly Gen<AssetReference[]> ReferenceLists = References.Array[0, 6];

    static readonly Gen<OutputDeclaration> Outputs = Gen.Select(
            Text,
            Text,
            Gen.String.AlphaNumeric.Where(static s => s.Length > 0).Array[0, 3],
            Gen.Dictionary(Gen.String.AlphaNumeric, Text)[0, 3]
        )
        .Select(static t => new OutputDeclaration(t.Item1, t.Item2, [..t.Item3.Distinct()], [], t.Item4));

    static readonly Gen<ProjectDocument> Projects = Gen.Select(
            Gen.Guid.Where(static g => g != Guid.Empty),
            Text,
            Text.Array[0, 5],
            ReferenceLists,
            Outputs.Array[0, 3]
        )
        .Select(static t => new ProjectDocument(
                new(t.Item1),
                t.Item2,
                t.Item3,
                [..t.Item4.DistinctBy(static r => r.Asset)],
                [..t.Item5.DistinctBy(static o => o.Name)]
            )
        );

    static readonly Gen<AssetDocument> Assets = Gen.Select(
            Gen.Guid.Where(static g => g != Guid.Empty),
            Text,
            Text,
            Text.Array[0, 5],
            ReferenceLists
        )
        .Select(static t => new AssetDocument(
                new(t.Item1),
                t.Item2,
                t.Item3,
                t.Item4,
                [..t.Item5.DistinctBy(static r => r.Asset)]
            )
        );

    static readonly Gen<LibraryDocument> Libraries = Gen.Select(
            Gen.Guid.Where(static g => g != Guid.Empty),
            Text,
            Gen.Double[1, 100_000],
            Text.Array[0, 4],
            Text,
            Text.Array[0, 4]
        )
        .Select(static t => new LibraryDocument(new(t.Item1), t.Item2, t.Item3, t.Item4, t.Item5, t.Item6));

    [Fact]
    [Trait("Category", "Property")]
    public void A_project_survives_the_round_trip_byte_for_byte() =>
        Projects.Sample(static project => {
                var text = DocumentCodec.Write(project);
                var read = DocumentCodec.ReadProject(text, File).Document;
                Assert.Equal(text, DocumentCodec.Write(read));
                Assert.Equal(project.Id, read.Id);
                Assert.Equal(project.Name, read.Name);
                Assert.Equal(project.References.ToHashSet(), read.References.ToHashSet());
                Assert.Equal(
                    project.Outputs.Select(static o => o.Name).ToHashSet(),
                    read.Outputs.Select(static o => o.Name).ToHashSet()
                );
            }
        );

    /// <summary>
    ///     A project's export settings live in its own file, so they travel with the library and a deleted cache
    ///     cannot lose them. Every field comes back, files sort however they were made, and no key is left unread.
    /// </summary>
    [Fact]
    public void Export_settings_survive_the_round_trip_and_sort_their_files() {
        var project = TestLibrary.Project("P") with {
            Export = new() {
                Preset = "unreal",
                Folder = "/out/game",
                Size = 2048,
                Filter = "bilinear",
                Padding = new("colour", 4, [1f, 0.5f, 0.25f, 1f]),
                Dither = true,
                Skipped = ["Strap", "Body"],
                Files = [new("unreal", "T_{project}_N", true, "tga"), new("godot", "{project}_orm", false, "png8")]
            }
        };

        var text = DocumentCodec.Write(project);
        var read = DocumentCodec.ReadProject(text, File);

        Assert.Empty(read.UnknownKeys);
        Assert.Equal(text, DocumentCodec.Write(read.Document));
        var export = read.Document.Export!;
        Assert.Equal(
            ("unreal", "/out/game", 2048, "bilinear", true),
            (export.Preset, export.Folder, export.Size, export.Filter, export.Dither)
        );
        Assert.Equal(("colour", 4), (export.Padding!.Mode, export.Padding.Distance));
        Assert.Equal([1f, 0.5f, 0.25f, 1f], export.Padding.Colour);
        Assert.Equal(["Body", "Strap"], export.Skipped);
        Assert.Equal(["godot", "unreal"], export.Files.Select(static f => f.Preset));
        Assert.False(export.Files[0].Enabled);
        Assert.DoesNotContain("export:", DocumentCodec.Write(TestLibrary.Project("Q")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Property")]
    public void An_asset_survives_the_round_trip_byte_for_byte() =>
        Assets.Sample(static asset => {
                var text = DocumentCodec.Write(asset);
                var read = DocumentCodec.ReadAsset(text, File).Document;
                Assert.Equal(text, DocumentCodec.Write(read));
                Assert.Equal(asset.Name, read.Name);
                Assert.Equal(asset.Kind, read.Kind);
                Assert.Equal(asset.Dependencies.ToHashSet(), read.Dependencies.ToHashSet());
            }
        );

    [Fact]
    [Trait("Category", "Property")]
    public void A_library_document_survives_the_round_trip_byte_for_byte() =>
        Libraries.Sample(static library => {
                var text = DocumentCodec.Write(library);
                var read = DocumentCodec.ReadLibrary(text, File).Document;
                Assert.Equal(text, DocumentCodec.Write(read));
                Assert.Equal(library.TexelDensity, read.TexelDensity);
                Assert.Equal(library.Channels, read.Channels);
            }
        );

    [Fact]
    [Trait("Category", "Property")]
    public void The_order_lists_were_built_in_does_not_reach_the_file() =>
        Projects.Sample(static project => {
                var reversed = project with {
                    Tags = [..project.Tags.Reverse()],
                    References = [..project.References.Reverse()],
                    Outputs = [..project.Outputs.Reverse()]
                };
                Assert.Equal(DocumentCodec.Write(project), DocumentCodec.Write(reversed));
            }
        );

    [Fact]
    public void A_document_has_one_exact_form() {
        var asset = new AssetId(Guid.Parse("0190a000-0000-7000-8000-000000000001"));
        var other = new AssetId(Guid.Parse("0190a000-0000-7000-8000-000000000002"));
        var revision = Revision.Of([1, 2, 3]);
        var project = new ProjectDocument(
            new(Guid.Parse("0190b000-0000-7000-8000-000000000001")),
            "Crate 01",
            ["wood", "prop"],
            [AssetReference.Pinned(other, revision), AssetReference.Floating(asset)],
            [
                new(
                    "ao",
                    "bake.ao",
                    ["meshes/low.glb"],
                    [asset],
                    new Dictionary<string, string> { ["samples"] = "256", ["resolution"] = "2048" }
                )
            ]
        );

        Assert.Equal(
            $"""
             schema: 2
             id: 0190b000-0000-7000-8000-000000000001
             name: Crate 01
             tags:
               - prop
               - wood
             references:
               - asset: 0190a000-0000-7000-8000-000000000001
                 mode: floating
               - asset: 0190a000-0000-7000-8000-000000000002
                 mode: pinned
                 revision: {revision}
             outputs:
               - name: ao
                 tool: bake.ao
                 files:
                   - meshes/low.glb
                 uses:
                   - 0190a000-0000-7000-8000-000000000001
                 settings:
                   resolution: "2048"
                   samples: "256"

             """,
            DocumentCodec.Write(project)
        );
    }

    static readonly Gen<ProjectBakeSettings> Bakes = Gen.Select(
            Text,
            Text,
            Gen.Double[0, 10],
            Gen.Bool,
            Gen.Int[16, 4096],
            Gen.OneOf("measured", "rays", "custom"),
            Text.Array[0, 3]
        )
        .Select(static t => new ProjectBakeSettings {
                Cage = t.Item1,
                High = t.Item7,
                Sets = [t.Item2],
                Options = new() {
                    Resolution = t.Item5,
                    Maps = ["ao", "id"],
                    Rays = new() { Front = t.Item3 + 0.01, LowSuffix = t.Item2, IgnoreBackfaces = t.Item4 },
                    Occlusion = new() { Spread = 1 + t.Item3 * 8, GroundPlane = !t.Item4 },
                    Height = new() { Range = t.Item6, Scale = t.Item3 + 0.1 },
                    Thickness = new() { Normalised = t.Item4 },
                    Id = new() { Source = "mesh", Palette = "grey" }
                }
            }
        );

    static string Json(ProjectBakeSettings? bake) =>
        JsonSerializer.Serialize(bake, LibraryTreeJsonContext.Default.ProjectBakeSettings!);

    /// <summary>
    ///     A project's bake settings live in its own file, so they survive a restart, a copy and a Git checkout, and
    ///     they come back as they went in, every member of every map's settings with them.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void A_projects_bake_settings_survive_the_round_trip() =>
        Bakes.Sample(static bake => {
                var project = TestLibrary.Project("Crate") with { Bake = bake };
                var text = DocumentCodec.Write(project);
                var read = DocumentCodec.ReadProject(text, File);

                Assert.Empty(read.UnknownKeys);
                Assert.Equal(Json(bake), Json(read.Document.Bake));
                Assert.Equal(text, DocumentCodec.Write(read.Document));
            }
        );

    /// <summary>
    ///     A project.yaml holds its export, bake, viewport, paint and checks blocks and its documents side by side,
    ///     each written by the one record mechanism, and all of them come back whole, every field a change made since
    ///     the cache stopped holding them among them, with nothing unread and no byte changed by a second save.
    /// </summary>
    [Fact]
    public void Every_block_and_document_field_survives_the_round_trip_together() {
        var project = TestLibrary.Project("Crate") with {
            Tags = ["metal", "props"],
            Documents = [
                new(AssetId.New(), "graph", "rust.vxtexgraph", "Rust", Role: "mask") { Tags = ["weathering"] },
                new(AssetId.New(), "layered-material", "crate.vxlayers", "Crate Paint", Material: "Body") {
                    Tags = ["hero", "painted"]
                }
            ],
            Export = new() {
                Preset = "godot",
                Size = 512,
                Padding = new("diffusion", 8, [0.25f, 0.5f, 1f, 1f]),
                Dither = false,
                Skipped = ["Strap"],
                Files = [new("godot", "{project}_albedo", true, "tga")]
            },
            Bake = new() {
                Sets = ["Body"],
                High = ["/art/crate_high.fbx"],
                Options = new() {
                    Resolution = 1024,
                    Maps = ["ao", "id", "colour"],
                    Occlusion = new() { Rays = 32 },
                    Rays = new() { DistanceMap = "/art/crate_distance.png" },
                    Rounding = new() { Enabled = true, Width = 0.02, Edges = "hard", Angle = 45 },
                    Colour = new() { Source = "texture", Texture = "/art/crate_high_albedo.png" }
                }
            },
            Viewport = new(
                Exposure: -1.25,
                View: "picture",
                Picture: "/bakes/ao.png",
                Wireframe: true,
                Look: null,
                Bloom: 0.4,
                Warmth: -0.3,
                Tint: 0.2,
                BackgroundColor: "#336699",
                Grid: true,
                PictureLane: "a",
                PictureScale: 4
            ),
            Paint = new() {
                RecentColours = ["#aa3300"],
                Palette = ["#112233", "#ffffff"],
                Brush = new(0.05f, Occlusion: false, OcclusionTolerance: 6.5f, SizeSpace: "model"),
                Symmetry = new("radial", "y", "origin", 0.25f, 6)
            },
            Checks = new() { MemoryBudget = 21.5, MaxSize = 2048, TexelDensity = 512, DensityTolerance = 0.1 }
        };

        var text = DocumentCodec.Write(project);
        var read = DocumentCodec.ReadProject(text, File);

        Assert.Empty(read.UnknownKeys);
        Assert.Equal(text, DocumentCodec.Write(read.Document));
        Assert.Equal(Json(project.Bake), Json(read.Document.Bake));
        Assert.Equal(project.Viewport, read.Document.Viewport);
        Assert.Equal(project.Checks, read.Document.Checks);
        Assert.Equal(project.Tags, read.Document.Tags);
        Assert.Equal(project.Paint.Brush, read.Document.Paint!.Brush);
        Assert.Equal(project.Paint.Symmetry, read.Document.Paint.Symmetry);
        Assert.Equal(project.Paint.RecentColours, read.Document.Paint.RecentColours);
        Assert.Equal(project.Paint.Palette, read.Document.Paint.Palette);
        Assert.Equal(
            project.Documents.Select(static item => (item.Id, item.Kind, item.File, item.Name, item.Role, item.Material)
            ),
            read.Document.Documents.Select(static item => (item.Id, item.Kind, item.File, item.Name, item.Role,
                    item.Material)
            )
        );
        Assert.Equal(
            project.Documents.SelectMany(static item => item.Tags),
            read.Document.Documents.SelectMany(static item => item.Tags)
        );
        var export = read.Document.Export!;
        Assert.Equal(("godot", 512, false), (export.Preset, export.Size, export.Dither));
        Assert.Equal([0.25f, 0.5f, 1f, 1f], export.Padding!.Colour);
        Assert.Equal("tga", Assert.Single(export.Files).Format);
        Assert.Null(export.Folder);
        Assert.DoesNotContain("look:", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A value of the wrong type is refused in any block, and a record in a list needs every field it has.
    /// </summary>
    [Theory]
    [InlineData("exposure: -1.25", "exposure: bright")]
    [InlineData("size: 512", "size: 512.5")]
    [InlineData("dither: false", "dither: no")]
    [InlineData("format: tga", "")]
    public void A_block_value_of_the_wrong_type_or_missing_is_refused(string written, string edited) {
        var text = DocumentCodec.Write(
            TestLibrary.Project("Crate") with {
                Export = new() { Size = 512, Dither = false, Files = [new("godot", "{project}_albedo", true, "tga")] },
                Viewport = new(Exposure: -1.25)
            }
        );

        var wrong = text.Replace(written, edited, StringComparison.Ordinal);

        Assert.NotEqual(text, wrong);
        Assert.Throws<LibraryFormatException>(() => DocumentCodec.ReadProject(wrong, File));
    }

    /// <summary>A project with no bake settings writes none, and reads back as having none.</summary>
    [Fact]
    public void A_project_with_no_bake_settings_writes_none() {
        var text = DocumentCodec.Write(TestLibrary.Project("Crate"));

        Assert.DoesNotContain("bake:", text, StringComparison.Ordinal);
        Assert.Null(DocumentCodec.ReadProject(text, File).Document.Bake);
    }

    /// <summary>A setting a bake cannot use is refused when the file is read, and an unknown key is reported.</summary>
    [Fact]
    public void A_wrong_bake_setting_is_refused_and_an_unknown_one_reported() {
        var text = DocumentCodec.Write(TestLibrary.Project("Crate") with { Bake = new() });

        var wrong = text.Replace("spread: 90", "spread: 400", StringComparison.Ordinal);
        Assert.NotEqual(text, wrong);
        Assert.Contains(
            "spread",
            Assert.Throws<LibraryFormatException>(() => DocumentCodec.ReadProject(wrong, File)).Message,
            StringComparison.Ordinal
        );

        var typed = text.Replace("gutter: 4", "gutter: four", StringComparison.Ordinal);
        Assert.Throws<LibraryFormatException>(() => DocumentCodec.ReadProject(typed, File));

        var unknown = text.Replace("    gutter: 4", "    gutter: 4\n    future: 1", StringComparison.Ordinal);
        Assert.Contains("bake.options.future", DocumentCodec.ReadProject(unknown, File).UnknownKeys);
    }

    [Fact]
    public void Text_that_looks_like_another_type_is_quoted() =>
        Assert.Contains("name: \"true\"\n", DocumentCodec.Write(TestLibrary.Project("true")), StringComparison.Ordinal);

    [Fact]
    public void A_newer_schema_is_refused_as_such() {
        var error = Assert.Throws<LibraryFormatException>(static () =>
            DocumentCodec.ReadProject($"schema: 3\nid: {ProjectId.New()}\nname: x\n", File)
        );

        Assert.True(error.NewerSchema);
    }

    [Theory]
    [InlineData("name: x\n")]
    [InlineData("schema: one\n")]
    [InlineData("- a\n- b\n")]
    [InlineData("<<<<<<< HEAD\nschema: 1\n=======\n")]
    [InlineData("")]
    public void What_is_not_a_document_is_refused(string text) =>
        Assert.False(Assert.Throws<LibraryFormatException>(() => DocumentCodec.ReadProject(text, File)).NewerSchema);

    [Fact]
    public void Keys_the_schema_does_not_know_are_reported() {
        var read = DocumentCodec.ReadProject(
            $"""
             schema: 1
             id: {ProjectId.New()}
             name: x
             future: yes
             references:
               - asset: {AssetId.New()}
                 weight: 2
             """,
            File
        );

        Assert.Equal(["future", "references[0].weight"], read.UnknownKeys);
    }

    [Theory]
    [InlineData("../other/secret.glb")]
    [InlineData("/etc/passwd")]
    [InlineData("meshes/../../up.glb")]
    [InlineData("""back\slash.glb""")]
    public void An_output_cannot_read_outside_its_project(string file) =>
        Assert.Throws<LibraryFormatException>(() =>
            DocumentCodec.ReadProject(
                $"""
                 schema: 1
                 id: {ProjectId.New()}
                 name: x
                 outputs:
                   - name: ao
                     tool: bake.ao
                     files:
                       - {file}
                 """,
                File
            )
        );

    [Fact]
    public void An_output_can_only_use_what_the_project_references() =>
        Assert.Throws<LibraryFormatException>(static () =>
            DocumentCodec.ReadProject(
                $"""
                 schema: 1
                 id: {ProjectId.New()}
                 name: x
                 outputs:
                   - name: ao
                     tool: bake.ao
                     uses:
                       - {AssetId.New()}
                 """,
                File
            )
        );

    [Fact]
    public void One_asset_referenced_twice_is_refused() {
        var asset = AssetId.New();

        Assert.Throws<LibraryFormatException>(() =>
            DocumentCodec.ReadProject(
                $"""
                 schema: 1
                 id: {ProjectId.New()}
                 name: x
                 references:
                   - asset: {asset}
                   - asset: {asset}
                 """,
                File
            )
        );
    }

    [Fact]
    public void A_pin_needs_a_revision() =>
        Assert.Throws<LibraryFormatException>(static () =>
            DocumentCodec.ReadProject(
                $"""
                 schema: 1
                 id: {ProjectId.New()}
                 name: x
                 references:
                   - asset: {AssetId.New()}
                     mode: pinned
                 """,
                File
            )
        );

    [Fact]
    public void A_migration_chain_upgrades_an_old_file_step_by_step() {
        var chain = new SchemaChain(
            3,
            new DocumentMigration(1, static root => Rename(root, "title", "label")),
            new DocumentMigration(2, static root => Rename(root, "label", "name"))
        );

        var read = DocumentCodec.ReadProject($"schema: 1\nid: {ProjectId.New()}\ntitle: Old\n", File, chain);

        Assert.Equal("Old", read.Document.Name);
        Assert.Equal(1, read.FileSchema);
        Assert.Empty(read.UnknownKeys);
    }

    [Fact]
    public void A_gap_in_the_chain_is_an_error_not_a_guess() {
        var chain = new SchemaChain(3, new DocumentMigration(2, static _ => { }));

        Assert.Throws<LibraryFormatException>(() =>
            DocumentCodec.ReadProject($"schema: 1\nid: {ProjectId.New()}\nname: x\n", File, chain)
        );
    }

    static void Rename(YamlMappingNode root, string from, string to) {
        root.Children[to] = root.Children[from];
        root.Children.Remove(from);
    }
}
