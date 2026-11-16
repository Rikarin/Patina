using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Api;

namespace Patina.Server.Viewport;

/// <summary>
///     The bake setup the viewport shows (#149), for every client and for a new Worker. The Server holds what was asked
///     and the key the Worker answered with; the Worker holds the shapes, which it makes again when it is told again.
/// </summary>
/// <remarks>
///     <para>
///         Its own state beside <see cref="ViewportSession" /> rather than inside it: a preview lies over whatever the
///         viewport shows and changes nothing about it, so the session only has to tell a new Worker and draw again.
///     </para>
///     <para>
///         A preview that names a project follows its <c>project.yaml</c> (#300): when the file has changed since it
///         was read, by any client or by hand, the next frame reads the meshes, cage and options from it again, as
///         <c>PUT /viewport/bake-preview</c> did. A file that no longer reads keeps what was shown, with the reason.
///     </para>
/// </remarks>
public sealed class BakePreviewSession(WorkerLink worker, LibraryHost host) : IDisposable {
    static readonly TimeSpan WorkerPatience = TimeSpan.FromSeconds(10);

    readonly Lock changing = new();
    readonly SemaphoreSlim telling = new(1, 1);
    ViewportBakePreview? preview;
    Followed? following;
    ViewportIsolation? isolation;
    BakePreviewState state = BakePreviewState.Off;
    long version;
    (IpcConnection? Connection, long Version) told;

    public void Dispose() => telling.Dispose();

    /// <summary>What is shown, and the key to it.</summary>
    public BakePreviewState State {
        get {
            lock (changing) {
                return state;
            }
        }
    }

    /// <summary>
    ///     The project file a preview as sent follows, as it stands before it is read, or null for a preview that names
    ///     no project. Taken before the preview is resolved, so a change written while it is read is not missed.
    /// </summary>
    public Followed? Follow(ViewportBakePreview asked) {
        ArgumentNullException.ThrowIfNull(asked);
        if (asked.Project is not { } project || host.Library is not { } library) {
            return null;
        }

        try {
            var file = LibraryFiles.ProjectFile(library.RootPath, project);
            return new(asked, file, Stamp(file));
        } catch (Exception e) when (e is ArgumentException or IOException) {
            return null; // resolving it says why
        }
    }

    /// <summary>Whether the preview shown follows a project, by its path below <c>projects/</c>.</summary>
    public bool Follows(string project) {
        lock (changing) {
            return following?.Asked.Project == project;
        }
    }

    /// <summary>Shows a bake's setup, or takes it off for an empty low mesh, and answers with the key.</summary>
    /// <param name="asked">The setup, with a project's paths and options already in it.</param>
    /// <param name="followed">What <see cref="Follow" /> said before it was resolved, read again on a change.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <exception cref="RendererMissingException">No GPU process connected in time.</exception>
    /// <exception cref="IpcException">The Worker could not read the meshes or show them.</exception>
    public async Task<BakePreviewState> ShowAsync(
        ViewportBakePreview asked,
        Followed? followed,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(asked);
        lock (changing) {
            preview = asked.Low.Length == 0 ? null : asked;
            following = preview is null ? null : followed;
            version++;
            if (preview is null) {
                state = BakePreviewState.Off;
            }
        }

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(WorkerPatience);
        IpcConnection connection;
        try {
            connection = await worker.ConnectionAsync(patience.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RendererMissingException();
        }

        await TellAsync(connection, true, cancellationToken);
        return State;
    }

    /// <summary>
    ///     Draws the preview over what the viewport draws alone (#136), told to the Worker with the next frame; null is
    ///     the whole model.
    /// </summary>
    public void Isolate(ViewportIsolation? shown) {
        lock (changing) {
            if (shown == isolation) {
                return;
            }

            isolation = shown;
            if (preview is not null) {
                version++;
            }
        }
    }

    /// <summary>Tells a Worker the preview when it has not heard this one: a new Worker has heard nothing.</summary>
    /// <param name="connection">The Worker's link.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <remarks>A Worker that cannot show it costs the preview and not the frame: the reason goes in the key.</remarks>
    public Task TellAsync(IpcConnection connection, CancellationToken cancellationToken) =>
        TellAsync(connection, false, cancellationToken);

    async Task TellAsync(IpcConnection connection, bool throwing, CancellationToken cancellationToken) {
        await telling.WaitAsync(cancellationToken);
        try {
            if (!throwing) {
                ReadAgain();
            }

            ViewportBakePreview? showing;
            ViewportIsolation? alone;
            long asked;
            lock (changing) {
                (showing, alone, asked) = (preview, isolation, version);
                // A Worker that never heard of a preview has none to take off.
                if (told == (connection, asked) || (showing is null && told.Connection != connection && asked == 0)) {
                    return;
                }
            }

            try {
                var reply = await connection.SendAsync(
                    new() {
                        Type = IpcTypes.ViewportBakePreview,
                        BakePreview = showing ?? new(),
                        BakePreviewIsolation = alone
                    },
                    cancellationToken
                );
                lock (changing) {
                    if (version == asked) {
                        state = reply.BakePreviewShown ?? BakePreviewState.Off;
                    }
                }

                told = (connection, asked);
            } catch (Exception e) when (e is IpcException || (e is TimeoutException && !throwing)) {
                // ⚠ A Worker too slow to answer is told once and not on every frame, or the viewport would wait on
                // the same meshes for ever; a client asking again tells it again.
                lock (changing) {
                    state = new(showing, [], [e.Message]);
                }

                told = (connection, asked);
                if (throwing) {
                    throw;
                }
            }
        } finally {
            telling.Release();
        }
    }

    /// <summary>Reads a followed project's setup again when its file has changed since it was read (#300).</summary>
    void ReadAgain() {
        Followed? now;
        lock (changing) {
            now = following;
        }

        if (now is null) {
            return;
        }

        var stamp = Stamp(now.File);
        if (stamp == now.Written) {
            return;
        }

        var refused = ProjectBakePlanEndpoint.Resolve(host, now.Asked, out var resolved);
        lock (changing) {
            if (following != now) {
                return; // a client showed another meanwhile
            }

            following = now with { Written = stamp };
            if (refused is null && resolved.Low.Length > 0) {
                preview = resolved;
                version++;
            } else {
                var why = refused?.ProblemDetails.Detail ?? "it names no mesh to bake onto";
                state = state with { Warnings = [$"The project's bake settings changed and no longer read: {why}"] };
            }
        }
    }

    /// <summary>When a file was last written, and how long it is: anything that rewrites it changes one.</summary>
    static (long Ticks, long Length) Stamp(string file) {
        var info = new FileInfo(file);
        return info.Exists ? (info.LastWriteTimeUtc.Ticks, info.Length) : (0, -1);
    }

    /// <summary>A preview as sent that names a project, the project's file, and how that stood when read.</summary>
    /// <param name="Asked">The preview as a client sent it.</param>
    /// <param name="File">The project's <c>project.yaml</c>.</param>
    /// <param name="Written">When it was written and how long it was.</param>
    public sealed record Followed(ViewportBakePreview Asked, string File, (long Ticks, long Length) Written);
}
