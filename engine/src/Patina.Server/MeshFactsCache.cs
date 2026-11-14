using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;

namespace Patina.Server;

/// <summary>
///     What the Worker said a mesh file is (<c>GET /library/mesh</c>), kept until the file's bytes change (#292), as
///     <see cref="MeshSetCache" /> keeps its texture sets: a client asking again about the same file is answered here,
///     not by another parse of it. The Server still never reads the file (ADR 0039), only its length, time and digest.
/// </summary>
/// <remarks>
///     ⚠ Only an answer is kept. A refusal, or no Worker, asks again the next time, so a file fixed in place or a
///     Worker that came back is read.
/// </remarks>
public sealed class MeshFactsCache {
    readonly Func<string, CancellationToken, Task<MeshFacts?>> ask;
    readonly Dictionary<string, Kept> kept = new(StringComparer.Ordinal);

    /// <summary>A cache that asks the Worker the link reaches.</summary>
    public MeshFactsCache(WorkerLink worker) : this((file, token) => AskAsync(worker, file, token)) { }

    /// <summary>A cache that asks <paramref name="ask" />, for a test.</summary>
    /// <param name="ask">Reads a file's facts, or null when the reader said nothing.</param>
    public MeshFactsCache(Func<string, CancellationToken, Task<MeshFacts?>> ask) => this.ask = ask;

    /// <summary>What a file is: kept while its bytes stay, or read again. Null when the Worker said nothing.</summary>
    /// <exception cref="IpcException">The Worker could not read it.</exception>
    public async Task<MeshFacts?> GetAsync(string file, CancellationToken cancellationToken) {
        var full = Path.GetFullPath(file);
        FileInfo info = new(full);
        Stamp stamp = new(info.Length, info.LastWriteTimeUtc);
        string? digest;
        lock (kept) {
            if (kept.TryGetValue(full, out var known) && known.Stamp == stamp) {
                return known.Facts;
            }

            digest = Digest(full);
            // Written again with the same bytes, as an exporter does: what was read of them still holds.
            if (known is not null && digest is not null && digest == known.Digest) {
                kept[full] = known with { Stamp = stamp };
                return known.Facts;
            }
        }

        var facts = await ask(full, cancellationToken);
        if (facts is not null && digest is not null) {
            lock (kept) {
                kept[full] = new(stamp, digest, facts);
            }
        }

        return facts;
    }

    static string? Digest(string file) {
        try {
            return LibraryFiles.Digest(file);
        } catch (IOException) {
            return null; // being written: read it, but keep nothing
        }
    }

    static async Task<MeshFacts?> AskAsync(WorkerLink worker, string file, CancellationToken cancellationToken) {
        var connection = await worker.ConnectionAsync(cancellationToken);
        var reply = await connection.SendAsync(new() { Type = IpcTypes.MeshFacts, MeshFile = file }, cancellationToken);
        return reply.Facts;
    }

    readonly record struct Stamp(long Length, DateTime Written);

    sealed record Kept(Stamp Stamp, string Digest, MeshFacts Facts);
}
