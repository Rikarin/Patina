using System.Diagnostics;

namespace Patina.Ipc;

/// <summary>
///     Notices device work that has stopped moving. A stuck GPU fence holds the device forever while the link stays up
///     and the health check keeps answering, so only this process can tell, and it ends itself: the Worker holds
///     nothing that cannot be rebuilt, and what supervises it starts another.
/// </summary>
public sealed class DeviceWatch(TimeSpan limit, Func<TimeSpan> now) {
    const string HangVariable = "PATINA_WORKER_HANG_SECONDS";
    const int DefaultHangSeconds = 300;
    const int Exited = 3;
    static readonly TimeSpan Look = TimeSpan.FromSeconds(5);

    readonly TimeSpan limit = limit;
    readonly Func<TimeSpan> now = now;
    long lastMark = -1;

    public DeviceWatch() : this(ConfiguredLimit(), Elapsed) { }

    /// <summary>How long the device work has gone without progress, or nothing if there is none.</summary>
    public TimeSpan Busy =>
        Interlocked.Read(ref lastMark) is var mark and >= 0 ? now() - TimeSpan.FromTicks(mark) : TimeSpan.Zero;

    /// <summary>Whether device work has been still for too long.</summary>
    public bool Hung => Busy > limit;

    /// <summary>Work starts on the device, or the work on it moved.</summary>
    public void Beat() => Interlocked.Exchange(ref lastMark, now().Ticks);

    /// <summary>The device is free.</summary>
    public void Idle() => Interlocked.Exchange(ref lastMark, -1);

    /// <summary>Watches until stopped, and ends the process if the device hangs.</summary>
    public async Task WatchAsync(TextWriter log, CancellationToken cancellationToken) {
        using PeriodicTimer timer = new(Look);
        while (await timer.WaitForNextTickAsync(cancellationToken)) {
            if (Hung) {
                await log.WriteLineAsync(
                    $"patina-worker: the device has not moved for {Busy.TotalSeconds:0} s, stopping",
                    cancellationToken
                );
                await log.FlushAsync(cancellationToken);
                Environment.ExitCode = Exited;
                Process.GetCurrentProcess().Kill();
            }
        }
    }

    static TimeSpan Elapsed() =>
        TimeSpan.FromTicks(Stopwatch.GetTimestamp() * TimeSpan.TicksPerSecond / Stopwatch.Frequency);

    static TimeSpan ConfiguredLimit() =>
        TimeSpan.FromSeconds(
            int.TryParse(Environment.GetEnvironmentVariable(HangVariable), out var seconds) && seconds > 0
                ? seconds
                : DefaultHangSeconds
        );
}
