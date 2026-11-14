using Patina.Contracts;
using Patina.Library;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Vixen.Core.Mathematics;
using Vixen.Geometry.Remeshing;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     What a texture set's bake reads of the high meshes when that is less than every byte (#151, ADR 0008): only
///     the high parts its own parts match by name, when nothing it bakes reaches past them.
/// </summary>
/// <remarks>
///     <para>
///         <b>When a set reads only its matched parts</b>, which ADR 0008's table says option by option: rays match
///         by name and every part of the set has a high part of its name, since an unmatched face's rays may strike
///         all of the high mesh; occlusion, bent normals and thickness, when asked for, stay within the part; and the
///         set asks for no ID, colour or curvature map, no rounded edges and no edges map of the high mesh, and is not
///         tileable. Anything else reads the high meshes whole, by their bytes: the full rebake stays the answer
///         whenever it is unclear.
///     </para>
///     <para>
///         ⚠ <b>The whole high mesh's box is read even then</b>: relative distances are shares of its diagonal, the
///         ground plane stands at its foot, a position map spans it and a curvature radius is a share of it. So is
///         the name of every high part, since a part renamed or added can change what matches.
///     </para>
///     <para>
///         ⚠ <b>Each part is read as the bake meets it</b>: the world position and normal of every corner of its
///         faces, after flattening, and not its corners' numbers, which shift when another part changes. Flattening
///         welds within a file and never across two, so a file read alone gives each part the corners it has among
///         the others.
///     </para>
///     <para>
///         ⚠ <b>Nothing here reads a mesh</b>: the facts come through <see cref="IMeshSetReads" />, which in the
///         Server is what the Worker read (#285). <see cref="HighOf" /> is what the reader runs on a file it read.
///     </para>
/// </remarks>
public static class BakeHighReads {
    /// <summary>The revision of what a set reads of the high meshes, or null when it reads them whole.</summary>
    /// <param name="request">The bake of one texture set.</param>
    /// <param name="meshes">What reads the meshes: the files themselves, or in the Server what the Worker read.</param>
    /// <returns>The revision, standing for every high file at once, or null.</returns>
    public static Revision? Matched(BakeRequest request, IMeshSetReads meshes) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(meshes);
        if (!WithinParts(request.Options)
            || meshes.PartNames(request.Low, request.Material, request.UvSet ?? 0) is not { Count: > 0 } low) {
            return null;
        }

        List<MeshHighRead> files = [];
        foreach (var file in request.High) {
            if (meshes.High(file) is not { } read) {
                return null; // the bake that reads it says why
            }

            files.Add(read);
        }

        var rays = request.Options.Rays;
        var parts = files.SelectMany(static file => file.Parts).ToList();
        var highSuffixes = BakeRequestSettings.Suffixes(rays.HighSuffix);
        var numbers = Numbers(parts, highSuffixes);
        var lowSuffixes = BakeRequestSettings.Suffixes(rays.LowSuffix);
        HashSet<int> matched = [..low.Select(name => numbers.GetValueOrDefault(BakeParts.Key(name, lowSuffixes), -1))];
        HashSet<int> struck = [
            ..parts.Where(static part => part.Faces > 0)
                .Select(part => numbers[BakeParts.Key(part.Name, highSuffixes)])
        ];

        // A part that matched nothing, or only a part with no faces, casts at all of the high mesh.
        return matched.IsSubsetOf(struck)
            ? Revision.Of(Digest(files, parts, numbers, matched, highSuffixes))
            : null;
    }

    /// <summary>Whether a bake with these options reads nothing of the high mesh past its own faces' parts.</summary>
    /// <param name="options">The bake's options.</param>
    /// <returns>Whether narrowing a high-mesh change to the matched parts is safe for it.</returns>
    public static bool WithinParts(BakeOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        var maps = options.Maps;
        bool Asks(string map) => maps.Contains(map, StringComparer.Ordinal);
        var occludes = Asks("ao") || Asks("bent") || Asks("thickness");
        return options.Rays.MatchByName
            && options.Tileable != true
            && !options.Rounding.Enabled
            && (!occludes || options.Occlusion.WithinPart)
            && !Asks("id")
            && !Asks("colour")
            && !Asks("curvature")
            && !(Asks("edges") && options.Edges.Mesh != "low");
    }

    /// <summary>A file read as a bake's high mesh, the way a reader of the file reports it.</summary>
    /// <param name="high">The file, flattened as a bake flattens it.</param>
    /// <returns>Its box, whether it has normals, and each part with a digest of its world corners.</returns>
    public static MeshHighRead HighOf(FlatModel high) {
        ArgumentNullException.ThrowIfNull(high);
        var mesh = high.Mesh;
        var normals = mesh.Normals.Length == mesh.CornerCount;
        List<MeshHighPart> parts = [];
        var face = 0;
        foreach (var (name, faces) in high.Parts) {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            for (var end = face + faces; face < end; face++) {
                var entry = mesh.Faces[face];
                Count(hash, entry.Count);
                Count(hash, entry.Smoothing);
                for (var corner = entry.Start; corner < entry.Start + entry.Count; corner++) {
                    Add(hash, mesh.Positions[mesh.Corners[corner]]);
                    if (normals) {
                        Add(hash, mesh.Normals[corner]);
                    }
                }
            }

            parts.Add(new(name, faces, Convert.ToHexStringLower(hash.GetHashAndReset())));
        }

        var box = mesh.Bounds;
        return new(
            [box.Minimum.X, box.Minimum.Y, box.Minimum.Z],
            [box.Maximum.X, box.Maximum.Y, box.Maximum.Z],
            normals,
            parts
        );
    }

    /// <summary>Each part's number as matching by name gives it, by its name without the high ending.</summary>
    static Dictionary<string, int> Numbers(List<MeshHighPart> parts, string[] suffixes) {
        Dictionary<string, int> numbers = new(StringComparer.Ordinal);
        foreach (var part in parts) {
            numbers.TryAdd(BakeParts.Key(part.Name, suffixes), numbers.Count);
        }

        return numbers;
    }

    /// <summary>The digest of the box, every part's name, and the matched parts' faces.</summary>
    static byte[] Digest(
        List<MeshHighRead> files,
        List<MeshHighPart> parts,
        Dictionary<string, int> numbers,
        HashSet<int> matched,
        string[] suffixes
    ) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var at = 0; at < 3; at++) {
            var (low, high) = (float.PositiveInfinity, float.NegativeInfinity);
            foreach (var file in files) {
                (low, high) = (MathF.Min(low, file.Minimum[at]), MathF.Max(high, file.Maximum[at]));
            }

            hash.AppendData(BitConverter.GetBytes(low));
            hash.AppendData(BitConverter.GetBytes(high));
        }

        foreach (var file in files) {
            Count(hash, file.Normals ? 1 : 0);
        }

        Count(hash, parts.Count);
        foreach (var part in parts) {
            Count(hash, part.Name.Length);
            hash.AppendData(MemoryMarshal.AsBytes(part.Name.AsSpan()));
            if (part.Faces > 0 && matched.Contains(numbers[BakeParts.Key(part.Name, suffixes)])) {
                Count(hash, part.Faces);
                hash.AppendData(Convert.FromHexString(part.Digest));
            }
        }

        return hash.GetHashAndReset();
    }

    static void Count(IncrementalHash hash, int value) {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(number, value);
        hash.AppendData(number);
    }

    static void Add(IncrementalHash hash, Vector3 value) {
        Span<float> numbers = [value.X, value.Y, value.Z];
        hash.AppendData(MemoryMarshal.AsBytes(numbers));
    }
}
