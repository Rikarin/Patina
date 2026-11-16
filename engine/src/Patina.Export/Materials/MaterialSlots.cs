using Patina.Contracts;
using Patina.Export.Presets;

namespace Patina.Export.Materials;

/// <summary>A file a material reads, as the material file names it.</summary>
/// <param name="Uri">Its path relative to the material file, with forward slashes.</param>
/// <param name="File">What the export wrote.</param>
/// <param name="Lanes">How many lanes it has.</param>
public sealed record SlotFile(string Uri, ExportedFile File, int Lanes) {
    /// <summary>Whether its colour lanes hold sRGB numbers.</summary>
    public bool Srgb => File.Space == "srgb";
}

/// <summary>Three lanes of a file, in order, that hold one colour or vector map.</summary>
/// <param name="File">The file.</param>
public sealed record SlotColour(SlotFile File);

/// <summary>One lane of a file that holds one grey map, or one minus it.</summary>
/// <param name="File">The file.</param>
/// <param name="Lane">The lane, 0 to 3.</param>
/// <param name="Inverted">Whether the lane holds one minus the map.</param>
public sealed record SlotLane(SlotFile File, int Lane, bool Inverted);

/// <summary>
///     Where each of a material's inputs is in the files an export wrote for one texture set, worked out from the
///     preset's lanes. A material file names these and never looks inside an image, so a slot is wired only when a
///     file's lanes say plainly what it holds: a colour is three lanes of one map in order, and a grey map is lane
///     <c>r</c> of the map in any lane.
/// </summary>
public sealed record MaterialSlots {
    public SlotColour? BaseColor { get; init; }
    public SlotColour? Normal { get; init; }
    public SlotColour? Emissive { get; init; }
    public SlotColour? Specular { get; init; }
    public SlotLane? Opacity { get; init; }
    public SlotLane? Roughness { get; init; }
    public SlotLane? Metalness { get; init; }
    public SlotLane? Occlusion { get; init; }
    public SlotLane? Height { get; init; }

    /// <summary>OpenPBR's layers above the base (#142), each wired only when the set had that channel.</summary>
    public OpenPbrSlots Layers { get; init; } = new();

    /// <summary>Which way the normal file's green runs.</summary>
    public GreenAxis Green { get; init; } = GreenAxis.Up;

    /// <summary>
    ///     Whether a file holds the normal but not as three lanes in order, as a two-lane BC5 file does, so no
    ///     material file can wire it: each reads the normal's three lanes from one texture.
    /// </summary>
    public bool NormalApart { get; init; }

    /// <summary>
    ///     Finds the slots in one set's files. The first file in the preset's order that holds a map is the one read.
    ///     Opacity counts only when the set had an opacity map, since its default is opaque and a renderer told an
    ///     image holds opacity draws the surface as transparent.
    /// </summary>
    /// <param name="preset">The preset the files were written by, with the export's changes.</param>
    /// <param name="files">The files of one set, and where each was delivered.</param>
    /// <param name="folder">The folder the material file goes in.</param>
    public static MaterialSlots Find(
        ExportPreset preset,
        IReadOnlyList<(ExportedFile File, string Path)> files,
        string folder
    ) {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(files);
        var slots = new MaterialSlots { Green = preset.Normal };
        foreach (var (file, path) in files) {
            var texture = preset.For(file.Set).Textures.FirstOrDefault(t => t.File == file.Template);
            if (texture is null) {
                continue;
            }

            var uri = Path.GetRelativePath(folder, path).Replace('\\', '/');
            var source = new SlotFile(uri, file, texture.Channels.Count);
            slots = slots.With(source, texture.Channels);
        }

        return slots;
    }

