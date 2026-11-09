// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;

namespace Patina.Viewport;

/// <summary>What the viewport shows of the model.</summary>
public enum ViewportView {
    /// <summary>The material, lit by the environment and the light.</summary>
    Lit,

    /// <summary>The surface's shape alone: grey, rough and not metal, with its normal map and occlusion.</summary>
    Clay,

    /// <summary>Base colour and emission with no light at all.</summary>
    Unlit,

    /// <summary>The base colour as stored, so a pixel of the frame is a pixel of the file.</summary>
    BaseColor,

    /// <summary>Roughness as a grey, black smooth and white rough.</summary>
    Roughness,

    /// <summary>Metalness as a grey.</summary>
    Metallic,

    /// <summary>The tangent-space normal map as stored, or flat where there is none.</summary>
    Normal,

    /// <summary>Ambient occlusion as a grey.</summary>
    Occlusion,

    /// <summary>Emission as stored.</summary>
    Emissive,

    /// <summary>Opacity as a grey, with nothing cut out.</summary>
    Opacity,

    /// <summary>A picture set by <see cref="ViewportRenderer.SetPicture" />, as stored: a mesh map or a mask.</summary>
    Picture,

    /// <summary>
    ///     Texels per unit of the model against the project's density: green on it, blue at half or less, red at twice
    ///     or more, under a checker of 32 texels that shows stretch.
    /// </summary>
    TexelDensity
}

/// <summary>The curve that takes the frame's light to the screen: Vixen's four, and a clamp.</summary>
public enum ViewportToneMapping {
    /// <summary>Reinhard's, extended to a white point.</summary>
    Reinhard,

    /// <summary>The ACES filmic fit, which the viewport opens with.</summary>
    Aces,

    /// <summary>Troy Sobotka's AgX, which desaturates bright colours as film does.</summary>
    AgX,

    /// <summary>Hable's filmic curve from Uncharted 2.</summary>
    Filmic,

    /// <summary>No curve: anything above white clips.</summary>
    None
}

/// <summary>
///     How the viewport is lit, shown and finished: none of it reaches a file, and all of it is the Server's to say
///     again to a new Worker. Rotations are radians; exposure is in stops; the post effects are zero when off.
/// </summary>
public sealed record ViewportShading {
    /// <summary>Stops above or below the picture as it comes.</summary>
    public float Exposure { get; init; }

    /// <summary>How far the light has turned around the model.</summary>
    public float LightRotation { get; init; }

    /// <summary>How far the environment has turned around the model, which the light does not follow.</summary>
    public float EnvironmentRotation { get; init; }

    /// <summary>How far down the environment's blur the background is drawn from, 0 sharp to 1 a haze.</summary>
    public float BackgroundBlur { get; init; } = 0.66f;

    /// <summary>How much of the environment shows behind the model, 0 none to 1 all of it.</summary>
    public float BackgroundOpacity { get; init; } = 1f;

    /// <summary>How dark a shadow is, from none at zero to no direct light at one.</summary>
    public float Shadows { get; init; } = 1f;

    /// <summary>What is shown.</summary>
    public ViewportView View { get; init; } = ViewportView.Lit;

    /// <summary>Whether the triangles' edges are drawn over the model.</summary>
    public bool Wireframe { get; init; }

    /// <summary>The curve to the screen.</summary>
    public ViewportToneMapping ToneMapping { get; init; } = ViewportToneMapping.Aces;

    /// <summary>Contrast about middle grey, where one leaves it alone.</summary>
    public float Contrast { get; init; } = 1f;

    /// <summary>Colour, from grey at zero through as it is at one.</summary>
    public float Saturation { get; init; } = 1f;

    /// <summary>How dark the corners go, 0 to 1.</summary>
    public float Vignette { get; init; }

    /// <summary>How much film grain, 0 to 1.</summary>
    public float Grain { get; init; }

    /// <summary>How far red and blue part at the edges, 0 to 1: lateral chromatic aberration.</summary>
    public float Aberration { get; init; }

    /// <summary>Contrast-adaptive sharpening, 0 to 1.</summary>
    public float Sharpen { get; init; }

    /// <summary>How much light spills past what is brighter than white, 0 none to 1.</summary>
    public float Bloom { get; init; }

    /// <summary>Blue towards red, -1 to 1, with zero leaving the frame alone.</summary>
    public float Warmth { get; init; }

    /// <summary>Green towards magenta, -1 to 1, with zero leaving the frame alone.</summary>
    public float Tint { get; init; }

    /// <summary>
    ///     What shows where the environment does not, in the scene's linear light: the background fades to it as its
    ///     opacity falls. It is seen through the same curve as the model, as the environment is.
    /// </summary>
    public Vector3 BackgroundColour { get; init; }

    /// <summary>Whether the floor's grid and the world's axes are drawn.</summary>
    public bool Grid { get; init; }

    /// <summary>Which lane of the picture shows: 0 its colour, then 1 red, 2 green, 3 blue and 4 alpha as greys.</summary>
    public int PictureLane { get; init; }

    /// <summary>What the picture's numbers are multiplied by before they are shown.</summary>
    public float PictureScale { get; init; } = 1f;

    /// <summary>The texel density the texel density view measures against, in texels per unit, or zero for none.</summary>
    public float DensityTarget { get; init; }

    /// <summary>The side of a map, in texels, for a material that wears none whose size can be read.</summary>
    public float DensityResolution { get; init; } = 2048f;

    /// <summary>The view as the forward shader numbers it, in the order of <see cref="ViewportView" />.</summary>
    public float ViewNumber => (float)View;

    /// <summary>Whether this shows a stored value and not a picture, so no curve, look or effect touches it.</summary>
    public bool ShowsData => View is not (ViewportView.Lit or ViewportView.Clay);
}
