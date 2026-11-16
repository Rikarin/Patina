using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Testing;
using System.Collections.Concurrent;

namespace Patina.Server.Tests;

/// <summary>What a material preset's picture is drawn from.</summary>
public sealed class ThumbnailPresetTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-thumbnail-preset-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => TempFolders.Delete(folder.FullName);

    /// <summary>
    ///     A preset of a shipped compound was pictured as the compound ships (#311), so two presets of one compound
    ///     looked alike and neither as it paints. Its node is now drawn at the preset's values, and the compound
    ///     itself still at its defaults.
    /// </summary>
    [Fact]
    public async Task A_shipped_compounds_preset_is_drawn_at_its_values() {
        var root = Path.Combine(folder.FullName, "Library");
        LibraryFiles.CreateDefaultLibrary(root, "Game");
        var compound = ShelfCompounds.All.First(static material => material.Parameters is { Count: > 0 });
        var setting = compound.Parameters![0].Name;
        var preset = "Ours" + MaterialPresets.Extension;
        MaterialPresets.Write(Path.Combine(root, preset), new(compound.Source, [new(setting, "0.37")]));
        using var library = OpenLibrary.Open(root);
        var store = new PreviewStore(new(root, null));
        var socketPath = TempFolders.NewSocket();
        using var link = new WorkerLink(
            Options.Create(new ServerOptions { Worker = { SocketPath = socketPath } }),
            NullLogger<WorkerLink>.Instance
        );
        ConcurrentQueue<string> asked = [];
        await link.StartAsync(Token);
        try {
            await using var worker = await ThumbnailRefusalTests.WorkerAsync(
                socketPath,
                request => {
                    if (request.Type != IpcTypes.MaterialThumbnail) {
                        return IpcMessage.Ok(request.Id);
                    }

                    asked.Enqueue(request.MaterialThumbnail!.Document);
                    return ThumbnailRefusalTests.Drawn(request, store.Root);
                }
            );
            await link.ConnectionAsync(Token);
            using var thumbnails = new Thumbnails(link, store);

            var drawn = await thumbnails.DrawAsync(library, [compound.Source, preset], Token);

            Assert.All(drawn, static thumbnail => Assert.NotNull(thumbnail.Hash));
            Assert.NotEqual(drawn[0].Hash, drawn[1].Hash);
            Assert.False(DrawnTexts(asked.ElementAt(0)).ContainsKey(setting));
            Assert.Equal("0.37", DrawnTexts(asked.ElementAt(1))[setting]);
        } finally {
            await link.StopAsync(Token);
        }
    }

    /// <summary>The settings of the node a picture is drawn of, the one the noise feeds.</summary>
    static Dictionary<string, string> DrawnTexts(string document) {
        var graph = GraphDocuments.Parse(document);
        var drawn = graph.Edges.Select(static edge => edge.ToNode).DefaultIfEmpty(2).First();
        return graph.Nodes.Single(node => node.Id == drawn).Texts;
    }
}
