using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Stacks;
using Patina.Server.Viewport;

namespace Patina.Server.Api;

/// <summary>What a whole project shares, which can take a Worker's time when a mesh has no UV layout.</summary>
static class ProjectSettingsEndpoint {
    public static async Task<Results<Ok<ProjectContents>, ProblemHttpResult>> SetAsync(
        LibraryHost host,
        WorkerLink worker,
        StackSessions stacks,
        ViewportSession viewport,
        ProjectSettingsRequest request,
        CancellationToken cancellationToken
    ) {
        if (host.Library is not { } library) {
            return LibraryTreeEndpoints.NoLibrary();
        }

        if (request.Unwrap?.Problem() is { } wrong) {
            return TypedResults.Problem(wrong, statusCode: StatusCodes.Status400BadRequest);
        }

        var kept = request.SameLayout == true;
        var project = request.Project;
        try {
            if (kept && await MovesAsync(library, worker, request, cancellationToken) is { } refused) {
                return refused;
            }

            var options = library.Contents(project).Unwrap;
            library.SetMeshHandling(project, request.AutoUnwrap, request.PreservePaint, request.Unwrap);

            var before = library.Contents(project);
            var onDocument = before.Documents.FirstOrDefault(static d => d.Mesh is not null)?.Mesh;
            var current = before.Meshes.FirstOrDefault(m => m.Id == onDocument);

            string? Held(string? below) =>
                below is null ? null : library.ProjectFile(project, LibraryTreeEndpoints.Relative(project, below));

            var again = Again(request, before, options, current);
            var mesh = request.Mesh ?? (again is null ? Held(current?.Path) : library.ProjectFile(project, again));
            if (mesh is null || Idle(library, request, again, mesh)) {
                return TypedResults.Ok(before);
            }

            // ⚠ Bringing a file of the same name overwrites the old mesh, and the paint is still on its layout. On the
            // same layout nothing moves, and open documents could not have paint moved under them.
            var moving = !kept && (request.Mesh is not null || again is not null) && current is not null
                ? Moving(library, stacks, project, before, Held(current.Path)!)
                : null;
            var added = library.AddMesh(project, mesh, request.High ?? Held(current?.High));
            if (before.AutoUnwrap) {
                added = await UnwrappedAsync(library, worker, project, added, before.Unwrap, cancellationToken);
            }

            await PutOnAsync(library, worker, stacks, project, added, moving, request.Reassign, cancellationToken);
            // The viewport reads a mesh rewritten in place again when it next draws.
            viewport.Redraw();
            host.RequestRescan();
            return TypedResults.Ok(library.Contents(project));
        } catch (IpcException e) {
            return LibraryTreeEndpoints.Problem(e, StatusCodes.Status422UnprocessableEntity);
        } catch (Exception e) when (LibraryTreeEndpoints.Refusal(e) is { } problem) {
            return problem;
        }
    }

    /// <summary>
    ///     The file Auto Unwrap laid the current mesh out from, when new options mean laying it out again, or null.
    /// </summary>
    static string? Again(
        ProjectSettingsRequest request,
        ProjectContents before,
        UnwrapOptions options,
        ProjectMeshInfo? current
    ) =>
        before.AutoUnwrap && request.Mesh is null && before.Unwrap != options ? current?.Unwrapped?.From : null;

    /// <summary>
    ///     Why a reload asked to move nothing is refused before anything changes, or null when nothing on the mesh
    ///     moves (#89).
    /// </summary>
    static async Task<ProblemHttpResult?> MovesAsync(
        OpenLibrary library,
        WorkerLink worker,
        ProjectSettingsRequest request,
        CancellationToken cancellationToken
    ) {
        if (request.Mesh is not { } mesh) {
            return Refusals.Problem("Name the mesh to reload.", StatusCodes.Status400BadRequest, Refusals.MeshUnnamed);
        }

        return await MeshReloadCheck.WhyItMovesAsync(
            library,
            worker,
            request.Project,
            mesh,
            cancellationToken
        ) is { } why
            ? Refusals.Problem(
                why,
                StatusCodes.Status409Conflict,
                Refusals.MeshLayoutChanged,
                new Dictionary<string, string> { ["reason"] = why }
            )
            : null;
    }

