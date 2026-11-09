using Patina.Contracts;
using Patina.Server.Api;
using Patina.Server.Stacks;
using Vixen.Texturing.Layers;

namespace Patina.Server.Tests;

/// <summary>
///     Instance layers (#108) as commands: what they name, what they refuse, and what they say in History.
/// </summary>
public sealed class StackInstanceTests {
    [Fact]
    public void An_instance_names_its_source_and_goes_just_above_where_it_is_asked() {
        var editor = new StackEditor("/tmp/Instances.vxlayers", Sample());

        editor.Apply(new([new StackAddInstance(new("base"), "base")]));

        var instance = editor.Set.Layers[1];
        Assert.Equal(
            (LayerKind.Instance, "Base Instance", "base"),
            (instance.Kind, instance.Name, instance.Instance.Layer)
        );
        Assert.Equal(new("base"), StackDocuments.Layers(editor.Set.Layers)[1].Instance);
        Assert.Equal("Add instance", editor.History.UndoLabel);
        Assert.Equal("Add instance", editor.Undo());
        Assert.Equal(2, editor.Set.Layers.Count);
    }

    [Fact]
    public void A_source_that_is_not_there_and_a_cycle_are_refused_with_their_codes() {
        var editor = new StackEditor("/tmp/Instances.vxlayers", Sample());

        var missing = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackAddInstance(new("gone"))]))
        );
        Assert.Equal(Refusals.InstanceSourceMissing, missing.Code);

        editor.Apply(new([new StackAddInstance(new("base"))]));
        var first = editor.Set.Layers[^1].Id;
        editor.Apply(new([new StackAddInstance(new(first))]));
        var second = editor.Set.Layers[^1].Id;

        var cycle = Assert.Throws<StackEditRefusedException>(() =>
            editor.Apply(new([new StackSetInstance(first, new(second))]))
        );
        Assert.Equal(Refusals.InstanceCycle, cycle.Code);
        Assert.Equal("base", Layer(editor, first).Instance.Layer);

        editor.Apply(new([new StackSetInstance(second, new("paint"))]));
        Assert.Equal(
            ("paint", "Change instance source"),
            (Layer(editor, second).Instance.Layer, editor.History.UndoLabel)
        );
    }

    [Fact]
    public void An_instance_reads_a_layer_of_another_texture_set_by_its_name() {
        TextureSetAsset lid = new() {
            Name = "Lid",
            Channels = [new() { Usage = "baseColor", Default = [0f, 0f, 0f, 1f] }],
            Layers = [new() { Id = "rust", Name = "Rust", Kind = LayerKind.Fill }]
        };
        var editor = new StackEditor("/tmp/Instances.vxlayers", Sample()) {
            Sibling = name => name == "Lid" ? lid : null
        };

        editor.Apply(new([new StackAddInstance(new("rust", "Lid"))]));

        var instance = editor.Set.Layers[^1];
        Assert.Equal(("Lid", "rust", "Rust Instance"), (instance.Instance.Set, instance.Instance.Layer, instance.Name));
        Assert.Equal(
            Refusals.InstanceSourceMissing,
            Assert.Throws<StackEditRefusedException>(() => editor.Apply(
                        new([new StackAddInstance(new("rust", "Door"))])
                    )
                )
                .Code
        );
    }

    /// <summary>
    ///     A session whose instance reads another set is sent that set as it stands in its own session, unsaved, and an
    ///     edit there asks it to evaluate again without anyone asking it.
    /// </summary>
    [Fact]
    public void An_open_set_hands_its_instances_elsewhere_its_unsaved_edits_and_wakes_them() {
        var folder = Directory.CreateTempSubdirectory("patina-instances-");
        try {
            ServerIdentity identity = new(null, null);
            using StackSessions sessions = new(identity, new(identity));
            var bodyPath = Path.Combine(folder.FullName, "Body.vxlayers");
            var lidPath = Path.Combine(folder.FullName, "Lid.vxlayers");
            StackDocuments.Create(bodyPath, 64, 64);
            StackDocuments.Create(lidPath, 64, 64);
            StackDocuments.SetSetName(lidPath, "Lid");
            var body = sessions.Open(bodyPath);
            var lid = sessions.Open(lidPath);

            lid.Apply(new([new StackAddInstance(new("base", "Body"))]));
            Assert.False(lid.Pending()!.Forced);
            Assert.Contains("name: Body", lid.Pending()!.Document, StringComparison.Ordinal);

            body.Apply(
                new(
                    [
                        new StackSetLayer(
                            "base",
                            Values: new Dictionary<string, IReadOnlyList<float>> {
                                ["baseColor"] = [0.25f, 0.125f, 0.0625f, 1f]
                            }
                        )
                    ]
                )
            );

            var work = lid.Pending()!;
            Assert.True(work.Forced);
            Assert.Contains("0.125", work.Document, StringComparison.Ordinal);
        } finally {
            folder.Delete(true);
        }
    }

    [Fact]
    public void An_instance_is_not_a_kind_a_plain_add_makes() {
        var editor = new StackEditor("/tmp/Instances.vxlayers", Sample());

        Assert.Throws<StackEditRefusedException>(() => editor.Apply(new([new StackAddLayer("instance", "Copy")])));
    }

    static LayerAsset Layer(StackEditor editor, string id) => LayerStackEdit.Find(editor.Document, new("Body", id))!;

    static LayerStackAsset Sample() =>
        new() {
            Name = "Crate",
            BaseWidth = 64,
            BaseHeight = 64,
            Sets = [
                new() {
                    Name = "Body",
                    Channels = [new() { Usage = "baseColor", Default = [0.5f, 0.5f, 0.5f, 1f] }],
                    Layers = [
                        new() { Id = "base", Name = "Base", Kind = LayerKind.Fill },
                        new() { Id = "paint", Name = "Paint", Kind = LayerKind.Paint }
                    ]
                }
            ]
        };
}
