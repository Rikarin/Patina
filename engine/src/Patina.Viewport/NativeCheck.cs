using Vixen.Graphics.Vulkan;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>
///     Whether this copy of the engine can use its native libraries where it stands: <c>patina-worker --check-natives</c>,
///     for a build, a publish or an installed app (ADR 0010).
/// </summary>
public static class NativeCheck {
    /// <summary>
    ///     Reads a triangle and fails unless the application's own Assimp answered, since a library found on the machine
    ///     would pass here and be missing on the next one. Vulkan is reported and does not fail it: a Worker with no
    ///     device still reads meshes and answers health.
    /// </summary>
    /// <param name="output">Where the report goes.</param>
    /// <returns>The exit code: 0 when a mesh reads through the application's own Assimp.</returns>
    public static int Run(TextWriter output) => Run(output, AppContext.BaseDirectory);

    /// <param name="output">Where the report goes.</param>
    /// <param name="applicationDirectory">Where the Assimp has to be from.</param>
    /// <inheritdoc cref="Run(TextWriter)" />
    internal static int Run(TextWriter output, string applicationDirectory) {
        output.WriteLine(Vulkan());

        try {
            var read = ModelReader.Read(
                "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n"u8,
                ".obj",
                "Triangle",
                new ModelReadOptions { GenerateTangents = false }
            );
            var path = ModelReader.NativeLibraryPath;
            output.WriteLine($"assimp: {path}");
            output.WriteLine($"mesh: {read.Meshes.Sum(static mesh => mesh.Positions.Length)} vertices");
            if (path.StartsWith(applicationDirectory, StringComparison.Ordinal)) {
                return 0;
            }

            output.WriteLine($"assimp: not the application's own copy, which belongs in {applicationDirectory}");
            return 1;
        }
        catch (DllNotFoundException exception) {
            output.WriteLine($"assimp: {exception.Message}");
            return 1;
        }
    }

    static string Vulkan() {
        if (!VulkanDevice.TryCreate(new(), out var device, out var refusal)) {
            return $"vulkan: {refusal}";
        }

        using (device) {
            return $"vulkan: {device.Adapter.Name}";
        }
    }
}
