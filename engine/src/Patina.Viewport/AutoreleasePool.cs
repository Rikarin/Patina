using System.Runtime.InteropServices;

namespace Patina.Viewport;

/// <summary>
///     An Objective-C autorelease pool around a piece of GPU work, on macOS only (#310). MoltenVK draws through Metal,
///     whose objects a frame autoreleases; a thread with no pool of its own never drains them, so they lived until the
///     process ended and MoltenVK reported a megabyte still allocated after one frame. Elsewhere it does nothing.
/// </summary>
/// <remarks>⚠ A pool is left on the thread that entered it: never hold one across an <c>await</c>.</remarks>
public readonly partial struct AutoreleasePool : IDisposable {
    const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    readonly nint token;

    AutoreleasePool(nint token) => this.token = token;

    /// <summary>Whether this pool drains anything when it is left: only on macOS.</summary>
    public bool Drains => token != 0;

    /// <summary>Enters a pool on this thread; <see cref="Dispose" /> leaves it, releasing what it collected.</summary>
    public static AutoreleasePool Enter() => OperatingSystem.IsMacOS() ? new(Push()) : default;

    public void Dispose() {
        if (token != 0) {
            Pop(token);
        }
    }

    [LibraryImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPush")]
    private static partial nint Push();

    [LibraryImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPop")]
    private static partial void Pop(nint pool);
}
