using Patina.Contracts;
using Patina.Jobs;
using System.Text.Json;
using Vixen.Core.Imaging;
using Vixen.Geometry;
using Vixen.Geometry.Remeshing;
using Vixen.Interchange;

namespace Patina.Export;

/// <summary>A bake from paths to files, with nothing held anywhere else: the Worker's job and the CLI's verb.</summary>
public static class MeshMapBakeRun {
    /// <summary>The share of the reported fraction that reading the meshes takes.</summary>
    const double ReadingShare = 0.05;

    /// <summary>
    ///     Bakes into a new folder, which appears at <paramref name="folder" /> only when it is whole. A process that
    ///     dies on the way leaves a <c>.part</c> folder and nothing else, and the next attempt starts again.
    /// </summary>
    /// <param name="request">What to bake.</param>
    /// <param name="folder">The folder the result goes to.</param>
    /// <param name="threads">How many threads may cast, or zero for all of them.</param>
    /// <param name="progress">Told how far through it is, from any thread.</param>
    /// <param name="cancellationToken">Stops it between rows.</param>
    /// <returns>What it made, with paths relative to the folder.</returns>
    /// <exception cref="OperationCanceledException">It was stopped.</exception>
    /// <exception cref="ModelFormatException">A mesh is not a readable model.</exception>
    /// <exception cref="InvalidDataException">An input is not what a bake needs.</exception>
    public static BakeManifest Run(
        BakeRequest request,
        string folder,
        int threads,
        Action<double>? progress,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(request);

        var high = Read(request.High);
        cancellationToken.ThrowIfCancellationRequested();
        var low = Read(request.Low, request.Material);
        cancellationToken.ThrowIfCancellationRequested();

        if (low.Mesh.TexCoords.Length != low.Mesh.CornerCount) {
            throw new InvalidDataException(
                $"'{request.Low}' has no texture coordinates, so there is no atlas to bake into."
            );
        }

        var cage = request.Cage is { } path ? Read(path, request.Material).Mesh : null;
        progress?.Invoke(ReadingShare);

        var maps = MapBaker.Bake(
            high.Mesh,
            low.Mesh,
            Settings(request, high, low, cage, threads),
            fraction => progress?.Invoke(ReadingShare + (1 - ReadingShare) * fraction),
            cancellationToken
        );

        var part = $"{folder}.{Guid.NewGuid():N}.part";
        var name = request.Name ?? Path.GetFileNameWithoutExtension(request.Low);
        var manifest = Write(maps, request, part, name);

        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        try {
            Directory.Move(part, folder);
        } catch (IOException) when (Directory.Exists(folder)) {
            // Another attempt got here first, with the same bytes: that is what the key says.
            Directory.Delete(part, true);
        }

        return manifest;
    }

    /// <summary>Reads a mesh file into one mesh, in world space, keeping what a bake needs.</summary>
    /// <exception cref="ModelFormatException">It is not a readable model.</exception>
    public static FlatModel Read(string path) => Read(path, null);

    /// <summary>Reads a mesh file, keeping only one material's parts.</summary>
    /// <param name="path">The file.</param>
    /// <param name="material">The material, or null for the whole mesh.</param>
    /// <exception cref="ModelFormatException">It is not a readable model.</exception>
    /// <exception cref="InvalidDataException">
    ///     It has no such material. A texture set whose material was renamed asks for this, and the whole mesh in its
    ///     place would bake every part into one atlas, overlaps included.
    /// </exception>
    public static FlatModel Read(string path, string? material) {
        var options = new ModelReadOptions {
            GenerateTangents = false, ImportAnimations = false, ImportBlendShapes = false, ImportVertexColours = true
        };
        var read = ModelReader.Read(
            File.ReadAllBytes(path),
            Path.GetExtension(path),
            Path.GetFileNameWithoutExtension(path),
            options
        );
        var names = MaterialNames.Unique(read.Model.Materials);
        if (material is not null) {
            var index = Array.IndexOf(names, material);
            if (index < 0) {
                throw new InvalidDataException(
                    $"'{Path.GetFileName(path)}' has no material '{material}'. Its materials are "
                    + $"{string.Join(", ", names)}. Rename the material back, or reload the mesh."
                );
            }

            read.Model.Parts = [..read.Model.Parts.Where(part => part.Material == index)];
        }

        return ModelGeometry.Flatten(read);
    }

    static BakeSettings Settings(BakeRequest request, FlatModel high, FlatModel low, EditMesh? cage, int threads) =>
        new() {
            Resolution = request.Resolution,
            Gutter = request.Gutter,
            Space = request.Space == "object" ? BakeSpace.Object : BakeSpace.Tangent,
            SearchRadius = (float)request.SearchRadius,
            Maps = Maps(request.Maps),
            OcclusionSamples = request.OcclusionRays,
            OcclusionRadius = (float)request.OcclusionRadius,
            Supersampling = request.Supersampling,
            Cage = cage,
            Skew = (float)request.Skew,
            SkewMap = request.SkewMap is { } map ? SkewMask(map) : null,
            Parts = request.MatchByName ? BakeParts.ByName(high.Parts, low.Parts) : null,
            GroupsAreColours = high.ColouredGroups,
            Threads = threads
        };

    static MeshMaps Maps(IReadOnlyList<string>? names) {
        var maps = MeshMaps.None;
        foreach (var name in names ?? []) {
            maps |= name switch {
                "ao" => MeshMaps.AmbientOcclusion,
                "bent" => MeshMaps.BentNormal,
                "curvature" => MeshMaps.Curvature,
                "thickness" => MeshMaps.Thickness,
                "position" => MeshMaps.Position,
                "world" => MeshMaps.WorldNormal,
                "id" => MeshMaps.Id,
                _ => MeshMaps.None
            };
        }

        return maps;
    }

    /// <summary>A grey image as a mask, its rows the atlas's rows: the first is v = 0, as in every baked map.</summary>
    static BakeMask SkewMask(string path) {
        var picture = MapPictures.ReadFile(path);
        if (picture.Width != picture.Height) {
            throw new InvalidDataException(
                $"The skew map '{path}' is {picture.Width} × {picture.Height}, and the atlas is square."
            );
        }

        var values = new float[picture.Width * picture.Height];
        for (var at = 0; at < values.Length; at++) {
            values[at] = picture.Samples[at * picture.Channels];
        }

        return new(picture.Width, values);
    }

    static BakeManifest Write(BakedMaps maps, BakeRequest request, string folder, string name) {
        Directory.CreateDirectory(folder);

        var extension = MeshMapFiles.ExtensionOf(request.Format);
        List<BakedMapFile> files = [];
        foreach (var plane in MeshMapPlanes.Of(maps)) {
            var map = MeshMapFiles.NameOf(plane.Kind);
            var file = $"{map}{extension}";
            File.WriteAllBytes(Path.Combine(folder, file), MeshMapFiles.Encode(plane, request.Format));
            File.WriteAllBytes(Path.Combine(folder, $"{map}.preview.png"), PngCodec.Encode(MeshMapFiles.Rgba8(plane)));
            files.Add(new(map, file, plane.Resolution, plane.Scale));
        }

        var manifest = new BakeManifest(
            name,
            request.Format,
            new(files, maps.Covered, maps.Missed, maps.Warnings, false)
        );
        File.WriteAllBytes(
            Path.Combine(folder, BakeCache.ManifestFile),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.BakeManifest)
        );
        return manifest;
    }
}
