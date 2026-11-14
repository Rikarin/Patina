using Patina.Contracts;
using Patina.Library;
using Vixen.Core.Imaging;
using Vixen.Core.Mathematics;
using Vixen.Geometry.Remeshing;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     Where a colour map's colour comes from: the high mesh's vertex colours, its materials, or a picture carried from
///     the high mesh's layout to the low mesh's (#45). Vixen carries numbers; what they mean is decided here.
/// </summary>
/// <remarks>
///     ⚠ <b>Colour is carried in linear light and written in sRGB</b>, as a base colour picture is stored, so blending
///     between texels happens where it is right. A picture carried as it is (<c>texture</c>) is never converted: it may
///     be a roughness map moved to a new layout, and its numbers are its meaning.
/// </remarks>
public static class BakeColourSources {
    /// <summary>The colours a bake carries across, as the request's colour source says.</summary>
    /// <param name="request">The bake.</param>
    /// <param name="high">The high meshes, flattened.</param>
    /// <param name="files">The files they were flattened from, in the order of the request's high meshes.</param>
    /// <param name="warnings">Where a source the mesh cannot give is said.</param>
    /// <returns>The colours, or null for none, which bakes white.</returns>
    /// <exception cref="InvalidDataException">The picture to carry is not one this reads.</exception>
    public static BakeColours? Of(
        BakeRequest request,
        FlatModel high,
        IReadOnlyList<ReadModel> files,
        List<string> warnings
    ) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(high);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(warnings);

