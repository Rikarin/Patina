// SPDX-License-Identifier: GPL-3.0-or-later

using Vixen.Core.Mathematics;

namespace Patina.Viewport;

/// <summary>
///     A camera that circles the model. <see cref="Distance" /> is in multiples of the model's bounding radius, so one
///     camera suits a ring and a cathedral.
/// </summary>
/// <param name="Yaw">Radians around the up axis.</param>
/// <param name="Pitch">Radians above the horizon.</param>
/// <param name="Distance">From the model's centre, in bounding radii.</param>
/// <param name="Pan">How far the point it circles has moved off the model's centre, in bounding radii.</param>
/// <param name="Exposure">How bright the picture is, where one is as it comes.</param>
/// <param name="LightRotation">How far the light has turned around the model, in radians.</param>
public readonly record struct OrbitCamera(
    float Yaw,
    float Pitch,
    float Distance,
    Vector3 Pan = default,
    float Exposure = 1f,
    float LightRotation = 0f
) {
    /// <summary>The vertical field of view, in radians.</summary>
    public const float FieldOfView = 0.7f;

    /// <summary>Three-quarters from above: the view a turntable opens on.</summary>
    public static OrbitCamera Default => new(0.6f, 0.35f, 2.2f);

    /// <summary>Where the camera sits for a model with these bounds.</summary>
    public Vector3 Eye(BoundingBox bounds) {
        var radial = MathF.Cos(Pitch);
        var direction = new Vector3(radial * MathF.Sin(Yaw), MathF.Sin(Pitch), radial * MathF.Cos(Yaw));
        return Target(bounds) + direction * (Distance * Radius(bounds));
    }

    /// <summary>The point it looks at and circles.</summary>
    public Vector3 Target(BoundingBox bounds) => bounds.Center + Pan * Radius(bounds);

    /// <summary>The camera's axes for a model with these bounds: Vixen's look-at, right-handed.</summary>
    public (Vector3 Right, Vector3 Up, Vector3 Forward) Axes(BoundingBox bounds) {
        var forward = Vector3.Normalize(Target(bounds) - Eye(bounds));
        var right = Vector3.Normalize(Vector3.Cross(forward, new(0f, 1f, 0f)));
        return (right, Vector3.Cross(right, forward), forward);
    }

    /// <summary>The view-projection matrix for a model with these bounds, on Vixen's reversed depth.</summary>
    public Matrix4x4 ViewProjection(BoundingBox bounds, float aspectRatio) {
        var radius = Radius(bounds);
        var view = Matrix4x4.LookAt(Eye(bounds), Target(bounds), new(0f, 1f, 0f));
        var near = Math.Max(0.01f * radius, (Distance - 1.5f) * radius);
        var far = (Distance + 2f + Pan.Length()) * radius;
        return view * Matrix4x4.PerspectiveFieldOfView(FieldOfView, aspectRatio, near, far);
    }

    static float Radius(BoundingBox bounds) => Math.Max(bounds.Extent.Length(), 1e-4f);
}
