using Patina.Contracts;
using Patina.Export.Materials;
using Patina.Ipc;
using Patina.Library;
using System.Globalization;
using System.Text;

namespace Patina.Server.Graphs;

/// <summary>
///     Brings the materials of a material file into a project, each as a material graph of its own (#157): a picture
///     node per image, its lane, factor and turn of green as nodes after it, and a constant for an input with no
///     image. A graph needs no format of its own, and opening it is all it takes to go further, as an image material.
///     MaterialX and USD's text form are read here; a model's materials are read by the Worker, because Assimp is a
///     native parser that may crash. The pictures are copied into the project's <c>pictures</c>, in a folder named
///     after the file, so the graphs read them wherever the library moves.
/// </summary>
static class MaterialImport {
    const string Shuffle = "Colour/Channel Shuffle";
    const string Mix = "Colour/Channel Mix";
    static readonly string[] Channels = ["Red", "Green", "Blue"];
    static readonly string[] Lanes = ["FirstRed", "FirstGreen", "FirstBlue", "FirstAlpha"];

    /// <summary>Reads a material file and makes a graph of each of its materials.</summary>
    /// <param name="library">The library.</param>
    /// <param name="worker">The Worker, which reads a model's materials.</param>
    /// <param name="request">The project and the file.</param>
    /// <param name="cancellationToken">Stops waiting for the Worker.</param>
    /// <exception cref="ProjectRefusedException">Materials are not read from the file, or it has none.</exception>
    /// <exception cref="FileNotFoundException">The file or the project is not there.</exception>
    /// <exception cref="IpcException">The Worker would not read the model.</exception>
    public static async Task<MaterialImportResult> ImportAsync(
        OpenLibrary library,
        WorkerLink worker,
        MaterialImportRequest request,
        CancellationToken cancellationToken
    ) {
        var file = Path.GetFullPath(request.File);
        if (!MaterialFormats.IsMaterial(file)) {
            throw new ProjectRefusedException(
                $"'{Path.GetFileName(file)}' is not a file Patina reads materials from: use "
                + $"{string.Join(", ", MaterialFormats.Materials)}."
            );
        }

        if (!File.Exists(file)) {
            throw new FileNotFoundException($"There is no file '{request.File}'.", request.File);
        }

        library.Contents(request.Project);
        var stem = Clean(Path.GetFileNameWithoutExtension(file));
        var pictures = library.ProjectFile(request.Project, $"pictures/{stem}");
        IReadOnlyList<ImportedMaterial> materials;
        try {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            materials = extension == MaterialFormats.MaterialX ? MaterialXReader.Read(file)
                : MaterialFormats.Texts.Contains(extension, StringComparer.Ordinal) ? UsdaReader.Read(file)
                : await ReadModelAsync(worker, file, pictures, cancellationToken);
        } catch (InvalidDataException e) {
            throw new ProjectRefusedException(e.Message);
        }

        if (materials.Count == 0) {
            throw new ProjectRefusedException($"'{Path.GetFileName(file)}' names no material.");
        }

        List<string> documents = [];
        List<string> warnings = [];
        foreach (var material in materials) {
            warnings.AddRange(material.Warnings);
            var name = material.Name.Length > 0 ? material.Name : stem;
            var text = Graph(name, material, new(pictures, warnings));
            var graph = library.NewDocumentFile(request.Project, name, LibraryLayout.GraphExtension);
            File.WriteAllText(library.ProjectFile(request.Project, graph), text);
            library.AddDocument(request.Project, DocumentKinds.Graph, graph, name, null);
            documents.Add($"{LibraryLayout.ProjectsFolder}/{request.Project}/{graph}");
        }

        return new(documents, [..warnings.Distinct(StringComparer.Ordinal)]);
    }