    /// <summary>
    ///     A copy of the mesh the paint is on, when Preserve Paint is to move it, or null. ⚠ Refused before the mesh is
    ///     overwritten while a document is open: refused after, the project stood on the new mesh with its paint on
    ///     the old layout.
    /// </summary>
    /// <exception cref="ProjectRefusedException">A texturing document of the project is open.</exception>
    static string? Moving(
        OpenLibrary library,
        StackSessions stacks,
        string project,
        ProjectContents before,
        string held
    ) {
        if (!before.PreservePaint) {
            return null;
        }

        if (OpenOn(library, stacks, project, before) is { } open) {
            throw new ProjectRefusedException($"Close '{open}' first: its paint is about to move.");
        }

        return KeepOld(held);
    }

    /// <summary>The name of a texturing document of the project that is open, or null when none is.</summary>
    static string? OpenOn(OpenLibrary library, StackSessions stacks, string project, ProjectContents contents) {
        HashSet<string> open = [..stacks.All.Select(static session => session.Path)];
        return contents.Documents.FirstOrDefault(d =>
                d.Kind == DocumentKinds.Texturing
                && open.Contains(library.ProjectFile(project, LibraryTreeEndpoints.Relative(project, d.Path)))
            )
            ?.Name;
    }

    /// <summary>Whether a request has nothing to do to the mesh.</summary>
    /// <remarks>⚠ Nothing can improve on the same file, and all of it costs: the paint would be resampled.</remarks>
    static bool Idle(OpenLibrary library, ProjectSettingsRequest request, string? again, string mesh) {
        if (request.High is not null || request.AutoUnwrap is not null || again is not null) {
            return false;
        }

        return request.Mesh is null || !library.MeshChanged(request.Project, mesh);
    }

    /// <summary>Puts every texturing document on a mesh, makes its texture sets and moves the paint across.</summary>
    static async Task PutOnAsync(
        OpenLibrary library,
        WorkerLink worker,
        StackSessions stacks,
        string project,
        ProjectMesh added,
        string? moving,
        IReadOnlyDictionary<string, string>? reassign,
        CancellationToken cancellationToken
    ) {
        HashSet<string> open = [..stacks.All.Select(static session => session.Path)];
        var documents = library.UseMesh(project, added.Id);
        // Which material each set was on, for paint that moves from a material the reload gave another name.
        var was = documents.Where(static d => d.Material is not null)
            .ToDictionary(static d => d.Id, static d => d.Material!);
        string[] files = [..documents.Select(document => library.ProjectFile(project, document.File))];
        if (!files.Any(open.Contains)) {
            await MakeSetsAsync(library, worker, project, added, documents, reassign, cancellationToken);
            documents = library.UseMesh(project, added.Id);
            files = [..documents.Select(document => library.ProjectFile(project, document.File))];
        }

        foreach (var file in files.Where(file => !open.Contains(file))) {
            StackDocuments.SetModel(file, added.File);
        }

        if (moving is null) {
            return;
        }

        try {
            await MovePaintAsync(
                library,
                worker,
                project,
                moving,
                added,
                [..documents.Select(d => (d, was.TryGetValue(d.Id, out var old) && old != d.Material ? old : null))],
                open,
                cancellationToken
            );
        } finally {
            File.Delete(moving);
        }
    }

