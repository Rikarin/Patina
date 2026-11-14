using Patina.Contracts;
using Patina.Library;
using System.Buffers.Binary;
using System.Collections.Concurrent;
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
///         by name and every face of the set has a high part of its name, since an unmatched face's rays may strike
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
///         faces, after flattening, and not its corners' numbers, which shift when another part changes. Whether the
///         high mesh carries normals at all is read too, since a mesh without them is struck with flat normals.
///     </para>
/// </remarks>
public static class BakeHighReads {
    /// <summary>How many high meshes' part digests are kept, which is a few bytes each per part.</summary>
    const int Kept = 16;

    static readonly ConcurrentDictionary<string, HighParts> Read = new(StringComparer.Ordinal);

    /// <summary>The revision of what a set reads of the high meshes, or null when it reads them whole.</summary>
    /// <param name="request">The bake of one texture set.</param>
    /// <param name="low">The set's low faces, as the bake reads them.</param>
    /// <returns>The revision, standing for every high file at once, or null.</returns>
    /// <exception cref="ModelFormatException">A high mesh is not a readable model.</exception>
    /// <exception cref="IOException">A high mesh cannot be read.</exception>
    public static Revision? Matched(BakeRequest request, FlatModel low) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(low);
        if (!WithinParts(request.Options)) {
            return null;
        }

        var high = Parts(request.High);
        var rays = request.Options.Rays;
        var match = BakeParts.ByName(
            [..high.Parts.Select(static part => (part.Name, part.Faces))],
            low.Parts,
            BakeRequestSettings.Suffixes(rays.HighSuffix),
            BakeRequestSettings.Suffixes(rays.LowSuffix)
        );
        HashSet<int> matched = [..match.Target];
        HashSet<int> present = [..match.Source];
        if (matched.Any(part => part < 0 || !present.Contains(part))) {
            return null; // a face that matched nothing casts at all of the high mesh
        }

        return Revision.Of(high.Digest(match.Source, matched));
    }

    /// <summary>Whether a bake with these options reads nothing of the high mesh past the parts its own faces match.</summary>
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

    /// <summary>The high meshes' parts and their digests, read once for each set of file contents.</summary>
    static HighParts Parts(IReadOnlyList<string> files) {
        var key = string.Join('|', files.Select(static file => Revision.OfFile(file).Text));
        if (Read.TryGetValue(key, out var known)) {
            return known;
        }

        var parts = HighParts.Of(ModelGeometry.Flatten([..files.Select(static file => MeshMapBakeRun.Model(file))]));
        if (Read.Count >= Kept) {
            Read.Clear();
        }

        return Read.GetOrAdd(key, parts);
    }

    /// <summary>One high part: its name, how many faces it has, and a digest of how the bake meets them.</summary>
    sealed record HighPart(string Name, int Faces, byte[] Digest);

    /// <summary>The high meshes as a set's key reads them.</summary>
    sealed record HighParts(BoundingBox Bounds, bool Normals, IReadOnlyList<HighPart> Parts) {
        public static HighParts Of(FlatModel high) {
            var mesh = high.Mesh;
            var normals = mesh.Normals.Length == mesh.CornerCount;
            List<HighPart> parts = [];
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

                parts.Add(new(name, faces, hash.GetHashAndReset()));
            }

            return new(mesh.Bounds, normals, parts);
        }

        /// <summary>The digest of the box, every part's name, and the matched parts' faces, by source part number.</summary>
        public byte[] Digest(IReadOnlyList<int> sources, HashSet<int> matched) {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Add(hash, Bounds.Minimum);
            Add(hash, Bounds.Maximum);
            Count(hash, Normals ? 1 : 0);
            Count(hash, Parts.Count);
            var face = 0;
            foreach (var part in Parts) {
                Count(hash, part.Name.Length);
                hash.AppendData(MemoryMarshal.AsBytes(part.Name.AsSpan()));
                if (part.Faces > 0 && matched.Contains(sources[face])) {
                    Count(hash, part.Faces);
                    hash.AppendData(part.Digest);
                }

                face += part.Faces;
            }

            return hash.GetHashAndReset();
        }
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
