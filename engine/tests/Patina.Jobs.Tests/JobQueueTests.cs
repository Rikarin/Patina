using CsCheck;
using Patina.Contracts;
using System.Text.Json;

namespace Patina.Jobs.Tests;

/// <summary>The queue on its own: what survives a Worker and a Server is decided here.</summary>
public sealed class JobQueueTests : IDisposable {
    static readonly BakeRequest Bake = new() { High = ["high.glb"], Low = "low.glb", Output = "out" };
    static readonly BakeResult Made = new([new("normal", "normal.png", 64, 0)], 10, 0, [], false);

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-jobs-");

    public void Dispose() => folder.Delete(true);

    JobQueue Open() => new(folder.FullName, new Ticking());

    /// <summary>A clock that moves on each time it is read, so that one job is older than another.</summary>
    sealed class Ticking : TimeProvider {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => now = now.AddSeconds(1);
    }

    [Fact]
    public void A_job_is_handed_out_once_and_oldest_first() {
        var queue = Open();
        var first = queue.Enqueue("a", Bake);
        var second = queue.Enqueue("b", Bake);

        Assert.Equal(first.Id, queue.TakeNext()!.Id);
        Assert.Equal(second.Id, queue.TakeNext()!.Id);
        Assert.Null(queue.TakeNext());
    }

    /// <summary>S5: a killed Worker's job goes back in the queue, and the next Worker gets it from the start.</summary>
    [Fact]
    public void A_lost_workers_job_goes_back_in_the_queue() {
        var queue = Open();
        var job = queue.Enqueue("a", Bake);
        var running = queue.TakeNext()!;
        queue.Report(job.Id, running.Attempts, 0.5);

        queue.Requeue(job.Id, running.Attempts);

        var again = queue.Find(job.Id)!;
        Assert.Equal(JobStates.Queued, again.State);
        Assert.Equal(0, again.Progress);
        Assert.Equal(2, queue.TakeNext()!.Attempts);
    }

    /// <summary>A job that kills every Worker it meets must not keep the machine in a crash loop.</summary>
    [Fact]
    public void A_job_is_given_up_on_after_too_many_lost_workers() {
        var queue = Open();
        var job = queue.Enqueue("a", Bake);

        for (var attempt = 1; attempt <= JobQueue.MaxAttempts; attempt++) {
            Assert.Equal(attempt, queue.TakeNext()!.Attempts);
            queue.Requeue(job.Id, attempt);
        }

        Assert.Equal(JobStates.Failed, queue.Find(job.Id)!.State);
        Assert.Contains("Workers died", queue.Find(job.Id)!.Error, StringComparison.Ordinal);
        Assert.Null(queue.TakeNext());
    }

    /// <summary>
    ///     The Server restarting finds the running job back in the queue, and the finished one still finished.
    /// </summary>
    [Fact]
    public void A_restart_puts_a_running_job_back_and_keeps_what_was_finished() {
        var queue = Open();
        var done = queue.Enqueue("a", Bake);
        queue.TakeNext();
        queue.Complete(done.Id, 1, Made);
        var running = queue.Enqueue("b", Bake);
        queue.TakeNext();
        var waiting = queue.Enqueue("c", Bake);

        var reopened = Open();

        Assert.Equal(JobStates.Done, reopened.Find(done.Id)!.State);
        Assert.Equal(Made.Covered, reopened.Find(done.Id)!.Result!.Covered);
        Assert.Equal(JobStates.Queued, reopened.Find(running.Id)!.State);
        Assert.Equal(1, reopened.Find(running.Id)!.Attempts);
        Assert.Equal(JobStates.Queued, reopened.Find(waiting.Id)!.State);
    }

    /// <summary>A job asked to stop just before the Server died does not start again after it.</summary>
    [Fact]
    public void A_restart_does_not_run_what_was_asked_to_stop() {
        var queue = Open();
        var job = queue.Enqueue("a", Bake);
        queue.TakeNext();
        queue.Cancel(job.Id);

        Assert.Equal(JobStates.Cancelled, Open().Find(job.Id)!.State);
    }

    /// <summary>A queued job stops at once; a running one when its Worker says it has.</summary>
    [Fact]
    public void Cancelling_stops_a_queued_job_and_asks_a_running_one() {
        var queue = Open();
        var running = queue.Enqueue("a", Bake);
        var waiting = queue.Enqueue("b", Bake);
        queue.TakeNext();

        Assert.Equal(JobStates.Cancelled, queue.Cancel(waiting.Id)!.State);
        var asked = queue.Cancel(running.Id)!;

        Assert.Equal(JobStates.Running, asked.State);
        Assert.True(asked.CancelRequested);

        queue.Stopped(running.Id, 1);
        Assert.Equal(JobStates.Cancelled, queue.Find(running.Id)!.State);
    }

    /// <summary>A Worker that lost its job and only now reports must not touch the next attempt.</summary>
    [Fact]
    public void A_report_from_an_earlier_attempt_changes_nothing() {
        var queue = Open();
        var job = queue.Enqueue("a", Bake);
        queue.TakeNext();
        queue.Requeue(job.Id, 1);
        queue.TakeNext();

        queue.Report(job.Id, 1, 0.9);
        queue.Complete(job.Id, 1, Made);
        queue.Fail(job.Id, 1, "too late");

        var now = queue.Find(job.Id)!;
        Assert.Equal(JobStates.Running, now.State);
        Assert.Equal(0, now.Progress);
        Assert.Null(now.Result);
    }

