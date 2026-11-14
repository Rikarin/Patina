using Patina.Contracts;
using Vixen.Geometry;
using Vixen.Geometry.Remeshing;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>A bake request's options as Vixen's bake takes them, for a bake and for its preview of the rays.</summary>
static class BakeRequestSettings {
    /// <summary>The settings Vixen bakes with, from the request and the meshes it read.</summary>
    public static BakeSettings Of(
        BakeRequest request,
        FlatModel high,
        FlatModel low,
        EditMesh? cage,
        int threads
    ) {
        var options = request.Options;
        var rays = options.Rays;
        var occlusion = options.Occlusion;
        return new() {
            Resolution = options.Resolution,
            Height = options.Rows,
            Gutter = options.Gutter,
            Diffusion = options.Diffusion,
            Space = options.Space == "object" ? BakeSpace.Object : BakeSpace.Tangent,
            Units = rays.Units == "absolute" ? BakeUnits.Absolute : BakeUnits.Relative,
            SearchRadius = (float)rays.Front,
            RearDistance = (float)rays.Rear,
            IgnoreBackfaces = rays.IgnoreBackfaces,
            AutomaticCage = rays.AutomaticCage,
            Maps = Maps(options.Maps),
            OcclusionSamples = occlusion.Rays,
            OcclusionRadius = (float)occlusion.Distance,
            OcclusionSpread = (float)occlusion.Spread,
            OcclusionFalloff = occlusion.Falloff switch {
                "linear" => OcclusionFalloff.Linear,
                "smooth" => OcclusionFalloff.Smooth,
                _ => OcclusionFalloff.None
            },
            OcclusionDistribution = occlusion.Distribution == "uniform"
                ? OcclusionDistribution.Uniform
                : OcclusionDistribution.Cosine,
            OcclusionMinimum = (float)occlusion.Minimum,
            OcclusionIgnoresBackfaces = occlusion.IgnoreBackfaces,
            OcclusionWithinPart = occlusion.WithinPart,
            GroundPlane = occlusion.GroundPlane,
            GroundOffset = (float)occlusion.GroundOffset,
            ThicknessNormalised = options.Thickness.Normalised,
            HeightRange = options.Height.Range switch {
                "rays" => HeightRange.RayDistance,
                "custom" => HeightRange.Custom,
                _ => HeightRange.Measured
            },
            HeightScale = (float)options.Height.Scale,
            PositionRange = options.Position.Range switch {
                "cube" => PositionRange.Cube,
                "sphere" => PositionRange.Sphere,
                "none" => PositionRange.None,
                _ => PositionRange.Box
            },
            IdPalette = options.Id.Palette switch {
                "hue" => IdPalette.HueShift,
                "grey" => IdPalette.Grey,
                _ => IdPalette.Random
            },
            Supersampling = options.Supersampling,
            Cage = cage,
            Skew = (float)rays.Skew,
            SkewMap = rays.SkewMap is { Length: > 0 } map ? Mask(map, "skew map") : null,
            DistanceMap = rays.DistanceMap is { Length: > 0 } distance ? Mask(distance, "distance map") : null,
            Rounding = BakeEdgeSettings.Rounding(options.Rounding),
            EdgeMap = BakeEdgeSettings.Marked(options.Edges),
            EdgeMapOf = options.Edges.Mesh == "low" ? BakeMesh.Target : BakeMesh.Source,
            BorderWidth = (float)options.Borders.Width,
            Parts = rays.MatchByName
                ? BakeParts.ByName(high.Parts, low.Parts, Suffixes(rays.HighSuffix), Suffixes(rays.LowSuffix))
                : null,
            Threads = threads
        };
    }

    /// <summary>A name ending as the matching takes it: one ending, or none for an empty field.</summary>
    public static string[] Suffixes(string suffix) => suffix.Trim() is { Length: > 0 } trimmed ? [trimmed] : [];

    static MeshMaps Maps(IReadOnlyList<string> names) {
        var maps = MeshMaps.None;
        foreach (var name in names) {
            maps |= name switch {
                "ao" => MeshMaps.AmbientOcclusion,
                "bent" => MeshMaps.BentNormal,
                "curvature" => MeshMaps.Curvature,
                "thickness" => MeshMaps.Thickness,
                "position" => MeshMaps.Position,
                "world" => MeshMaps.WorldNormal,
                "id" => MeshMaps.Id,
                "colour" => MeshMaps.Colour,
                "opacity" => MeshMaps.Opacity,
                "edges" => MeshMaps.Edges,
                "islands" => MeshMaps.Islands,
                "borders" => MeshMaps.Borders,
                _ => MeshMaps.None
            };
        }

        return maps;
    }

    /// <summary>A grey image as a mask, its rows the atlas's rows: the first is v = 0, as in every baked map.</summary>
    /// <param name="path">The picture, in any format a bake reads.</param>
    /// <param name="what">What the mask is, for the refusal of one that is not square.</param>
    static BakeMask Mask(string path, string what) {
        var picture = MapPictures.ReadFile(path);
        if (picture.Width != picture.Height) {
            throw new InvalidDataException(
                $"The {what} '{path}' is {picture.Width} × {picture.Height}, and the atlas is square."
            );
        }

        var values = new float[picture.Width * picture.Height];
        for (var at = 0; at < values.Length; at++) {
            values[at] = picture.Samples[at * picture.Channels];
        }

        return new(picture.Width, values);
    }
}
