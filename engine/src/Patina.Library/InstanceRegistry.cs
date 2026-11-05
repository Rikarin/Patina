using Patina.Contracts;
using System.Diagnostics;
using System.Text.Json;

namespace Patina.Library;

/// <summary>
///     The per-user discovery file: which Servers run on this machine, with their ports and tokens (plan §8). A client
///     looks here to attach to a library's Server instead of starting a second one. The file is plain JSON so that a
///     non-.NET client (Blender's Python) can read it. It holds tokens, so it is readable by its owner only.
/// </summary>
/// <param name="path">The discovery file; <see cref="DefaultPath" /> outside tests.</param>
public sealed class InstanceRegistry(string path) {
    static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Where the file lives: the per-user local data folder, which on Windows is already private to the user.
    /// </summary>
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create
            ),
            "Patina",
            "servers.json"
        );

    /// <summary>The canonical form of a library path, so two spellings of one folder are one library.</summary>
    public static string NormaliseLibrary(string library) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(library));

    /// <summary>The Servers that are running; entries left by a process that died do not count.</summary>
    public Task<IReadOnlyList<ServerInstance>> ListAsync(CancellationToken cancellationToken = default) =>
        LockedAsync<IReadOnlyList<ServerInstance>>(
            async () => Alive(await ReadAsync(cancellationToken).ConfigureAwait(false)),
            cancellationToken
        );

    /// <summary>The running Server for <paramref name="library" />, or null.</summary>
    public async Task<ServerInstance?> FindAsync(string library, CancellationToken cancellationToken = default) {
        var wanted = NormaliseLibrary(library);
        return (await ListAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(i => SameLibrary(i.Library, wanted));
    }

    /// <summary>
    ///     Lists <paramref name="instance" />, unless a running Server already owns its library: then that one is
    ///     returned and nothing changes. One Server per library per machine.
    /// </summary>
    public Task<ServerInstance?> RegisterAsync(
        ServerInstance instance,
        CancellationToken cancellationToken = default
    ) =>
        LockedAsync(
            async () => {
                var alive = Alive(await ReadAsync(cancellationToken).ConfigureAwait(false));
                if (alive.FirstOrDefault(i => SameLibrary(i.Library, instance.Library)) is { } owner) {
                    return owner;
                }

                await WriteAsync([..alive, instance], cancellationToken).ConfigureAwait(false);
                return null;
            },
            cancellationToken
        );

    /// <summary>Removes the entry of the Server with <paramref name="processId" />.</summary>
    public Task UnregisterAsync(int processId, CancellationToken cancellationToken = default) =>
        LockedAsync(
            async () => {
                var alive = Alive(await ReadAsync(cancellationToken).ConfigureAwait(false));
                await WriteAsync([..alive.Where(i => i.ProcessId != processId)], cancellationToken)
                    .ConfigureAwait(false);
                return true;
            },
            cancellationToken
        );

    static StringComparison PathComparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    static bool SameLibrary(string left, string right) =>
        string.Equals(NormaliseLibrary(left), NormaliseLibrary(right), PathComparison);

    static List<ServerInstance> Alive(IEnumerable<ServerInstance> instances) => [..instances.Where(IsRunning)];

    static bool IsRunning(ServerInstance instance) {
        try {
            using var process = Process.GetProcessById(instance.ProcessId);
            return !process.HasExited;
        } catch (ArgumentException) {
            return false; // no such process
        }
    }

    /// <summary>
    ///     Runs <paramref name="action" /> under an exclusive lock on a file beside the discovery file: Servers
    ///     starting together must not lose each other's entries. The operating system drops the lock if the holder
    ///     dies.
    /// </summary>
    async Task<T> LockedAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) {
        var gate = await OpenLockAsync(cancellationToken).ConfigureAwait(false);
        await using (gate.ConfigureAwait(false)) {
            return await action().ConfigureAwait(false);
        }
    }

    async Task<FileStream> OpenLockAsync(CancellationToken cancellationToken) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var deadline = DateTime.UtcNow + LockTimeout;
        while (true) {
            try {
                return new(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } catch (IOException) when (DateTime.UtcNow < deadline) {
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    async Task<IReadOnlyList<ServerInstance>> ReadAsync(CancellationToken cancellationToken) {
        if (!File.Exists(path)) {
            return [];
        }

        try {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false)) {
                var document = await JsonSerializer
                    .DeserializeAsync(stream, ContractsJsonContext.Default.DiscoveryDocument, cancellationToken)
                    .ConfigureAwait(false);
                return document?.Instances ?? [];
            }
        } catch (JsonException) {
            return []; // A damaged file holds nothing to keep: it lists running processes, which register again.
        }
    }

    /// <summary>Replaces the file whole, so a reader that takes no lock never sees half of it.</summary>
    async Task WriteAsync(IReadOnlyList<ServerInstance> instances, CancellationToken cancellationToken) {
        var temporary = $"{path}.{Environment.ProcessId}.tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try {
            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false)) {
                await JsonSerializer
                    .SerializeAsync(
                        stream,
                        new(instances),
                        ContractsJsonContext.Default.DiscoveryDocument,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            File.Move(temporary, path, true);
        } finally {
            File.Delete(temporary); // already moved, unless the write failed
        }
    }
}
