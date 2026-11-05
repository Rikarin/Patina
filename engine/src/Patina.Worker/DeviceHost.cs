using Patina.Ipc;
using Vixen.Graphics.Vulkan;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>
///     The one device and the one evaluator of a Worker, which graphs and stacks share, so that a result cache is one
///     budget. The device opens on the first use, so a Worker with no Vulkan still answers health.
/// </summary>
sealed class DeviceHost : IDisposable {
    /// <summary>
    ///     What a stack's outputs and the pool under a stroke need, or the pool is rebuilt per stamp (ADR
    ///     0009).
    /// </summary>
    const long ResultBudget = 6L << 30;

    VulkanDevice? device;
    TexturePlanEvaluator? evaluator;

    /// <summary>
    ///     Taken around anything that uses <see cref="Evaluator" />, which is one thread's at a time: the IPC loop answers
    ///     graphs and stacks, and a build's steps run beside it on their own thread.
    /// </summary>
    public Lock Gate { get; } = new();

    public TexturePlanEvaluator Evaluator {
        get {
            if (evaluator is null) {
                if (!VulkanDevice.TryCreate(new(), out device, out var refusal)) {
                    throw new IpcException($"No Vulkan device: {refusal}");
                }

                evaluator = new(device) { Results = { Budget = ResultBudget } };
            }

            return evaluator;
        }
    }

    public void Dispose() {
        evaluator?.Dispose();
        device?.Dispose();
    }
}
