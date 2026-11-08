using Patina.Contracts;
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
            ? JsonSerializer.Deserialize(File.ReadAllBytes(file), ContractsJsonContext.Default.ExportManifest)
            : null;
    }

    /// <summary>Encodes a project's maps. The folder appears only when it is whole.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="maps">The maps, by name.</param>
    /// <param name="folder">Where the object goes.</param>
    /// <param name="progress">Told how far through it is.</param>
    /// <param name="cancellationToken">Stops it between blocks.</param>
    public static ExportManifest Run(
        ExportPreset preset,
        IReadOnlyDictionary<string, MapPicture> maps,
        string folder,
        Action<double>? progress,
        CancellationToken cancellationToken
    ) =>
        Run(preset, [new(string.Empty, maps)], folder, progress, cancellationToken);

    /// <summary>Encodes every texture set of a project. The folder appears only when it is whole.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="sets">The sets, each of which makes the preset's files.</param>
    /// <param name="folder">Where the object goes.</param>
    /// <param name="progress">Told how far through it is.</param>
    /// <param name="cancellationToken">Stops it between blocks.</param>
    public static ExportManifest Run(
        ExportPreset preset,
        IReadOnlyList<ExportSet> sets,
        string folder,
        Action<double>? progress,
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

        var steps = Math.Max(1, preset.Textures.Count * sets.Count);
        for (var at = 0; at < sets.Count; at++) {
            var (set, maps) = (sets[at].Name, sets[at].Maps);
            var named = several ? $"{set}: " : string.Empty;
            for (var index = 0; index < preset.Textures.Count; index++) {
                cancellationToken.ThrowIfCancellationRequested();
                var texture = preset.Textures[index];
                if (!ChannelPacker.Applies(texture, maps)) {
                    skipped.Add(named + texture.File);
                    continue;
                }

                var picture = Finish(ChannelPacker.Pack(texture, maps, preset.Normal), maps, preset);
                warnings.AddRange(picture.Warnings.Select(warning => named + warning));
                var (bytes, levels) = ExportEncoder.Encode(picture, texture, preset.Dither, cancellationToken);
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
                    )
                );
                progress?.Invoke((at * preset.Textures.Count + index + 1.0) / steps);
            }
        }

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
    static PackedPicture Finish(
        PackedPicture picture,
        IReadOnlyDictionary<string, MapPicture> maps,
        ExportPreset preset
    ) {
        var image = new FloatImage(picture.Width, picture.Height, picture.Lanes, picture.Samples);
        var warnings = picture.Warnings;
        if (preset.Padding.Mode != PaddingMode.None && maps.TryGetValue(ExportMaps.Coverage, out var coverage)) {
            var islands = ImageResize.Resize(coverage.Image, image.Width, image.Height, ResizeFilter.Nearest);
            var covered = new bool[image.Width * image.Height];
            for (var at = 0; at < covered.Length; at++) {
                covered[at] = islands.Samples[at * islands.Channels] >= 0.5f;
            }

            ImagePadding.Pad(
                image,
                covered,
                preset.Padding.Mode,
                preset.Padding.Distance,
                [..preset.Padding.Colour]
            );
        }

        if (preset.Size is { } side && side != Math.Max(image.Width, image.Height)) {
            var scale = side / (double)Math.Max(image.Width, image.Height);
            var (width, height) = (
                Math.Max(1, (int)Math.Round(image.Width * scale)),
                Math.Max(1, (int)Math.Round(image.Height * scale))
            );
            image = ImageResize.Resize(image, width, height, ExportFilters.Of(preset.Filter));
            if (scale > 1) {
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
    /// <returns>The files, absolute.</returns>
    public static IReadOnlyList<string> Deliver(
        string key,
        string folder,
        ExportManifest manifest,
        string target,
        NamingValues values,
        IReadOnlyList<(string From, string Name)>? beside = null
    ) {
        ArgumentNullException.ThrowIfNull(manifest);
        Directory.CreateDirectory(target);
        var before = BuildObjects.Delivered(target);

        var several = manifest.PerSet
            || manifest.Files.Select(static file => file.Set).Distinct(StringComparer.Ordinal).Count() > 1;
        List<string> delivered = [];
        foreach (var file in manifest.Files) {
            var name = NamingTemplate.Resolve(
                several ? NamingTemplate.PerSet(file.Template) : file.Template,
                values with { Set = file.Set },
                Path.GetExtension(file.File)
            );
            var path = Path.Combine(target, name);
            Copy(Path.Combine(folder, file.File), path);
            delivered.Add(path);
        }

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
        return delivered;
    }

    static void Copy(string from, string to) {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        var part = $"{to}.{Guid.NewGuid():N}.part";
        File.Copy(from, part);
        File.Move(part, to, true);
    }
}
