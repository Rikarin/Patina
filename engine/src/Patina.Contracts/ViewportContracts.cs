namespace Patina.Contracts;

/// <summary>The viewport's camera: an orbit around the model. Its lens is in <see cref="ViewportDisplay" />.</summary>
/// <param name="Yaw">Radians around the up axis.</param>
/// <param name="Pitch">Radians above the horizon.</param>
/// <param name="Distance">
///     From the point it circles, in framings: at one, the sphere round the model's bounds just fits the picture's
///     narrower side, whatever its shape and the field of view. <see cref="Framed" /> shows the whole model.
/// </param>
/// <param name="PanX">How far that point is off the model's centre along x, in bounding radii.</param>
/// <param name="PanY">The same along y.</param>
/// <param name="PanZ">The same along z.</param>
public sealed record ViewportCameraState(
    double Yaw,
    double Pitch,
    double Distance,
    double PanX = 0,
    double PanY = 0,
    double PanZ = 0
) {
    /// <summary>The distance the viewport opens at, and F goes back to: the whole model with a little room.</summary>
    public const double Framed = 1.05;
}

/// <summary>The views <see cref="ViewportDisplay.View" /> takes.</summary>
public static class ViewportViews {
    /// <summary>The material, lit.</summary>
    public const string Lit = "lit";

    /// <summary>The shape alone, in grey.</summary>
    public const string Clay = "clay";

    /// <summary>Base colour and emission with no light.</summary>
    public const string Unlit = "unlit";

    /// <summary>
    ///     The stored values, one channel each: a pixel of the frame is the number in the map, with no light, curve,
    ///     look or effect.
    /// </summary>
    public static IReadOnlyList<string> Channels { get; } =
        ["baseColor", "roughness", "metallic", "normal", "occlusion", "emissive", "opacity"];

    /// <summary>The file in <see cref="ViewportDisplay.Picture" />, as stored: a bake's mesh map, or a mask.</summary>
    public const string Picture = "picture";

    /// <summary>Every view, in the order the Worker numbers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Lit, Clay, Unlit, ..Channels, Picture];
}

/// <summary>The curves <see cref="ViewportDisplay.ToneMapping" /> takes: Vixen's four, and none.</summary>
public static class ViewportToneMappings {
    /// <summary>Every curve, in the order the Worker numbers them.</summary>
    public static IReadOnlyList<string> All { get; } = ["reinhard", "aces", "agx", "filmic", "none"];
}

