using Patina.Contracts;
using Patina.Ipc;
using Patina.Jobs;
using Patina.Library;

namespace Patina.Server;

/// <summary>
///     Hands jobs to the Worker, one at a time, and keeps the queue true about them. The Worker's connection closing is
///     what re-queues a job: nothing it held matters, since a job is rebuilt from what is on file here. Nothing in this
///     loop waits on a bake's reply. A bake answers at once and reports on the side, so the IPC timeout that counts a
///     silent Worker as lost never applies to one (ADR 0008).
/// </summary>
public sealed class JobRunner : BackgroundService {
    readonly JobQueue queue;
    readonly BakeCache cache;
    readonly WorkerLink worker;
    readonly ILogger<JobRunner> logger;

    public JobRunner(JobQueue queue, BakeCache cache, WorkerLink worker, ILogger<JobRunner> logger) {
        this.queue = queue;
        this.cache = cache;
        this.worker = worker;
        this.logger = logger;
        worker.Reported += Hear;
    }

    public override void Dispose() {
        worker.Reported -= Hear;
        base.Dispose();
    }

    /// <summary>The folders a Server keeps its jobs and bakes in.</summary>
    public static (string Jobs, string Bakes) Folders(ServerIdentity identity) {
        if (identity.Library is { } library) {
            return (LibraryLayout.JobsPath(library), LibraryLayout.BakesPath(library));
        }

        var scratch = Path.Combine(Path.GetTempPath(), $"patina-jobs-{Environment.ProcessId}");
        return (Path.Combine(scratch, "jobs"), Path.Combine(scratch, "bakes"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            while (true) {
                var changed = queue.Changed(stoppingToken);
                if (queue.Page().Jobs.All(static job => job.State != JobStates.Queued)) {
                    await changed;
                    continue;
                }

                var connection = await worker.ConnectionAsync(stoppingToken);
                if (queue.TakeNext() is { } job) {
                    await RunAsync(connection, job, stoppingToken);
                }
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Stopping: a running job goes back in the queue when the queue is next opened.
        }
    }

    /// <summary>Starts one job and stays with it until it is over or its Worker is.</summary>
    async Task RunAsync(IpcConnection connection, JobState job, CancellationToken cancellationToken) {
        var ticket = new BakeTicket(job.Id, job.Attempts);
        try {
            await connection.SendAsync(
                new() { Type = IpcTypes.BakeStart, BakeStart = new(ticket, job.Bake, cache.FolderOf(job.Key)) },
                cancellationToken
            );
        } catch (IpcException e) {
            queue.Fail(job.Id, job.Attempts, e.Message);
            return;
        } catch (Exception e) when (e is IOException or TimeoutException) {
            ServerLog.JobWorkerLost(logger, job.Id, job.Attempts);
            queue.Requeue(job.Id, job.Attempts);
            return;
        }

        var cancelling = false;
        while (queue.Find(job.Id) is { State: JobStates.Running } current && current.Attempts == job.Attempts) {
            if (current.CancelRequested && !cancelling) {
                cancelling = true;
                await CancelAsync(connection, ticket, cancellationToken);
            }

            var changed = queue.Changed(cancellationToken);
            if (await Task.WhenAny(changed, connection.Closed) == connection.Closed) {
                ServerLog.JobWorkerLost(logger, job.Id, job.Attempts);
                queue.Requeue(job.Id, job.Attempts);
                return;
            }

            await changed;
        }
    }

    static async Task CancelAsync(IpcConnection connection, BakeTicket ticket, CancellationToken cancellationToken) {
        try {
            await connection.SendAsync(new() { Type = IpcTypes.BakeCancel, BakeCancel = ticket }, cancellationToken);
        } catch (Exception e) when (e is IOException or TimeoutException or IpcException) {
            // Its Worker has gone, which the loop finds out for itself.
        }
    }

    /// <summary>
    ///     What the Worker says about a bake, on the link's own loop, so nothing here may wait on the link.
    /// </summary>
    void Hear(IpcMessage message) {
        if (message is { Type: IpcTypes.BakeProgress, BakeProgress: { } progress }) {
            queue.Report(progress.Ticket.Job, progress.Ticket.Attempt, progress.Fraction);
        } else if (message is { Type: IpcTypes.BakeEnded, BakeEnded: { } ending }) {
            var (id, attempt) = ending.Ticket;
            if (ending.Error is { } error) {
                queue.Fail(id, attempt, error);
            } else if (ending.Manifest is { } manifest) {
                _ = Task.Run(() => Deliver(id, attempt, manifest));
            } else {
                queue.Stopped(id, attempt);
            }
        }
    }

    /// <summary>
    ///     Puts the maps where the request wanted them. Off the link's loop, since the files can be large.
    /// </summary>
    void Deliver(string id, int attempt, BakeManifest manifest) {
        if (queue.Find(id) is not { } job) {
            return;
        }

        try {
            queue.Complete(id, attempt, cache.Deliver(job.Key, manifest, job.Bake, false));
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            queue.Fail(id, attempt, e.Message);
        }
    }
}
