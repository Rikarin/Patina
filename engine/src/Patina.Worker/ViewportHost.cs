using Patina.Ipc;
using Patina.Viewport;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;

namespace Patina.Worker;

/// <summary>
///     The viewport's commands. Everything here is derived: a mesh file read, a device opened, a file mapped. A new Worker
///     starts empty and the Server tells it the mesh and the camera again, so this holds nothing that cannot be rebuilt.
///     The device opens on the first command, so a Worker on a machine with no Vulkan still answers health.
/// </summary>
sealed class ViewportHost(string shaderCache) : IDisposable {
    ViewportRenderer? renderer;
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