    static async Task<IReadOnlyList<ImportedMaterial>> ReadModelAsync(
        WorkerLink worker,
        string file,
        string pictures,
        CancellationToken cancellationToken
    ) {
        var connection = await worker.ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.MaterialRead, MaterialRead = new(file, pictures) },
            cancellationToken
        );
        return reply.MaterialsRead ?? [];
    }

    /// <summary>
    ///     A picture where the graph reads it, relative to the project: left where it is when it is in the project
    ///     already, and otherwise copied into the file's folder of pictures. Null, and said, for one a picture node
    ///     cannot read.
    /// </summary>
    static string? Bring(string picture, string pictures, List<string> warnings, string material) {
        if (!PictureFormats.IsPicture(picture)) {
            warnings.Add(
                $"{material}: '{Path.GetFileName(picture)}' is left out, because a graph reads "
                + $"{string.Join(", ", PictureFormats.Pictures)}."
            );
            return null;
        }

        if (!File.Exists(picture)) {
            warnings.Add($"{material}: its picture '{Path.GetFileName(picture)}' is not there.");
            return null;
        }

        var root = Path.GetDirectoryName(pictures)!;
        var projectFolder = Path.GetDirectoryName(root)!;
        var full = Path.GetFullPath(picture);
        if (!full.StartsWith(projectFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
            Directory.CreateDirectory(pictures);
            var copy = Path.Combine(pictures, Path.GetFileName(full));
            if (!File.Exists(copy) || LibraryFiles.Digest(copy) != LibraryFiles.Digest(full)) {
                File.Copy(full, copy, true);
            }

            full = copy;
        }

        return Path.GetRelativePath(projectFolder, full).Replace('\\', '/');
    }

    /// <summary>The graph of one material, as the text of its file.</summary>
    /// <summary>Where a material's pictures go in the project, and what was said of them.</summary>
    sealed record Pictures(string Folder, List<string> Warnings);

    static string Graph(string name, ImportedMaterial material, Pictures pictures) {
        var graph = new GraphText();
        foreach (var slot in material.Slots) {
            if (Made(graph, slot, pictures, name) is { } node) {
                var output = graph.Node("Output/Output", [("Usage", slot.Usage)]);
                graph.Edge(node, output, "Input");
            }
        }

        return GraphDocuments.Text(GraphDocuments.Parse(graph.Text(name)));
    }

    /// <summary>The node a slot comes out of: its picture's, its constant's, or none for a picture left out.</summary>
    static int? Made(GraphText graph, ImportedSlot slot, Pictures pictures, string material) {
        if (slot.Texture is null) {
            return Constant(graph, slot);
        }

        return Bring(slot.Texture, pictures.Folder, pictures.Warnings, material) is { } relative
            ? Picture(graph, slot, relative, pictures.Warnings)
            : null;
    }

    /// <summary>
    ///     A picture moved, turned and scaled where its material places it, as a <c>Space/Transform 2D</c> after it,
    ///     or the picture as it is. A placement no transform can draw is left out and said.
    /// </summary>
    static int Placed(GraphText graph, ImportedSlot slot, int picture, List<string> warnings) {
        if (slot.Placement is not { } placement) {
            return picture;
        }

        if (PlacementNode.Of(placement) is not { } node) {
            warnings.Add(
                $"{slot.Usage}: its picture's texture transform scales it to nothing, so it is left as it is."
            );
            return picture;
        }

        return graph.Chain(
            picture,
            "Space/Transform 2D",
            [
                ("Rotation", [node.Rotation]), ("Scale X", [node.ScaleX]), ("Scale Y", [node.ScaleY]),
                ("Offset X", [node.OffsetX]), ("Offset Y", [node.OffsetY]), ("Shear X", [node.ShearX]),
                ("Shear Y", [node.ShearY])
            ]
        );
    }

    /// <summary>
    ///     A picture's lanes as a map: one lane spread over three, then the factor, one minus it, or green turned so it
    ///     runs down the picture as a graph's normal does (ADR 0011). A color's factor is linear, so a base color is
    ///     taken to linear numbers for it and back; an emissive picture holds sRGB numbers and a graph's emissive is
    ///     linear, so it is taken to linear.
    /// </summary>
    static int Picture(GraphText graph, ImportedSlot slot, string relative, List<string> warnings) {
        var bitmap = graph.Node("Source/Bitmap", [("Source", relative)]);
        var node = Lane(graph, Placed(graph, slot, bitmap, warnings), slot.Lane);
        var colour = slot.Usage == "baseColor";
        float[] factor = [..slot.Value.Take(3)];
        var scaled = slot.Inverted || factor.Any(static f => Math.Abs(f - 1f) > 1e-4f);
        if (slot.Usage == "emissive" || (colour && scaled)) {
            node = graph.Chain(node, "Colour/sRGB to Linear");
        }

        if (scaled) {
            node = graph.Chain(node, Mix, Weights(factor, slot.Inverted));
        }

        if (slot.Usage == "normal" && slot.GreenUp) {
            node = graph.Chain(node, Mix, [("Green from Green", [-1f]), ("Green Offset", [1f])]);
        }

        return colour && scaled ? graph.Chain(node, "Colour/Linear to sRGB") : node;
    }

    /// <summary>One lane of a picture, alpha too, in red, green and blue, or the picture as it is for none.</summary>
    static int Lane(GraphText graph, int picture, int lane) {
        if (lane is < 0 or > 3) {
            return picture;
        }

        var from = Lanes[lane];
        var shuffled = graph.Node(
            Shuffle,
            [("Red From", from), ("Green From", from), ("Blue From", from), ("Alpha From", "One")]
        );
        graph.Edge(picture, shuffled, "First");
        return shuffled;
    }

    /// <summary>A channel mix that multiplies each lane by its factor, or makes it factor times one minus it.</summary>
    static List<(string Name, float[] Value)> Weights(float[] factor, bool inverted) {
        List<(string, float[])> weights = [];
        for (var row = 0; row < 3; row++) {
            var scale = inverted ? -factor[row] : factor[row];
            for (var column = 0; column < 3; column++) {
                weights.Add(($"{Channels[row]} from {Channels[column]}", [row == column ? scale : 0f]));
            }

            weights.Add(($"{Channels[row]} Offset", [inverted ? factor[row] : 0f]));
        }

        return weights;
    }

    /// <summary>A constant as a map. A base color is sRGB numbers in a graph and linear in a material file.</summary>
    static int Constant(GraphText graph, ImportedSlot slot) {
        float[] value = [..slot.Value];
        if (slot.Usage == "baseColor") {
            for (var lane = 0; lane < 3; lane++) {
                value[lane] = Vixen.Core.Imaging.MipChain.ToSrgb(value[lane]);
            }
        }

        return graph.Node("Source/Uniform", values: [("Colour", value)]);
    }

    static string Clean(string name) {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(letter => invalid.Contains(letter) ? '_' : letter));
    }

    /// <summary>A graph's file as YAML, node by node, laid out in columns.</summary>
    sealed class GraphText {
        readonly StringBuilder nodes = new();
        readonly StringBuilder edges = new();
        int count;
        int row;
        int column;

        public int Node(
            string type,
            IReadOnlyList<(string Name, string Value)>? texts = null,
            IReadOnlyList<(string Name, float[] Value)>? values = null
        ) {
            var id = ++count;
            column = type == "Output/Output" ? Math.Max(6, column + 1)
                : type is "Source/Bitmap" or "Source/Uniform" ? 0
                : column + 1;
            if (type is "Source/Bitmap" or "Source/Uniform") {
                row++;
            }

            nodes.Append(
                CultureInfo.InvariantCulture,
                $"  - id: {id}\n    type: {type}\n    x: {column * 280}\n    y: {row * 220}\n"
            );
            if (texts is { Count: > 0 }) {
                nodes.Append("    texts:\n");
                foreach (var (name, value) in texts) {
                    nodes.Append(CultureInfo.InvariantCulture, $"      {Quoted(name)}: {Quoted(value)}\n");
                }
            }

            if (values is { Count: > 0 }) {
                nodes.Append("    values:\n");
                foreach (var (name, value) in values) {
                    var numbers = string.Join(
                        ", ",
                        value.Select(static v => v.ToString("R", CultureInfo.InvariantCulture))
                    );
                    nodes.Append(CultureInfo.InvariantCulture, $"      {Quoted(name)}: [{numbers}]\n");
                }
            }

            return id;
        }

        public int Chain(int from, string type, IReadOnlyList<(string Name, float[] Value)>? values = null) {
            var node = Node(type, values: values);
            Edge(from, node, "Input");
            return node;
        }

        public void Edge(int from, int to, string port) =>
            edges.Append(
                CultureInfo.InvariantCulture,
                $"  - {{ fromNode: {from}, fromPort: Out, toNode: {to}, toPort: {Quoted(port)} }}\n"
            );

        public string Text(string name) {
            var side = TextureSizes.Default.ToString(CultureInfo.InvariantCulture);
            return $$"""
                     name: {{Quoted(name)}}
                     nodes:
                     {{(count == 0 ? "  []\n" : nodes.ToString())}}edges:
                     {{(edges.Length == 0 ? "  []\n" : edges.ToString())}}groups: []
                     comments: []
                     interface: []
                     settings: { baseWidth: '2048', baseHeight: '2048', seed: '1', size: '{{side}}' }
                     parameters: []
                     """;
        }

        static string Quoted(string text) => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
