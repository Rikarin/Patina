using Patina.Contracts;
using Patina.Jobs;
using Patina.Library;
using System.Diagnostics;
using System.Globalization;
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

    /// <summary>How many parts the log names one by one before it only counts the rest.</summary>
    const int NamedParts = 40;

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

        var clock = Stopwatch.StartNew();
        List<string> log = [];
        List<string> warnings = [];

        List<ReadModel> files = [];
        foreach (var file in request.High) {
            files.Add(Model(file));
            cancellationToken.ThrowIfCancellationRequested();
        }

        var high = ModelGeometry.Flatten(files);
        var low = Read(request.Low, request.Material, request.UvSet ?? 0);
        cancellationToken.ThrowIfCancellationRequested();

        if (low.Mesh.TexCoords.Length != low.Mesh.CornerCount) {
            throw new InvalidDataException(
                $"'{request.Low}' has no texture coordinates, so there is no atlas to bake into."
            );
        }

        // ⚠ A bake fills the first tile and leaves the rest empty without a word, so a layout past it is refused.
        var named = request.Material is { } set
            ? $"Texture set '{set}' of '{Path.GetFileName(request.Low)}'"
            : $"'{Path.GetFileName(request.Low)}'";
        if (OneTile.Problem(low.Mesh.TexCoords, named) is { } tiles) {
            throw new InvalidDataException(tiles);
        }

        Describe(request, high, low, log);

        var cage = request.Cage is { } path ? Read(path, request.Material, request.UvSet ?? 0).Mesh : null;
        var (source, colours) = Ids(request.Options.Id.Source, high, files, warnings);
        var settings = Settings(request, high, low, cage, threads) with {
            IdSource = source,
            GroupsAreColours = colours,
            Colours = request.Options.Maps.Contains("colour", StringComparer.Ordinal)
                ? BakeColourSources.Of(request, high, files, warnings)
                : null
        };
        if (settings.Parts is not null) {
            Matches(request.Options.Rays, high, low, log);
        }

        progress?.Invoke(ReadingShare);

        var maps = MapBaker.Bake(
            high.Mesh,
            low.Mesh,
            settings,
            fraction => progress?.Invoke(ReadingShare + (1 - ReadingShare) * fraction),
            cancellationToken
        );

        log.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{maps.Covered} texels held a chart, {maps.Missed} of them found no surface and took the nearest "
                + $"point, and the gutter filled {maps.Dilated} more."
            )
        );
        log.Add(string.Create(CultureInfo.InvariantCulture, $"Baked in {clock.Elapsed.TotalSeconds:F1} s."));

        var part = $"{folder}.{Guid.NewGuid():N}.part";
        var name = request.Name ?? Path.GetFileNameWithoutExtension(request.Low);
        var manifest = Write(maps, request, part, name, [..warnings, ..maps.Warnings], log);

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

    /// <summary>Reads a mesh file, keeping only one material's parts, laid out in one of its coordinate sets.</summary>
    /// <param name="path">The file.</param>
    /// <param name="material">The material, or null for the whole mesh.</param>
    /// <param name="uvSet">The coordinate set, by the file's number, a texture set is painted and baked in.</param>
    /// <exception cref="ModelFormatException">It is not a readable model.</exception>
    /// <exception cref="InvalidDataException">
    ///     It has no such material, or a part of it has no such set. A texture set whose material was renamed asks for
    ///     this, and the whole mesh in its place would bake every part into one atlas, overlaps included.
    /// </exception>
    public static FlatModel Read(string path, string? material, int uvSet = 0) {
        var read = Model(path, uvSet != 0);
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

        if (uvSet != 0) {
            LayOut(read, uvSet, Path.GetFileName(path));
        }

        return ModelGeometry.Flatten(read);
    }

    /// <summary>
    ///     Puts a coordinate set in the place of the first on every mesh a part draws, which is all a bake and a
    ///     coverage read look at (#210).
    /// </summary>
    /// <exception cref="InvalidDataException">A part has no such set.</exception>
    internal static void LayOut(ReadModel read, int uvSet, string file) {
        var index = read.Meshes.Select(static (mesh, at) => (mesh.Name, at))
            .ToDictionary(static pair => pair.Name, static pair => pair.at, StringComparer.Ordinal);
        foreach (var name in read.Model.Parts.Select(static part => part.Mesh).Distinct(StringComparer.Ordinal)) {
            var at = index[name];
            var sets = at < read.TexCoordSets.Length ? read.TexCoordSets[at] : [];
            if (uvSet >= sets.Length || sets[uvSet].Length == 0) {
                throw new InvalidDataException(
                    $"'{file}' has no UV set {uvSet + 1} on '{name}', "
                    + $"which has {sets.Count(static s => s.Length > 0)}. "
                    + "Choose a UV set every part of the texture set has."
                );
            }

            read.Meshes[at].TexCoords = sets[uvSet];
        }
    }

    /// <summary>Reads a mesh file as it is, with what a bake and an unwrap need, and every UV set if asked.</summary>
    /// <exception cref="ModelFormatException">It is not a readable model.</exception>
    internal static ReadModel Model(string path, bool allSets = false) =>
        ModelReader.Read(
            SharedFile.ReadAllBytes(path),
            Path.GetExtension(path),
            Path.GetFileNameWithoutExtension(path),
            new ModelReadOptions {
                GenerateTangents = false,
                ImportAnimations = false,
                ImportBlendShapes = false,
                ImportVertexColours = true,
                ImportAllTexCoords = allSets
            },
            sideFiles: SharedFile.Beside(path)
        );

    internal static BakeSettings Settings(
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
            Gutter = options.Gutter,
            Space = options.Space == "object" ? BakeSpace.Object : BakeSpace.Tangent,
            Units = rays.Units == "absolute" ? BakeUnits.Absolute : BakeUnits.Relative,
            SearchRadius = (float)rays.Front,
            RearDistance = (float)rays.Rear,
            IgnoreBackfaces = rays.IgnoreBackfaces,
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
            Rounding = Rounding(options.Rounding),
            Parts = rays.MatchByName
                ? BakeParts.ByName(high.Parts, low.Parts, Suffixes(rays.HighSuffix), Suffixes(rays.LowSuffix))
                : null,
            Threads = threads
        };
    }

    /// <summary>The edges to round, or null when rounding is off.</summary>
    static BakeRounding? Rounding(BakeRoundingOptions rounding) {
        if (!rounding.Enabled) {
            return null;
        }

        var edges = rounding.Edges == "hard" ? RoundedEdges.Hard : RoundedEdges.Angle;
        return new((float)rounding.Width, edges, (float)rounding.Angle);
    }

    /// <summary>A name ending as the matching takes it: one ending, or none for an empty field.</summary>
    internal static string[] Suffixes(string suffix) => suffix.Trim() is { Length: > 0 } trimmed ? [trimmed] : [];

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
                _ => MeshMaps.None
            };
        }

        return maps;
    }

    /// <summary>
    ///     What the ID map labels, relabelling the high mesh's faces when the source is one Vixen does not know: a
    ///     material, its colour, or a named part.
    /// </summary>
    /// <param name="source">The ID source the request names.</param>
    /// <param name="high">The high meshes, flattened. Its face groups are rewritten for some sources.</param>
    /// <param name="files">The files it was flattened from, in order, for each part's material.</param>
    /// <param name="warnings">Where a source the mesh cannot give is said.</param>
    /// <returns>What Vixen labels by, and whether the labels are packed colours.</returns>
    /// <remarks>
    ///     ⚠ <b>Flattening reads vertex colours into the groups when a file has them, and materials otherwise</b>,
    ///     so the groups as they come are only ever one of the two. Every other source writes its own.
    /// </remarks>
    static (BakeIdSource Source, bool Colours) Ids(
        string source,
        FlatModel high,
        IReadOnlyList<ReadModel> files,
        List<string> warnings
    ) {
        switch (source) {
            case "shell":
                return (BakeIdSource.Shells, false);
            case "vertex-colour" when !high.ColouredGroups:
                warnings.Add("The high mesh has no vertex colours, so the ID map holds its connected shells.");
                return (BakeIdSource.Shells, false);
            case "vertex-colour":
                return (BakeIdSource.Groups, true);
            case "material" or "material-colour" or "mesh":
                var colour = source == "material-colour";
                var labels = PartLabels(source, high, files);
                Relabel(high, labels);
                return (BakeIdSource.Groups, colour);
            default:
                return (BakeIdSource.Automatic, high.ColouredGroups);
        }
    }

    /// <summary>A label per part of the flattened mesh, in its order.</summary>
    static List<int> PartLabels(string source, FlatModel high, IReadOnlyList<ReadModel> files) {
        List<int> labels = [];
        var at = 0;
        foreach (var file in files) {
            foreach (var part in file.Model.Parts) {
                labels.Add(
                    source switch {
                        "mesh" => at,
                        "material" => high.Materials[at],
                        _ => ModelGeometry.PackedColour(
                            part.Material >= 0 && part.Material < file.MaterialFactors.Length
                                ? file.MaterialFactors[part.Material].BaseColor
                                : ModelMaterialFactors.Default.BaseColor
                        )
                    }
                );
                at++;
            }
        }

        return labels;
    }

    static void Relabel(FlatModel high, List<int> labels) {
        var face = 0;
        for (var part = 0; part < high.Parts.Count; part++) {
            for (var end = face + high.Parts[part].Faces; face < end; face++) {
                high.Mesh.SetGroup(face, labels[part]);
            }
        }
    }

    /// <summary>Says what was read, so a log shows which meshes a bake used.</summary>
    static void Describe(BakeRequest request, FlatModel high, FlatModel low, List<string> log) {
        log.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"High: {string.Join(", ", request.High.Select(Path.GetFileName))}, {high.Mesh.FaceCount} faces in "
                + $"{high.Parts.Count} part(s)."
            )
        );
        var set = request.Material is { } material ? $", texture set {material}" : string.Empty;
        log.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Low: {Path.GetFileName(request.Low)}{set}, {low.Mesh.FaceCount} faces in {low.Parts.Count} part(s)."
            )
        );
    }

    /// <summary>Says which low part matched which high part by name, and which matched nothing.</summary>
    /// <remarks>
    ///     ⚠ <b>The answer to "why is my bolt baked into the plate"</b>: a low part that matched nothing strikes
    ///     the whole high mesh, and only a log that names it says so.
    /// </remarks>
    static void Matches(BakeRayOptions rays, FlatModel high, FlatModel low, List<string> log) {
        var highSuffixes = Suffixes(rays.HighSuffix);
        var lowSuffixes = Suffixes(rays.LowSuffix);
        var highFaces = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, faces) in high.Parts) {
            var key = BakeParts.Key(name, highSuffixes);
            highFaces[key] = highFaces.GetValueOrDefault(key) + faces;
        }

        var lowFaces = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, faces) in low.Parts) {
            var key = BakeParts.Key(name, lowSuffixes);
            lowFaces[key] = lowFaces.GetValueOrDefault(key) + faces;
        }

        foreach (var (key, faces) in lowFaces.Take(NamedParts)) {
            log.Add(
                highFaces.TryGetValue(key, out var struck)
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"Part {key}: {faces} low faces strike only its {struck} high faces."
                    )
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"Part {key}: matched no high part, so its {faces} low faces strike all of the high mesh."
                    )
            );
        }

        if (lowFaces.Count > NamedParts) {
            log.Add(string.Create(CultureInfo.InvariantCulture, $"And {lowFaces.Count - NamedParts} more low parts."));
        }

        var unmatched = highFaces.Keys.Where(key => !lowFaces.ContainsKey(key)).Order(StringComparer.Ordinal).ToList();
        if (unmatched.Count > 0) {
            log.Add($"High parts no low part matched: {string.Join(", ", unmatched.Take(NamedParts))}.");
        }
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

    /// <summary>Writes the maps the request asked for, and only those, with the manifest that undoes them.</summary>
    /// <remarks>
    ///     ⚠ <b>The normal and the height come out of every bake</b>, because they are the ray every other map is
    ///     measured from. Writing them only when asked is what lets a bake of an AO map leave no normal map behind.
    /// </remarks>
    static BakeManifest Write(
        BakedMaps maps,
        BakeRequest request,
        string folder,
        string name,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> log
    ) {
        Directory.CreateDirectory(folder);

        var wanted = request.Options.Maps.ToHashSet(StringComparer.Ordinal);
        var extension = MeshMapFiles.ExtensionOf(request.Options.Format);
        List<BakedMapFile> files = [];
        foreach (var baked in MeshMapPlanes.Of(maps)) {
            var plane = baked.Kind == MeshMapKind.Colour
                ? BakeColourSources.Encoded(baked, request.Options.Colour)
                : baked;
            var map = MeshMapFiles.NameOf(plane.Kind);
            if (!wanted.Contains(map)) {
                continue;
            }

            var file = $"{map}{extension}";
            File.WriteAllBytes(Path.Combine(folder, file), MeshMapFiles.Encode(plane, request.Options.Format));
            File.WriteAllBytes(Path.Combine(folder, $"{map}.preview.png"), PngCodec.Encode(MeshMapFiles.Rgba8(plane)));
            files.Add(new(map, file, plane.Resolution, plane.Scale));
        }

        var manifest = new BakeManifest(
            name,
            request.Options.Format,
            new(files, maps.Covered, maps.Missed, warnings, false) { Log = log },
            LibraryFiles.Digest(request.Low),
            request.UvSet ?? 0,
            request.Options.Space
        );
        File.WriteAllBytes(
            Path.Combine(folder, BakeCache.ManifestFile),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.BakeManifest)
        );
        return manifest;
    }
}