/// <summary>
///     How the viewport is lit, shown and finished. None of it reaches a file that is exported; a project keeps it, so
///     it opens as it was left. Angles are radians and exposure is in stops; an effect at zero is off. A field left out
///     of a request is the one the viewport opens with.
/// </summary>
/// <param name="Environment">
///     A built-in room (studio, overcast or sunset), or the path of an <c>.hdr</c> or <c>.exr</c> panorama.
/// </param>
/// <param name="EnvironmentRotation">How far the environment has turned around the model. The light stays.</param>
/// <param name="BackgroundBlur">How blurred the background is, 0 sharp to 1 a haze. Lighting is not blurred.</param>
/// <param name="BackgroundOpacity">How much of the environment shows behind the model, 0 none (black) to 1 all.</param>
/// <param name="LightRotation">How far the light has turned around the model.</param>
/// <param name="Exposure">Stops above or below the picture as it comes.</param>
/// <param name="Shadows">How dark the model's shadow is, 0 none to 1 no direct light.</param>
/// <param name="ToneMapping">One of <see cref="ViewportToneMappings.All" />.</param>
/// <param name="Look">A <c>.cube</c> table the frame is viewed through after the curve, or null.</param>
/// <param name="View">One of <see cref="ViewportViews.All" />.</param>
/// <param name="Picture">The picture the <c>picture</c> view shows, on the Server's machine, or null.</param>
/// <param name="Wireframe">Whether the triangles' edges are drawn over the model.</param>
/// <param name="Orthographic">
///     Whether parallel lines stay parallel. It keeps the size the picture has at the point the camera circles.
/// </param>
/// <param name="FieldOfView">The vertical field of view, from 0.1 to 2.6.</param>
/// <param name="Contrast">Contrast about middle grey, 0 to 2, where one leaves it alone.</param>
/// <param name="Saturation">Colour, 0 grey to 2, where one leaves it alone.</param>
/// <param name="Vignette">How dark the corners go, 0 to 1.</param>
/// <param name="Grain">Film grain, 0 to 1.</param>
/// <param name="Aberration">How far red and blue part towards the edges, 0 to 1.</param>
/// <param name="Sharpen">Contrast-adaptive sharpening, 0 to 1.</param>
// SK7005: a wire record's constructor is its JSON shape, one parameter per field, and a default on each is what makes a
// field left out of a request mean the viewport's own; init-only properties would come through as nothing.
#pragma warning disable SK7005
public sealed record ViewportDisplay(
    string Environment = "studio",
    double EnvironmentRotation = 0,
    double BackgroundBlur = 0.66,
    double BackgroundOpacity = 1,
    double LightRotation = 0,
    double Exposure = 0,
    double Shadows = 1,
    string ToneMapping = "aces",
    string? Look = null,
    string View = ViewportViews.Lit,
    string? Picture = null,
    bool Wireframe = false,
    bool Orthographic = false,
    double FieldOfView = 0.7,
    double Contrast = 1,
    double Saturation = 1,
    double Vignette = 0,
    double Grain = 0,
    double Aberration = 0,
    double Sharpen = 0
) {
#pragma warning restore SK7005
    /// <summary>Why this cannot be shown, or null when it can. Files are checked where they are read.</summary>
    public string? Problem() {
        if (!ViewportViews.All.Contains(View)) {
            return $"'{View}' is no view. Choose one of {string.Join(", ", ViewportViews.All)}.";
        }

        if (!ViewportToneMappings.All.Contains(ToneMapping)) {
            return $"'{ToneMapping}' is no curve. Choose one of {string.Join(", ", ViewportToneMappings.All)}.";
        }

        if (View == ViewportViews.Picture && Picture is null) {
            return "The picture view needs a picture.";
        }

        (string Name, double Value, double Low, double High)[] ranges = [
            (nameof(FieldOfView), FieldOfView, 0.1, 2.6), (nameof(Exposure), Exposure, -10, 10),
            (nameof(BackgroundBlur), BackgroundBlur, 0, 1), (nameof(BackgroundOpacity), BackgroundOpacity, 0, 1),
            (nameof(Shadows), Shadows, 0, 1), (nameof(Contrast), Contrast, 0, 2),
            (nameof(Saturation), Saturation, 0, 2),
            (nameof(Vignette), Vignette, 0, 1), (nameof(Grain), Grain, 0, 1), (nameof(Aberration), Aberration, 0, 1),
            (nameof(Sharpen), Sharpen, 0, 1)
        ];
        foreach (var (name, value, low, high) in ranges) {
            if (!double.IsFinite(value) || value < low || value > high) {
                return $"{name} is {value}, and runs from {low} to {high}.";
            }
        }

        return double.IsFinite(EnvironmentRotation) && double.IsFinite(LightRotation)
            ? null
            : "A rotation is not a number.";
    }
}

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
/// <param name="Emissive">Light the surface gives off.</param>
/// <param name="Opacity">What the surface covers, read from red and shown as cut out or not.</param>
public sealed record ViewportMapPaths(
    string? BaseColor = null,
    string? Normal = null,
    string? Roughness = null,
    string? Metallic = null,
    string? Occlusion = null,
    string? Emissive = null,
    string? Opacity = null
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
/// <param name="Display">How the model is lit, shown and finished.</param>
/// <param name="Frames">How many frames this Server has published.</param>
/// <param name="Error">Why the last command failed, or null.</param>
// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005
public sealed record ViewportState(
    string? Mesh,
    ViewportMapPaths Maps,
    ViewportCameraState Camera,
    int Width,
    int Height,
    double RenderScale,
    long Frames,
    string? Error,
    ViewportDisplay Display,
    string? Stack = null
);
#pragma warning restore SK7005

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