    /// <summary>A file that is not a job is left alone, and does not stop the queue opening.</summary>
    [Fact]
    public void A_torn_file_does_not_stop_the_queue_opening() {
        File.WriteAllText(Path.Combine(folder.FullName, "broken.json"), "{");

        Assert.Empty(Open().Page().Jobs);
    }

    /// <summary>A client that dropped its socket gets what it missed, and is told when that has gone.</summary>
    [Fact]
    public void Events_are_numbered_and_a_client_can_catch_up() {
        var queue = Open();
        var job = queue.Enqueue("a", Bake);
        queue.TakeNext();
        queue.Report(job.Id, 1, 0.5);

        Assert.Equal([1L, 2L, 3L], queue.Since(0)!.Select(static e => e.Sequence));
        Assert.Equal([3L], queue.Since(2)!.Select(static e => e.Sequence));
        Assert.Empty(queue.Since(3)!);
    }

    [Fact]
    public async Task Changed_completes_only_when_something_changes() {
        var queue = Open();
        var changed = queue.Changed(TestContext.Current.CancellationToken);

        Assert.False(changed.IsCompleted);

        queue.Enqueue("a", Bake);
        await changed;
    }

    /// <summary>
    ///     Whatever order things happen in, across restarts: a job only ever moves forward, its attempts never go past
    ///     the limit, and nothing finished comes back.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void Jobs_only_move_forward_whatever_happens() =>
        Gen.Int[0, 6]
            .Array[1, 80]
            .Sample(
                static steps => {
                    var scratch = Directory.CreateTempSubdirectory("patina-jobs-");
                    try {
                        var queue = new JobQueue(scratch.FullName);
                        var finished = new Dictionary<string, JobState>();
                        var last = (Id: string.Empty, Attempt: 0);

                        foreach (var step in steps) {
                            switch (step) {
                                case 0:
                                    queue.Enqueue("key", Bake);
                                    break;
                                case 1:
                                    if (queue.TakeNext() is { } taken) {
                                        last = (taken.Id, taken.Attempts);
                                    }

                                    break;
                                case 2:
                                    queue.Requeue(last.Id, last.Attempt);
                                    break;
                                case 3:
                                    queue.Complete(last.Id, last.Attempt, Made);
                                    break;
                                case 4:
                                    queue.Cancel(last.Id);
                                    break;
                                case 5:
                                    queue.Report(last.Id, last.Attempt, 0.5);
                                    break;
                                default:
                                    queue = new(scratch.FullName);
                                    break;
                            }

                            foreach (var job in queue.Page().Jobs) {
                                Assert.InRange(job.Attempts, 0, JobQueue.MaxAttempts);
                                if (finished.TryGetValue(job.Id, out var was)) {
                                    Assert.Equal(was.State, job.State);
                                }

                                if (job.State is JobStates.Done or JobStates.Failed or JobStates.Cancelled) {
                                    finished[job.Id] = job;
                                }
                            }
                        }
                    } finally {
                        scratch.Delete(true);
                    }
                },
                iter: 150
            );

    /// <summary>The file is the job: nothing about it is held only in memory, the progress aside.</summary>
    [Fact]
    public void A_job_on_disk_is_the_whole_job() {
        var queue = Open();
        var job = queue.Enqueue("a", Bake with { Options = new() { Resolution = 512 } });
        var text = File.ReadAllBytes(Path.Combine(folder.FullName, $"{job.Id}.json"));
        var read = JsonSerializer.Deserialize(text, ContractsJsonContext.Default.JobState)!;

        Assert.Equal(512, read.Bake!.Options.Resolution);
        Assert.Equal("a", read.Key);
    }

    [Fact]
    public void A_bake_goes_ahead_of_a_build_that_was_asked_for_first() {
        var queue = Open();
        var build = queue.Enqueue(new());
        var bake = queue.Enqueue("a", Bake);

        Assert.Equal(bake.Id, queue.TakeNext()!.Id);
        Assert.Equal(build.Id, queue.TakeNext()!.Id);
    }

    [Fact]
    public void A_running_build_lets_a_bake_by_and_never_takes_another_build() {
        var queue = Open();
        var first = queue.Enqueue(new());
        queue.Enqueue(new());
        Assert.Equal(first.Id, queue.TakeNext()!.Id);

        Assert.Null(queue.TakeInteractive());
        var bake = queue.Enqueue("a", Bake);
        Assert.Equal(bake.Id, queue.TakeInteractive()!.Id);
    }

    [Fact]
    public void A_build_survives_more_lost_workers_than_a_bake_because_it_counts_them_per_step() {
        var queue = Open();
        var build = queue.Enqueue(new());

        for (var attempt = 1; attempt <= JobQueue.MaxAttempts + 2; attempt++) {
            Assert.Equal(attempt, queue.TakeNext()!.Attempts);
            queue.Requeue(build.Id, attempt);
        }

        Assert.Equal(JobStates.Queued, queue.Find(build.Id)!.State);
    }

    [Fact]
    public void A_build_in_a_restarted_server_goes_back_in_the_queue() {
        var queue = Open();
        var build = queue.Enqueue(new());
        queue.TakeNext();

        Assert.Equal(JobStates.Queued, Open().Find(build.Id)!.State);
    }
}
