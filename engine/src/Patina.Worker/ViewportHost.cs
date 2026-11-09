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
        // A material index means nothing on another mesh, so what it wore comes off and is sent again.
        renderer?.ClearLive();
        followed.Clear();
        Renderer().OpenMesh(path);
        return IpcMessage.Ok(request.Id);
    }

    /// <summary>The stacks worn: the one in front and the model's other texture sets.</summary>
    readonly HashSet<string> followed = new(StringComparer.Ordinal);

    public ViewportRenderer Live => Renderer();

    /// <summary>What is under a position on the picture, through the camera asked about. It opens no device.</summary>
    static OrbitCamera Orbit(ViewportCamera camera) =>
        new(
            (float)camera.Yaw,
            (float)camera.Pitch,
            (float)camera.Distance,
            new((float)camera.PanX, (float)camera.PanY, (float)camera.PanZ),
            (float)camera.FieldOfView,
            camera.Orthographic
        );

    /// <summary>The named meshes and materials the viewport draws alone, both empty for the whole model.</summary>
    string[] isolatedParts = [];

    string[] isolatedMaterials = [];

    /// <summary>
    ///     What is under a place on the picture. ⚠ Through what the viewport draws (#136): a part isolated away is not
    ///     there to be clicked, so the ray goes on to what is shown behind it.
    /// </summary>
    public IpcMessage Pick(IpcMessage request) {
        var pick = request.Pick ?? throw new IpcException("viewport.pick says nothing.");
        var shelf = meshes.Of(pick.Mesh);
        var orbit = Orbit(pick.Camera);
        var bounds = shelf.Geometry.Bounds;
        var (right, up, forward) = orbit.Axes();
        var view = new PaintView(
            orbit.Eye(bounds, (float)pick.Width / Math.Max(pick.Height, 1)),
            right,
            up,
            forward,
            orbit.Lens,
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
        var isolated = isolatedParts.Length > 0 || isolatedMaterials.Length > 0;
        var (through, triangles) = isolated
            ? meshes.Shown(pick.Mesh, isolatedParts, isolatedMaterials)
            : (shelf.Projection, null);
        var found = through.TryHit(ray, out var hit);
        var box = shelf.Projection.Bounds;
        IReadOnlyList<float> extent = [..Numbers(box.Minimum), ..Numbers(box.Maximum)];
        return new() {
            Type = IpcTypes.Ok,
            Picked = found
                ? new(
                    true,
                    wire,
                    triangles?[hit.Triangle] ?? hit.Triangle,
                    hit.Coordinate.X,
                    hit.Coordinate.Y,
                    Numbers(hit.Point),
                    extent
                )
                : new(false, wire, Bounds: extent)
        };
    }

    /// <summary>Whether a stack is worn, so its evaluations reach the mesh.</summary>
    public bool Follows(string session) => followed.Contains(session);

    /// <summary>
    ///     Wears these stacks. Going between a model's sets changes nothing on it, and a 4K map costs seconds to send,
    ///     so what stays worn is not sent again. Anything else comes off.
    /// </summary>
    /// <returns>The stacks that have to send themselves.</returns>
    public IReadOnlyList<string> Follow(IReadOnlyCollection<string> sessions) {
        if (!followed.IsSubsetOf(sessions)) {
            renderer?.ClearLive();
            followed.Clear();
        }

        string[] fresh = [..sessions.Where(session => !followed.Contains(session))];
        followed.UnionWith(fresh);
        return fresh;
    }

    string environment = "studio";
    Viewport.ViewportMapFile? picture;

    public IpcMessage SetMaps(IpcMessage request) {
        var maps = request.Maps ?? throw new IpcException("viewport.maps names no maps.");
        Renderer()
            .SetMaps(
                new(
                    File(maps.BaseColor),
                    File(maps.Normal),
                    File(maps.Roughness),
                    File(maps.Metallic),
                    File(maps.Occlusion),
                    File(maps.Emissive),
                    File(maps.Opacity),
                    maps.GreenDown
                )
            );
        return IpcMessage.Ok(request.Id);
    }

    static Viewport.ViewportMapFile? File(Ipc.ViewportMapFile? file) => file is null ? null : new(file.Path, file.Hash);

    /// <summary>
    ///     Lights, shows and finishes frames as the Server says. A file is read once for its hash, so moving a slider
    ///     reads nothing.
    /// </summary>
    public IpcMessage SetDisplay(IpcMessage request) {
        var told = request.Display ?? throw new IpcException("viewport.display says nothing.");
        var settings = told.Display;
        var renderer = Renderer();
        isolatedParts = [..told.Isolation?.Parts ?? []];
        isolatedMaterials = [..told.Isolation?.Materials ?? []];
        renderer.Isolate(isolatedParts, isolatedMaterials);
        SetEnvironment(settings.Environment);
        try {
            SetLook(told.Look?.Path, told.Look?.Hash);
        } catch (FormatException e) {
            throw new IpcException($"'{told.Look?.Path}' is not a viewing table: {e.Message}");
        }

        var shown = File(told.Picture);
        if (shown != picture) {
            try {
                renderer.SetPicture(shown);
                picture = shown;
            } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
                throw new IpcException($"'{shown?.Path}' cannot be shown on the mesh: {e.Message}");
            }
        }

        renderer.Shading = Shading(settings) with {
            DensityTarget = (float)told.TexelDensity, DensityResolution = Math.Max(1, told.Resolution)
        };
        return IpcMessage.Ok(request.Id);
    }

    /// <summary>
    ///     An sRGB <c>#rrggbb</c> as the linear light the frame is cleared to, or black for anything else.
    /// </summary>
    static Vector3 Linear(string colour) {
        var (r, g, b) = ViewportDisplay.Colour(colour) ?? (0, 0, 0);
        return new(Decode(r), Decode(g), Decode(b));

        static float Decode(double c) => (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
    }

    /// <summary>The renderer's settings from the wire's: names become numbers, in the order the lists give.</summary>
    static ViewportShading Shading(ViewportDisplay settings) =>
        new() {
            Bloom = (float)settings.Bloom,
            Warmth = (float)settings.Warmth,
            Tint = (float)settings.Tint,
            BackgroundColour = Linear(settings.BackgroundColor),
            Grid = settings.Grid,
            PictureLane = Math.Max(0, IndexOf(ViewportLanes.All, settings.PictureLane)),
            PictureScale = (float)settings.PictureScale,
            Exposure = (float)settings.Exposure,
            LightRotation = (float)settings.LightRotation,
            EnvironmentRotation = (float)settings.EnvironmentRotation,
            BackgroundBlur = (float)settings.BackgroundBlur,
            BackgroundOpacity = (float)settings.BackgroundOpacity,
            Shadows = (float)settings.Shadows,
            View = (ViewportView)Math.Max(0, IndexOf(ViewportViews.All, settings.View)),
            Wireframe = settings.Wireframe,
            ToneMapping = (ViewportToneMapping)Math.Max(0, IndexOf(ViewportToneMappings.All, settings.ToneMapping)),
            Contrast = (float)settings.Contrast,
            Saturation = (float)settings.Saturation,
            Vignette = (float)settings.Vignette,
            Grain = (float)settings.Grain,
            Aberration = (float)settings.Aberration,
            Sharpen = (float)settings.Sharpen
        };

    static int IndexOf(IReadOnlyList<string> names, string name) {
        for (var at = 0; at < names.Count; at++) {
            if (names[at] == name) {
                return at;
            }
        }

        return -1;
    }

    void SetEnvironment(string source) {
        if (source == environment) {
            return;
        }

        try {
            Renderer().SetEnvironment(source);
            environment = source;
        } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
            throw new IpcException($"'{source}' cannot light the viewport: {e.Message}");
        }
    }

    /// <summary>Views frames through the <c>.cube</c> at a path, or without one.</summary>
    /// <exception cref="FormatException">It is not a 3D table.</exception>
    void SetLook(string? path, string? hash) {
        if (path != lookPath || hash != lookHash) {
            look = path is null ? null : ViewportLook.Parse(SharedFile.ReadAllText(path));
            lookPath = path;
            lookHash = hash;
        }
    }

    static float[] Numbers(Vector3 vector) => [vector.X, vector.Y, vector.Z];

    public IpcMessage SetCamera(IpcMessage request) {
        var next = request.Camera ?? throw new IpcException("viewport.camera has no camera.");
        camera = Orbit(next);
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
        var renderer = Renderer();
        renderer.Render(camera, render.Width, render.Height, new(memory + render.Offset, bytes));
        if (!renderer.Shading.ShowsData) {
            // A table is a look, and a view of stored numbers has none.
            look?.Apply(memory + render.Offset, render.Width, render.Height);
        }

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
