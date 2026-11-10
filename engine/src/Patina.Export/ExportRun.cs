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
public sealed record ExportSet(string Name, IReadOnlyDictionary<string, MapPicture> Maps);

/// <summary>What a delivery put in a folder.</summary>
/// <param name="Files">The files, absolute.</param>
/// <param name="Warnings">What a person should know: a material slot left out, and why.</param>
public sealed record ExportDelivered(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

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
                    ) { Space = texture.Space, Maps = [..texture.Maps.Where(maps.ContainsKey)] }
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
    /// <returns>The files, absolute, and what a person should know of the material files.</returns>
    public static ExportDelivered Deliver(
        string key,
        string folder,
        ExportManifest manifest,
        string target,
        NamingValues values,
        IReadOnlyList<(string From, string Name)>? beside = null,
        ExportPreset? preset = null
    ) {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(values);
        Directory.CreateDirectory(target);
        var before = BuildObjects.Delivered(target);

        var several = manifest.PerSet
            || manifest.Files.Select(static file => file.Set).Distinct(StringComparer.Ordinal).Count() > 1;
        List<string> delivered = [];
        List<(ExportedFile File, string Path)> textures = [];
        foreach (var file in manifest.Files) {
            var name = NamingTemplate.Resolve(
                several ? NamingTemplate.PerSet(file.Template) : file.Template,
                values.Of(file.Set) with { Space = file.Space, Size = Math.Max(file.Width, file.Height) },
                Path.GetExtension(file.File)
            );
            var path = Path.Combine(target, name);
            Copy(Path.Combine(folder, file.File), path);
            delivered.Add(path);
            textures.Add((file, path));
        }

        var materials = preset is { Materials.Count: > 0 }
            ? Materials(preset, textures, target, values, several)
            : new([], []);
        delivered.AddRange(materials.Files);

        foreach (var (from, name) in beside ?? []) {
            var path = Path.Combine(target, name);
            Copy(from, path);
            delivered.Add(path);
        }

        var relative = delivered.Select(path => Path.GetRelativePath(target, path)).ToList();
        foreach (var old in before?.Files.Except(relative, StringComparer.Ordinal) ?? []) {
            // What was delivered is read from a file in the export folder, so it is not trusted to stay inside it.
            File.Delete(LibraryLayout.Beside(target, old));
        }

        LibraryFiles.Replace(
            Path.Combine(target, BuildObjects.DeliveredFile),
            JsonSerializer.SerializeToUtf8Bytes(
                new(key, relative),
                ContractsJsonContext.Default.ExportDelivery
            )
        );
        return new(delivered, materials.Warnings);
    }

    /// <summary>
    ///     Each set's material files, named like <c>{project}</c>, with the set's name when there are several.
    /// </summary>
    static MaterialFiles.Written Materials(
        ExportPreset preset,
        List<(ExportedFile File, string Path)> textures,
        string target,
        NamingValues values,
        bool several
    ) {
        List<string> files = [];
        List<string> warnings = [];
        foreach (var set in textures.GroupBy(static t => t.File.Set, StringComparer.Ordinal)) {
            var template = several ? NamingTemplate.PerSet("{project}") : "{project}";
            var stem = NamingTemplate.Resolve(template, values.Of(set.Key), string.Empty);
            var written = MaterialFiles.Write(preset, [..set], target, stem, values.Of(set.Key).Material);
            files.AddRange(written.Files);
            var named = several ? $"{set.Key}: " : string.Empty;
            foreach (var warning in written.Warnings) {
                warnings.Add(named + warning);
            }
        }

        return new(files, warnings);
    }

    static void Copy(string from, string to) {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        var part = $"{to}.{Guid.NewGuid():N}.part";
        File.Copy(from, part);
        File.Move(part, to, true);
    }
}
