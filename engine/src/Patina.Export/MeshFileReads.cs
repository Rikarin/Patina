using Patina.Contracts;
using Patina.Library;
using System.Collections.Concurrent;
using Vixen.Geometry;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>
///     Reads what <see cref="IMeshSetReads" /> asks from the mesh file itself, as the Worker and the command line do.
///     The Server never uses this: it keeps what the Worker read with <see cref="All" /> (plan §3.1, #285).
/// </summary>
public sealed class MeshFileReads : IMeshSetReads {
    readonly ConcurrentDictionary<(string File, string Set, int UvSet), (long Length, DateTime Written, int? Udim)>
        tiles = new();

    MeshFileReads() { }

    /// <summary>The one reader, which keeps each tile it read until its file changes.</summary>
    public static MeshFileReads Instance { get; } = new();

    /// <inheritdoc />
    public int? Udim(string file, string? material, int uvSet) {
        FileInfo info = new(file);
        if (!info.Exists) {
            return null;
        }

        var key = (file, material ?? string.Empty, uvSet);
        if (tiles.TryGetValue(key, out var known)
            && known.Length == info.Length
            && known.Written == info.LastWriteTimeUtc) {
            return known.Udim;
        }

        int? udim;
        try {
            udim = MeshMapBakeRun.Placed(file, material, uvSet).Udim;
        } catch (Exception e) when (Unreadable(e)) {
            udim = null;
        }

        tiles[key] = (info.Length, info.LastWriteTimeUtc, udim);
        return udim;
    }

    /// <inheritdoc />
    public string? Geometry(string file, string? material, int uvSet) {
        try {
            return BakeKeyReads.Geometry(MeshMapBakeRun.Read(file, material, uvSet)).Text;
        } catch (Exception e) when (Unreadable(e)) {
            return null; // a set that cannot be read in it any more does not fit its bake
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Pictures(string file) {
        try {
            return BakeColourSources.PicturesNamed(MeshMapBakeRun.Model(file), file);
        } catch (Exception e) when (e is ModelFormatException or IOException) {
            return []; // the bake that reads it says why
        }
    }

    /// <summary>
    ///     Everything <see cref="IMeshSetReads" /> asks of a file, in one answer: each material and the whole mesh in
    ///     each UV set, and the pictures. The file is read twice at most, once for the first UV set as a bake reads it
    ///     and once with every set.
    /// </summary>
    /// <param name="file">The mesh file.</param>
    /// <exception cref="ModelFormatException">It is not a readable model.</exception>
    public static MeshSetsRead All(string file) {
        var first = MeshMapBakeRun.Model(file);
        // A material no part wears is one the reader added of its own: no texture set stands on it.
        var unique = MaterialNames.Unique(first.Model.Materials);
        string[] names = [
            ..first.Model.Parts.Select(static part => part.Material)
                .Where(at => at >= 0 && at < unique.Length)
                .Select(at => unique[at])
                .Distinct(StringComparer.Ordinal)
        ];
        List<MeshSetRead> sets = [..Laid(first, file, names, 0)];
        var every = MeshMapBakeRun.Model(file, true);
        for (var uvSet = 1; uvSet < SetsOf(every); uvSet++) {
            sets.AddRange(Laid(every, file, names, uvSet));
        }

        return new(sets, BakeColourSources.PicturesNamed(first, file));
    }

    /// <summary>The whole mesh and each material in one UV set, those that can be laid out in it.</summary>
    static IEnumerable<MeshSetRead> Laid(ReadModel read, string file, string[] names, int uvSet) {
        foreach (var material in names.Prepend(string.Empty).Distinct(StringComparer.Ordinal)) {
            if (Placed(read, file, material.Length == 0 ? null : material, uvSet) is var (model, udim)) {
                yield return new(material, uvSet, udim, BakeKeyReads.Geometry(model).Text);
            }
        }
    }

    /// <summary>One set placed, with the read left as it was for the next.</summary>
    static (FlatModel Model, int Udim)? Placed(ReadModel read, string file, string? material, int uvSet) {
        var parts = read.Model.Parts;
        var coordinates = read.Meshes.Select(static mesh => mesh.TexCoords).ToArray();
        try {
            return MeshMapBakeRun.Placed(read, file, material, uvSet);
        } catch (Exception e) when (Unreadable(e)) {
            return null;
        } finally {
            read.Model.Parts = parts;
            for (var at = 0; at < coordinates.Length; at++) {
                read.Meshes[at].TexCoords = coordinates[at];
            }
        }
    }

    /// <summary>The most UV sets any mesh of the file has before the first it lacks.</summary>
    static int SetsOf(ReadModel read) =>
        read.TexCoordSets.Length == 0
            ? 0
            : read.TexCoordSets.Max(static sets => sets.TakeWhile(static set => set.Length > 0).Count());

    static bool Unreadable(Exception e) => e is ModelFormatException or InvalidDataException or IOException;
}
