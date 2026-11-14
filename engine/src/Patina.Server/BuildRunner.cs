using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Ipc;
using Patina.Jobs;
using Patina.Library;

namespace Patina.Server;

/// <summary>
///     Runs a build's job on the Server, one step at a time through the Worker. The plan, the keys, the delivery and
///     the reports are here, and every pixel is the Worker's. A lost Worker puts the job back in the queue, and running
///     it again plans again, so a finished step is simply no longer in the plan.
/// </summary>
public sealed class BuildRunner(LibraryHost libraries, ToolCatalog tools, IMeshSetReads meshes) {
    static readonly TimeSpan StopPatience = TimeSpan.FromSeconds(30);

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

    /// <summary>Takes how far the Worker says a step is.</summary>
    public void Hear(BuildProgressNote note) {
        Step? step;
        lock (waiting) {
            step = current;
        }

        if (step is not null && step.Ticket == note.Ticket) {
            step.Progress?.Invoke(note.Fraction, note.File);
        }
    }

    /// <summary>The folder a build's reports are in.</summary>
    public static string FolderOf(string library, string job) => Path.Combine(LibraryLayout.ReportsPath(library), job);

    /// <summary>Runs a job's build until it ends or its Worker does.</summary>
    /// <returns>The report.</returns>
    /// <exception cref="BuildExecutorLostException">
    ///     The Worker went away. The job should go back in the
    ///     queue.
    /// </exception>
    public Task<LibraryReport> RunAsync(
        JobState job,
        IpcConnection connection,
        JobQueue queue,
        Func<Task> yield,
        CancellationToken cancellationToken
    ) {
        var library = libraries.Library ?? throw new BuildRefusedException("The Server was started with no library.");
        var ticket = new BakeTicket(job.Id, job.Attempts);
        var pipeline = new BuildPipeline(library, new Executor(this, connection, ticket), tools, meshes);
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
        Action<double, string>? progress,
        CancellationToken cancellationToken
    ) {
        var step = new Step(ticket, new(TaskCreationOptions.RunContinuationsAsynchronously), progress);
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
            try {
                await step.Ended.Task.WaitAsync(StopPatience, CancellationToken.None);
            } catch (TimeoutException e) {
                // A step that ignores being stopped is the Worker's to answer for, and the job stops regardless.
                throw new BuildExecutorLostException(e.Message, e);
            }

            throw;
        }

        return await step.Ended.Task;
    }

    static async Task CancelAsync(IpcConnection connection, BakeTicket ticket) {
        try {
            await connection.SendAsync(
                new() { Type = IpcTypes.BuildCancel, BakeCancel = ticket },
                CancellationToken.None
            );
        } catch (Exception e) when (e is IOException or TimeoutException or IpcException) {
            // Its Worker has gone, and the step with it.
        }
    }

    sealed record Step(
        BakeTicket Ticket,
        TaskCompletionSource<BuildEnding> Ended,
        Action<double, string>? Progress
    );

    sealed class Executor(BuildRunner runner, IpcConnection connection, BakeTicket ticket) : IBuildExecutor {
        public async Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) {
            var ending = await Run(new(ticket, work, null), null, cancellationToken);
            return ending.Output ?? throw Refused(ending);
        }

        public async Task<ExportManifest> ExportAsync(
            ExportStep work,
            Action<double, string>? progress,
            CancellationToken cancellationToken
        ) {
            var ending = await Run(new(ticket, null, work), progress, cancellationToken);
            return ending.Export ?? throw Refused(ending);
        }

        Task<BuildEnding> Run(
            BuildStart start,
            Action<double, string>? progress,
            CancellationToken cancellationToken
        ) =>
            runner.SendAsync(
                connection,
                new() { Type = IpcTypes.BuildStart, BuildStart = start },
                ticket,
                progress,
                cancellationToken
            );

        static BuildStepFailedException Refused(BuildEnding ending) =>
            ending.Error is { } error ? new(error) : new("The step was stopped.");
    }
}