    MaterialSlots With(SlotFile file, IReadOnlyList<ChannelSource> channels) {
        var triple = Triple(channels);
        var found = triple is { } map ? WithColour(map, new(file)) : this;
        if (triple != ExportMaps.Normal && channels.Any(static c => c.Map == ExportMaps.Normal)) {
            found = found with { NormalApart = true };
        }

        for (var lane = 0; lane < channels.Count; lane++) {
            if (channels[lane] is { Map: { } name, Lane: 0 } source) {
                found = found.WithLane(name, new(file, lane, source.Inverted));
            }
        }

        return found;
    }

    /// <summary>A colour slot filled, unless an earlier file filled it.</summary>
    MaterialSlots WithColour(string map, SlotColour colour) =>
        map switch {
            ExportMaps.BaseColor => this with { BaseColor = BaseColor ?? colour },
            ExportMaps.Normal => this with { Normal = Normal ?? colour },
            ExportMaps.Emissive => this with { Emissive = Emissive ?? colour },
            ExportMaps.Specular => this with { Specular = Specular ?? colour },
            ExportMaps.SheenColor when Had(colour.File, map) =>
                this with { Layers = Layers with { SheenColor = Layers.SheenColor ?? colour } },
            _ => this
        };

    /// <summary>A grey slot filled, unless an earlier file filled it. Glossiness is roughness turned over.</summary>
    MaterialSlots WithLane(string map, SlotLane read) =>
        map switch {
            ExportMaps.Roughness => this with { Roughness = Roughness ?? read },
            ExportMaps.Glossiness => this with { Roughness = Roughness ?? read with { Inverted = !read.Inverted } },
            ExportMaps.Metalness => this with { Metalness = Metalness ?? read },
            ExportMaps.Occlusion => this with { Occlusion = Occlusion ?? read },
            ExportMaps.Height => this with { Height = Height ?? read },
            ExportMaps.Opacity when Had(read.File, map) => this with { Opacity = Opacity ?? read },
            _ when Had(read.File, map) => this with { Layers = Layers.WithLane(map, read) },
            _ => this
        };

    /// <summary>Whether the set had a map, not only its default, in a file.</summary>
    static bool Had(SlotFile file, string map) => file.File.Maps.Contains(map, StringComparer.Ordinal);

    /// <summary>The map whose r, g and b fill a file's first three lanes in order, or null.</summary>
    static string? Triple(IReadOnlyList<ChannelSource> channels) {
        if (channels.Count < 3 || channels[0].Map is not { } map) {
            return null;
        }

        for (var lane = 0; lane < 3; lane++) {
            if (channels[lane] is not { Inverted: false } source || source.Map != map || source.Lane != lane) {
                return null;
            }
        }

        return map;
    }
}

/// <summary>
///     Where OpenPBR's layer maps (#142) are in a set's files: a coat, a sheen and transmission. Their defaults leave
///     the base alone, and a set that does not have one has no slot for it, so a material file says nothing of that
///     layer.
/// </summary>
public sealed record OpenPbrSlots {
    public SlotLane? Coat { get; init; }
    public SlotLane? CoatRoughness { get; init; }
    public SlotLane? Sheen { get; init; }
    public SlotColour? SheenColor { get; init; }
    public SlotLane? SheenRoughness { get; init; }
    public SlotLane? Transmission { get; init; }

    /// <summary>Whether any layer has a slot.</summary>
    public bool Any =>
        Coat is not null
        || CoatRoughness is not null
        || Sheen is not null
        || SheenColor is not null
        || SheenRoughness is not null
        || Transmission is not null;

    /// <summary>A grey slot filled, unless an earlier file filled it.</summary>
    internal OpenPbrSlots WithLane(string map, SlotLane read) =>
        map switch {
            ExportMaps.Coat => this with { Coat = Coat ?? read },
            ExportMaps.CoatRoughness => this with { CoatRoughness = CoatRoughness ?? read },
            ExportMaps.Sheen => this with { Sheen = Sheen ?? read },
            ExportMaps.SheenRoughness => this with { SheenRoughness = SheenRoughness ?? read },
            ExportMaps.Transmission => this with { Transmission = Transmission ?? read },
            _ => this
        };
}
