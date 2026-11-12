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

    /// <summary>
    ///     Texels per unit of the model against the project's density (<see cref="ProjectCheckSettings" />): green on
    ///     it, blue at half or less, red at twice or more, under a checker of 32 texels that shows stretch.
    /// </summary>
    public const string TexelDensity = "texelDensity";

    /// <summary>Every view, in the order the Worker numbers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Lit, Clay, Unlit, ..Channels, Picture, TexelDensity];
}

/// <summary>The lanes <see cref="ViewportDisplay.PictureLane" /> takes: the colour, or one lane as a grey.</summary>
public static class ViewportLanes {
    /// <summary>Every lane, in the order the Worker numbers them.</summary>
    public static IReadOnlyList<string> All { get; } = ["rgb", "r", "g", "b", "a"];
}

/// <summary>
///     The shapes <see cref="ViewportDisplay.Shape" /> takes: what a material or a graph is seen on when it has no
///     mesh of its own.
/// </summary>
public static class ViewportShapes {
    /// <summary>A ball, with two turns of the picture around it.</summary>
    public const string Sphere = "sphere";

    /// <summary>A flat square facing the front, the picture once across it.</summary>
    public const string Plane = "plane";

    /// <summary>A box, the picture once on each face.</summary>
    public const string Cube = "cube";

    /// <summary>A drum, with the sphere's two turns of the picture around its side.</summary>
    public const string Cylinder = "cylinder";