    /// <summary>
    ///     Gives each material of the mesh a document of its own, which is its texture set: its own layers, maps and
    ///     undo. A mesh with one material needs nothing, and a document already on a material keeps it.
    /// </summary>
    static async Task MakeSetsAsync(
        OpenLibrary library,
        WorkerLink worker,
        string project,
        ProjectMesh mesh,
        IReadOnlyList<ProjectItem> documents,
        IReadOnlyDictionary<string, string>? reassign,
        CancellationToken cancellationToken
    ) {
        var connection = await worker.ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.MeshMaterials, MeshFile = library.ProjectFile(project, mesh.File) },
            cancellationToken
        );
        var materials = reply.Materials ?? [];
        if (materials.Count < 2 || documents.Count == 0) {
            return;
        }

        documents = Reassigned(library, project, mesh, documents, materials, reassign);

        var side = StackDocuments.Side(library.ProjectFile(project, documents[0].File));
        var waiting = new Queue<string>(materials.Except(documents.Select(static d => d.Material ?? string.Empty)));
        foreach (var document in documents.Where(static d => d.Material is null).Where(_ => waiting.Count > 0)) {
            Claim(library, project, document, waiting.Dequeue());
        }

        foreach (var material in waiting) {
            LibraryTreeEndpoints.MakeDocument(
                library,
                new(project, DocumentKinds.Texturing, material, mesh.Id.Value, side, null)
            );
            var made = library.UseMesh(project, mesh.Id).Last(static d => d.Material is null);
            Claim(library, project, made, material);
        }
    }

    /// <summary>
    ///     Puts each set on a vanished material onto the new one it was sent to, when no other set holds that one, and
    ///     moves its bake folder with it, since a set's bake is found by its material's name.
    /// </summary>
    static IReadOnlyList<ProjectItem> Reassigned(
        OpenLibrary library,
        string project,
        ProjectMesh mesh,
        IReadOnlyList<ProjectItem> documents,
        IReadOnlyList<string> materials,
        IReadOnlyDictionary<string, string>? reassign
    ) {
        if (reassign is not { Count: > 0 }) {
            return documents;
        }

        HashSet<string> held = [..documents.Select(static d => d.Material).OfType<string>()];
        var bakes = library.ProjectFile(project, "bakes/maps");
        foreach (var document in documents) {
            if (document.Material is not { } old
                || materials.Contains(old)
                || !reassign.TryGetValue(old, out var chosen)
                || !materials.Contains(chosen)
                || !held.Add(chosen)) {
                continue;
            }

            held.Remove(old);
            Claim(library, project, document, chosen);
            var (from, to) = (Path.Combine(bakes, old), Path.Combine(bakes, chosen));
            if (Directory.Exists(from) && !Directory.Exists(to)) {
                Directory.Move(from, to);
            }
        }

        return library.UseMesh(project, mesh.Id);
    }

    static void Claim(OpenLibrary library, string project, ProjectItem document, string material) {
        library.SetDocumentMaterial(project, document.Id, material);
        StackDocuments.SetSetName(library.ProjectFile(project, document.File), material);
    }

    static string KeepOld(string mesh) {
        var kept = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{Path.GetExtension(mesh)}");
        File.Copy(mesh, kept);
        return kept;
    }

    /// <summary>Puts each document's paint on the new layout. The Worker keeps a copy of what it moves.</summary>
    static async Task MovePaintAsync(
        OpenLibrary library,
        WorkerLink worker,
        string project,
        string from,
        ProjectMesh to,
        IReadOnlyList<(ProjectItem Document, string? Was)> documents,
        HashSet<string> open,
        CancellationToken cancellationToken
    ) {
        var backup = library.ProjectFile(
            project,
            $"{LibraryLayout.PaintBackupFolder}/{DateTime.UtcNow:yyyyMMdd-HHmmss}"
        );
        var connection = await worker.ConnectionAsync(cancellationToken);
        foreach (var (document, was) in documents) {
            var stack = library.ProjectFile(project, document.File);
            if (open.Contains(stack)) {
                throw new ProjectRefusedException($"Close '{document.Name}' first: its paint is about to move.");
            }

            string[] paints = [
                ..StackInputs.PaintsOf(document.File, SharedFile.ReadAllText(stack))
                    .Select(paint => library.ProjectFile(project, paint))
                    .Where(File.Exists)
            ];
            if (paints.Length == 0) {
                continue;
            }

            await connection.SendAsync(
                new() {
                    Type = IpcTypes.MeshMovePaint,
                    MeshMovePaint = new(
                        from,
                        library.ProjectFile(project, to.File),
                        paints,
                        document.Material ?? string.Empty,
                        backup,
                        StackDocuments.Parse(SharedFile.ReadAllText(stack)).Sets.FirstOrDefault()?.UvSet ?? 0,
                        was
                    )
                },
                cancellationToken
            );
        }
    }

    /// <summary>The mesh, or a new one beside it if it had no UV layout. What it stood on stays as it was.</summary>
    static async Task<ProjectMesh> UnwrappedAsync(
        OpenLibrary library,
        WorkerLink worker,
        string project,
        ProjectMesh mesh,
        UnwrapOptions options,
        CancellationToken cancellationToken
    ) {
        var source = library.ProjectFile(project, mesh.File);
        var output = library.ProjectFile(project, $"{Path.GetFileNameWithoutExtension(mesh.File)}_unwrapped.glb");
        var connection = await worker.ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(
            new() { Type = IpcTypes.MeshUnwrap, MeshUnwrap = new(source, output, options) },
            cancellationToken
        );
        var high = mesh.High is null ? null : library.ProjectFile(project, mesh.High);
        return reply.MeshUnwrapped is { } sets
            ? library.AddMesh(project, output, high, library.SourceOf(mesh), new(mesh.File, sets))
            : mesh;
    }
}
