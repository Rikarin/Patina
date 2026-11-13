using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Jobs;

namespace Patina.Export.Tests;

/// <summary>
///     An export of some of a project's sets, or of one document on its own, into the folder the whole project goes to
///     (#153): it replaces its own sets' files and leaves the others'.
/// </summary>
public sealed class ExportPartTests : IDisposable {
    const string Preset = """
                          version: 1
                          id: p
                          name: P
                          normal: y-up
                          textures:
                            - file: T_{project}_R
                              format: png8
                              space: linear
                              channels: [roughness]
                          """;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-part-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    string Target => Path.Combine(folder.FullName, "delivered");

    ExportSource Source(string name, string set) {
        var path = Directory.CreateDirectory(Path.Combine(folder.FullName, name)).FullName;
        var map = MapPictures.Write(path, "roughness", new(1, 1, 1, [0.5f]), true, null);
        return new(path, new("texture-graph", [map], [], [])) { Set = set };
    }

    ExportManifest Run(string name, params ExportSource[] sources) =>
        ExportSteps.Run(new(Preset, sources, Path.Combine(folder.FullName, name)), null, Token);

    IReadOnlyList<string> Deliver(string made, ExportManifest manifest, ExportPart? part) =>
        [
            ..ExportRun.Deliver(made, Path.Combine(folder.FullName, made), manifest, Target, new("Crate", "p"), part: part)
                .Files.Select(Path.GetFileName)
                .OfType<string>()
        ];

    /// <summary>
    ///     Each export once removed every file the last one made and it did not, so exporting one set threw away the
    ///     other's.
    /// </summary>
    [Fact]
    public void An_export_of_one_set_leaves_the_other_sets_files_and_a_whole_export_tidies_them() {
        var body = Source("body", "Body");
        var strap = Source("strap", "Strap");
        Deliver("whole", Run("whole", body, strap), null);

        var only = Deliver("one", Run("one", body), new ExportPart(["Body"], true));

        Assert.Equal(["T_Crate_Body_R.png"], only);
        Assert.True(File.Exists(Path.Combine(Target, "T_Crate_Strap_R.png")));
        var kept = BuildObjects.Delivered(Target)!;
        Assert.Contains("T_Crate_Strap_R.png", kept.Files);
        Assert.Equal("Strap", kept.Sets!["T_Crate_Strap_R.png"]);

        Deliver("body-only", Run("body-only", body), null);

        Assert.False(File.Exists(Path.Combine(Target, "T_Crate_Strap_R.png")));
    }

    /// <summary>A graph exported on its own takes its set's name, and never the name of the project's own file.</summary>
    [Fact]
    public void A_document_on_its_own_is_named_by_its_set_even_alone() {
        var stack = Source("stack", "Body");
        Deliver("whole", Run("whole", stack), null);

        var graph = Deliver("graph", Run("graph", Source("rust", "rust")), new ExportPart(["rust"], true));

        Assert.Equal(["T_Crate_rust_R.png"], graph);
        Assert.True(File.Exists(Path.Combine(Target, "T_Crate_R.png")));
    }

    /// <summary>A delivery from before sets were kept is not known to be another set's, so a part keeps all of it.</summary>
    [Fact]
    public void A_part_keeps_what_an_older_delivery_did_not_place() {
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "old.png"), "x");
        File.WriteAllBytes(
            Path.Combine(Target, BuildObjects.DeliveredFile),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                new ExportDelivery("old", ["old.png"]),
                ContractsJsonContext.Default.ExportDelivery
            )
        );

        Deliver("one", Run("one", Source("body", "Body")), new ExportPart(["Body"], false));

        Assert.True(File.Exists(Path.Combine(Target, "old.png")));
    }
}
