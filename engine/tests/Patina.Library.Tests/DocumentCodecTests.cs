using CsCheck;
using YamlDotNet.RepresentationModel;

namespace Patina.Library.Tests;

public sealed class DocumentCodecTests {
    const string File = "test.yaml";

    /// <summary>Text built to trip a YAML writer: everything here looks like syntax or like another type.</summary>
    static readonly Gen<string> Text = Gen.OneOf(
        Gen.String.AlphaNumeric,
        Gen.OneOfConst(
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
