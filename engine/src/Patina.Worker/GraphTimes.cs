using Patina.Ipc;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>
///     What each node of a graph cost on the GPU, from the times of the operations it was compiled to (#182). The
///     result cache is the device's, shared by every graph and thumbnail, so an operation that ran for one is reused by
///     the next without running again; each result's time is kept by its key, so a reused one still says what it cost.
/// </summary>
sealed class GraphTimes {
    /// <summary>How many results' times are kept. Past it they all go, and the next run times them again.</summary>
    const int Held = 16_384;

    readonly Dictionary<TextureResultKey, double> byResult = [];

    /// <summary>
    ///     Sums each operation's time into the node it was emitted for: the time it took if it ran, or the time it took
    ///     when its result was made if the cache answered it. Operations past the compiled plan's, the previews'
    ///     reductions, those of no node and results never timed are nobody's, so they are left out.
    /// </summary>
    /// <param name="bake">An incremental, timed evaluation of <paramref name="plan" />.</param>
    /// <param name="plan">What ran.</param>
    /// <param name="owners">Whose each operation of the compiled plan is; 0 for no node's.</param>
    public GraphResultTime[] Of(TextureBake bake, TexturePlan plan, IReadOnlyList<int> owners) {
        Keep(bake, plan);
        Dictionary<int, double> nodes = [];
        for (var op = 0; op < Math.Min(owners.Count, plan.Ops.Length); op++) {
            if (owners[op] != 0 && byResult.TryGetValue(bake.KeyOf(plan.Ops[op].Output), out var milliseconds)) {
                nodes[owners[op]] = nodes.GetValueOrDefault(owners[op]) + milliseconds;
            }
        }

        return [
            ..nodes.OrderBy(static pair => pair.Key).Select(static pair => new GraphResultTime(pair.Key, pair.Value))
        ];
    }

    /// <summary>Keeps the time of each operation that ran, by its result's key.</summary>
    /// <param name="bake">An incremental, timed evaluation of <paramref name="plan" />.</param>
    /// <param name="plan">What ran.</param>
    public void Keep(TextureBake bake, TexturePlan plan) {
        if (byResult.Count + bake.OpMilliseconds.Count > Held) {
            byResult.Clear();
        }

        foreach (var (op, milliseconds) in bake.OpMilliseconds) {
            if (op < plan.Ops.Length) {
                byResult[bake.KeyOf(plan.Ops[op].Output)] = milliseconds;
            }
        }
    }
}
