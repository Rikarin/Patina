using Patina.Contracts;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Patina.Jobs;

/// <summary>
///     The Server's job queue. Each job is a file, so the queue survives the Server, and a job in the Worker's hands
///     is only ever a job that can be run again, so it survives the Worker. Everything here is synchronous and fast:
///     reporting is the caller's business, and <see cref="Changed" /> says when to look.
/// </summary>
/// <remarks>
///     A job that keeps killing its Worker must not keep the machine in a crash loop, so a job is given up on after
///     <see cref="MaxAttempts" /> Workers have died on it (a build counts per step). A finished job stays as a record
///     of what was made, up to
///     <see cref="Remembered" /> of them.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711",
    Justification = "A queue of jobs is what it is, and no collection is mistaken for it."
)]
public sealed class JobQueue {
    /// <summary>How many Workers a job may be started on before it is given up on.</summary>
    public const int MaxAttempts = 3;

    /// <summary>How many finished jobs are kept.</summary>
    const int Remembered = 200;

    /// <summary>How many events a client that dropped its socket can catch up on.</summary>
    const int LogCapacity = 1024;

    readonly Lock changing = new();
    readonly string folder;
    readonly TimeProvider clock;
    readonly Dictionary<string, JobState> jobs = [];
    readonly Queue<JobEvent> log = new();
    TaskCompletionSource next = NewSignal();
    long sequence;

    /// <summary>Opens the queue in a folder, picking up whatever was left there.</summary>
    /// <param name="folder">Where the jobs live.</param>
    /// <param name="clock">What says when a job was asked for, which is the queue's order.</param>
    public JobQueue(string folder, TimeProvider? clock = null) {
        this.folder = folder;
        this.clock = clock ?? TimeProvider.System;
        Directory.CreateDirectory(folder);

        foreach (var file in Directory.EnumerateFiles(folder, "*.json")) {
            if (Read(file) is not { } job) {
                continue;
            }

            // Whatever had this job is gone or will be told nothing. Its work can be run again, and that is all that
            // was ever in its hands.
            if (job.State == JobStates.Running) {
                job = AfterLoss(job);
                Write(job);
            }

            jobs[job.Id] = job;
        }
    }

    /// <summary>Every job, newest first, and the sequence to follow from.</summary>
    public JobPage Page() {
        lock (changing) {
            return new(
                [..jobs.Values.OrderByDescending(static job => job.Created).ThenBy(static job => job.Id)],
                sequence
            );
        }
    }

    /// <summary>A job, or null.</summary>
    public JobState? Find(string id) {
        lock (changing) {
            return jobs.GetValueOrDefault(id);
        }
    }

    /// <summary>The events after a number, or null when the log no longer reaches back that far.</summary>
    public IReadOnlyList<JobEvent>? Since(long after) {
        lock (changing) {
            if (after >= sequence) {
                return [];
            }

            return log.TryPeek(out var oldest) && oldest.Sequence <= after + 1
                ? [..log.Where(e => e.Sequence > after)]
                : sequence == 0
                    ? []
                    : null;
        }
    }

