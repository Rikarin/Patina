using Vixen.Graphics.Vulkan;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>
///     Whether this copy of the engine can use its native libraries where it stands, for a build, a publish or an
///     installed app: <c>patina-worker --check-natives</c> (ADR 0010).
/// </summary>
public static class NativeCheck {
    /// <summary>A triangle with a UV layout, as an OBJ.</summary>
    const string Triangle = "v -0.5 -0.5 0\nv 0.5 -0.5 0\nv 0 0.5 0\nvt 0 0\nvt 1 0\nvt 0 1\nf 1/1 2/2 3/3\n";

    /// <summary>
    ///     Reads a triangle and fails unless the application's own Assimp answered, since a library found on the
    ///     machine would pass here and be missing on the next one. Vulkan is reported and does not fail it: a Worker
    ///     with no device still reads meshes and answers health.
    /// </summary>
    /// <param name="output">Where the report goes.</param>
    /// <returns>The exit code: 0 when a mesh reads through the application's own Assimp.</returns>
    public static int Run(TextWriter output) => Run(output, AppContext.BaseDirectory);

    /// <summary>
    ///     What a package has to pass as well: on macOS it carries its own Vulkan, since a clean Mac has none. A device
    ///     is still not required, because a build machine may have no GPU, but the library must be the package's.
    /// </summary>
    /// <param name="output">Where the report goes.</param>
    /// <returns>The exit code: 0 when both libraries are the application's own.</returns>
    public static int RunPackaged(TextWriter output) =>
        RunPackaged(output, AppContext.BaseDirectory, OperatingSystem.IsMacOS());

    /// <param name="output">Where the report goes.</param>
    /// <param name="applicationDirectory">Where the libraries have to be from.</param>
    /// <param name="carriesVulkan">Whether this platform's package has a Vulkan of its own.</param>
    /// <inheritdoc cref="RunPackaged(TextWriter)" />
    internal static int RunPackaged(TextWriter output, string applicationDirectory, bool carriesVulkan) {
        var natives = Run(output, applicationDirectory);
        if (HasItsVulkan(VulkanDevice.LibraryPath, applicationDirectory, carriesVulkan)) {
            return natives;
        }

        output.WriteLine($"vulkan: {VulkanDevice.LibraryPath ?? "nothing loaded"} is not the application's own copy");
        return 1;
    }

    /// <summary>
    ///     Draws a triangle in the viewport's renderer and fails unless a picture came back (#48): a device that opens
    ///     but compiles no shader, as a Vulkan on Metal could, would pass the checks above and show nothing. For a
    ///     machine that has a device, such as the macOS package job, whose runner opens one through the package's own
    ///     MoltenVK.
    /// </summary>
    /// <param name="output">Where the report goes.</param>
    /// <returns>The exit code: 0 when a frame of more than one colour came back.</returns>
    public static int Render(TextWriter output) {
        ArgumentNullException.ThrowIfNull(output);
        var folder = Directory.CreateTempSubdirectory("patina-check-");
        try {
            if (!ViewportRenderer.TryCreate(new(folder.FullName), out var renderer, out var refusal)) {
                output.WriteLine($"render: no renderer: {refusal}");
                return 1;
            }

            using (renderer) {
                var mesh = Path.Combine(folder.FullName, "triangle.obj");
                File.WriteAllText(mesh, Triangle);
                const int Side = 64;
                var pixels = new byte[ViewportRenderer.FrameBytes(Side, Side)];
                // ⚠ Drained before the device goes, or MoltenVK counts the frame's Metal objects as leaked (#310).
                using (AutoreleasePool.Enter()) {
                    renderer!.OpenMesh(mesh);
                    renderer.Render(OrbitCamera.Default, Side, Side, pixels);
                }

                var colours = Enumerable.Range(0, Side * Side)
                    .Select(at => BitConverter.ToInt32(pixels, at * 4))
                    .Distinct()
                    .Count();
                output.WriteLine($"render: {Side} x {Side}, {colours} colours");
                return colours > 1 ? 0 : 1;
            }
        } finally {
            folder.Delete(true);
        }
    }

    /// <summary>Whether the Vulkan that loaded is the one the package carries, where it carries one.</summary>
    /// <param name="library">Where Vulkan was loaded from, if anywhere.</param>
    /// <param name="applicationDirectory">Where it has to be from.</param>
    /// <param name="carriesVulkan">Whether this platform's package has a Vulkan of its own.</param>
    internal static bool HasItsVulkan(string? library, string applicationDirectory, bool carriesVulkan) =>
        !carriesVulkan || library?.StartsWith(applicationDirectory, StringComparison.Ordinal) == true;

    /// <param name="output">Where the report goes.</param>
    /// <param name="applicationDirectory">Where the Assimp has to be from.</param>
    /// <inheritdoc cref="Run(TextWriter)" />
    internal static int Run(TextWriter output, string applicationDirectory) {
        output.WriteLine(Vulkan());
        output.WriteLine($"vulkan library: {VulkanDevice.LibraryPath ?? "none"}");

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
        } catch (DllNotFoundException exception) {
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
