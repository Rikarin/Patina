using Patina.Contracts;
using Patina.Graphs;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;

namespace Patina.Server;

public sealed partial class Thumbnails {
    /// <summary>
    ///     A material preset's picture (#124): its material's, drawn at the preset's values, so two presets of one
    ///     material look as they paint. A shipped compound's preset draws the compound's node at those values (#311).
    /// </summary>
    (string Key, MaterialThumbnailAsk Ask)? OfPreset(OpenLibrary library, string source, string full) {
        var preset = MaterialPresets.Read(full);
        var material = preset.Material;
        if (ShelfCompounds.NodeOf(material) is { } node) {
            return OfCompound(library, node, preset.Values.ToDictionary(static v => v.Name, static v => v.Value));
        }

        if (!material.StartsWith(StarterPrefix, StringComparison.Ordinal)
            && !MaterialShelf.IsLibraryGraph(library, material)) {
            return MaterialPresets.Names(material) ? null : Readable(library, material);
        }

        var starter = material.StartsWith(StarterPrefix, StringComparison.Ordinal);
        var file = starter
            ? Path.Combine(library.RootPath, material[StarterPrefix.Length..])
            : Path.GetFullPath(material, library.RootPath);
        var text = starter
            ? MaterialShelf.StarterText(material[StarterPrefix.Length..])
            : SharedFile.ReadAllText(file);
        var graph = GraphFactsOf.WithValues(
            GraphDocuments.Parse(text),
            null,
            [..preset.Values.Select(static value => KeyValuePair.Create(value.Name, value.Value))],
            null
        );
        return OfGraph(
            source,
            GraphDocuments.Text(graph),
            file,
            starter ? null : CompoundFolders.Of(file),
            starter ? [] : CompoundFolders.PictureRoots(file, library.RootPath)
        );
    }
}
