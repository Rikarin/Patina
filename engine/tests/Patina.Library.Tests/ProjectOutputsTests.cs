namespace Patina.Library.Tests;

/// <summary>What a project builds from its documents, with nothing written by hand.</summary>
public sealed class ProjectOutputsTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-outputs-");

    public void Dispose() => folder.Delete(true);

    static ProjectItem Item(string kind, string file, string? material = null, string? role = null) =>
        new(AssetId.New(), kind, file, Path.GetFileNameWithoutExtension(file), Role: role, Material: material);

    static ProjectDocument Project(params ProjectItem[] items) => TestLibrary.Project("P") with { Documents = items };

    void Bake(string relative) {
        var bake = Directory.CreateDirectory(Path.Combine(folder.FullName, relative)).FullName;
        File.WriteAllText(Path.Combine(bake, "low.bake.json"), "{}");
        File.WriteAllText(Path.Combine(bake, "low_normal.png"), "n");
        File.WriteAllText(Path.Combine(bake, "low_normal.preview.png"), "p");
    }

    [Fact]
    public void A_new_project_has_no_outputs_written_and_still_builds_its_document() {
        var (_, document) = NewProjects.Resolve(new("Crate"));
        var made = document with { Documents = [Item(DocumentKinds.Texturing, "crate.vxlayers")] };

        Assert.Empty(made.Outputs);
        var output = Assert.Single(ProjectOutputs.Of(made, folder.FullName));
        Assert.Equal(BuildTools.LayerStack, output.Tool);
        Assert.Equal("crate.vxlayers", output.Files[0]);
    }

    [Fact]
    public void Each_texturing_document_is_a_set_with_its_own_bake() {
        Bake("bakes/maps/Body");
        Bake("bakes/maps/Strap");
        var project = Project(
            Item(DocumentKinds.Texturing, "body.vxlayers", "Body"),
            Item(DocumentKinds.Texturing, "strap.vxlayers", "Strap")
        );

        var outputs = ProjectOutputs.Of(project, folder.FullName);

        Assert.Equal(
            [
                (BuildTools.LayerStack, "Body"),
                (BuildTools.BakedMaps, "Body"),
                (BuildTools.LayerStack, "Strap"),
                (BuildTools.BakedMaps, "Strap")
            ],
            outputs.Select(static o => (o.Tool, o.Settings[ProjectOutputs.SetSetting]))
        );
        Assert.Equal("bakes/maps/Body", outputs[0].Settings[ProjectOutputs.BakeSetting]);
        Assert.Contains("bakes/maps/Strap/low_normal.png", outputs[2].Files);
        Assert.DoesNotContain("bakes/maps/Strap/low_normal.preview.png", outputs[2].Files);
    }

    [Fact]
    public void A_set_with_no_bake_of_its_own_does_not_take_its_neighbours() {
        Bake("bakes/maps/Body");
        var project = Project(
            Item(DocumentKinds.Texturing, "body.vxlayers", "Body"),
            Item(DocumentKinds.Texturing, "strap.vxlayers", "Strap")
        );

        var outputs = ProjectOutputs.Of(project, folder.FullName);

        Assert.Equal(3, outputs.Count);
        Assert.False(outputs[2].Settings.ContainsKey(ProjectOutputs.BakeSetting));
    }

    [Fact]
    public void A_bake_a_person_chose_outranks_the_usual_folder_and_a_gone_one_does_not() {
        Bake("bakes/maps");
        Bake("bakes/other");
        var item = Item(DocumentKinds.Texturing, "crate.vxlayers");
        var pointer = GraphMeshMaps.FileOf(Path.Combine(folder.FullName, item.File));

        File.WriteAllText(pointer, "bakes/other");
        Assert.Equal(Path.Combine(folder.FullName, "bakes", "other"), DocumentBakes.FolderOf(folder.FullName, item));

        File.WriteAllText(pointer, "bakes/deleted");
        Assert.Equal(Path.Combine(folder.FullName, "bakes", "maps"), DocumentBakes.FolderOf(folder.FullName, item));
    }

    /// <summary>Stacks and graphs open on one rule, and neither needs a client to remember for it.</summary>
    [Fact]
    public void A_document_finds_its_bake_from_its_file_alone() {
        Bake("bakes/maps/Body");
        var mesh = AssetId.New();
        var body = Item(DocumentKinds.Texturing, "body.vxlayers", "Body") with { Mesh = mesh };
        var strap = Item(DocumentKinds.Texturing, "strap.vxlayers", "Strap") with { Mesh = mesh };
        File.WriteAllText(
            Path.Combine(folder.FullName, LibraryLayout.ProjectFile),
            DocumentCodec.Write(Project(body, strap) with { Meshes = [new(mesh, "low.glb")] })
        );

        Assert.Equal(
            Path.Combine(folder.FullName, "bakes", "maps", "Body"),
            DocumentBakes.OfFile(Path.Combine(folder.FullName, "body.vxlayers"))
        );
        Assert.Null(DocumentBakes.OfFile(Path.Combine(folder.FullName, "strap.vxlayers")));
    }

    [Fact]
    public void A_project_lists_each_finished_bake_however_deep() {
        Bake("bakes/maps/Body");
        Bake("bakes/maps/Strap");
        Directory.CreateDirectory(Path.Combine(folder.FullName, "bakes", "empty"));

        var maps = Path.Combine(folder.FullName, "bakes", "maps");
        Assert.Equal(
            [Path.Combine(maps, "Body"), Path.Combine(maps, "Strap")],
            DocumentBakes.Below(Path.Combine(folder.FullName, "bakes"))
        );
    }

    [Fact]
    public void A_material_project_exports_its_materials_and_not_its_masks() {
        var project = Project(
            Item(DocumentKinds.LayeredMaterial, "wood.vxlayers"),
            Item(DocumentKinds.Graph, "rust.vxtexgraph"),
            Item(DocumentKinds.Graph, "scratches.vxtexgraph", role: GraphRoles.Mask),
            Item(DocumentKinds.ImageMaterial, "photo.vxtexgraph", role: GraphRoles.Material)
        );

        var outputs = ProjectOutputs.Of(project, folder.FullName);

        Assert.Equal(["wood.vxlayers", "rust.vxtexgraph", "photo.vxtexgraph"], outputs.Select(static o => o.Name));
        Assert.Equal(BuildTools.TextureGraph, outputs[1].Tool);
        Assert.Equal("rust.vxtexgraph", outputs[1].Settings[ProjectOutputs.GraphSetting]);
    }

    [Fact]
    public void A_texturing_project_ships_its_stacks_and_depends_on_what_they_read() {
        File.WriteAllText(Path.Combine(folder.FullName, "rust.vxtexgraph"), "x");
        Directory.CreateDirectory(Path.Combine(folder.FullName, "pictures"));
        File.WriteAllText(Path.Combine(folder.FullName, "pictures", "decal.png"), "x");
        var shared = AssetReference.Floating(AssetId.New());
        var project = Project(
            Item(DocumentKinds.Texturing, "crate.vxlayers"),
            Item(DocumentKinds.Graph, "rust.vxtexgraph")
        ) with {
            References = [shared]
        };

        var output = Assert.Single(ProjectOutputs.Of(project, folder.FullName));

        Assert.Equal(["crate.vxlayers", "rust.vxtexgraph", "pictures/decal.png"], output.Files);
        Assert.Equal([shared.Asset], output.Uses);
    }

    /// <summary>
    ///     A set on a mesh reads the mesh's layout, which its export pads from, so the mesh is one of its files and a new
    ///     mesh gives it a new key. A mesh that is not there yet is not named.
    /// </summary>
    [Fact]
    public void A_set_on_a_mesh_names_the_mesh_and_its_material() {
        File.WriteAllText(Path.Combine(folder.FullName, "crate.glb"), "x");
        var mesh = new ProjectMesh(AssetId.New(), "crate.glb");
        var gone = new ProjectMesh(AssetId.New(), "gone.glb");
        var project = Project(
            Item(DocumentKinds.Texturing, "body.vxlayers", "Body") with { Mesh = mesh.Id },
            Item(DocumentKinds.Texturing, "lid.vxlayers", "Lid") with { Mesh = gone.Id }
        ) with {
            Meshes = [mesh, gone]
        };

        var outputs = ProjectOutputs.Of(project, folder.FullName);

        Assert.Contains("crate.glb", outputs[0].Files);
        Assert.Equal("crate.glb", outputs[0].Settings[ProjectOutputs.MeshSetting]);
        Assert.Equal("Body", outputs[0].Settings[ProjectOutputs.MaterialSetting]);
        Assert.False(outputs[1].Settings.ContainsKey(ProjectOutputs.MeshSetting));
    }

    [Fact]
    public void Outputs_written_by_hand_replace_what_the_documents_imply() {
        OutputDeclaration written = new("lods", BuildTools.MeshLod, ["low.glb"], [], new Dictionary<string, string>());
        var project = Project(Item(DocumentKinds.Texturing, "crate.vxlayers")) with { Outputs = [written] };

        Assert.Equal([written], ProjectOutputs.Of(project, folder.FullName));
    }

    [Fact]
    public void A_stacks_own_paint_passes_by_its_full_path_and_paint_outside_does_not() {
        var stack = Path.Combine(folder.FullName, "crate.vxlayers");

        StackInputs.CheckPaint(stack, "paint: crate.paint.vxpaint\n");

        Assert.Throws<InvalidDataException>(() => StackInputs.CheckPaint(stack, "paint: ../other.vxpaint\n"));
    }

    [Fact]
    public void An_empty_project_builds_nothing() =>
        Assert.Empty(ProjectOutputs.Of(TestLibrary.Project("P"), folder.FullName));
}
