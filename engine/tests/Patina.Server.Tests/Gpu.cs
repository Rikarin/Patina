namespace Patina.Server.Tests;

/// <summary>
///     A machine with no GPU skips, and nothing else may. Skipping on any failure turned broken evaluators into skips
///     and left CI green, so a runner that has Vulkan sets <c>PATINA_REQUIRE_VULKAN</c> and then a skip is a failure.
/// </summary>
static class Gpu {
    const string Required = "PATINA_REQUIRE_VULKAN";

    /// <summary>Skips this test if the reason is a missing device, and fails it for any other reason.</summary>
    public static void Skip(string reason) {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Required))) {
            Assert.Fail($"Vulkan is required here: {reason}");
        }

        if (!reason.Contains("no vulkan", StringComparison.OrdinalIgnoreCase)) {
            Assert.Fail(reason);
        }

        Assert.Skip(reason);
    }
}
