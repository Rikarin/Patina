namespace Patina.Contracts;

/// <summary>The viewport's camera: an orbit around the model.</summary>
/// <param name="Yaw">Radians around the up axis.</param>
/// <param name="Pitch">Radians above the horizon.</param>
/// <param name="Distance">From the model's centre, in bounding radii.</param>
public sealed record ViewportCameraState(double Yaw, double Pitch, double Distance);

/// <summary>Asks what is under a position in the viewport.</summary>
/// <param name="X">Across the picture, 0 to 1.</param>
/// <param name="Y">Down it, 0 to 1.</param>
/// <param name="Camera">The camera to look through, or null for the viewport's.</param>
/// <param name="Width">The picture's width in the units a stroke will use, or null for the viewport's.</param>
/// <param name="Height">Its height.</param>
public sealed record ViewportPickRequest(
    double X,
    double Y,
    ViewportCameraState? Camera = null,
    int? Width = null,
    int? Height = null
);

/// <summary>What a position in the viewport landed on.</summary>
/// <param name="Found">Whether it met the mesh.</param>
/// <param name="View">The camera it was looked through, which a stroke on the mesh carries.</param>
/// <param name="Triangle">The triangle.</param>
/// <param name="U">The texture coordinate across.</param>
/// <param name="V">The texture coordinate down.</param>
/// <param name="Position">Where on the mesh, in its own space: three numbers.</param>
public sealed record ViewportPick(
    bool Found,
    StackView View,
    int Triangle = -1,
    float U = 0f,
    float V = 0f,
    IReadOnlyList<float>? Position = null
);

/// <summary>Asks the viewport to wear a stack: its mesh, and its maps as the strokes leave them.</summary>
/// <param name="Stack">The stack session.</param>
public sealed record ViewportStackRequest(string Stack);

/// <summary>Asks the viewport to show a mesh.</summary>
/// <param name="Path">The file's path, on the Server's machine.</param>
public sealed record ViewportMeshRequest(string Path);

/// <summary>
///     Pictures to lay over the mesh's own materials, on every part. Each is a path on the Server's machine, and a slot
///     left null keeps what the material asks for. A filled slot replaces the material's factor too, so what shows
///     is the picture. Roughness is read from green, metallic from blue and occlusion from red, so one packed file or
///     three grey ones both work. PNG, JPEG and EXR are read, base colour as sRGB and the rest as linear.
/// </summary>
/// <param name="BaseColor">The base colour.</param>
/// <param name="Normal">A tangent-space normal map in MikkTSpace, with green up the image.</param>
/// <param name="Roughness">Perceptual roughness.</param>
/// <param name="Metallic">How metallic it is.</param>
/// <param name="Occlusion">Ambient occlusion.</param>
public sealed record ViewportMapPaths(
    string? BaseColor = null,
    string? Normal = null,
    string? Roughness = null,
    string? Metallic = null,
    string? Occlusion = null
);

/// <summary>Asks the viewport to show a finished bake on the mesh it was baked onto.</summary>
/// <param name="Job">The bake's job.</param>
public sealed record ViewportBakeRequest(string Job);

/// <summary>Asks the viewport to lay a graph's Output nodes over the mesh it is showing, as they stand now.</summary>
/// <param name="Graph">The graph's session.</param>
public sealed record ViewportGraphRequest(string Graph);

/// <summary>
///     The reply to <c>GET /api/v1/viewport</c>. The Server owns all of this; a Worker can die without losing it.
/// </summary>
/// <param name="Mesh">The mesh's path, or null when none is open.</param>
/// <param name="Maps">The pictures laid over the mesh's own materials.</param>
/// <param name="Camera">The camera.</param>
/// <param name="Width">The size clients asked for, in pixels.</param>
/// <param name="Height">The size clients asked for, in pixels.</param>
/// <param name="RenderScale">
///     The share of that size frames are rendered at, which drops while a client cannot keep up.
/// </param>
/// <param name="Stack">The stack session it is wearing, or null.</param>
/// <param name="Frames">How many frames this Server has published.</param>
/// <param name="Error">Why the last command failed, or null.</param>
public sealed record ViewportState(
    string? Mesh,
    ViewportMapPaths Maps,
    ViewportCameraState Camera,
    int Width,
    int Height,
    double RenderScale,
    long Frames,
    string? Error,
    string? Stack = null
);

/// <summary>What a client sends over <c>/ws/viewport</c>, as JSON text. Any field may be left out.</summary>
/// <param name="Camera">Where to look.</param>
/// <param name="Width">The size of the client's canvas in pixels; the Server keeps the largest client's.</param>
/// <param name="Height">The size of the client's canvas in pixels.</param>
/// <param name="Input">A number the client picks, echoed in the first frame that includes this input.</param>
/// <param name="Interacting">Whether the user's hand is on the view, which allows a lower render scale.</param>
public sealed record ViewportInput(
    ViewportCameraState? Camera = null,
    int? Width = null,
    int? Height = null,
    uint? Input = null,
    bool? Interacting = null
);
