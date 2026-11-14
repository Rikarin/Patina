using Patina.Contracts;
using Patina.Export;
using Patina.Library;
using Patina.Server.Api;
using System.Globalization;
using Vixen.NodeGraph;
using Vixen.TextureGraph;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     A layer stack as the graph it stands for, written beside it as a new graph document (#205). Vixen's
///     <see cref="LayerStackGraph.Build" /> is the graph the stack is computed from, so the copy is no simplified
///     picture of it: every node is one a person could have placed, with a comment naming the layer it came from.
///     <para>
///         It goes one way. The stack is left as it was, and nothing keeps the two in step: the graph is to take
///         further than layers can.
///     </para>
/// </summary>
static class StackGraphs {
    /// <summary>How many numbered names are tried after the one asked for.</summary>
    const int Tries = 99;

    /// <summary>Writes the graph of the set the session shows into the stack's project.</summary>
    /// <exception cref="StackEditRefusedException">
    ///     The stack has unsaved edits, is in no project, or a layer cannot become a graph.
    /// </exception>
    public static StackGraphMade Write(OpenLibrary library, StackSession session, StackGraphRequest request) {
        var name = request.Name.Trim();
        if (name.Length == 0) {
            throw new StackEditRefusedException("A graph needs a name.");
        }

        // ⚠ Painted layers are read from their files, and strokes not yet saved are only in the Worker's memory, so a
        // graph written now would show the stack as it was saved and not as it is seen.
        if (session.Summary.Dirty) {
            throw new StackEditRefusedException(
                "Save the stack first: its graph reads painted layers from their files.",
                Refusals.StackGraphUnsaved
            );
        }

        var project = StackChecking.ProjectOf(library, session.Path)
            ?? throw new StackEditRefusedException(
                "This stack is in no project, so its graph has no project to go into.",
                Refusals.StackGraphNoProject
            );

        var build = Build(session);
        var (file, named) = NewFile(library, project.Path, name);
        var full = library.ProjectFile(project.Path, file);
        // Called what the project lists it as, where the build names it after the stack and its set.
        build.Graph.Name = named;
        LibraryFiles.Replace(full, LayerStackExplode.ToYaml(build));
        if (session.MeshMaps is { } bake) {
            // The bake the stack reads, which the graph's mesh maps read too; relative, as a graph's own choice is.
            LibraryFiles.Replace(GraphMeshMaps.FileOf(full), Path.GetRelativePath(Path.GetDirectoryName(full)!, bake));
        }

        library.AddDocument(project.Path, DocumentKinds.Graph, file, named, null);
        return new(
            $"{LibraryLayout.ProjectsFolder}/{project.Path.Trim('/')}/{file}",
            named,
            [..build.Problems.Select(static problem => problem.Message).Distinct(StringComparer.Ordinal)]
        );
    }

    /// <summary>
    ///     The graph the stack's Worker computes, at full quality: with the project's compounds, the bake's normal and
    ///     occlusion beneath the layers, decals read through the bake's box, and the stack's size and seed declared.
    /// </summary>
    /// <exception cref="StackEditRefusedException">A layer cannot become part of a graph.</exception>
    public static LayerStackBuild Build(StackSession session) {
        var document = StackDocuments.Parse(session.Text);
        var set = LayerStackEdit.SetFor(document, session.Set)
            ?? throw new StackEditRefusedException("This document has no texture set to make a graph of.");
        if (BakedMapPicture.PositionBox(session.MeshMaps) is { Count: 6 } box) {
            var at = document.Sets.IndexOf(set);
            document = LayerSurfaceBoxes.Of(document, box);
            set = at >= 0 ? document.Sets[at] : set;
        }

        var registry = StackNodeKinds.Registry(session.Folder, out var compounds);
        var build = LayerStackGraph.Build(
            document,
            set,
            registry,
            false,
            compounds,
            BakedMapPicture.Shading(session.MeshMaps)
        );
        if (build.HasErrors) {
            var reason = string.Join(
                ' ',
                build.Problems.Where(static problem => problem.Severity == NodeSeverity.Error)
                    .Select(static problem => problem.Message)
            );
            throw new StackEditRefusedException(
                $"This stack cannot become a graph. {reason}",
                Refusals.StackGraphRefused,
                new Dictionary<string, string> { ["reason"] = reason }
            );
        }

        LayerStackExplode.Decorate(build);
        var graph = build.Graph;
        // Vixen's header says the layers are gone, which they are not here: the stack stays beside its graph.
        foreach (var comment in graph.Comments.Where(static c => c.Text == LayerStackExplode.ExplodedHeader)) {
            comment.Text = $"Written from the layers of {document.Name}, which stay as they were. Nothing keeps this "
                + "graph in step with them, and every node here is yours to change.";
        }

        TextureGraphSettings.Declare(graph, document.BaseWidth, document.BaseHeight, document.Seed);
        var side = Math.Max(document.BaseWidth, document.BaseHeight);
        if (TextureSizes.Sides.Contains(side)) {
            graph.Settings[TextureSizes.GraphSetting] = side.ToString(CultureInfo.InvariantCulture);
        }

        return build;
    }

    /// <summary>
    ///     The file a new graph goes into, and its name: the one asked for, or the first free one numbered.
    /// </summary>
    static (string File, string Name) NewFile(OpenLibrary library, string project, string name) {
        for (var number = 1;; number++) {
            var candidate = number == 1 ? name : $"{name} {number.ToString(CultureInfo.InvariantCulture)}";
            try {
                return (library.NewDocumentFile(project, candidate, LibraryLayout.GraphExtension), candidate);
            } catch (ProjectExistsException) when (number <= Tries) {
                // Taken; the next number may not be.
            }
        }
    }
}
