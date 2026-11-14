using Patina.Contracts;
using Patina.Export.Materials;
using Patina.Export.Presets;
using Patina.Jobs;
using Patina.Library;
using System.Text.Json;
using Vixen.Core.Imaging;

namespace Patina.Export;

/// <summary>The maps of one texture set.</summary>
/// <param name="Name">Its name, or nothing for a project with no named sets.</param>
/// <param name="Maps">The maps, by name.</param>
public sealed record ExportSet(string Name, IReadOnlyDictionary<string, MapPicture> Maps) {
    /// <summary>The UDIM tile the set's layout sits on, which its files are named by (#20).</summary>
    public int Udim { get; init; } = OneTile.First;
}

/// <summary>What a delivery put in a folder.</summary>
/// <param name="Files">The files, absolute.</param>
/// <param name="Warnings">What a person should know: a material slot left out, and why.</param>
public sealed record ExportDelivered(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

/// <summary>
///     An export of some of a project's texture sets into the folder of all of them: it replaces only these sets'
///     files, and leaves the rest of what was delivered there.
/// </summary>
/// <param name="Sets">The sets it is the export of.</param>
/// <param name="Named">
///     Whether its files take their set's name even when there is only one, as they do for a document exported on its
///     own, so that they never take the name of a file of the whole project.
/// </param>
public sealed record ExportPart(IReadOnlyCollection<string> Sets, bool Named);

/// <summary>
///     A preset from maps to files. The object under an export key holds the files under names of its own, and a
///     project's name goes on when they are delivered, so that two projects with one key share one encode.
/// </summary>
public static class ExportRun {
    /// <summary>The file in an export's object that says what is there.</summary>
    public const string ManifestFile = "export.json";

    /// <summary>The object a key names, if it is whole, which it is whenever its folder exists.</summary>
    public static ExportManifest? Find(string folder) {
        var file = Path.Combine(folder, ManifestFile);
        return File.Exists(file)
            ? JsonSerializer.Deserialize(SharedFile.ReadAllBytes(file), ContractsJsonContext.Default.ExportManifest)
            : null;
    }

    /// <summary>Encodes a project's maps. The folder appears only when it is whole.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="maps">The maps, by name.</param>
    /// <param name="folder">Where the object goes.</param>
    /// <param name="progress">Told how far through it is, and the file it is on.</param>
    /// <param name="cancellationToken">Stops it between blocks.</param>
    public static ExportManifest Run(
        ExportPreset preset,
        IReadOnlyDictionary<string, MapPicture> maps,
        string folder,
        Action<double, string>? progress,
        CancellationToken cancellationToken
    ) =>
        Run(preset, [new(string.Empty, maps)], folder, progress, cancellationToken);

    /// <summary>Encodes every texture set of a project. The folder appears only when it is whole.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="sets">The sets, each of which makes the preset's files.</param>
    /// <param name="folder">Where the object goes.</param>
    /// <param name="progress">Told how far through it is, and the file it is on.</param>
    /// <param name="cancellationToken">Stops it between blocks.</param>
    public static ExportManifest Run(
        ExportPreset preset,
        IReadOnlyList<ExportSet> sets,
        string folder,
        Action<double, string>? progress,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(sets);
        var part = $"{folder}.{Guid.NewGuid():N}.part";
        Directory.CreateDirectory(part);

        List<ExportedFile> files = [];
        List<string> skipped = [];
        List<string> warnings = [];
        var several = sets.Count > 1;
        if (preset.Sets is { } chosen) {
            var missing = chosen.Where(name => sets.All(set => set.Name != name)).ToList();
            if (missing.Count > 0) {
                warnings.Add($"No texture set is called {string.Join(", ", missing)}, so nothing was written for it.");
            }

            sets = [..sets.Where(set => chosen.Contains(set.Name, StringComparer.Ordinal))];
        }

        var namedByTile = preset.Textures.All(static texture => SetMerge.NamesTile(texture.File));
        if (preset.Merge && SetMerge.PerTile(sets, namedByTile, warnings) is { } merged) {
            Unused(preset, sets, warnings);
            sets = merged;
            several = false;
        }

        var steps = Math.Max(1, sets.Sum(set => preset.For(set.Name).Textures.Count));
        var done = 0;
        for (var at = 0; at < sets.Count; at++) {
            var set = sets[at].Name;
            var own = preset.For(set);
            var maps = DerivedMaps.Complete(sets[at].Maps, own.Textures.SelectMany(static t => t.Maps));
            var named = several ? $"{set}: " : string.Empty;
            for (var index = 0; index < own.Textures.Count; index++) {
                cancellationToken.ThrowIfCancellationRequested();
                var texture = own.Textures[index];
                progress?.Invoke(done / (double)steps, named + texture.File);
                done++;
                if (!ChannelPacker.Applies(texture, maps)) {
                    skipped.Add(named + texture.File);
                    continue;
                }

                var picture = Finish(ChannelPacker.Pack(texture, maps, own.Normal), maps, own, texture);
                warnings.AddRange(picture.Warnings.Select(warning => named + warning));
                var (bytes, levels) = ExportEncoder.Encode(
                    picture,
                    texture,
                    texture.Dither ?? own.Dither,
                    cancellationToken
                );
                var extension = ExportFormats.ExtensionOf(texture.Format);
                var name = $"s{at}t{index}{extension}";
                File.WriteAllBytes(Path.Combine(part, name), bytes);
                files.Add(
                    new(
                        texture.File,
                        name,
                        texture.Format,
                        texture.Compression,
                        picture.Width,
                        picture.Height,
                        levels,
                        bytes.LongLength,
                        set
                    ) { Space = texture.Space, Maps = [..texture.Maps.Where(maps.ContainsKey)], Udim = sets[at].Udim }
                );
            }
        }

        progress?.Invoke(1, string.Empty);

        var manifest = new ExportManifest(preset.Id, files, skipped, warnings) { PerSet = several };
        File.WriteAllBytes(
            Path.Combine(part, ManifestFile),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.ExportManifest)
        );
        BuildObjects.Publish(part, folder);
        return manifest;
    }

    /// <summary>
    ///     Names the sets whose own changes a merge did not use (#252): one set of files has one size and one set of
    ///     choices, the export's, and a set's own are said rather than lost quietly.
    /// </summary>
    static void Unused(ExportPreset preset, IReadOnlyList<ExportSet> sets, List<string> warnings) {
        var own = sets.Where(set => preset.PerSet.ContainsKey(set.Name))
            .Select(set => preset.PerSet[set.Name].Size is { } size && size != preset.Size
                    ? $"{set.Name} ({size} px)"
                    : set.Name
            )
            .ToList();
        if (own.Count > 0) {
            warnings.Add(
                "The texture sets were merged into one set of files, written with the export's own settings, so "
                + "what was chosen for "
                + string.Join(", ", own)
                + " alone was not used."
            );
        }
    }

    /// <summary>
    ///     Pads a packed picture from its set's coverage, then changes its size. Padding comes first, at the maps' own
    ///     size, so that a smaller file's edge texels average the islands' colour and not the background's.
    /// </summary>
    public static PackedPicture Finish(
        PackedPicture picture,
        IReadOnlyDictionary<string, MapPicture> maps,
        ExportPreset preset,
        ExportTexture texture
    ) {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(texture);
        var image = new FloatImage(picture.Width, picture.Height, picture.Lanes, picture.Samples);
        var warnings = picture.Warnings;
        var padding = texture.Padding ?? preset.Padding;
        if (padding.Mode != PaddingMode.None && maps.TryGetValue(ExportMaps.Coverage, out var coverage)) {
            var islands = ImageResize.Resize(coverage.Image, image.Width, image.Height, ResizeFilter.Nearest);
            var covered = new bool[image.Width * image.Height];
            for (var at = 0; at < covered.Length; at++) {
                covered[at] = islands.Samples[at * islands.Channels] >= 0.5f;
            }

            ImagePadding.Pad(image, covered, padding.Mode, padding.Distance, [..padding.Colour]);
        }

        // A file's scale is of the side every file is written at, which is the preset's size or else the maps' own.
        var (width, height) = ExportMemory.Sized(preset, texture, image.Width, image.Height);
        if ((width, height) != (image.Width, image.Height)) {
            image = ImageResize.Resize(image, width, height, ExportFilters.Of(preset.Filter));
            if (width > picture.Width || height > picture.Height) {
                warnings = [
                    ..warnings, $"The maps are {picture.Width}×{picture.Height} and were enlarged to {width}×{height}."
                ];
            }
        }

        return new(image.Width, image.Height, image.Channels, image.Samples, warnings);
    }

    /// <summary>
    ///     Puts an export's files where a game can have them, under the names its preset gives. A file that is left
    ///     over from an earlier export and that this one no longer makes is removed, or a game would ship it.
    /// </summary>
    /// <param name="key">The export's key.</param>
    /// <param name="folder">The object's folder.</param>
    /// <param name="manifest">What it holds.</param>
    /// <param name="target">The folder they go to.</param>
    /// <param name="values">What the names are made from.</param>
    /// <param name="beside">
    ///     Other files that go with them, such as a mesh's levels of detail: from, then its
    ///     name.
    /// </param>
    /// <param name="preset">The preset, whose material files are written beside the textures, or null for none.</param>
    /// <param name="part">The sets this export is of, when it is not of the whole project, or null.</param>
    /// <returns>The files, absolute, and what a person should know of the material files.</returns>
    public static ExportDelivered Deliver(
        string key,
        string folder,
        ExportManifest manifest,
        string target,
        NamingValues values,
        IReadOnlyList<(string From, string Name)>? beside = null,
        ExportPreset? preset = null,
        ExportPart? part = null
    ) {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(values);
        Directory.CreateDirectory(target);
        var before = BuildObjects.Delivered(target);

        var several = part is { Named: true }
            || manifest.PerSet
            || manifest.Files.Select(static file => file.Set).Distinct(StringComparer.Ordinal).Count() > 1;
        List<string> delivered = [];
        Dictionary<string, string> sets = new(StringComparer.Ordinal);
        List<(ExportedFile File, string Path)> textures = [];
        foreach (var file in manifest.Files) {
            var name = NamingTemplate.Resolve(
                several ? NamingTemplate.PerSet(file.Template) : file.Template,
                values.Of(file.Set) with {
                    Space = file.Space, Size = Math.Max(file.Width, file.Height), Udim = file.Udim
                },
                Path.GetExtension(file.File)
            );
            var path = Path.Combine(target, name);
            Copy(Path.Combine(folder, file.File), path);
            delivered.Add(path);
            textures.Add((file, path));
            sets[Path.GetRelativePath(target, path)] = file.Set;
        }

        var materials = preset is { Materials.Count: > 0 }
            ? Materials(preset, textures, target, values, several)
            : new([], [], new Dictionary<string, string>());
        delivered.AddRange(materials.Files);
        foreach (var (file, set) in materials.Sets) {
            sets[Path.GetRelativePath(target, file)] = set;
        }

        foreach (var (from, name) in beside ?? []) {
            var path = Path.Combine(target, name);
            Copy(from, path);
            delivered.Add(path);
        }

        var relative = delivered.Select(path => Path.GetRelativePath(target, path)).ToList();
        var placed = before?.Sets ?? new Dictionary<string, string>();
        foreach (var old in before?.Files.Except(relative, StringComparer.Ordinal).ToList() ?? []) {
            var set = placed.GetValueOrDefault(old);
            if (part is not null && !part.Sets.Contains(set ?? Unplaced, StringComparer.Ordinal)) {
                // Another set's file, which this export does not make and does not speak for.
                relative.Add(old);
                if (set is not null) {
                    sets[old] = set;
                }

                continue;
            }

            // What was delivered is read from a file in the export folder, so it is not trusted to stay inside it.
            File.Delete(LibraryLayout.Beside(target, old));
        }

        LibraryFiles.Replace(
            Path.Combine(target, BuildObjects.DeliveredFile),
            JsonSerializer.SerializeToUtf8Bytes(
                new(key, relative) { Sets = sets },
                ContractsJsonContext.Default.ExportDelivery
            )
        );
        return new(delivered, materials.Warnings);
    }

    /// <summary>
    ///     What a file with no set is taken to belong to, which no set is called: a part of a project keeps every file
    ///     it cannot place, and only a whole project's export removes it.
    /// </summary>
    const string Unplaced = "\0";

    /// <summary>
    ///     Each set's material files, named like <c>{project}</c>, with the set's name when there are several.
    /// </summary>
    static SetMaterials Materials(
        ExportPreset preset,
        List<(ExportedFile File, string Path)> textures,
        string target,
        NamingValues values,
        bool several
    ) {
        List<string> files = [];
        List<string> warnings = [];
        Dictionary<string, string> sets = new(StringComparer.Ordinal);
        foreach (var set in textures.GroupBy(static t => t.File.Set, StringComparer.Ordinal)) {
            var template = several ? NamingTemplate.PerSet("{project}") : "{project}";
            var stem = NamingTemplate.Resolve(template, values.Of(set.Key), string.Empty);
            var written = MaterialFiles.Write(preset, [..set], target, stem, values.Of(set.Key).Material);
            files.AddRange(written.Files);
            foreach (var file in written.Files) {
                sets[file] = set.Key;
            }

            var named = several ? $"{set.Key}: " : string.Empty;
            foreach (var warning in written.Warnings) {
                warnings.Add(named + warning);
            }
        }

        return new(files, warnings, sets);
    }

    /// <summary>The material files of every set, what a person should know, and each file's set.</summary>
    sealed record SetMaterials(
        IReadOnlyList<string> Files,
        IReadOnlyList<string> Warnings,
        IReadOnlyDictionary<string, string> Sets
    );

    static void Copy(string from, string to) {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        var part = $"{to}.{Guid.NewGuid():N}.part";
        File.Copy(from, part);
        SharedFile.MoveOver(part, to);
    }
}
