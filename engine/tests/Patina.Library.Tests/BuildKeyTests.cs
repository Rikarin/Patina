using CsCheck;

namespace Patina.Library.Tests;

public sealed class BuildKeyTests {
    static readonly Revision Mesh = Revision.Of("mesh"u8);
    static readonly Revision Bytes = Revision.Of("bytes"u8);
    static readonly Revision NewBytes = Revision.Of("new bytes"u8);
    static readonly ToolCatalog Tools = new(new Dictionary<string, string> { ["bake"] = "1" }, "0");

    static Revision? Files(string _) => Mesh;

    static (ProjectDocument Project, OutputDeclaration Output) Project(params AssetReference[] references) {
        var output = new OutputDeclaration(
            "ao",
            "bake",
            ["low.glb"],
            [..references.Select(static r => r.Asset)],
            new Dictionary<string, string> { ["samples"] = "256" }
        );
        return (TestLibrary.Project("P", references) with { Outputs = [output] }, output);
    }

    static BuildKey? Key((ProjectDocument Project, OutputDeclaration Output) project, params AssetNode[] nodes) =>
        OutputKeys.Compute(project.Output, project.Project, Files, new(nodes), Tools);

    [Fact]
    public void The_same_inputs_give_the_same_key() {
        var asset = AssetId.New();
        var project = Project(AssetReference.Floating(asset));

        Assert.Equal(Key(project, new AssetNode(asset, Bytes, [])), Key(project, new AssetNode(asset, Bytes, [])));
    }

    [Fact]
    public void A_key_is_stable_across_releases_of_this_code() {
        // Keys name files on farms and other machines, so a change here is a rebuild of every library. Do it on
        // purpose.
        var asset = new AssetId(Guid.Parse("0190a000-0000-7000-8000-000000000001"));
        var project = Project(AssetReference.Floating(asset));

        Assert.Equal(
            "1a999457917d38753c3da229b22c8d1a6f17debbdfc7e26911cd71264318d86c",
            Key(project, new AssetNode(asset, Bytes, []))?.Hex
        );
    }

    [Fact]
    public void A_floating_reference_follows_the_asset() {
        var asset = AssetId.New();
        var project = Project(AssetReference.Floating(asset));

        Assert.NotEqual(
            Key(project, new AssetNode(asset, Bytes, [])),
            Key(project, new AssetNode(asset, NewBytes, []))
        );
    }

    [Fact]
    public void A_pinned_reference_does_not() {
        var asset = AssetId.New();
        var project = Project(AssetReference.Pinned(asset, Bytes));

        Assert.Equal(Key(project, new AssetNode(asset, Bytes, [])), Key(project, new AssetNode(asset, NewBytes, [])));
    }

    [Fact]
    public void Pinning_to_the_current_bytes_rebuilds_nothing() {
        var asset = AssetId.New();
        var node = new AssetNode(asset, Bytes, []);

        Assert.Equal(
            Key(Project(AssetReference.Floating(asset)), node),
            Key(Project(AssetReference.Pinned(asset, Bytes)), node)
        );
    }

    [Fact]
    public void A_pin_does_not_hide_a_change_in_what_the_pinned_file_reads() {
        var material = AssetId.New();
        var graph = AssetId.New();
        var project = Project(AssetReference.Pinned(material, Bytes));

        AssetNode Material() => new(material, Bytes, [AssetReference.Floating(graph)]);

        Assert.NotEqual(
            Key(project, Material(), new AssetNode(graph, Bytes, [])),
            Key(project, Material(), new AssetNode(graph, NewBytes, []))
        );
    }

    [Fact]
    public void A_tool_update_changes_the_key() {
        var project = Project();
        var newer = new ToolCatalog(new Dictionary<string, string> { ["bake"] = "2" }, "0");

        Assert.NotEqual(
            OutputKeys.Compute(project.Output, project.Project, Files, new([]), Tools),
            OutputKeys.Compute(project.Output, project.Project, Files, new([]), newer)
        );
    }

