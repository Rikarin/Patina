using Patina.Contracts;
using Patina.Export.Presets;
using Patina.Jobs;
using Patina.Library;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Patina.Export.Builds;

/// <summary>A preview that cannot be made because the project's maps are not built under their current keys.</summary>
public sealed class ExportUnbuiltException : Exception {
    public ExportUnbuiltException() { }

    public ExportUnbuiltException(string message) : base(message) { }

    public ExportUnbuiltException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
///     The compression preview of one exported file (#217), from the maps a build left: the same objects, preset,
///     packing, padding and size an export reads, so what it shows is what the file holds. It writes three PNGs and
///     what was measured into the cache under a hash of everything it was made from, and a second ask is a read.
///     It runs no GPU: the maps are files, and block compression of a region is a moment's work.
/// </summary>
public static class ExportPreviews {
    /// <summary>The pictures a preview has.</summary>
    public static IReadOnlyList<string> Pictures { get; } = ["source", "written", "difference"];

    const string Measured = "preview.json";

    /// <summary>The folder a preview's files are in.</summary>
    public static string FolderOf(string root, string id) =>
        Path.Combine(LibraryLayout.PreviewsPath(root), "compression", id);

    /// <summary>Makes a preview, or reads the one made before from the same things.</summary>
    /// <exception cref="ExportUnbuiltException">The project's maps are not built.</exception>
    /// <exception cref="BuildRefusedException">A preset, file or set that is not there, or changes it cannot take.</exception>
    public static CompressionPreviewInfo Make(
        OpenLibrary library,
        CompressionPreviewRequest request,
        ToolCatalog tools,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(request);
        var contents = library.Contents(request.Project);
        var build = new BuildRequest { Projects = [contents.Id], Presets = [request.Preset], Options = request.Options };
        var presets = new BuildPresets(library, build);
        var plan = BuildPipeline.Plan(library, build, tools, cancellationToken);
        var project = plan.Projects.FirstOrDefault(p => p.Project.Path == request.Project);
        if (project is null || project.Stale.Count > 0 || project.Unresolved.Count > 0 || project.Exports.Count == 0) {
            throw new ExportUnbuiltException(
                $"The maps of '{contents.Name}' are not built as they are now, so there is nothing to compress yet."
            );
        }

        var id = Revision.Of(
                Encoding.UTF8.GetBytes(
                    project.Exports[0].Key.Hex
                    + JsonSerializer.Serialize(request, ContractsJsonContext.Default.CompressionPreviewRequest)
                )
            )
            .Hex[..32];
        var folder = FolderOf(library.RootPath, id);
        var known = Path.Combine(folder, Measured);
        if (File.Exists(known)) {
            return JsonSerializer.Deserialize(SharedFile.ReadAllBytes(known), ContractsJsonContext.Default.CompressionPreviewInfo)!;
        }

        var sets = ExportSteps.SetsOf(BuildPipeline.SourcesOf(new(library.RootPath), project));
        var set = request.Set is { } named
            ? sets.FirstOrDefault(s => s.Name == named) ?? throw new BuildRefusedException($"There is no texture set '{named}'.")
            : sets[0];
        ExportPreset preset;
        try {
            preset = ExportOverrides.Apply(presets.Find(request.Project, request.Preset), request.Options).For(set.Name);
        } catch (ExportPresetException e) {
            throw new BuildRefusedException(e.Message, e);
        }

        var texture = preset.Textures.FirstOrDefault(t => t.File == request.File)
            ?? throw new BuildRefusedException($"{preset.Name} writes no file '{request.File}' here.");
        var maps = DerivedMaps.Complete(set.Maps, texture.Maps);
        if (!ChannelPacker.Applies(texture, maps)) {
            throw new BuildRefusedException($"'{request.File}' reads no map this texture set has, so it is not written.");
        }

        var picture = ExportRun.Finish(ChannelPacker.Pack(texture, maps, preset.Normal), maps, preset, texture);
        var region = CompressionPreview.Make(
            picture,
            texture,
            request.Level,
            (request.X, request.Y),
            request.Side,
            cancellationToken
        );
        var errors = CompressionPreview.Errors(region);
        var largest = errors.Count == 0 ? 0 : errors.Max(static e => e.MaxError);
        var gain = largest <= 0 ? 1 : Math.Clamp(Math.Pow(2, Math.Floor(Math.Log2(1 / largest))), 1, 256);
        var (x, y, width, height) = region.Region;
        var info = new CompressionPreviewInfo(
            id,
            texture.Format,
            texture.Compression,
            Math.Clamp(request.Level, 0, region.Levels - 1),
            region.Levels,
            region.Size.Width,
            region.Size.Height,
            [x, y, width, height],
            errors,
            gain
        ) {
            Bytes = (long)Math.Ceiling(ExportMemory.Bytes(texture, picture.Width, picture.Height)),
            Uncompressed = (long)Math.Ceiling(
                ExportMemory.Bytes(texture with { Compression = ExportCompressions.None }, picture.Width, picture.Height)
            )
        };

        var part = $"{folder}.{Guid.NewGuid():N}.part";
        Directory.CreateDirectory(part);
        File.WriteAllBytes(Path.Combine(part, "source.png"), CompressionPreview.Png(region.Source));
        File.WriteAllBytes(Path.Combine(part, "written.png"), CompressionPreview.Png(region.Written));
        File.WriteAllBytes(
            Path.Combine(part, "difference.png"),
            CompressionPreview.Png(CompressionPreview.Difference(region, gain))
        );
        File.WriteAllBytes(
            Path.Combine(part, Measured),
            JsonSerializer.SerializeToUtf8Bytes(info, ContractsJsonContext.Default.CompressionPreviewInfo)
        );
        BuildObjects.Publish(part, folder);
        return info;
    }

    /// <summary>A preview's picture, or null when there is no such preview or picture.</summary>
    public static string? PictureOf(string root, string id, string picture) =>
        id.Length == 32 && id.All(char.IsAsciiHexDigitLower) && Pictures.Contains(picture, StringComparer.Ordinal)
            ? Path.Combine(FolderOf(root, id), string.Create(CultureInfo.InvariantCulture, $"{picture}.png")) is var path
              && File.Exists(path)
                ? path
                : null
            : null;
}
