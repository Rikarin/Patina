using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Library;
using System.Collections.Immutable;

namespace Patina.Server.Api;

/// <summary>
///     The sample projects Patina ships, <c>content/samples</c> under CC0 (#190, #34). A sample is a folder: one mesh,
///     laid out already, and a texturing document for each of its materials, named for the material. Making one copies
///     it into the library as a project of its own, which the person then owns like any other.
/// </summary>
static class SampleEndpoints {
    const string Resource = "Sample/";
    const string StackExtension = ".vxlayers";
    const int MostCopies = 100;

    /// <summary>Each sample's files, by sample, as the Server embeds them.</summary>
    static readonly ImmutableSortedDictionary<string, ImmutableArray<string>> Files = Embedded();

    public static void MapSampleRoutes(this RouteGroupBuilder library) {
        library.MapGet("/samples", static () => TypedResults.Ok(new SampleList([..Files.Keys])))
            .Described("GetSamples", "The sample projects that can be made, by name.");

        library.MapPost("/samples", Make)
            .Described(
                "MakeSample",
                "Copies a sample into the library as a new project: its mesh, laid out, and a texturing document for "
                + "each of its materials. A name already taken gets a number."
            );
    }

    static Results<Ok<ProjectSummary>, ProblemHttpResult> Make(LibraryHost host, SampleRequest request) =>
        LibraryTreeEndpoints.Guarded(
            host,
            library => {
                if (!Files.TryGetValue(request.Name, out var files)) {
                    throw new ProjectRefusedException($"Patina has no sample called '{request.Name}'.");
                }

                var project = NewProject(library, request);
                Copy(library, project.Path, files);
                host.RequestRescan();
                return TypedResults.Ok(project);
            }
        );

    /// <summary>A new project for the sample, named for it, with a number when that name is taken.</summary>
    static ProjectSummary NewProject(OpenLibrary library, SampleRequest request) {
        for (var copy = 1;; copy++) {
            var name = copy == 1 ? request.Name : $"{request.Name} {copy}";
            try {
                return library.CreateProject(new(name, request.Folder, ["sample"]));
            } catch (ProjectExistsException) when (copy < MostCopies) {
                // taken: try the next number
            }
        }
    }

    /// <summary>Writes the sample's files into the project, then lists its mesh and a document per material.</summary>
    static void Copy(OpenLibrary library, string project, ImmutableArray<string> files) {
        string? mesh = null;
        foreach (var file in files) {
            var name = Path.GetFileName(file);
            var target = library.ProjectFile(project, name);
            using (var stream = typeof(SampleEndpoints).Assembly.GetManifestResourceStream(file)!) {
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                LibraryFiles.Replace(target, memory.ToArray());
            }

            if (MeshFormats.IsMesh(name)) {
                mesh = target;
            }
        }

        var added = library.AddMesh(
            project,
            mesh ?? throw new InvalidOperationException("A sample has no mesh."),
            null
        );
        foreach (var file in files.Where(static file => file.EndsWith(StackExtension, StringComparison.Ordinal))) {
            var name = Path.GetFileNameWithoutExtension(file);
            var document = library.AddDocument(
                project,
                DocumentKinds.Texturing,
                Path.GetFileName(file),
                name,
                added.Id
            );
            library.SetDocumentMaterial(project, document.Id, name);
        }
    }

    /// <summary>The embedded samples. A resource's folder has the build system's slash.</summary>
    static ImmutableSortedDictionary<string, ImmutableArray<string>> Embedded() =>
        typeof(SampleEndpoints).Assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(Resource, StringComparison.Ordinal))
            .GroupBy(static name => name[Resource.Length..].Replace('\\', '/').Split('/')[0])
            .ToImmutableSortedDictionary(
                static group => group.Key,
                static group => group.Order(StringComparer.Ordinal).ToImmutableArray(),
                StringComparer.Ordinal
            );
}
