using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Jobs;
using Patina.Library;
using System.Net.WebSockets;
using System.Text.Json;

namespace Patina.Server.Api;

/// <summary>
///     Jobs: ask for a bake with paths, follow it through numbered events, stop it, and get paths back. Any client
///     can do
///     all of it (plan §8). Pixels are never in these replies; <c>/jobs/{id}/previews/{map}</c> serves a picture for a
///     client that wants to look at one, and the maps themselves are files.
/// </summary>
static class JobEndpoints {
    const string Tag = "Jobs";

    public static void MapJobApi(this IEndpointRouteBuilder api) {
        var jobs = api.MapGroup("/jobs").WithTags(Tag);
        const int Missing = StatusCodes.Status404NotFound;
        const int Refused = StatusCodes.Status422UnprocessableEntity;

        jobs.MapGet("/", static (JobQueue queue) => TypedResults.Ok(queue.Page()))
            .WithName("GetJobs")
            .WithSummary("Every job the Server remembers, newest first, and the sequence to follow from.");
        jobs.MapPost("/bakes", BakeAsync)
            .WithName("StartBake")
            .WithSummary("Queues a bake, or answers from the cache when the same bake has been made.")
            .ProducesProblem(Missing)
            .ProducesProblem(Refused);
        jobs.MapGet("/{id}", GetOne).WithName("GetJob").WithSummary("One job.").ProducesProblem(Missing);
        jobs.MapPost("/{id}/cancel", Cancel)
            .WithName("CancelJob")
            .WithSummary("Asks a job to stop. A running job stops at the end of its row of the atlas.")
            .ProducesProblem(Missing);
        jobs.MapGet("/events", GetEvents)
            .WithName("GetJobEvents")
            .WithSummary("The events after a number. /ws/jobs?since= sends the same events as they happen.");
        jobs.MapGet("/{id}/previews/{map}", GetPreview)
            .WithName("GetJobPreview")
            .WithSummary("An 8-bit PNG of one map of a finished bake.")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(Missing);
    }

    public static void MapJobSocket(this IEndpointRouteBuilder app) =>
        app.Map("/ws/jobs", FollowAsync).ExcludeFromDescription();

    static async Task<Results<Accepted<JobState>, ProblemHttpResult>> BakeAsync(
        BakeRequest request,
        JobQueue queue,
        BakeCache cache,
        CancellationToken cancellationToken
    ) {
        try {
            BakeInputs.Check(request);
            var key = (await Task.Run(() => BakeInputs.KeyOf(request, ToolCatalog.Default), cancellationToken)).Hex;
            var job = cache.Find(key) is { } manifest
                ? queue.Finished(
                    key,
                    request,
                    await Task.Run(() => cache.Deliver(key, manifest, request, true), cancellationToken)
                )
                : queue.Enqueue(key, request);
            return TypedResults.Accepted($"/api/v1/jobs/{job.Id}", job);
        } catch (FileNotFoundException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status404NotFound);
        } catch (BakeRefusedException e) {
            return TypedResults.Problem(e.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    static Results<Ok<JobState>, ProblemHttpResult> GetOne(string id, JobQueue queue) =>
        queue.Find(id) is { } job ? TypedResults.Ok(job) : NoJob(id);

    static Results<Ok<JobState>, ProblemHttpResult> Cancel(string id, JobQueue queue) =>
        queue.Cancel(id) is { } job ? TypedResults.Ok(job) : NoJob(id);

    static Ok<IReadOnlyList<JobEvent>> GetEvents(JobQueue queue, long after = 0) =>
        TypedResults.Ok(queue.Since(after) ?? []);

    static Results<PhysicalFileHttpResult, ProblemHttpResult> GetPreview(
        string id,
        string map,
        JobQueue queue,
        BakeCache cache
    ) =>
        queue.Find(id) is { } job && cache.Preview(job.Key, map) is { } path
            ? TypedResults.PhysicalFile(path, "image/png")
            : TypedResults.Problem($"Job '{id}' has no picture of '{map}'.", statusCode: StatusCodes.Status404NotFound);

    static ProblemHttpResult NoJob(string id) =>
        TypedResults.Problem($"There is no job '{id}'.", statusCode: StatusCodes.Status404NotFound);

    static async Task FollowAsync(HttpContext context, JobQueue queue, long? since = null) {
        if (!context.WebSockets.IsWebSocketRequest) {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var listening = WatchForCloseAsync(socket, stop);
        try {
            await SendEventsAsync(socket, queue, since ?? queue.Page().Sequence, stop.Token);
        } catch (WebSocketException) {
            // The client went away without a goodbye.
        } catch (OperationCanceledException) {
            // The client left, or the Server is stopping.
        } finally {
            await stop.CancelAsync();
            await listening;
        }
    }

    static async Task SendEventsAsync(
        WebSocket socket,
        JobQueue queue,
        long after,
        CancellationToken cancellationToken
    ) {
        while (socket.State == WebSocketState.Open) {
            var changed = queue.Changed(cancellationToken);
            foreach (var change in queue.Since(after) ?? []) {
                var message = JsonSerializer.SerializeToUtf8Bytes(change, ContractsJsonContext.Default.JobEvent);
                await socket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken);
                after = change.Sequence;
            }

            await changed;
        }
    }

    static async Task WatchForCloseAsync(WebSocket socket, CancellationTokenSource stop) {
        var buffer = new byte[256];
        try {
            while ((await socket.ReceiveAsync(buffer, stop.Token)).MessageType != WebSocketMessageType.Close) {
                // A client has nothing to say here.
            }
        } catch (Exception e) when (e is WebSocketException or OperationCanceledException) {
            // Gone, either way.
        }

        await stop.CancelAsync();
    }
}
