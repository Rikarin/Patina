using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;

namespace Patina.Server.Viewport;

/// <summary>
///     One frame's bytes in shared memory. It is held by whoever may still read it; the last release returns it to the
///     pool. A slot in the Worker's hands has one holder, the render loop, so the two processes never touch a frame at
///     the same time.
/// </summary>
public sealed class FrameSlot : MemoryManager<byte> {
    readonly FrameMemory owner;
    readonly unsafe byte* start;
    readonly int capacity;
    int holders;

    internal unsafe FrameSlot(FrameMemory owner, byte* start, long offset, int capacity) {
        this.owner = owner;
        this.start = start;
        this.capacity = capacity;
        Offset = offset;
    }

    public long Offset { get; }

    internal void Acquire() => Volatile.Write(ref holders, 1);

    public void Hold() => Interlocked.Increment(ref holders);

    public void Release() {
        if (Interlocked.Decrement(ref holders) == 0) {
            owner.Return(this);
        }
    }

    public override unsafe Span<byte> GetSpan() => new(start + Offset, capacity);

    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new(start + Offset + elementIndex);

    public override void Unpin() { }

    protected override void Dispose(bool disposing) { }
}

/// <summary>
///     The file frames pass through (plan Â§3.1): the Server creates it, the Worker writes a slot, the Server reads it.
///     Its pages are the same physical memory in both processes, and a frame's cost on this link is the name of a slot.
/// </summary>
public sealed class FrameMemory : IDisposable {
    const int Slots = 4;

    /// <summary>Four kilobytes per pixel row of 4K UHD, which is where the render size stops.</summary>
    public const int SlotBytes = MaxWidth * MaxHeight * 4;

    public const int MaxWidth = 3840;
    public const int MaxHeight = 2160;

    readonly Stack<FrameSlot> free = new();
    readonly SemaphoreSlim available = new(0);
    MemoryMappedFile? file;
    MemoryMappedViewAccessor? view;

    /// <summary>Only this user may read a frame, since temp folders are shared.</summary>
    static FileStreamOptions Private() {
        FileStreamOptions created = new() {
            Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.ReadWrite | FileShare.Delete
        };
        if (!OperatingSystem.IsWindows()) {
            created.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return created;
    }

    /// <summary>A killed Server leaves its gigabytes of frames, under a name that says whose they were.</summary>
    void SweepDead() {
        var folder = System.IO.Path.GetDirectoryName(Path)!;
        if (!Directory.Exists(folder)) {
            return;
        }

        foreach (var stale in Directory.EnumerateFiles(folder, WorkerOptions.FramesPattern)) {
            var parts = System.IO.Path.GetFileName(stale).Split('-');
            if (parts.Length == 3 && int.TryParse(parts[1], out var owner) && !Alive(owner)) {
                TryDelete(stale);
            }
        }
    }

    /// <summary>Tidying up must never stop a Server: Windows keeps a dead process's mapping for a moment.</summary>
    static void TryDelete(string file) {
        try {
            File.Delete(file);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // The next Server sweeps it.
        }
    }

    static bool Alive(int process) {
        try {
            using var found = Process.GetProcessById(process);
            return !found.HasExited;
        } catch (ArgumentException) {
            return false;
        }
    }

    /// <summary>
    ///     Takes no room yet: the file is made when the first frame is drawn, so a Server that never shows a viewport,
    ///     such as a test's or a CLI client's, starts on a full disk (#291).
    /// </summary>
    public FrameMemory(IOptions<ServerOptions> options) {
        ArgumentNullException.ThrowIfNull(options);
        Path = options.Value.Worker.FramesPath;
        TryDelete(Path);
        SweepDead();
    }

    /// <summary>What the file takes on disk once made: four frames at the largest render size.</summary>
    public static long FileBytes => (long)SlotBytes * Slots;

    /// <summary>Makes the file on the first frame, and says where and how much room it wanted if it cannot.</summary>
    unsafe void Map() {
        lock (free) {
            if (view is not null) {
                return;
            }

            try {
                file = MemoryMappedFile.CreateFromFile(
                    new FileStream(Path, Private()),
                    null,
                    FileBytes,
                    MemoryMappedFileAccess.ReadWrite,
                    HandleInheritability.None,
                    false
                );
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                TryDelete(Path);
                throw new FramesUnavailableException(Path, FileBytes, e);
            }

            view = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            byte* pointer = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            for (var i = 0; i < Slots; i++) {
                free.Push(new(this, pointer, (long)i * SlotBytes, SlotBytes));
            }
        }

        available.Release(Slots);
    }

    public string Path { get; }

    /// <exception cref="FramesUnavailableException">The file for frames could not be made.</exception>
    public async Task<FrameSlot> RentAsync(CancellationToken cancellationToken) {
        if (Volatile.Read(ref view) is null) {
            Map();
        }

        await available.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (free) {
            var slot = free.Pop();
            slot.Acquire();
            return slot;
        }
    }

    internal void Return(FrameSlot slot) {
        lock (free) {
            free.Push(slot);
        }

        available.Release();
    }

    public void Dispose() {
        if (view is not null) {
            view.SafeMemoryMappedViewHandle.ReleasePointer();
            view.Dispose();
        }

        file?.Dispose();
        available.Dispose();
        TryDelete(Path);
    }
}

/// <summary>
///     The file frames pass through could not be made, most often because its disk is full. It says where and how much
///     room it wanted, so the person can free some or point <c>Patina:Worker:FramesPath</c> elsewhere.
/// </summary>
public sealed class FramesUnavailableException : IOException {
    public FramesUnavailableException() : base("The viewport could not make the file its frames pass through.") { }

    public FramesUnavailableException(string message) : base(message) { }

    public FramesUnavailableException(string message, Exception innerException) : base(message, innerException) { }

    public FramesUnavailableException(string path, long bytes, Exception innerException) : base(
        $"The viewport needs {bytes / (1024 * 1024)} MB free for its frames at {path}, and could not make it there: "
        + innerException?.Message,
        innerException
    ) { }
}
