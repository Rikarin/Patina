using Patina.Contracts;
using Patina.Library;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     What a bake reads of its files, where that is less than the bytes: so a mesh change rebakes only the texture sets
///     it touched (#151). <see cref="Patina.Jobs.BakeInputs.KeyOf(BakeRequest, Patina.Library.ToolCatalog, Func{BakeRequest, IReadOnlyDictionary{string, Revision}})" />
///     takes these in place of the bytes.
/// </summary>
/// <remarks>
///     <para>
///         <b>What "touched" means, decided here.</b> A bake is one job per texture set, and each set's maps are cast
///         from its own low faces. So the low mesh is read as the geometry of that set's faces alone, and a set whose
///         faces did not change keeps its key and comes back from the cache, however the rest of the file changed.
///     </para>
///     <para>
///         ⚠ <b>The high meshes and the cage stay keyed by their bytes</b>: any low face's rays may strike any high
///         face, and occlusion reaches across the whole model, so a change to either is a change to every set. That is
///         the full rebake, kept as the answer whenever it is unclear.
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

    /// <summary>Revisions that stand in for bytes or add to them, by role. A file that cannot be read keeps its bytes.</summary>
    /// <param name="request">The bake.</param>
    /// <returns>The low mesh's geometry for the texture set, and the pictures a colour bake reads from materials.</returns>
    public static IReadOnlyDictionary<string, Revision> Of(BakeRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<string, Revision> reads = new(StringComparer.Ordinal);

        // ⚠ A low mesh that is also a high mesh is still keyed by its bytes in that role, which the rays read whole.
        try {
            reads["low"] = Geometry(MeshMapBakeRun.Read(request.Low, request.Material));
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
