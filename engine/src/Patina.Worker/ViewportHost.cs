using Patina.Contracts;
using Patina.Ipc;
using Patina.Viewport;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Vixen.Core.Mathematics;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     The viewport's commands. Everything here is derived: a mesh file read, a device opened, a file mapped. A new
///     Worker starts empty and the Server tells it the mesh and the camera again, so this holds nothing that cannot be
///     rebuilt. The device opens on the first command, so a Worker on a machine with no Vulkan still answers health.
/// </summary>
sealed class ViewportHost(string shaderCache, MeshShelf meshes) : IDisposable {
    ViewportRenderer? renderer;
    ViewportLook? look;
    string? lookPath;
    string? lookHash;
    OrbitCamera camera = OrbitCamera.Default;
    MemoryMappedFile? frames;
    MemoryMappedViewAccessor? view;
    string? framesPath;
    unsafe byte* memory;

    public void Dispose() {
        Unmap();
        renderer?.Dispose();
    }

    public IpcMessage Open(IpcMessage request) {
        var path = request.Mesh ?? throw new IpcException("viewport.open names no mesh.");
        Renderer().OpenMesh(path);
        return IpcMessage.Ok(request.Id);
    }

    /// <summary>The stack the viewport wears, whose maps <see cref="StackHost" /> puts here.</summary>
    public string? Following { get; private set; }

    public ViewportRenderer Live => Renderer();

    /// <summary>What is under a position on the picture, through the camera asked about. It opens no device.</summary>
    public IpcMessage Pick(IpcMessage request) {
        var pick = request.Pick ?? throw new IpcException("viewport.pick says nothing.");
        var shelf = meshes.Of(pick.Mesh);
        var (yaw, pitch, distance) = (pick.Camera.Yaw, pick.Camera.Pitch, pick.Camera.Distance);
        var orbit = new OrbitCamera((float)yaw, (float)pitch, (float)distance);
        var bounds = shelf.Geometry.Bounds;
        var (right, up, forward) = orbit.Axes(bounds);
        var view = new PaintView(
            orbit.Eye(bounds),
            right,
            up,
            forward,
            OrbitCamera.FieldOfView,
            pick.Width,
            pick.Height
        );
        var wire = new StackView(
            Numbers(view.Eye),
            Numbers(right),
            Numbers(up),
            Numbers(forward),
            view.VerticalFieldOfView,
            view.Width,
            view.Height
        );
        var ray = view.RayAt(new((float)(pick.X * pick.Width), (float)(pick.Y * pick.Height)));
        var found = shelf.Projection.TryHit(ray, out var hit);
        return new() {
            Type = IpcTypes.Ok,
            Picked = found
                ? new(true, wire, hit.Triangle, hit.Coordinate.X, hit.Coordinate.Y, Numbers(hit.Point))
                : new(false, wire)
        };
    }

    /// <summary>Starts or stops wearing a stack. What it wore comes off, and the stack sends itself again.</summary>
    public void Follow(string? session) {
        if (session == Following) {
            return;
        }

        renderer?.ClearLive();
        Following = session;
    }

    public IpcMessage SetMaps(IpcMessage request) {
        var maps = request.Maps ?? throw new IpcException("viewport.maps names no maps.");
        Renderer()
            .SetMaps(
                new(
                    File(maps.BaseColor),
                    File(maps.Normal),
                    File(maps.Roughness),
                    File(maps.Metallic),
                    File(maps.Occlusion)
                )
            );
        try {
            SetLook(maps.Look?.Path, maps.Look?.Hash);
        } catch (FormatException e) {
            throw new IpcException($"'{maps.Look?.Path}' is not a viewing table: {e.Message}");
        }

        return IpcMessage.Ok(request.Id);

        static Viewport.ViewportMapFile? File(Ipc.ViewportMapFile? file) =>
            file is null ? null : new(file.Path, file.Hash);
    }

    /// <summary>Views frames through the <c>.cube</c> at a path, or without one.</summary>
    /// <exception cref="FormatException">It is not a 3D table.</exception>
    void SetLook(string? path, string? hash) {
        if (path != lookPath || hash != lookHash) {
            look = path is null ? null : ViewportLook.Parse(File.ReadAllText(path));
            lookPath = path;
            lookHash = hash;
        }
    }

    static float[] Numbers(Vector3 vector) => [vector.X, vector.Y, vector.Z];

    public IpcMessage SetCamera(IpcMessage request) {
        var next = request.Camera ?? throw new IpcException("viewport.camera has no camera.");
        camera = new((float)next.Yaw, (float)next.Pitch, (float)next.Distance);
        return IpcMessage.Ok(request.Id);
    }

    public unsafe IpcMessage Render(IpcMessage request) {
        var render = request.Render ?? throw new IpcException("viewport.render says nothing to render.");
        var bytes = ViewportRenderer.FrameBytes(render.Width, render.Height);
        if (bytes > render.Capacity) {
            throw new IpcException(
                $"A {render.Width}x{render.Height} frame does not fit its {render.Capacity}-byte slot."
            );
        }

        Map(render.SharedMemory);
        var start = Stopwatch.GetTimestamp();
        Renderer().Render(camera, render.Width, render.Height, new(memory + render.Offset, bytes));
        look?.Apply(memory + render.Offset, render.Width, render.Height);
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return new() { Type = IpcTypes.Ok, Frame = new(render.Width, render.Height, elapsed) };
    }

    ViewportRenderer Renderer() {
        if (renderer is null && !ViewportRenderer.TryCreate(new(shaderCache), out renderer, out var refusal)) {
            throw new IpcException($"No Vulkan device: {refusal}");
        }

        return renderer!;
    }

    unsafe void Map(string path) {
        if (string.Equals(path, framesPath, StringComparison.Ordinal)) {
            return;
        }

        Unmap();
        frames = MemoryMappedFile.CreateFromFile(
            new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete),
            null,
            0,
            MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None,
            false
        );
        view = frames.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        byte* pointer = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        memory = pointer;
        framesPath = path;
    }

    unsafe void Unmap() {
        if (view is not null) {
            view.SafeMemoryMappedViewHandle.ReleasePointer();
            view.Dispose();
            frames?.Dispose();
        }

        memory = null;
        view = null;
        frames = null;
        framesPath = null;
    }
}
