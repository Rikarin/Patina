using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Vixen.Graphics.Vulkan;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>
///     The one device and the one evaluator of a Worker, which graphs and stacks share, so that a result cache is one
///     budget. The device opens on the first use, so a Worker with no Vulkan still answers health.
/// </summary>
sealed class DeviceHost : IDisposable {
    /// <summary>
    ///     What a stack's outputs and the pool under a stroke need, or the pool is rebuilt per stamp (ADR 0009). The
    ///     most the result cache holds, on a device with room for it.
    /// </summary>
    const long MostResults = 6L << 30;

    VulkanDevice? device;
    TexturePlanEvaluator? evaluator;

    /// <summary>
    ///     Taken around anything that uses <see cref="Evaluator" />, which is one thread's at a time: the IPC loop
    ///     answers graphs and stacks, and a build's steps run beside it on their own thread.
    /// </summary>
    public Lock Gate { get; } = new();

    public TexturePlanEvaluator Evaluator {
        get {
            if (evaluator is null) {
                if (!VulkanDevice.TryCreate(new(), out device, out var refusal)) {
                    throw new IpcException($"No Vulkan device: {refusal}");
                }

                evaluator = new(device) { Results = { Budget = ResultBudget(device.Adapter.DeviceMemory) } };
            }

            return evaluator;
        }
    }

    /// <summary>
    ///     How much the result cache may hold on a device with this much memory of its own (#247). ⚠ Half of it and not
    ///     a fixed number: the budget binds only between evaluations, so an evaluation's own pool, the uploaded
    ///     canvases, the viewport's maps and every other program on the card need the rest. A session that swaps filter
    ///     after filter fills the cache to its budget, and with 6 GiB on an 8 GiB card the filter roll call peaked at
    ///     6.2 GiB of this device's own textures, against 4.0 GiB at half. A device that does not say is given the
    ///     most.
    /// </summary>
    /// <param name="deviceMemory">The device's own memory in bytes, or zero when it does not say.</param>
    internal static long ResultBudget(ulong deviceMemory) =>
        deviceMemory == 0 ? MostResults : Math.Min(MostResults, (long)(deviceMemory / 2));

    /// <summary>
    ///     Keeps the result cache's disk tier in a library's results folder, on a shelf of this adapter's, its
    ///     driver's and this build's own: a result's key names no device (#208).
    /// </summary>
    /// <param name="results">The folder, or null to keep none.</param>
    public void Keep(string? results) {
        var cache = Evaluator.Results;
        if (results is null) {
            cache.Store = null;
            return;
        }

        var adapter = device!.Adapter;
        var shelf = ResultShelf.For(results, $"{adapter.Name}|{adapter.DriverVersion}|{ProductInfo.Version}");
        if (cache.Store is not ShelfStore held || held.Shelf.Folder != shelf.Folder) {
            cache.Store = new ShelfStore(shelf);
        }
    }

    public void Dispose() {
        evaluator?.Dispose();
        device?.Dispose();
    }
}
