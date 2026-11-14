using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Stacks;
using System.Security.Cryptography;
using System.Text;

namespace Patina.Server.Api;

/// <summary>
///     What an open stack's mesh says, which the Worker reads from the file: its named parts, a picture of where
///     some of them lie, and its UV layout as lines for a 2D view (#137), drawn into the preview folder so that the
///     socket carries a hash and not the numbers.
/// </summary>
static class StackMeshEndpoints {
    const int Missing = StatusCodes.Status404NotFound;
    const int Refused = StatusCodes.Status422UnprocessableEntity;
    const int Unavailable = StatusCodes.Status503ServiceUnavailable;

    public static void MapStackMesh(this RouteGroupBuilder stacks) {
        stacks.MapGet("/{id}/mesh-parts", GetMeshParts)
            .Describe(
                "GetStackMeshParts",
                "The named meshes in the stack's texture set, which a layer can be limited to.",
                Missing,
                Refused,
                Unavailable
            );
        stacks.MapPost("/{id}/mesh-mask", DrawMeshMask)
            .Describe(
                "DrawStackMeshMask",
                "Draws where some named meshes lie in the layout, as a picture for a setLayer command's meshMask.",
                Missing,
                Refused,
                Unavailable
            );
        stacks.MapGet("/{id}/uv-wire", GetUvWire)
            .Produces<string>(StatusCodes.Status200OK, "image/svg+xml")
            .Describe(
                "GetStackUvWire",
                "The texture set's UV layout as lines, each edge of its triangles once, as an SVG over the unit square "
                + "with v down, as the map lies: what a 2D view lays over the map. A stack on no mesh has none.",
                Missing,
                Refused,
                Unavailable
            );
    }

    /// <summary>The set's layout as lines, drawn by the Worker into the preview folder and sent from there.</summary>
    static async Task<Results<PhysicalFileHttpResult, ProblemHttpResult>> GetUvWire(
        string id,
        StackSessions sessions,
        WorkerLink worker,
        PreviewStore store,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        if (session.ModelFile is not { } mesh) {
            return Refusals.Problem("This document is on no mesh, so it has no UV layout.", Missing, Refusals.NoMesh);
        }

        var (reply, problem) = await AskAboutMeshAsync(
            worker,
            new() { Type = IpcTypes.MeshUvWire, UvWire = new(mesh, session.Set, session.UvSet, store.Root) },
            cancellationToken
        );
        if (problem is not null) {
            return problem;
        }

        return reply?.ThumbnailHash is { } hash && store.Find(hash, GraphPreviewFiles.Drawing) is { } file
            ? TypedResults.PhysicalFile(file, "image/svg+xml")
            : Refusals.Problem("The Worker drew no layout.", Unavailable, Refusals.UvLayoutUndrawn);
    }

    /// <summary>
    ///     A question the Worker answers about a stack's mesh from the file alone: a mesh that would not read is
    ///     refused, and a Worker that is missing or went is unavailable.
    /// </summary>
    static async Task<(IpcMessage? Reply, ProblemHttpResult? Problem)> AskAboutMeshAsync(
        WorkerLink worker,
        IpcMessage question,
        CancellationToken cancellationToken
    ) {
        try {
            var connection = await worker.ConnectionAsync(cancellationToken);
            return (await connection.SendAsync(question, cancellationToken), null);
        } catch (IpcException e) {
            return (null, Refusals.OfEngine(e, Refused));
        } catch (Exception e) when (e is IOException or TimeoutException) {
            return (null, Refusals.OfEngine(e, Unavailable));
        }
    }

    static async Task<Results<Ok<StackMeshParts>, ProblemHttpResult>> GetMeshParts(
        string id,
        StackSessions sessions,
        WorkerLink worker,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        return await MeshPartsAsync(session, worker, null, cancellationToken);
    }

    static async Task<Results<Ok<StackMeshParts>, ProblemHttpResult>> DrawMeshMask(
        string id,
        StackMeshMaskRequest request,
        StackSessions sessions,
        WorkerLink worker,
        ServerIdentity identity,
        CancellationToken cancellationToken
    ) {
        if (sessions.Find(id) is not { } session) {
            return StackEndpoints.NoSession(id);
        }

        if (session.ModelFile is not { } mesh || identity.Library is not { } library) {
            return Refusals.Problem("This document is on no mesh.", Refused, Refusals.NoMesh);
        }

        var side = session.State.Width;
        var name = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes($"{side}\n{string.Join('\n', request.Parts.Order(StringComparer.Ordinal))}")
            )
        )[..16];
        var file = Path.Combine(
            Path.GetDirectoryName(session.Path)!,
            "pictures",
            $"{LibraryLayout.MeshMaskPrefix}{name}.png"
        );
        var (_, refused) = await AskAboutMeshAsync(
            worker,
            new() { Type = IpcTypes.MeshPartMask, MeshPartMask = new(mesh, request.Parts, side, file) },
            cancellationToken
        );
        if (refused is not null) {
            return refused;
        }

        return await MeshPartsAsync(
            session,
            worker,
            Path.GetRelativePath(library, file).Replace('\\', '/'),
            cancellationToken
        );
    }

    static async Task<Results<Ok<StackMeshParts>, ProblemHttpResult>> MeshPartsAsync(
        StackSession session,
        WorkerLink worker,
        string? asset,
        CancellationToken cancellationToken
    ) {
        if (session.ModelFile is not { } mesh) {
            return TypedResults.Ok(new StackMeshParts([], asset));
        }

        var (reply, problem) = await AskAboutMeshAsync(
            worker,
            new() { Type = IpcTypes.MeshParts, MeshFile = mesh, MeshSet = session.Set },
            cancellationToken
        );
        return problem is null ? TypedResults.Ok(new StackMeshParts(reply?.Parts ?? [], asset)) : problem;
    }
}
