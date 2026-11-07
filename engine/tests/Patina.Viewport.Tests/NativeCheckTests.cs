namespace Patina.Viewport.Tests;

public sealed class NativeCheckTests {
    /// <summary>On Linux CI this is the proof for a build: no Assimp is installed there.</summary>
    [Fact]
    public void A_mesh_reads_through_the_application_s_own_assimp() {
        using var report = new StringWriter();

        Assert.Equal(0, NativeCheck.Run(report));
        Assert.Contains("mesh: 3 vertices", report.ToString(), StringComparison.Ordinal);
        Assert.Contains($"assimp: {AppContext.BaseDirectory}", report.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Assimp from anywhere else fails, so a machine's copy cannot hide a package without one.</summary>
    [Fact]
    public void An_assimp_from_elsewhere_fails_the_check() {
        using var report = new StringWriter();

        Assert.Equal(1, NativeCheck.Run(report, Path.Combine(AppContext.BaseDirectory, "elsewhere")));
        Assert.Contains("not the application's own copy", report.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A machine's Vulkan would pass on the build Mac and be missing on the next one.</summary>
    [Theory]
    [InlineData("/app/libMoltenVK.dylib", true, true)]
    [InlineData("/opt/homebrew/lib/libvulkan.dylib", true, false)]
    [InlineData(null, true, false)]
    [InlineData(null, false, true)]
    public void A_package_that_carries_vulkan_needs_its_own_copy(string? library, bool carries, bool passes) =>
        Assert.Equal(passes, NativeCheck.HasItsVulkan(library, "/app/", carries));
}