        var options = request.Options.Colour;
        switch (options.Source) {
            case "texture":
                return new(null, [Image(MapPictures.ReadFile(options.Texture), false)]);
            case "vertex-colour" when high.Colours.Count != high.Mesh.CornerCount:
                warnings.Add("The high mesh has no vertex colours, so the colour map is white.");
                return null;
            case "vertex-colour":
                return new(high.Colours, []);
            case "auto" when high.Colours.Count == high.Mesh.CornerCount && high.Colours.Count > 0:
                return new(high.Colours, []);
            default:
                return Materials(request, high, files, warnings);
        }
    }

    /// <summary>Each face's material: its base colour on every corner, and its base colour picture.</summary>
    static BakeColours Materials(
        BakeRequest request,
        FlatModel high,
        IReadOnlyList<ReadModel> files,
        List<string> warnings
    ) {
        var corners = new Vector4[high.Mesh.CornerCount];
        var faceImages = new int[high.Mesh.FaceCount];
        var (images, read) = Pictures(request, files, warnings);
        var part = 0;
        var face = 0;
        for (var at = 0; at < files.Count; at++) {
            var file = files[at];
            foreach (var each in file.Model.Parts) {
                var material = each.Material;
                var colour = material >= 0 && material < file.MaterialFactors.Length
                    ? file.MaterialFactors[material].BaseColor
                    : ModelMaterialFactors.Default.BaseColor;
                var image = read[(at, material)];
                for (var end = face + high.Parts[part].Faces; face < end; face++) {
                    faceImages[face] = image;
                    var entry = high.Mesh.Faces[face];
                    corners.AsSpan(entry.Start, entry.Count).Fill(colour);
                }

                part++;
            }
        }

        return new(corners, images, faceImages);
    }

    /// <summary>
    ///     Each material's base colour picture that a part uses, read once however many parts use it, and which of them
    ///     each file's material reads, <c>-1</c> for none.
    /// </summary>
    static (List<BakeImage> Images, Dictionary<(int File, int Material), int> Read) Pictures(
        BakeRequest request,
        IReadOnlyList<ReadModel> files,
        List<string> warnings
    ) {
        List<BakeImage> images = [];
        Dictionary<(int File, int Material), int> read = [];
        for (var at = 0; at < files.Count; at++) {
            foreach (var material in files[at].Model.Parts.Select(static part => part.Material).Distinct()) {
                var picture = Picture(files[at], material, request.High[at], warnings);
                read[(at, material)] = picture is null ? -1 : images.Count;
                if (picture is not null) {
                    images.Add(picture);
                }
            }
        }

        return (images, read);
    }

    /// <summary>
    ///     A material's base colour picture in linear light, or null when it names none or it cannot be
    ///     read.
    /// </summary>
    static BakeImage? Picture(ReadModel file, int material, string path, List<string> warnings) {
        if (material < 0
            || material >= file.MaterialTextures.Length
            || file.MaterialTextures[material].BaseColor is not { } texture) {
            return null;
        }

        try {
            var bytes = texture.Embedded ?? SharedFile.ReadAllBytes(Resolved(texture, path)!);
            return Image(MapPictures.Decode(bytes, texture.Extension), true);
        } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
            warnings.Add($"A base colour picture of '{Path.GetFileName(path)}' could not be read: {e.Message}");
            return null;
        }
    }

    /// <summary>Where a picture a model names lies, or null when it is inside the model.</summary>
    static string? Resolved(ModelTexture texture, string model) =>
        texture.Path is { } relative
            ? Path.GetFullPath(relative, Path.GetDirectoryName(Path.GetFullPath(model))!)
            : null;

    /// <summary>
    ///     The pictures beside the high meshes that a colour bake from materials reads, so a key changes when one does.
    /// </summary>
    /// <param name="request">The bake.</param>
    /// <param name="meshes">What reads the high meshes for the pictures they name.</param>
    /// <returns>The pictures that are there, by path.</returns>
    /// <remarks>A request that bakes no colour, or carries a picture or vertex colours, reads none of them.</remarks>
    public static IReadOnlyList<string> PicturesRead(BakeRequest request, IMeshSetReads meshes) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(meshes);
        if (!request.Options.Maps.Contains("colour", StringComparer.Ordinal)
            || request.Options.Colour.Source is "texture" or "vertex-colour") {
            return [];
        }

        return [
            ..request.High.SelectMany(meshes.Pictures).Where(File.Exists).Distinct(StringComparer.Ordinal)
        ];
    }

    /// <summary>The base colour pictures a model's materials name beside its file, there or not.</summary>
    /// <param name="file">The model, read.</param>
    /// <param name="path">Where it was read from.</param>
    public static IReadOnlyList<string> PicturesNamed(ReadModel file, string path) {
        ArgumentNullException.ThrowIfNull(file);
        List<string> pictures = [];
        foreach (var textures in file.MaterialTextures) {
            if (textures.BaseColor is { } texture && Resolved(texture, path) is { } picture) {
                pictures.Add(picture);
            }
        }

        return pictures;
    }

    /// <summary>A decoded picture as the bake reads it, from sRGB to linear light when it is a colour.</summary>
    static BakeImage Image(FloatImage picture, bool srgb) {
        var pixels = new Vector4[picture.Width * picture.Height];
        var channels = picture.Channels;
        for (var at = 0; at < pixels.Length; at++) {
            var samples = picture.Samples.AsSpan(at * channels, channels);
            var pixel = channels switch {
                1 => new Vector4(samples[0], samples[0], samples[0], 1f),
                2 => new Vector4(samples[0], samples[0], samples[0], samples[1]),
                3 => new Vector4(samples[0], samples[1], samples[2], 1f),
                _ => new Vector4(samples[0], samples[1], samples[2], samples[3])
            };
            pixels[at] = srgb ? new(Linear(pixel.X), Linear(pixel.Y), Linear(pixel.Z), pixel.W) : pixel;
        }

        return new(picture.Width, picture.Height, pixels);
    }

    /// <summary>The colour map as it is written: in sRGB, unless its numbers were carried as they are.</summary>
    /// <param name="plane">The baked colour, in linear light.</param>
    /// <param name="options">Where it came from.</param>
    /// <returns>The plane to write.</returns>
    public static MeshMapPlane Encoded(MeshMapPlane plane, BakeColourOptions options) {
        ArgumentNullException.ThrowIfNull(plane);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Source == "texture") {
            return plane;
        }

        var samples = new float[plane.Samples.Length];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = Srgb(plane.Samples[at]);
        }

        return plane with { Samples = samples };
    }

    static float Linear(float value) => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

    static float Srgb(float value) {
        var clamped = Math.Clamp(value, 0f, 1f);
        return clamped <= 0.0031308f ? clamped * 12.92f : 1.055f * MathF.Pow(clamped, 1f / 2.4f) - 0.055f;
    }
}
