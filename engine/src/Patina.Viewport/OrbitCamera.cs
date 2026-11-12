// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;

namespace Patina.Viewport;

/// <summary>
///     A camera that circles the model. <see cref="Distance" /> is in framings: at one, the sphere round the model's
///     bounds just fits the picture's narrower side, whatever the picture's shape and the field of view. So the opening
///     view is never cropped, a tall pane shows the model whole, and F is a distance of one.
/// </summary>
/// <param name="Yaw">Radians around the up axis.</param>
/// <param name="Pitch">Radians above the horizon.</param>
/// <param name="Distance">From the point it circles, in framings.</param>
/// <param name="Pan">How far the point it circles has moved off the model's centre, in bounding radii.</param>
/// <param name="FieldOfView">The vertical field of view in radians. An orthographic view keeps its size.</param>
/// <param name="Orthographic">
///     Whether lines that are parallel on the model stay parallel. It is drawn through <see cref="OrthographicLens" />
///     from far off rather than with a parallel projection, so that picking, painting and the picture share one kind
///     of camera: what is in front of the model's centre is about one per cent larger than what is behind it.
/// </param>
public readonly record struct OrbitCamera(
    float Yaw,
    float Pitch,
    float Distance,
    Vector3 Pan = default,
    float FieldOfView = OrbitCamera.DefaultFieldOfView,
    bool Orthographic = false
) {
    /// <summary>The vertical field of view a camera opens with, in radians: about 40°.</summary>
    public const float DefaultFieldOfView = 0.7f;

    /// <summary>The field of view an orthographic view is drawn through, in radians: about half a degree.</summary>
    public const float OrthographicLens = 0.01f;

    /// <summary>How far a camera opens and F goes back to, in framings: the model with room round it.</summary>
    public const float Framed = 1.05f;

    /// <summary>Three-quarters from above: the view a turntable opens on.</summary>
    public static OrbitCamera Default => new(0.6f, 0.35f, Framed);

    /// <summary>The vertical field of view the picture is drawn through.</summary>
    public float Lens => Orthographic ? OrthographicLens : FieldOfView;

    /// <summary>Where the camera sits for a model with these bounds, in a picture of this shape.</summary>
    public Vector3 Eye(BoundingBox bounds, float aspectRatio) =>
        Target(bounds) - Direction * Reach(bounds, aspectRatio);

    /// <summary>The point it looks at and circles.</summary>
    public Vector3 Target(BoundingBox bounds) => bounds.Center + Pan * Radius(bounds);

    /// <summary>The camera's axes: Vixen's look-at, right-handed.</summary>
    public (Vector3 Right, Vector3 Up, Vector3 Forward) Axes() {
        var forward = Direction;
        var right = Vector3.Normalize(Vector3.Cross(forward, Up));
        return (right, Vector3.Cross(right, forward), forward);
    }

    /// <summary>
    ///     Which way is up in the picture: the way the eye moves as the pitch grows. It is the world's up tipped
    ///     towards
    ///     the camera, so the orbit is free over the poles: straight down has an up, and past it the model turns over.
    /// </summary>
    public Vector3 Up {
        get {
            var (sinPitch, cosPitch) = MathF.SinCos(Pitch);
            var (sinYaw, cosYaw) = MathF.SinCos(Yaw);
            return new(-sinPitch * sinYaw, cosPitch, -sinPitch * cosYaw);
        }
    }

    /// <summary>
    ///     How far the eye is from the point it circles, in the model's units. At one framing in perspective the
    ///     bounding sphere's silhouette, whose half-angle is the arcsine of the radius over this, meets the narrower
    ///     half of the field of view. Orthographic keeps the size the picture has at the target and moves back until
    ///     the narrow lens sees that much.
    /// </summary>
    public float Reach(BoundingBox bounds, float aspectRatio) {
        var perspective = Distance * Radius(bounds) / MathF.Sin(Narrower(FieldOfView, aspectRatio));
        return Orthographic
            ? perspective * MathF.Tan(FieldOfView * 0.5f) / MathF.Tan(OrthographicLens * 0.5f)
            : perspective;
    }

    /// <summary>The view-projection matrix for a model with these bounds, on Vixen's reversed depth.</summary>
    public Matrix4x4 ViewProjection(BoundingBox bounds, float aspectRatio) {
        var view = Matrix4x4.LookAt(Eye(bounds, aspectRatio), Target(bounds), Up);
        var (near, far) = Planes(bounds, aspectRatio);
        return view * Matrix4x4.PerspectiveFieldOfView(Lens, aspectRatio, near, far);
    }

    /// <summary>How near and how far the camera sees, round the model and whatever it has panned to.</summary>
    public (float Near, float Far) Planes(BoundingBox bounds, float aspectRatio) {
        var radius = Radius(bounds);
        var reach = Reach(bounds, aspectRatio);
        return (Math.Max(0.01f * radius, reach - (1.5f + Pan.Length()) * radius), reach + (2f + Pan.Length()) * radius);
    }

    /// <summary>
    ///     The same direction of view with the world turned back by the environment's rotation, for the sky: the cube
    ///     it samples turns, and the sky pass takes nothing but this matrix. It keeps the field of view when the model
    ///     is orthographic, since a sky seen through half a degree is one colour.
    /// </summary>
    public Matrix4x4 SkyViewProjection(float aspectRatio, float environmentRotation) {
        var view = Matrix4x4.LookAt(
            Vector3.Zero,
            TurnedBack(Direction, environmentRotation),
            TurnedBack(Up, environmentRotation)
        );
        return view * Matrix4x4.PerspectiveFieldOfView(FieldOfView, aspectRatio, 0.1f, 10f);
    }

    /// <summary>The unit direction the camera looks in.</summary>
    Vector3 Direction {
        get {
            var radial = MathF.Cos(Pitch);
            return -new Vector3(radial * MathF.Sin(Yaw), MathF.Sin(Pitch), radial * MathF.Cos(Yaw));
        }
    }

    /// <summary>Half the angle across the picture's narrower side.</summary>
    static float Narrower(float fieldOfView, float aspectRatio) {
        var half = fieldOfView * 0.5f;
        return MathF.Min(half, MathF.Atan(aspectRatio * MathF.Tan(half)));
    }

    static Vector3 TurnedBack(Vector3 point, float rotation) {
        var (sin, cos) = MathF.SinCos(rotation);
        return new(point.X * cos - point.Z * sin, point.Y, point.Z * cos + point.X * sin);
    }

    static float Radius(BoundingBox bounds) => Math.Max(bounds.Extent.Length(), 1e-4f);
}
