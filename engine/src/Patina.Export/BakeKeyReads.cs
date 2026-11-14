using Patina.Contracts;
using Patina.Library;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     What a bake reads of its files, where that is less than the bytes: so a mesh change rebakes only the texture
///     sets it touched (#151). The key in <see cref="Patina.Jobs.BakeInputs" /> takes these in place of the bytes.
/// </summary>
/// <remarks>
///     <para>
///         <b>What "touched" means, decided here.</b> A bake is one job per texture set, and each set's maps are cast
///         from its own low faces. So the low mesh is read as the geometry of that set's faces alone, and a set whose
///         faces did not change keeps its key and comes back from the cache, however the rest of the file changed.
///     </para>
///     <para>
///         ⚠ <b>The high meshes and the cage are keyed by their bytes, unless a set reads only its matched parts</b>:
///         any low face's rays may strike any high face, and occlusion reaches across the whole model, so a change to
///         either is a change to every set. That is the full rebake, kept as the answer whenever it is unclear. When
///         rays match parts by name and nothing the set bakes reaches past them, <see cref="BakeHighReads" /> reads
///         the high meshes as those parts, so a change to another part keeps the set's maps (ADR 0008).
///     </para>
///     <para>
///         ⚠ <b>Not per map and not per region of the atlas.</b> Every map of a set rests on the same ray, so they
///         are made together or not at all; and occlusion rays and the gutter both cross any region a change could be
///         said to have, so a region could only be called untouched by looking at every texel of it again.
///     </para>
/// </remarks>
public static class BakeKeyReads {
    /// <summary>The key of what a request would make, reading each input for what the bake uses of it.</summary>
    /// <param name="request">The bake.</param>
    /// <param name="tools">The tools' versions.</param>
    /// <returns>The key.</returns>
    public static BuildKey KeyOf(BakeRequest request, ToolCatalog tools) => Jobs.BakeInputs.KeyOf(request, tools, Of);

    /// <summary>Revisions standing in for bytes or adding to them, by role. A file not read keeps its bytes.</summary>
    /// <param name="request">The bake.</param>
    /// <returns>The low mesh's geometry for its texture set, and the pictures a colour bake reads.</returns>
    public static IReadOnlyDictionary<string, Revision> Of(BakeRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<string, Revision> reads = new(StringComparer.Ordinal);

        // ⚠ A low mesh that is also a high mesh is still keyed by its bytes in that role, which the rays read whole.
        try {
            var low = MeshMapBakeRun.Read(request.Low, request.Material, request.UvSet ?? 0);
            reads["low"] = Geometry(low);
            if (BakeHighReads.Matched(request, low) is { } high) {
                for (var at = 0; at < request.High.Count; at++) {
                    reads[at == 0 ? "high" : $"high{at}"] = high; // one revision stands for every file, in order
                }
            }
        } catch (Exception e) when (e is ModelFormatException or InvalidDataException or IOException) {
            // The bake refuses it with its own words; the bytes key it until then.
        }

        try {
            var pictures = BakeColourSources.PicturesRead(request);
            for (var at = 0; at < pictures.Count; at++) {
                reads[$"picture{at}"] = Revision.OfFile(pictures[at]);
            }
        } catch (Exception e) when (e is ModelFormatException or IOException) {
            // A high mesh that cannot be read fails the bake, which says so.
        }

        return reads;
    }

    /// <summary>
    ///     The revision of one texture set's faces in a mesh file, as a bake of it records them, or null when the file
    ///     cannot be read so. This is what <see cref="BakeFit.Geometry" /> asks.
    /// </summary>
    /// <param name="file">The mesh file.</param>
    /// <param name="material">The set's material, or null for the whole mesh.</param>
    /// <param name="uvSet">The UV set the faces are laid out in.</param>
    /// <returns>The revision's text, or null.</returns>
    public static string? GeometryOf(string file, string? material, int uvSet) {
        try {
            return Geometry(MeshMapBakeRun.Read(file, material, uvSet)).Text;
        } catch (Exception e) when (e is ModelFormatException or InvalidDataException or IOException) {
            return null; // a set that cannot be read in it any more does not fit its bake
        }
    }

    /// <summary>A revision of everything a bake reads of a mesh it bakes onto, and nothing it does not.</summary>
    /// <param name="model">The mesh, flattened, with only the set's faces.</param>
    /// <returns>The revision.</returns>
    /// <remarks>
    ///     Positions, faces, corner normals and coordinates, smoothing, and the parts' names and sizes, which matching
    ///     by name reads. Not the face groups, which on a low mesh are its materials or colours and which no map of it
    ///     reads, so a recoloured material or a picture renamed in the file keeps every set's maps.
    /// </remarks>
    public static Revision Geometry(FlatModel model) {
        ArgumentNullException.ThrowIfNull(model);
        var mesh = model.Mesh;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        void Count(int value) {
            Span<byte> number = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(number, value);
            hash.AppendData(number);
        }

        Count(mesh.PositionCount);
        hash.AppendData(MemoryMarshal.AsBytes(mesh.Positions));
        Count(mesh.CornerCount);
        hash.AppendData(MemoryMarshal.AsBytes(mesh.Corners));
        Count(mesh.FaceCount);
        foreach (var face in mesh.Faces) {
            Count(face.Start);
            Count(face.Count);
            Count(face.Smoothing);
        }

        Count(mesh.Normals.Length);
        hash.AppendData(MemoryMarshal.AsBytes(mesh.Normals));
        Count(mesh.TexCoords.Length);
        hash.AppendData(MemoryMarshal.AsBytes(mesh.TexCoords));
        Count(model.Parts.Count);
        foreach (var (name, faces) in model.Parts) {
            Count(faces);
            Count(name.Length);
            hash.AppendData(MemoryMarshal.AsBytes(name.AsSpan()));
        }

        return Revision.Of(hash.GetHashAndReset());
    }
}
