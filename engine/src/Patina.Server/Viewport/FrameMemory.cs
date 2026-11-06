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
///     The file frames pass through (plan §3.1): the Server creates it, the Worker writes a slot, the Server reads it.
///     Its pages are the same physical memory in both processes, and a frame's cost on this link is the name of a slot.
/// </summary>
public sealed class FrameMemory : IDisposable {
    const int Slots = 4;

    /// <summary>Four kilobytes per pixel row of 4K UHD, which is where the render size stops.</summary>
    public const int SlotBytes = MaxWidth * MaxHeight * 4;

    public const int MaxWidth = 3840;
    public const int MaxHeight = 2160;

    readonly MemoryMappedFile file;
    readonly MemoryMappedViewAccessor view;
    readonly Stack<FrameSlot> free = new();
    readonly SemaphoreSlim available = new(0);

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
        foreach (var stale in Directory.EnumerateFiles(folder, WorkerOptions.FramesPattern)) {
            var parts = System.IO.Path.GetFileName(stale).Split('-');
            if (parts.Length == 3 && int.TryParse(parts[1], out var owner) && !Alive(owner)) {
                File.Delete(stale);
            }
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

    public unsafe FrameMemory(IOptions<ServerOptions> options) {
        ArgumentNullException.ThrowIfNull(options);
        Path = options.Value.Worker.FramesPath;
        File.Delete(Path);
        SweepDead();
        file = MemoryMappedFile.CreateFromFile(
            new FileStream(Path, Private()),
            null,
            (long)SlotBytes * Slots,
            MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None,
            false
        );
        view = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        byte* pointer = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        for (var i = 0; i < Slots; i++) {
            free.Push(new(this, pointer, (long)i * SlotBytes, SlotBytes));
        }

        available.Release(Slots);
    }

    public string Path { get; }

    public async Task<FrameSlot> RentAsync(CancellationToken cancellationToken) {
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
        view.SafeMemoryMappedViewHandle.ReleasePointer();
        view.Dispose();
        file.Dispose();
        available.Dispose();
        File.Delete(Path);
    }
}
