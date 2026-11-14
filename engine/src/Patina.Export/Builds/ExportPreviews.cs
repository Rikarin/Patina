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

    /// <summary>Makes a preview, or reads the one made before from the same things, in this process.</summary>
    /// <exception cref="ExportUnbuiltException">The project's maps are not built.</exception>
    /// <exception cref="BuildRefusedException">A preset, file or set not there, or a change refused.</exception>
    public static CompressionPreviewInfo Make(
        OpenLibrary library,
        CompressionPreviewRequest request,
        ToolCatalog tools,
        CancellationToken cancellationToken
    ) {
        var step = Plan(library, request, tools, cancellationToken);
        return Known(step) ?? Run(step, cancellationToken);
    }

    /// <summary>
    ///     What a preview is made from, decoding no map (#293): the Server plans it and a Worker makes it. The sources
    ///     are the chosen set's alone, so making it decodes no other set's maps.
    /// </summary>
    /// <exception cref="ExportUnbuiltException">The project's maps are not built.</exception>
    /// <exception cref="BuildRefusedException">A preset or set not there, or a change refused.</exception>
    public static CompressionStep Plan(
        OpenLibrary library,
        CompressionPreviewRequest request,
        ToolCatalog tools,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(request);
        var contents = library.Contents(request.Project);
        var build = new BuildRequest {
            Projects = [contents.Id], Presets = [request.Preset], Options = request.Options
        };
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
                + JsonSerializer.Serialize(request, ExportJsonContext.Default.CompressionPreviewRequest)
            )
        )
            .Hex[..32];
        var sources = BuildPipeline.SourcesOf(new(library.RootPath), project);
        var set = SetOf(sources, request.Set);
        var preset = presets.Find(request.Project, request.Preset);
        try {
            _ = ExportOverrides.Apply(preset, request.Options);
        } catch (ExportPresetException e) {
            throw new BuildRefusedException(e.Message, e);
        }

        return new(
            new(
                ExportPresetCatalog.TextOf(preset),
                [..sources.Where(s => s.Set.Length == 0 || s.Set == set)],
                FolderOf(library.RootPath, id),
                request.Options
            ),
            id,
            request with { Set = set }
        );
    }

    /// <summary>The set asked for, or the first: as <see cref="ExportSteps.SetsOf" /> names them.</summary>
    static string SetOf(IReadOnlyList<ExportSource> sources, string? asked) {
        List<string> names = [
            ..sources.Select(static s => s.Set)
                .Where(static s => s.Length > 0)
                .Distinct()
                .DefaultIfEmpty(string.Empty)
        ];
        return asked is null || names.Contains(asked, StringComparer.Ordinal)
            ? asked ?? names[0]
            : throw new BuildRefusedException($"There is no texture set '{asked}'.");
    }

    /// <summary>The preview made before from the same things, or null.</summary>
    public static CompressionPreviewInfo? Known(CompressionStep step) {
        ArgumentNullException.ThrowIfNull(step);
        var known = Path.Combine(step.Export.Folder, Measured);
        return File.Exists(known)
            ? JsonSerializer.Deserialize(
                SharedFile.ReadAllBytes(known),
                ExportJsonContext.Default.CompressionPreviewInfo
            )
            : null;
    }

    /// <summary>Makes a planned preview: decodes the set's maps, compresses the region and writes its files.</summary>
    /// <exception cref="BuildRefusedException">A file the preset does not write, or the set cannot have.</exception>
    public static CompressionPreviewInfo Run(CompressionStep step, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(step);
        var request = step.Request;
        var set = ExportSteps.SetsOf(step.Export.Sources)[0];
        ExportPreset preset;
        try {
            preset = ExportOverrides.Apply(ExportPresetReader.Read(step.Export.Preset, "the preset"), request.Options)
                .For(set.Name);
        } catch (ExportPresetException e) {
            throw new BuildRefusedException(e.Message, e);
        }

        var asked = preset.Textures.FirstOrDefault(t => t.File == request.File)
            ?? throw new BuildRefusedException($"{preset.Name} writes no file '{request.File}' here.");
        var maps = DerivedMaps.Complete(set.Maps, asked.Maps);
        var texture = ExportRun.Settled(asked, maps);
        if (!ChannelPacker.Applies(texture, maps)) {
            throw new BuildRefusedException(
                $"'{request.File}' reads no map this texture set has, so it is not written."
            );
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
            step.Id,
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
                ExportMemory.Bytes(
                    texture with { Compression = ExportCompressions.None },
                    picture.Width,
                    picture.Height
                )
            )
        };

        Write(step.Export.Folder, region, info);
        return info;
    }

    /// <summary>Writes a preview's three PNGs and what was measured, all at once.</summary>
    static void Write(string folder, CompressionRegion region, CompressionPreviewInfo info) {
        var gain = info.Gain;
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
            JsonSerializer.SerializeToUtf8Bytes(info, ExportJsonContext.Default.CompressionPreviewInfo)
        );
        BuildObjects.Publish(part, folder);
    }

    /// <summary>A preview's picture, or null when there is no such preview or picture.</summary>
    public static string? PictureOf(string root, string id, string picture) {
        ArgumentNullException.ThrowIfNull(id);
        if (id.Length != 32
            || !id.All(char.IsAsciiHexDigitLower)
            || !Pictures.Contains(picture, StringComparer.Ordinal)) {
            return null;
        }

        var path = Path.Combine(FolderOf(root, id), string.Create(CultureInfo.InvariantCulture, $"{picture}.png"));
        return File.Exists(path) ? path : null;
    }
}