    [Fact]
    public void Settings_and_files_change_the_key() {
        var project = Project();
        var key = Key(project);

        var settings = project with { Output = project.Output with { Settings = new Dictionary<string, string>() } };
        var files = project with { Output = project.Output with { Files = ["high.glb"] } };

        Assert.NotEqual(key, Key(settings));
        Assert.NotEqual(key, Key(files));
    }

    [Fact]
    public void A_label_is_not_an_input() {
        var project = Project();

        var renamed = (project.Project with { Id = ProjectId.New(), Name = "Other" },
            project.Output with { Name = "x" });

        Assert.Equal(Key(project), Key(renamed));
    }

    [Fact]
    public void The_order_of_declarations_is_not_an_input() {
        var a = AssetId.New();
        var b = AssetId.New();
        var nodes = new[] { new AssetNode(a, Bytes, []), new AssetNode(b, NewBytes, []) };

        Assert.Equal(
            Key(Project(AssetReference.Floating(a), AssetReference.Floating(b)), nodes),
            Key(Project(AssetReference.Floating(b), AssetReference.Floating(a)), nodes)
        );
    }

    [Fact]
    public void Field_boundaries_count() {
        var left = new KeyHash("t").Add("ab").Add("c").Finish();
        var right = new KeyHash("t").Add("a").Add("bc").Finish();

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void A_stack_keys_on_the_paint_and_bake_it_names_and_not_only_on_itself() {
        var output = new OutputDeclaration(
            "maps",
            "layer-stack",
            ["crate.vxlayers"],
            [],
            new Dictionary<string, string>()
        );
        var project = TestLibrary.Project("P", []) with { Outputs = [output] };
        const string Stack = """
                             model: crate.glb
                             sets:
                               - name: Body
                                 layers:
                                   - id: a
                                     paint: crate.Body.a.vxpaint
                                     mask: { paint: crate.Body.a.mask.vxpaint }
                             """;
        Dictionary<string, string> files = new() {
            ["crate.vxlayers"] = "1", ["crate.Body.a.vxpaint"] = "2", ["crate.Body.a.mask.vxpaint"] = "3"
        };

        BuildKey? Current() =>
            OutputKeys.Compute(
                output,
                project,
                file => Revision.Of(System.Text.Encoding.UTF8.GetBytes(files[file])),
                new([]),
                Tools,
                new(
                    _ => Stack,
                    file => files.TryGetValue(file, out var text)
                        ? Revision.Of(System.Text.Encoding.UTF8.GetBytes(text))
                        : null
                )
            );

        var before = Current();
        files["crate.Body.a.vxpaint"] = "painted";
        var painted = Current();
        files["crate.Body.a.mask.vxpaint"] = "masked";

        Assert.NotEqual(before, painted);
        Assert.NotEqual(painted, Current());
    }

    [Fact]
    public void A_stack_names_its_inputs_beside_it() =>
        Assert.Equal(
            ["p/a.vxpaint", "p/crate.glb", "p/crate.vxlayers.bake"],
            StackInputs.Of("p/crate.vxlayers", "model: crate.glb\nsets:\n  - layers:\n    - paint: a.vxpaint\n")
        );

    [Fact]
    public void A_missing_input_has_no_key() {
        var asset = AssetId.New();
        var project = Project(AssetReference.Floating(asset));

        Assert.Null(Key(project));
        Assert.Null(Key(project, new AssetNode(asset, null, [])));
        var noFile = OutputKeys.Compute(project.Output, project.Project, static _ => null, new([]), Tools);
        Assert.Null(noFile);
    }

    [Fact]
    public void A_cycle_has_no_key_and_is_named() {
        var a = AssetId.New();
        var b = AssetId.New();
        AssetGraph graph = new(
            [
                new(a, Bytes, [AssetReference.Floating(b)]),
                new(b, Bytes, [AssetReference.Floating(a)])
            ]
        );

        Assert.Null(graph.KeyOf(AssetReference.Floating(a)));
        Assert.Null(graph.KeyOf(AssetReference.Floating(b)));
        Assert.NotEmpty(graph.FindCycles());
    }

    [Fact]
    public void A_shared_dependency_is_no_cycle() {
        var top = AssetId.New();
        var left = AssetId.New();
        var right = AssetId.New();
        var bottom = AssetId.New();
        AssetGraph graph = new(
            [
                new(top, Bytes, [AssetReference.Floating(left), AssetReference.Floating(right)]),
                new(left, Bytes, [AssetReference.Floating(bottom)]),
                new(right, Bytes, [AssetReference.Floating(bottom)]),
                new(bottom, Bytes, [])
            ]
        );

        Assert.NotNull(graph.KeyOf(AssetReference.Floating(top)));
        Assert.Empty(graph.FindCycles());
    }

    /// <summary>
    ///     The two answers to "what does this change touch" must agree: the impact query in SQL, and the keys in C#.
    ///     Random acyclic graphs with random pins; the projects whose keys move are exactly the ones impact calls
    ///     unpinned, and pinned ones keep their keys.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void Impact_and_build_keys_agree() =>
        Gen.Int.Sample(
            static seed => {
                var random = new Random(seed);
                using var scratch = new TestLibrary();
                var assets = new List<(string Path, AssetDocument Document)>();
                var count = random.Next(2, 9);
                for (var i = 0; i < count; i++) {
                    // Edges only point at earlier assets, which keeps the graph acyclic.
                    var dependencies = assets.Where(_ => random.Next(3) == 0)
                        .Select(a => Reference(a.Document.Id, random))
                        .ToArray();
                    var document = TestLibrary.Asset($"A{i}", dependencies);
                    assets.Add(($"graphs/a{i}.vxtexgraph", document));
                    scratch.Write(assets[^1].Path, document);
                }

                var projects = Enumerable.Range(0, 12)
                    .Select(i => {
                            var references = assets.Where(_ => random.Next(3) == 0)
                                .Select(a => Reference(a.Document.Id, random))
                                .ToArray();
                            var output = new OutputDeclaration(
                                "ao",
                                "bake",
                                [],
                                [..references.Select(static r => r.Asset)],
                                new Dictionary<string, string>()
                            );
                            var project = TestLibrary.Project($"P{i}", references) with { Outputs = [output] };
                            scratch.Write($"p{i}", project);
                            return project;
                        }
                    )
                    .ToList();

                using var open = scratch.Open();
                open.Rescan();

                var changed = assets[random.Next(assets.Count)].Document.Id;
                var impact = open.Impact(changed, 0, 100)!;
                var before = Keys(projects, assets, static _ => Bytes);
                var after = Keys(projects, assets, id => id == changed ? NewBytes : Bytes);

                var moved = projects.Where(p => before[p.Id] != after[p.Id]).Select(static p => p.Id.Value).ToHashSet();
                var rebuilding = impact.Items.Where(static p => !p.Pinned).Select(static p => p.Id).ToHashSet();
                Assert.Equal(moved, rebuilding);
                Assert.Equal(impact.Projects - impact.Pinned, moved.Count);
                Assert.Equal(impact.Projects, impact.Items.Count);
            },
            iter: 60
        );

    static AssetReference Reference(AssetId asset, Random random) =>
        random.Next(3) == 0 ? AssetReference.Pinned(asset, Bytes) : AssetReference.Floating(asset);

    static Dictionary<ProjectId, BuildKey?> Keys(
        List<ProjectDocument> projects,
        List<(string Path, AssetDocument Document)> assets,
        Func<AssetId, Revision> content
    ) {
        AssetGraph graph = new(
            assets.Select(a => new AssetNode(a.Document.Id, content(a.Document.Id), a.Document.Dependencies))
        );
        return projects.ToDictionary(
            static p => p.Id,
            p => OutputKeys.Compute(p.Outputs[0], p, Files, graph, Tools)
        );
    }
}
