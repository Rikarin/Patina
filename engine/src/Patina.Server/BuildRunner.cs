using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Ipc;
using Patina.Jobs;
using Patina.Library;

namespace Patina.Server;

/// <summary>
///     Runs a build's job on the Server, one step at a time through the Worker. The plan, the keys, the delivery and
///     the reports are here, and every pixel is the Worker's. A lost Worker puts the job back in the queue, and running it
///     again plans again, so a finished step is simply no longer in the plan.
/// </summary>
sealed class BuildRunner(LibraryHost libraries, ToolCatalog tools) {
    readonly Lock waiting = new();
    Step? current;

    /// <summary>Takes what the Worker says about a step.</summary>
    public void Hear(BuildEnding ending) {
        lock (waiting) {
            if (current is { } step && step.Ticket == ending.Ticket) {
                current = null;
                step.Ended.TrySetResult(ending);
            }
        }
    }

    /// <summary>The folder a build's reports are in.</summary>
    public static string FolderOf(string library, string job) => Path.Combine(LibraryLayout.ReportsPath(library), job);

    /// <summary>Runs a job's build until it ends or its Worker does.</summary>
    /// <returns>The report.</returns>
    /// <exception cref="BuildExecutorLostException">The Worker went away. The job should go back in the queue.</exception>
    public Task<LibraryReport> RunAsync(
        JobState job,
        IpcConnection connection,
        JobQueue queue,
        Func<Task> yield,
        CancellationToken cancellationToken
    ) {
        var library = libraries.Library ?? throw new BuildRefusedException("This Server has no library.");
        var ticket = new BakeTicket(job.Id, job.Attempts);
        var pipeline = new BuildPipeline(library, new Executor(this, connection, ticket), tools);
        var journal = new BuildJournal(FolderOf(library.RootPath, job.Id), job.Id, job.Created);
        return Task.Run(
            () => pipeline.RunAsync(
                journal,
                job.Build!,
                (fraction, report) => queue.Report(job.Id, job.Attempts, fraction, report),
                yield,
                TimeProvider.System,
                cancellationToken
            ),
            cancellationToken
        );
    }

    async Task<BuildEnding> SendAsync(
        IpcConnection connection,
        IpcMessage message,
        BakeTicket ticket,
        CancellationToken cancellationToken
    ) {
        var step = new Step(ticket, new(TaskCreationOptions.RunContinuationsAsynchronously));
        lock (waiting) {
            current = step;
        }

        try {
            await connection.SendAsync(message, cancellationToken);
        } catch (Exception e) when (e is IOException or TimeoutException) {
            throw new BuildExecutorLostException(e.Message, e);
        }

        try {
            var finished = await Task.WhenAny(step.Ended.Task, connection.Closed).WaitAsync(cancellationToken);
            if (finished != step.Ended.Task) {
                throw new BuildExecutorLostException("The Worker went away during this step.");
            }
        } catch (OperationCanceledException) {
            await CancelAsync(connection, ticket);
            await step.Ended.Task.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
            throw;
        }

        return await step.Ended.Task;
    }

    static async Task CancelAsync(IpcConnection connection, BakeTicket ticket) {
        try {
            await connection.SendAsync(new() { Type = IpcTypes.BuildCancel, BakeCancel = ticket }, CancellationToken.None);
        } catch (Exception e) when (e is IOException or TimeoutException or IpcException) {
            // Its Worker has gone, and the step with it.
        }
    }

    sealed record Step(BakeTicket Ticket, TaskCompletionSource<BuildEnding> Ended);

    sealed class Executor(BuildRunner runner, IpcConnection connection, BakeTicket ticket) : IBuildExecutor {
        public async Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) {
            var ending = await Run(new(ticket, work, null), cancellationToken);
            return ending.Output ?? throw Refused(ending);
        }

        public async Task<ExportManifest> ExportAsync(ExportStep work, CancellationToken cancellationToken) {
            var ending = await Run(new(ticket, null, work), cancellationToken);
            return ending.Export ?? throw Refused(ending);
        }

        Task<BuildEnding> Run(BuildStart start, CancellationToken cancellationToken) =>
            runner.SendAsync(
                connection,
                new() { Type = IpcTypes.BuildStart, BuildStart = start },
                ticket,
                cancellationToken
            );

        static BuildStepFailedException Refused(BuildEnding ending) =>
            ending.Error is { } error ? new(error) : new("The step was stopped.");
    }
}
