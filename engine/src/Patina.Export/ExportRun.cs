using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Jobs;
using Patina.Library;
using System.Text.Json;

namespace Patina.Export;

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
    ) {
        ArgumentNullException.ThrowIfNull(preset);
        var part = $"{folder}.{Guid.NewGuid():N}.part";
        Directory.CreateDirectory(part);

        List<ExportedFile> files = [];
        List<string> skipped = [];
        List<string> warnings = [];
        for (var index = 0; index < preset.Textures.Count; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            var texture = preset.Textures[index];
            if (!ChannelPacker.Applies(texture, maps)) {
                skipped.Add(texture.File);
                continue;
            }

            var picture = ChannelPacker.Pack(texture, maps, preset.Normal);
            warnings.AddRange(picture.Warnings);
            var (bytes, levels) = ExportEncoder.Encode(picture, texture, cancellationToken);
            var extension = ExportFormats.ExtensionOf(texture.Format);
            var name = $"t{index}{extension}";
            File.WriteAllBytes(Path.Combine(part, name), bytes);
            files.Add(
                new(
                    texture.File,
                    name,
                    extension,
                    texture.Format,
                    texture.Compression,
                    picture.Width,
                    picture.Height,
                    levels,
                    bytes.LongLength
                )
            );
            progress?.Invoke((index + 1.0) / preset.Textures.Count);
        }

        var manifest = new ExportManifest(preset.Id, files, skipped, warnings);
        File.WriteAllBytes(
            Path.Combine(part, ManifestFile),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ContractsJsonContext.Default.ExportManifest)
        );
        BuildObjects.Publish(part, folder);
        return manifest;
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
    /// <param name="beside">Other files that go with them, such as a mesh's levels of detail: from, then its name.</param>
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

        List<string> delivered = [];
        foreach (var file in manifest.Files) {
            var path = Path.Combine(target, NamingTemplate.Resolve(file.Template, values, file.Extension));
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
            File.Delete(Path.Combine(target, old));
        }

        LibraryFiles.Replace(
            Path.Combine(target, BuildObjects.DeliveredFile),
            JsonSerializer.SerializeToUtf8Bytes(
                new ExportDelivery(key, relative),
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
