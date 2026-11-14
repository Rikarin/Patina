using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;

namespace Patina.Server;

/// <summary>
///     What the Worker read of each mesh file's texture sets, kept until the file's bytes change (#285). The Server never
///     parses a mesh (plan §3.1): a bad file may crash the native parser, and that must take down the Worker, which
///     can die at any time, never the Server, which holds every session.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>A file the Worker went away while reading is kept as one that does not read</b>, until it changes:
///         asked again, it would take the next Worker down too. A file that will not read has no tiles, no faces and
///         no pictures, which is what reading it here used to answer.
///     </para>
///     <para>
///         ⚠ <b>The Worker answers beside its queue</b> (<see cref="IpcTypes.MeshSets" />), so a set's state waits for
///         the file's read and never for an evaluation. With no Worker connected nothing is known, and nothing is
///         kept: the next question asks again.
///     </para>
/// </remarks>
public sealed class MeshSetCache(WorkerLink worker) : IMeshSetReads {
    /// <summary>How long a question that cannot wait on its own waits for a read the Worker has begun.</summary>
    static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);

    readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public int? Udim(string file, string? material, int uvSet) => Find(file, material, uvSet)?.Udim;

    /// <inheritdoc />
    public string? Geometry(string file, string? material, int uvSet) => Find(file, material, uvSet)?.Geometry;

    /// <inheritdoc />
    public IReadOnlyList<string> Pictures(string file) => Known(file).Pictures;

    /// <summary>
    ///     What the Worker reads of a file, waiting up to <paramref name="connecting" /> for a Worker to read it.
    ///     Nothing, for a file that is not there or will not read, or when no Worker came.
    /// </summary>
    /// <param name="file">The mesh file.</param>
    /// <param name="connecting">How long to wait for a Worker when none is connected.</param>
    /// <param name="cancellationToken">Ends the wait.</param>
    public async Task<MeshSetsRead> ReadAsync(string file, TimeSpan connecting, CancellationToken cancellationToken) =>
        await Start(file, connecting).WaitAsync(cancellationToken) ?? MeshSetsRead.None;

    MeshSetRead? Find(string file, string? material, int uvSet) {
        var name = material ?? string.Empty;
        return Known(file).Sets.FirstOrDefault(set => set.UvSet == uvSet && set.Material == name);
    }

    /// <summary>What is known of a file, waiting for a Worker that is reading it, but not for one to come.</summary>
    MeshSetsRead Known(string file) {
        var read = Start(file, TimeSpan.Zero);
        return read.Wait(Patience) ? read.Result ?? MeshSetsRead.None : MeshSetsRead.None;
    }

    /// <summary>The read of the file as it is now: the one kept, the one under way, or a new one.</summary>
    Task<MeshSetsRead?> Start(string file, TimeSpan connecting) {
        var full = Path.GetFullPath(file);
        FileInfo info = new(full);
        if (!info.Exists) {
            return Task.FromResult<MeshSetsRead?>(MeshSetsRead.None);
        }

        Stamp stamp = new(info.Length, info.LastWriteTimeUtc);
        lock (entries) {
            if (entries.TryGetValue(full, out var known) && !Lost(known.Read)) {
                if (known.Stamp == stamp) {
                    return known.Read;
                }

                // Written again with the same bytes, as an exporter does: what was read of them still holds.
                if (Digest(full) is { } digest && digest == known.Digest) {
                    entries[full] = known with { Stamp = stamp };
                    return known.Read;
                }
            }

            var read = FetchAsync(full, connecting);
            entries[full] = new(stamp, Digest(full), read);
            return read;
        }
    }

    /// <summary>A read no Worker came for, which is not kept.</summary>
    static bool Lost(Task<MeshSetsRead?> read) => read.IsCompleted && read.Result is null;

    static string? Digest(string file) {
        try {
            return LibraryFiles.Digest(file);
        } catch (IOException) {
            return null; // being written: the next question asks again
        }
    }

    /// <summary>Asks the Worker; null when no Worker came, which is not kept.</summary>
    async Task<MeshSetsRead?> FetchAsync(string file, TimeSpan connecting) {
        IpcConnection connection;
        using (CancellationTokenSource waiting = new(connecting)) {
            try {
                connection = await worker.ConnectionAsync(waiting.Token);
            } catch (OperationCanceledException) {
                return null;
            }
        }

        try {
            var reply = await connection.SendAsync(new() { Type = IpcTypes.MeshSets, MeshFile = file });
            return reply.MeshSets ?? MeshSetsRead.None;
        } catch (Exception e) when (e is IpcException or IOException) {
            return MeshSetsRead.None; // it will not read, or it took the Worker down: either way, not again
        }
    }

    readonly record struct Stamp(long Length, DateTime Written);

    readonly record struct Entry(Stamp Stamp, string? Digest, Task<MeshSetsRead?> Read);
}
