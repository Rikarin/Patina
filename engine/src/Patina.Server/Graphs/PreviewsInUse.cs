using Patina.Server.Stacks;
using Patina.Server.Viewport;

namespace Patina.Server.Graphs;

/// <summary>
///     Every preview cache file someone is looking at: a graph's or a stack's previews, and what the viewport draws
///     from the cache, such as a graph's panorama lighting the model. Trimming for one kind of session must never take
///     another's.
/// </summary>
public sealed class PreviewsInUse(GraphSessions graphs, StackSessions stacks, ViewportSession viewport) {
    /// <summary>The hashes in use now.</summary>
    public IEnumerable<string> Hashes() => [
        ..graphs.All.SelectMany(static session => session.PreviewHashes),
        ..stacks.All.SelectMany(static session => session.PreviewHashes),
        ..viewport.PreviewHashes
    ];
}
