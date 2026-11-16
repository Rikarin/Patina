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
///     <para>
///         Every preview follows its mesh files too (#305): one rewritten in place, as Reload Mesh does, is read again
///         by the Worker with the next frame. <c>GET /viewport/bake-preview</c> looks as a frame does, so it never
///         answers with a setup the files no longer say while no frame has been drawn.
///     </para>
/// </remarks>
public sealed class BakePreviewSession(WorkerLink worker, LibraryHost host) : IDisposable {
    /// <summary>What a preview says while no GPU engine is connected to draw it.</summary>
    public const string WaitsForEngine =
        "The preview waits for the GPU engine, which is not connected: it is drawn again when the engine is back.";

    static readonly TimeSpan WorkerPatience = TimeSpan.FromSeconds(10);

    readonly Lock changing = new();
    readonly SemaphoreSlim telling = new(1, 1);
    ViewportBakePreview? preview;
    Followed? following;
    (long Ticks, long Length)[] meshes = [];
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
    ///     What is shown as its files say now: a project file or mesh file changed since it was told is read again and
    ///     told to the Worker first, as the next frame would, and so is a preview a new Worker has not heard. With no
    ///     Worker connected, the preview asked for waits for one, with no key (#310).
    /// </summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    public async Task<BakePreviewState> StateAsync(CancellationToken cancellationToken) {
        if (!worker.IsConnected) {
            ReadAgain(); // no Worker to tell: the next one hears it
            return Waiting();
        }

        // A Worker that has heard this preview, from files that have not changed since, is not told it again.
        await TellAsync(await worker.ConnectionAsync(cancellationToken), false, cancellationToken);
        return State;
    }

    /// <summary>
    ///     A preview with no GPU engine to draw it: what was asked stays, and the key and warnings the last engine gave
    ///     are not the answer, since the next one draws it again from the files as they are then.
    /// </summary>
    BakePreviewState Waiting() {
        lock (changing) {
            return preview is null ? BakePreviewState.Off : new(preview, [], [WaitsForEngine]);
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
        // ⚠ Before the Worker reads them, so a file rewritten while it reads is read again.
        var written = Stamps(asked);
        lock (changing) {
            preview = asked.Low.Length == 0 ? null : asked;
            meshes = written;
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

    /// <summary>
    ///     Reads a followed project's setup again when its file has changed since it was read (#300), and shows a
    ///     preview again when one of its mesh files has (#305).
    /// </summary>
    void ReadAgain() {
        ReadProjectAgain();
        ViewportBakePreview? shown;
        (long Ticks, long Length)[] written;
        lock (changing) {
            (shown, written) = (preview, meshes);
        }

        if (shown is null) {
            return;
        }

        var now = Stamps(shown);
        if (now.SequenceEqual(written)) {
            return;
        }

        lock (changing) {
            if (preview == shown && meshes == written) {
                meshes = now;
                version++;
            }
        }
    }

    void ReadProjectAgain() {
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
        var written = Stamps(resolved);
        lock (changing) {
            if (following != now) {
                return; // a client showed another meanwhile
            }

            following = now with { Written = stamp };
            if (refused is null && resolved.Low.Length > 0) {
                preview = resolved;
                meshes = written;
                version++;
            } else {
                var why = refused?.ProblemDetails.Detail ?? "it names no mesh to bake onto";
                state = state with { Warnings = [$"The project's bake settings changed and no longer read: {why}"] };
            }
        }
    }

    /// <summary>When each file a preview reads was last written, and how long it is (#305).</summary>
    static (long Ticks, long Length)[] Stamps(ViewportBakePreview shown) {
        var rays = shown.Options.Rays;
        string?[] files = [shown.Low, ..shown.High, shown.Cage, rays.SkewMap, rays.DistanceMap];
        return [..files.Where(static file => file is { Length: > 0 }).Select(static file => Stamp(file!))];
    }

    /// <summary>When a file was last written, and how long it is: anything that rewrites it changes one.</summary>
    static (long Ticks, long Length) Stamp(string file) {
        try {
            var info = new FileInfo(file);
            return info.Exists ? (info.LastWriteTimeUtc.Ticks, info.Length) : (0, -1);
        } catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) {
            return (0, -2); // a path that does not read as one: the Worker says why
        }
    }

    /// <summary>A preview as sent that names a project, the project's file, and how that stood when read.</summary>
    /// <param name="Asked">The preview as a client sent it.</param>
    /// <param name="File">The project's <c>project.yaml</c>.</param>
    /// <param name="Written">When it was written and how long it was.</param>
    public sealed record Followed(ViewportBakePreview Asked, string File, (long Ticks, long Length) Written);
}