    /// <summary>Every shape, in the order a list offers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Sphere, Plane, Cube, Cylinder];
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
/// <param name="Bloom">How much light spills past what the curve takes to white, 0 none to 1.</param>
/// <param name="Warmth">
///     Blue towards red, -1 to 1, where zero leaves the picture alone. A grade, not a temperature.
/// </param>
/// <param name="Tint">Green towards magenta, -1 to 1, where zero leaves the picture alone.</param>
/// <param name="BackgroundColor">
///     What shows where the environment does not, as <c>#rrggbb</c> in sRGB: the background fades to it as
///     <paramref name="BackgroundOpacity" /> falls. It is lit by nothing and seen through the same curve as the model.
/// </param>
/// <param name="Grid">Whether a grid is drawn on the floor the model stands on, with the world's x and z axes.</param>
/// <param name="PictureLane">Which lane of the picture shows: one of <see cref="ViewportLanes.All" />.</param>
/// <param name="PictureScale">
///     What the picture's numbers are multiplied by before they are shown, 0.001 to 1000, so a height in the model's
///     units or a position comes into view.
/// </param>
/// <param name="Shape">
///     What a material or a graph with no mesh of its own is seen on: one of <see cref="ViewportShapes.All" />. A model
///     that has a mesh is seen on its mesh whatever this says.
/// </param>
/// <param name="DepthOfField">
///     How shallow the depth of field is, 0 none to 1 a lens wide open. The lens is the same against any model, as if
///     it were a small object close up.
/// </param>
/// <param name="Focus">
///     Where the lens focuses, -1 to 1: zero on the point the camera circles, and each unit a bounding radius nearer
///     (negative) or further (positive).
/// </param>
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
    double Sharpen = 0,
    double Bloom = 0,
    double Warmth = 0,
    double Tint = 0,
    string BackgroundColor = "#000000",
    bool Grid = false,
    string PictureLane = "rgb",
    double PictureScale = 1,
    string Shape = ViewportShapes.Sphere,
    double DepthOfField = 0,
    double Focus = 0
) {
#pragma warning restore SK7005
    /// <summary>
    ///     A colour as <c>#rrggbb</c>, each lane 0 to 1 and still in sRGB, or null for text that is not one.
    /// </summary>
    public static (double R, double G, double B)? Colour(string text) {
        if (text is not ['#', _, _, _, _, _, _]
            || !int.TryParse(
                text.AsSpan(1),
                System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture,
                out var packed
            )) {
            return null;
        }

        return ((packed >> 16 & 0xFF) / 255.0, (packed >> 8 & 0xFF) / 255.0, (packed & 0xFF) / 255.0);
    }

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

        if (!ViewportLanes.All.Contains(PictureLane)) {
            return $"'{PictureLane}' is no lane. Choose one of {string.Join(", ", ViewportLanes.All)}.";
        }

        if (!ViewportShapes.All.Contains(Shape)) {
            return $"'{Shape}' is no shape. Choose one of {string.Join(", ", ViewportShapes.All)}.";
        }

        if (Colour(BackgroundColor) is null) {
            return $"'{BackgroundColor}' is no colour. Write one as #rrggbb.";
        }

        (string Name, double Value, double Low, double High)[] ranges = [
            (nameof(FieldOfView), FieldOfView, 0.1, 2.6), (nameof(Exposure), Exposure, -10, 10),
            (nameof(BackgroundBlur), BackgroundBlur, 0, 1), (nameof(BackgroundOpacity), BackgroundOpacity, 0, 1),
            (nameof(Shadows), Shadows, 0, 1), (nameof(Contrast), Contrast, 0, 2),
            (nameof(Saturation), Saturation, 0, 2),
            (nameof(Vignette), Vignette, 0, 1), (nameof(Grain), Grain, 0, 1), (nameof(Aberration), Aberration, 0, 1),
            (nameof(Sharpen), Sharpen, 0, 1), (nameof(Bloom), Bloom, 0, 1), (nameof(Warmth), Warmth, -1, 1),
            (nameof(Tint), Tint, -1, 1), (nameof(PictureScale), PictureScale, 0.001, 1000),
            (nameof(DepthOfField), DepthOfField, 0, 1), (nameof(Focus), Focus, -1, 1)
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
/// <param name="Bounds">
///     The box the mesh fits inside, in its own space: the least corner's three numbers, then the greatest's. A
///     symmetry's model centre is its middle, and a client draws a symmetry's plane across it.
/// </param>
/// <param name="Normal">
///     The face it met, as a unit direction out of the side the camera sees: three numbers. A decal placed there faces
///     it, and a path drawn through it keeps to faces that turn the same way.
/// </param>
public sealed record ViewportPick(
    bool Found,
    StackView View,
    int Triangle = -1,
    float U = 0f,
    float V = 0f,
    IReadOnlyList<float>? Position = null,
    IReadOnlyList<float>? Bounds = null,
    IReadOnlyList<float>? Normal = null
);

/// <summary>
///     What the viewport draws alone (#136): the named meshes and the materials, which are texture sets, that show. The
///     rest of the model is not drawn and casts no shadow. Both empty is the whole model.
/// </summary>
/// <param name="Parts">Named meshes that show, as the model names them.</param>
/// <param name="Materials">Materials that show, as the model names them: a texture set is one.</param>
public sealed record ViewportIsolation(IReadOnlyList<string> Parts, IReadOnlyList<string> Materials) {
    /// <summary>Whether it hides nothing. Not on the wire: it is what the lists say.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Whole => Parts.Count == 0 && Materials.Count == 0;
}

/// <summary>
///     Asks for the viewport's picture as a PNG on the Server's machine: the camera, display settings and look it has
///     now, at a size of its own. Nothing about it reaches a client's stream.
/// </summary>
/// <param name="Path">Where to write it, ending in <c>.png</c>.</param>
/// <param name="Width">Its width in pixels, or null for the size clients asked for.</param>
/// <param name="Height">Its height in pixels, or null for the size clients asked for.</param>
public sealed record ViewportScreenshotRequest(string Path, int? Width = null, int? Height = null);

/// <summary>A picture of the viewport that is now a file.</summary>
/// <param name="Path">The file.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public sealed record ViewportScreenshot(string Path, int Width, int Height);

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
/// <param name="Map">
///     One of its maps to show alone on the mesh as stored, through the picture view, such as <c>normal</c> or
///     <c>ao</c>; or null for the normal and occlusion maps on the mesh's material (#149).
/// </param>
public sealed record ViewportBakeRequest(string Job, string? Map = null);

/// <summary>
///     Asks the viewport to show a graph on the mesh it is showing, or on the display's shape with none, as it stands
///     now: its Output nodes as a material, or one node's picture alone.
/// </summary>
/// <param name="Graph">The graph's session.</param>
/// <param name="Node">
///     A node whose picture to show as stored, lit by nothing, in place of the material, or null for the Output nodes.
///     The display's picture lane and scale apply to it, and its view, Lit or another, returns when the node is let go.
/// </param>
/// <param name="Port">The node's output, or null for the one its preview shows.</param>
public sealed record ViewportGraphRequest(string Graph, int? Node = null, string? Port = null);

/// <summary>A graph node whose picture the viewport shows in place of a material.</summary>
/// <param name="Graph">The graph's session.</param>
/// <param name="Node">The node.</param>
/// <param name="Port">Its output.</param>
public sealed record ViewportNodeShown(string Graph, int Node, string Port);

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
/// <param name="Isolation">What is drawn alone, or null for the whole model.</param>
/// <param name="Node">
///     The graph node whose picture is shown in place of the material, or null. While it is, the picture view is
///     what the frames show, whatever <see cref="ViewportDisplay.View" /> says.
/// </param>
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
    string? Stack = null,
    ViewportIsolation? Isolation = null,
    ViewportNodeShown? Node = null
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
