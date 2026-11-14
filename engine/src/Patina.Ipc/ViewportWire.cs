using Patina.Contracts;

namespace Patina.Ipc;

/// <summary>A picture on a disk the Worker can read, and the hash of what is in it.</summary>
public sealed record ViewportMapFile(string Path, string Hash);

/// <summary>The pictures laid over the open mesh's materials. A null slot keeps the material's own.</summary>
public sealed record ViewportMapSet(
    ViewportMapFile? BaseColor = null,
    ViewportMapFile? Normal = null,
    ViewportMapFile? Roughness = null,
    ViewportMapFile? Metallic = null,
    ViewportMapFile? Occlusion = null,
    ViewportMapFile? Emissive = null,
    ViewportMapFile? Opacity = null
) {
    /// <summary>Whether normals here point green down, as a graph's do.</summary>
    public bool GreenDown { get; init; }
}

/// <summary>
///     How frames are lit, shown and finished, with the files it names hashed: the viewing table, and the picture the
///     picture view shows.
/// </summary>
/// <param name="Display">The settings as a client gave them.</param>
/// <param name="Look">The <c>.cube</c> table, or null.</param>
/// <param name="Picture">The picture, or null.</param>
/// <param name="Isolation">The parts and materials drawn alone, or null for the whole model.</param>
/// <param name="TexelDensity">
///     The texel density the texel density view measures against, in texels per unit of the model, from the project of
///     the stack worn; zero for none.
/// </param>
/// <param name="Resolution">The side of a map for a material whose own size cannot be read, in texels.</param>
/// <param name="Highlight">What is marked on the model, or null for nothing.</param>
/// <param name="Symmetry">The symmetry whose mirror is drawn where it meets the model, or null for none.</param>
public sealed record ViewportDisplaySet(
    ViewportDisplay Display,
    ViewportMapFile? Look,
    ViewportMapFile? Picture,
    ViewportIsolation? Isolation = null,
    double TexelDensity = 0,
    int Resolution = 2048,
    ViewportHighlight? Highlight = null,
    StackSymmetry? Symmetry = null
);

/// <summary>
///     An orbit around the model and the lens it looks through: radians, a distance in framings, a pan in bounding
///     radii. The lens comes from the display settings, so a pick sees what the picture shows.
/// </summary>
public sealed record ViewportCamera(
    double Yaw,
    double Pitch,
    double Distance,
    double PanX = 0,
    double PanY = 0,
    double PanZ = 0,
    double FieldOfView = 0.7,
    bool Orthographic = false
);

/// <summary>
///     One frame to render, and the slot of the shared-memory file to write it to: RGBA, tightly packed, top row first.
///     The Server owns the file; the Worker maps it, and can map it again.
/// </summary>
public sealed record ViewportRender(int Width, int Height, string SharedMemory, long Offset, long Capacity);

/// <summary>A position on the viewport's picture to find on the mesh.</summary>
/// <param name="X">Across, 0 to 1.</param>
/// <param name="Y">Down, 0 to 1.</param>
/// <param name="Camera">The camera.</param>
/// <param name="Width">The picture's width in the units the reply's view is in.</param>
/// <param name="Height">Its height.</param>
/// <param name="Mesh">The mesh. Only its file is read, so this answers where there is no Vulkan.</param>
/// <param name="UvSets">
///     The coordinate set each worn material is laid out in, by name, where it is not the first (#210): the place on
///     the map a pick says is in its texture set's own layout, which Find Layer, the color picker and the clone source
///     read. Null reads every material in the first.
/// </param>
/// <param name="To">For viewport.marquee, the rectangle's opposite corner; null for a pick.</param>
public sealed record ViewportPickAsk(
    double X,
    double Y,
    ViewportCamera Camera,
    int Width,
    int Height,
    string Mesh,
    IReadOnlyDictionary<string, int>? UvSets = null,
    ViewportCorner? To = null
);

/// <summary>A corner of a rectangle on the viewport's picture.</summary>
/// <param name="X">Across, 0 to 1.</param>
/// <param name="Y">Down, 0 to 1.</param>
public sealed record ViewportCorner(double X, double Y);

/// <summary>Rays along one direction to meet the mesh with (#263).</summary>
/// <param name="Origins">Where each ray starts, in the mesh's own space: three numbers to a ray.</param>
/// <param name="Direction">The way every ray goes: three numbers.</param>
/// <param name="Mesh">The mesh. Only its file is read, as a pick's is.</param>
public sealed record ViewportCastAsk(IReadOnlyList<float> Origins, IReadOnlyList<float> Direction, string Mesh);

/// <summary>A frame that is now in shared memory.</summary>
public sealed record ViewportFrame(int Width, int Height, double RenderMilliseconds);
