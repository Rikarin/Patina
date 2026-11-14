namespace Patina.Library.Tests;

/// <summary>
///     What baking a project with its own settings asks for (#186): the requests <c>patina bake-maps --project</c>
///     makes, which are the ones the Bake dialog sends.
/// </summary>
public sealed class ProjectBakesTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-project-bakes-");

    public void Dispose() => folder.Delete(true);

    static readonly AssetId Crate = AssetId.New();
    static readonly AssetId Rock = AssetId.New();

    static ProjectItem Set(string file, AssetId mesh, string? material) =>
        new(AssetId.New(), DocumentKinds.Texturing, file, file, mesh, Material: material);

    static ProjectDocument Project(params ProjectItem[] items) =>
        TestLibrary.Project("P") with {
            Meshes = [new(Rock, "rock.fbx"), new(Crate, "meshes/crate_low.fbx", "meshes/crate_high.fbx")],
            Documents = items,
            Bake = new() {
                Options = new() { Resolution = 512, Maps = ["normal", "ao"] },
                High = ["extra_high.fbx"],
                Sets = ["Lid"],
                Cage = " cage.fbx "
            }
        };

    [Fact]
    public void A_project_bakes_each_chosen_set_of_its_documents_mesh_into_its_bake_folder() {
        var project = Project(Set("lid.vxlayers", Crate, "Lid"), Set("box.vxlayers", Crate, "Box"));
        var root = folder.FullName;

        var request = Assert.Single(ProjectBakes.Requests(root, project));

        Assert.Equal(Path.Combine(root, "meshes", "crate_low.fbx"), request.Low);
        Assert.Equal(
            [Path.Combine(root, "meshes", "crate_high.fbx"), Path.Combine(root, "extra_high.fbx")],
            request.High
        );
        Assert.Equal(Path.Combine(root, "bakes", "maps", "Lid"), request.Output);
        Assert.Equal(("Lid", Path.Combine(root, "cage.fbx")), (request.Material, request.Cage));
        Assert.Equal(512, request.Options.Resolution);
        Assert.Equal(["normal", "ao"], request.Options.Maps);

        var both = ProjectBakes.Requests(root, project, sets: ["Box", "Lid"], output: Path.Combine(root, "out"));
        Assert.Equal(
            [Path.Combine(root, "out", "Lid"), Path.Combine(root, "out", "Box")],
            both.Select(static r => r.Output)
        );
    }

    [Fact]
    public void A_mesh_with_no_sets_bakes_once_and_a_document_chooses_the_mesh() {
        var project = Project(Set("lid.vxlayers", Crate, "Lid"), Set("rock.vxlayers", Rock, null));

        var request = Assert.Single(ProjectBakes.Requests(folder.FullName, project, "rock.vxlayers"));

        Assert.Equal((Path.Combine(folder.FullName, "rock.fbx"), null), (request.Low, request.Material));
        Assert.Equal(Path.Combine(folder.FullName, "rock.fbx"), request.High[0]);
        Assert.Equal(Path.Combine(folder.FullName, "bakes", "maps"), request.Output);
    }

    [Fact]
    public void A_project_with_no_mesh_or_no_such_set_or_document_is_refused_in_a_sentence() {
        var root = folder.FullName;
        var project = Project(Set("lid.vxlayers", Crate, "Lid"));

        var unknown = Assert.Throws<ArgumentException>(() => ProjectBakes.Requests(root, project, sets: ["Hull"]));
        Assert.Contains("no texture set 'Hull'", unknown.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => ProjectBakes.Requests(root, project, "nothing.vxlayers"));
        Assert.Throws<ArgumentException>(() => ProjectBakes.Requests(root, project with { Meshes = [] }));
        Assert.Throws<FileNotFoundException>(() => ProjectBakes.Read(root));
    }
}
