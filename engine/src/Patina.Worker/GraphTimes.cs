using Patina.Ipc;
using System.Collections.Immutable;

namespace Patina.Worker;

/// <summary>What each node of a graph cost on the GPU, from the times of the operations it was compiled to (#182).</summary>
static class GraphTimes {
    /// <summary>
    ///     Sums each operation's time into the node it was emitted for. Operations past the compiled plan's, the
    ///     previews' reductions, and those of no node are nobody's, so they are left out.
    /// </summary>
    /// <param name="times">Each operation that ran, by its index, and its milliseconds.</param>
    /// <param name="owners">Whose each operation of the compiled plan is; 0 for no node's.</param>
    public static GraphResultTime[] Of(ImmutableDictionary<int, double> times, IReadOnlyList<int> owners) {
        Dictionary<int, double> nodes = [];
        foreach (var (op, milliseconds) in times) {
            if (op < owners.Count && owners[op] != 0) {
                nodes[owners[op]] = nodes.GetValueOrDefault(owners[op]) + milliseconds;
            }
        }

        return [
            ..nodes.OrderBy(static pair => pair.Key).Select(static pair => new GraphResultTime(pair.Key, pair.Value))
        ];
    }
}
