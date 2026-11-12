using Patina.Contracts;
using Patina.Export;
using Patina.Ipc;
using Patina.Viewport;
using System.Diagnostics;
using Vixen.Interchange;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>Stays connected to the Server until stopped.</summary>
sealed class WorkerLoop(
    ViewportHost viewport,
    GraphHost graphs,
    StackHost stacks,
    BakeHost bakes,
    BuildHost builds,
    DeviceHost gpu,
    DeviceWatch watch,
    MeshShelf meshes
) {
    static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    static readonly long Started = Stopwatch.GetTimestamp();

    /// <summary>
    ///     Keeps a link to the Server, reconnecting when it drops. <paramref name="log" /> gets one line per link,
    ///     which the desktop app's supervision reads.
    /// </summary>
    public async Task RunAsync(string socketPath, TextWriter log, CancellationToken cancellationToken) {
        _ = WatchAsync(log, cancellationToken);
        while (!cancellationToken.IsCancellationRequested) {
            try {
                await RunLinkAsync(socketPath, log, cancellationToken);
            } catch (Exception e) when (e is IOException or IpcException or System.Net.Sockets.SocketException) {
                await log.WriteLineAsync($"patina-worker: {e.Message}", CancellationToken.None);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                return;
            }

            try {
                await Task.Delay(250, cancellationToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    async Task WatchAsync(TextWriter log, CancellationToken cancellationToken) {
        try {
            await watch.WatchAsync(log, cancellationToken);
        } catch (OperationCanceledException) {
            // Stopping.
        }
    }

    async Task RunLinkAsync(string socketPath, TextWriter log, CancellationToken cancellationToken) {
        var stream = await IpcSocket.ConnectAsync(socketPath, ConnectTimeout, cancellationToken);
        IpcConnection? connection = null;
        connection = new(stream, (request, token) => AnswerAsync(connection!, request, token)) {
            Immediate = static type => type is IpcTypes.Health or IpcTypes.BakeCancel or IpcTypes.BuildCancel
        };
        await using (connection) {
            connection.Start();
            await connection.SendAsync(
                new() {
                    Type = IpcTypes.Hello,
                    Worker = new(IpcMessage.ProtocolVersion, ProductInfo.Version, Environment.ProcessId)
                },
                cancellationToken
            );
            await log.WriteLineAsync(
                $"patina-worker {ProductInfo.Version} connected to {socketPath}",
                cancellationToken
            );
            try {
                await connection.Closed.WaitAsync(cancellationToken);
                await log.WriteLineAsync("patina-worker: the Server disconnected", cancellationToken);
            } finally {
                bakes.LinkLost();
                builds.LinkLost();
            }
        }
    }

    ValueTask<IpcMessage> AnswerAsync(IpcConnection link, IpcMessage request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            request.Type switch {
                IpcTypes.Health => new() { Type = IpcTypes.Ok, Health = Health() },
                IpcTypes.BrushPreview => BrushPreviews.Answer(request),
                IpcTypes.ViewportOpen => viewport.Open(request),
                IpcTypes.ViewportMaps => viewport.SetMaps(request),
                IpcTypes.ViewportCamera => viewport.SetCamera(request),
                IpcTypes.ViewportDisplay => viewport.SetDisplay(request),
                IpcTypes.ViewportRender => viewport.Render(request),
                IpcTypes.ViewportPick => viewport.Pick(request),
                IpcTypes.ViewportBakePreview => BakePreviewHost.Show(request, viewport.Live),
                IpcTypes.GraphLoad
                    or IpcTypes.GraphEvaluate
                    or IpcTypes.GraphImage
                    or IpcTypes.GraphClose
                    or IpcTypes.StackLoad
                    or IpcTypes.StackSync
                    or IpcTypes.StackThumbnails
                    or IpcTypes.StackFind
                    or IpcTypes.StackSample
                    or IpcTypes.StackCheck
                    or IpcTypes.StackEvaluate
                    or IpcTypes.StackSave
                    or IpcTypes.StackCopy
                    or IpcTypes.StackCheckpoint
                    or IpcTypes.StackPixels
                    or IpcTypes.StackClose
                    or IpcTypes.MaterialThumbnail
                    or IpcTypes.ViewportStack => OnDevice(request),
                IpcTypes.MeshUnwrap => Unwrap(request),
                IpcTypes.MeshMovePaint => MovePaint(request),
                IpcTypes.MeshSameLayout => SameLayout(request),
                IpcTypes.MeshMaterials => Materials(request),
                IpcTypes.MeshParts => Parts(request),
                IpcTypes.MeshFacts => Facts(request),
                IpcTypes.MeshPartMask => PartMask(request),
                IpcTypes.MeshUvWire => UvWires.Answer(request),
                IpcTypes.BakeStart => bakes.Start(request, link),
                IpcTypes.BakeCancel => bakes.Cancel(request),
                IpcTypes.BuildStart => builds.Start(request, link),
                IpcTypes.BuildCancel => builds.Cancel(request),
                _ => IpcMessage.Fail(request.Id, $"Unknown request '{request.Type}'.")
            }
        );

    /// <summary>Uses no device, but this link waits for it: nothing else can be asked while a mesh is cut.</summary>
    static IpcMessage Unwrap(IpcMessage request) {
        var ask = request.MeshUnwrap ?? throw new IpcException("mesh.unwrap names no mesh.");
        try {
            return new() {
                Type = IpcTypes.Ok,
                Id = request.Id,
                MeshUnwrapped = MeshUnwrapRun.Run(ask.Source, ask.Output, ask.Options)
            };
        } catch (Exception e) when (e is ModelFormatException
                                        or IOException
                                        or InvalidOperationException
                                        or ArgumentException) {
            throw new IpcException($"'{Path.GetFileName(ask.Source)}' could not be unwrapped: {e.Message}");
        }
    }

    static IpcMessage Parts(IpcMessage request) {
        var file = request.MeshFile ?? throw new IpcException("mesh.parts names no file.");
        try {
            return new() {
                Type = IpcTypes.Ok,
                Id = request.Id,
                Parts = ViewportGeometry.Parts(file, request.MeshSet ?? string.Empty)
            };
        } catch (Exception e) when (e is ModelFormatException or IOException or UnauthorizedAccessException) {
            throw new IpcException($"'{Path.GetFileName(file)}' would not read: {e.Message}");
        }
    }

    static IpcMessage PartMask(IpcMessage request) {
        var ask = request.MeshPartMask ?? throw new IpcException("mesh.part-mask draws nothing.");
        try {
            MeshPartMask.Draw(ask);
            return IpcMessage.Ok(request.Id);
        } catch (Exception e) when (e is ModelFormatException or IOException or UnauthorizedAccessException) {
            throw new IpcException($"'{Path.GetFileName(ask.Mesh)}' would not read: {e.Message}");
        }
    }

    /// <summary>Uses no device: it reads the file and counts.</summary>
    static IpcMessage Facts(IpcMessage request) {
        var file = request.MeshFile ?? throw new IpcException("mesh.facts names no file.");
        try {
            return new() { Type = IpcTypes.Ok, Id = request.Id, Facts = MeshFactsReader.Read(file) };
        } catch (Exception e) when (e is ModelFormatException or IOException or UnauthorizedAccessException) {
            throw new IpcException($"'{Path.GetFileName(file)}' would not read: {e.Message}");
        }
    }

    static IpcMessage Materials(IpcMessage request) {
        var file = request.MeshFile ?? throw new IpcException("mesh.materials names no file.");
        try {
            return new() { Type = IpcTypes.Ok, Id = request.Id, Materials = ViewportGeometry.Materials(file) };
        } catch (Exception e) when (e is ModelFormatException or IOException or UnauthorizedAccessException) {
            throw new IpcException($"'{Path.GetFileName(file)}' would not read: {e.Message}");
        }
    }

    /// <summary>
    ///     Whether a texture set keeps its layout from one mesh to the other. A set the new mesh lacks, or a file that
    ///     will not read as a mesh with a layout, does not: the reload that follows says why.
    /// </summary>
    IpcMessage SameLayout(IpcMessage request) {
        var ask = request.MeshMovePaint ?? throw new IpcException("mesh.same-layout compares nothing.");
        bool same;
        try {
            same = PaintTransfer.SameLayout(
                meshes.Of(ask.From, ask.FromSet ?? ask.Set, ask.UvSet).Projection,
                meshes.Of(ask.To, ask.Set, ask.UvSet).Projection
            );
        } catch (IpcException) {
            same = false;
        }

        return new() { Type = IpcTypes.Ok, Id = request.Id, SameLayout = same };
    }

    /// <summary>
    ///     Uses no device either, and each file is replaced only when its new paint is whole. A layout that did not
    ///     change leaves the paint alone, since every move resamples it and costs a little sharpness.
    /// </summary>
    IpcMessage MovePaint(IpcMessage request) {
        var ask = request.MeshMovePaint ?? throw new IpcException("mesh.move-paint moves nothing.");
        var from = meshes.Of(ask.From, ask.FromSet ?? ask.Set, ask.UvSet).Projection;
        var to = meshes.Of(ask.To, ask.Set, ask.UvSet).Projection;
        if (PaintTransfer.SameLayout(from, to)) {
            return IpcMessage.Ok(request.Id);
        }

        Directory.CreateDirectory(ask.Backup);
        foreach (var file in ask.Paints) {
            File.Copy(file, Path.Combine(ask.Backup, Path.GetFileName(file)), true);
            PaintCanvas canvas;
            using (var read = SharedFile.OpenRead(file)) {
                canvas = PaintCanvas.Read(read);
            }

            var part = $"{file}.{Guid.NewGuid():N}.part";
            using (var write = File.Create(part)) {
                PaintTransfer.Move(canvas, from, to).Write(write);
            }

            File.Move(part, file, true);
        }

        return IpcMessage.Ok(request.Id);
    }

    /// <summary>What uses the device, in turn with a build's step. Health and bakes never wait for this.</summary>
    IpcMessage OnDevice(IpcMessage request) {
        lock (gpu.Gate) {
            watch.Beat();
            try {
                return Device(request);
            } finally {
                watch.Idle();
            }
        }
    }

    IpcMessage Device(IpcMessage request) =>
        request.Type switch {
            IpcTypes.ViewportStack => stacks.Follow(request),
            IpcTypes.GraphLoad => graphs.Load(request),
            IpcTypes.GraphEvaluate => graphs.Evaluate(request),
            IpcTypes.GraphImage => graphs.Image(request),
            IpcTypes.GraphClose => graphs.Close(request),
            IpcTypes.StackLoad => stacks.Load(request),
            IpcTypes.StackSync => stacks.Sync(request),
            IpcTypes.StackThumbnails => stacks.Thumbnails(request),
            IpcTypes.StackFind => stacks.FindLayer(request),
            IpcTypes.StackSample => stacks.Sample(request),
            IpcTypes.StackCheck => stacks.Check(request),
            IpcTypes.StackEvaluate => stacks.Evaluate(request),
            IpcTypes.StackSave or IpcTypes.StackCheckpoint => stacks.Save(request),
            IpcTypes.StackPixels => stacks.Pixels(request),
            IpcTypes.StackCopy => stacks.Copy(request),
            IpcTypes.MaterialThumbnail => Thumbnail(request),
            _ => stacks.Close(request)
        };

    IpcMessage Thumbnail(IpcMessage request) {
        var ask = request.MaterialThumbnail ?? throw new IpcException("material.thumbnail says nothing to draw.");
        return ask.Kind == MaterialThumbnailKinds.Stack ? stacks.Thumbnail(ask) : graphs.Thumbnail(ask);
    }

    WorkerHealth Health() =>
        new(Stopwatch.GetElapsedTime(Started).TotalSeconds, Environment.WorkingSet, watch.Busy.TotalSeconds);
}
