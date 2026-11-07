using Patina.Contracts;
using Patina.Library;
using System.Globalization;

namespace Patina.Server.Graphs;

/// <summary>
///     A material made from one picture (ADR 0012). It is an ordinary graph from a template, so it needs no format or
///     evaluator of its own, and opening it as a graph is all it takes to go further. ⚠ There is no neural step: its
///     height is the picture's brightness, so lighting baked into a photograph stays in the material.
/// </summary>
static class ImageMaterial {
    const string Template = """
                            name: 'NAME'
                            nodes:
                              - id: 1
                                type: Source/Bitmap
                                texts: { Source: 'PICTURE' }
                              - { id: 2, type: Colour/Grayscale }
                              - id: 3
                                type: Surface/Height to Normal
                                values: { Intensity: [2] }
                              - { id: 4, type: Colour/Levels }
                              - { id: 5, type: Output/Output, texts: { Usage: baseColor } }
                              - { id: 6, type: Output/Output, texts: { Usage: height } }
                              - { id: 7, type: Output/Output, texts: { Usage: normal } }
                              - { id: 8, type: Output/Output, texts: { Usage: roughness } }
                            edges:
                              - { fromNode: 1, fromPort: Out, toNode: 2, toPort: Input }
                              - { fromNode: 2, fromPort: Out, toNode: 3, toPort: Height }
                              - { fromNode: 2, fromPort: Out, toNode: 4, toPort: Input }
                              - { fromNode: 1, fromPort: Out, toNode: 5, toPort: Input }
                              - { fromNode: 2, fromPort: Out, toNode: 6, toPort: Input }
                              - { fromNode: 3, fromPort: Out, toNode: 7, toPort: Input }
                              - { fromNode: 4, fromPort: Out, toNode: 8, toPort: Input }
                            groups: []
                            comments: []
                            interface: []
                            settings: { baseWidth: '2048', baseHeight: '2048', seed: '1', size: 'SIZE' }
                            parameters: []
                            """;

    /// <summary>Brings a picture into a project, and writes the graph that makes a material of it.</summary>
    /// <exception cref="ProjectRefusedException">There is no picture.</exception>
    public static void Create(OpenLibrary library, ProjectDocumentRequest request) {
        if (request.Image is not { } image || !File.Exists(image)) {
            throw new ProjectRefusedException("An image material needs a picture.");
        }

        var file = library.NewDocumentFile(request.Project, request.Name, LibraryLayout.GraphExtension);
        var picture = Path.GetFileName(image);
        var copy = library.ProjectFile(request.Project, picture);
        if (Path.GetFullPath(image) != copy) {
            File.Copy(image, copy, true);
        }

        var quoted = picture.Replace("'", "''", StringComparison.Ordinal);
        var name = request.Name.Replace("'", "''", StringComparison.Ordinal);
        var text = Template.Replace("NAME", name, StringComparison.Ordinal)
            .Replace("PICTURE", quoted, StringComparison.Ordinal)
            .Replace(
                "SIZE",
                (request.Resolution ?? TextureSizes.Default).ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal
            );
        File.WriteAllText(library.ProjectFile(request.Project, file), GraphDocuments.Text(GraphDocuments.Parse(text)));
        library.AddDocument(request.Project, DocumentKinds.ImageMaterial, file, request.Name, null);
    }
}