    /// <summary>Completes when something has changed since this was called.</summary>
    public Task Changed(CancellationToken cancellationToken) {
        lock (changing) {
            return next.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>Takes a new job.</summary>
    public JobState Enqueue(string key, BakeRequest bake) =>
        Put(New(key) with { State = JobStates.Queued, Bake = bake });

    /// <summary>Takes a new build.</summary>
    public JobState Enqueue(BuildRequest build) => Put(New("build") with { State = JobStates.Queued, Build = build });

    /// <summary>Records a job that needed no running, because its result already exists.</summary>
    public JobState Finished(string key, BakeRequest bake, BakeResult result) =>
        Put(New(key) with { State = JobStates.Done, Progress = 1, Bake = bake, Result = result });

    /// <summary>
    ///     Hands out the oldest queued job, or null when there is none. Interactive work goes first (plan §5): a bake
    ///     someone is waiting for passes any build, however early the build was asked for.
    /// </summary>
    public JobState? TakeNext() => Take(false);

    /// <summary>Hands out the oldest queued bake, which a running build lets by between its steps.</summary>
    public JobState? TakeInteractive() => Take(true);

    JobState? Take(bool interactiveOnly) {
        lock (changing) {
            var job = jobs.Values
                .Where(job => job.State == JobStates.Queued && (!interactiveOnly || job.Build is null))
                .OrderBy(static job => job.Build is null ? 0 : 1)
                .ThenBy(static job => job.Created)
                .ThenBy(static job => job.Id)
                .FirstOrDefault();
            return job is null
                ? null
                : Put(job with { State = JobStates.Running, Attempts = job.Attempts + 1, Progress = 0 });
        }
    }

    /// <summary>Records how far a running job is. A report for anything else is one that arrived too late.</summary>
    public void Report(string id, int attempt, double progress) {
        lock (changing) {
            if (Current(id, attempt) is { } job) {
                Put(job with { Progress = Math.Clamp(progress, job.Progress, 1) }, false);
            }
        }
    }

    /// <summary>Records how a running build stands, for whoever is following it.</summary>
    public void Report(string id, int attempt, double progress, LibraryReport report) {
        lock (changing) {
            if (Current(id, attempt) is { } job) {
                Put(job with { Progress = Math.Clamp(progress, job.Progress, 1), Report = report }, false);
            }
        }
    }

    /// <summary>Records that a build finished.</summary>
    public void Complete(string id, int attempt, LibraryReport report) {
        lock (changing) {
            if (Current(id, attempt) is { } job) {
                var failed = report.Failed > 0 ? $"{report.Failed} of this build's steps failed." : null;
                Put(job with { State = JobStates.Done, Progress = 1, Report = report, Error = failed });
                Forget();
            }
        }
    }

    /// <summary>Records a job's result.</summary>
    public void Complete(string id, int attempt, BakeResult result) {
        lock (changing) {
            if (Current(id, attempt) is { } job) {
                Put(job with { State = JobStates.Done, Progress = 1, Result = result });
                Forget();
            }
        }
    }

    /// <summary>Records that a job cannot be done.</summary>
    public void Fail(string id, int attempt, string error) {
        lock (changing) {
            if (Current(id, attempt) is { } job) {
                Put(job with { State = JobStates.Failed, Error = error });
                Forget();
            }
        }
    }

    /// <summary>Records that a running job stopped because it was asked to.</summary>
    public void Stopped(string id, int attempt) {
        lock (changing) {
            if (Current(id, attempt) is { } job) {
                Put(job with { State = JobStates.Cancelled });
            }
        }
    }

    /// <summary>
    ///     Puts a job back because its Worker went away, which fails it once too many Workers have, and stops it if
    ///     someone had already asked for that.
    /// </summary>
    public void Requeue(string id, int attempt) {
        lock (changing) {
            if (Current(id, attempt) is not { } job) {
                return;
            }

            Put(AfterLoss(job));
        }
    }

    /// <summary>Asks a job to stop: at once when queued, and when its Worker says so when running.</summary>
    /// <returns>The job, or null when there is none.</returns>
    public JobState? Cancel(string id) {
        lock (changing) {
            return jobs.GetValueOrDefault(id) switch {
                null => null,
                { State: JobStates.Queued } job => Put(
                    job with { State = JobStates.Cancelled, CancelRequested = true }
                ),
                { State: JobStates.Running } job => Put(job with { CancelRequested = true }),
                var job => job
            };
        }
    }

    /// <summary>Which running job someone wants stopped, if any.</summary>
    public JobState? WantedStopped() {
        lock (changing) {
            return jobs.Values.FirstOrDefault(static job => job is { State: JobStates.Running, CancelRequested: true });
        }
    }

    JobState New(string key) =>
        new() { Id = Guid.NewGuid().ToString("N"), Key = key, State = JobStates.Queued, Created = clock.GetUtcNow() };

    /// <summary>What becomes of a running job when whoever had it is gone, by a Worker's death or a Server's.</summary>
    static JobState AfterLoss(JobState job) {
        if (job.CancelRequested) {
            return job with { State = JobStates.Cancelled };
        }

        // A build counts its own deaths step by step in its journal, since one Worker lost in ten thousand steps says
        // nothing against the other 9,999, and a step that kills three is reported and passed (BuildJournal).
        if (job.Build is not null) {
            return job with { State = JobStates.Queued };
        }

        return job.Attempts >= MaxAttempts
            ? job with { State = JobStates.Failed, Error = $"{job.Attempts} Workers died running this job." }
            : job with { State = JobStates.Queued, Progress = 0 };
    }

    static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    static JobState? Read(string file) {
        try {
            return JsonSerializer.Deserialize(SharedFile.ReadAllBytes(file), ContractsJsonContext.Default.JobState);
        } catch (Exception e) when (e is JsonException or IOException) {
            // A torn file cannot happen through Replace, and any other is not this queue's.
            return null;
        }
    }

    JobState? Current(string id, int attempt) =>
        jobs.GetValueOrDefault(id) is { State: JobStates.Running } job && job.Attempts == attempt ? job : null;

    JobState Put(JobState job, bool persist = true) {
        lock (changing) {
            jobs[job.Id] = job;
            if (persist) {
                Write(job);
            }

            var change = new JobEvent(++sequence, job);
            log.Enqueue(change);
            if (log.Count > LogCapacity) {
                log.Dequeue();
            }

            var signal = next;
            next = NewSignal();
            signal.TrySetResult();
            return job;
        }
    }

    void Write(JobState job) =>
        Patina.Library.LibraryFiles.Replace(
            Path.Combine(folder, $"{job.Id}.json"),
            JsonSerializer.SerializeToUtf8Bytes(job, ContractsJsonContext.Default.JobState)
        );

    void Forget() {
        var finished = jobs.Values
            .Where(static job => job.State is JobStates.Done or JobStates.Failed or JobStates.Cancelled)
            .OrderByDescending(static job => job.Created)
            .Skip(Remembered)
            .Select(static job => job.Id)
            .ToList();

        foreach (var id in finished) {
            jobs.Remove(id);
            File.Delete(Path.Combine(folder, $"{id}.json"));
        }
    }
}
