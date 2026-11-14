using System.Diagnostics;
using System.Net.Sockets;

namespace Patina.Ipc;

/// <summary>Unix domain sockets, which all three platforms have. The Server listens; the Worker connects.</summary>
public static class IpcSocket {
    /// <summary>
    ///     Connects to <paramref name="socketPath" />, retrying every 100 ms until <paramref name="timeout" />: the
    ///     listener may still be starting. The stream owns the socket.
    /// </summary>
    public static async Task<Stream> ConnectAsync(
        string socketPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) {
        var deadline = DateTime.UtcNow + timeout;
        while (true) {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, true);
            } catch (SocketException) when (DateTime.UtcNow < deadline) {
                socket.Dispose();
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            } catch {
                socket.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    ///     Binds and listens on <paramref name="socketPath" />, first removing a file left by a process that died
    ///     without cleaning up.
    /// </summary>
    public static Socket Listen(string socketPath) {
        File.Delete(socketPath);
        SweepAbandoned(Path.GetDirectoryName(Path.GetFullPath(socketPath))!, TimeSpan.FromDays(1));
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try {
            socket.Bind(new UnixDomainSocketEndPoint(socketPath));
            socket.Listen();
            return socket;
        } catch {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Removes the sockets that processes now gone left in a folder (#297): each named <c>patina-</c> and the id of
    ///     the process that chose it, as the desktop app and a Server name theirs, and older than
    ///     <paramref name="age" />. A process that ends without cleaning up, killed or crashed, leaves its file behind.
    /// </summary>
    /// <returns>How many went.</returns>
    public static int SweepAbandoned(string folder, TimeSpan age) {
        if (!Directory.Exists(folder)) {
            return 0;
        }

        var cutoff = DateTime.UtcNow - age;
        var removed = 0;
        foreach (var socket in new DirectoryInfo(folder).EnumerateFiles("patina-*.sock")) {
            var id = Path.GetFileNameWithoutExtension(socket.Name)["patina-".Length..];
            if (socket.LastWriteTimeUtc >= cutoff
                || id.Length is 0 or > 10
                || !id.All(char.IsAsciiDigit)
                || Running(int.Parse(id, System.Globalization.CultureInfo.InvariantCulture))) {
                continue;
            }

            try {
                socket.Delete();
                removed++;
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                // Held, or not ours to remove: the next start tries again.
            }
        }

        return removed;
    }

    /// <summary>Whether a process of that id runs, which may be another that took a finished one's id.</summary>
    static bool Running(int id) {
        try {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        } catch (Exception e) when (e is ArgumentException or InvalidOperationException) {
            return false;
        } catch (System.ComponentModel.Win32Exception) {
            return true; // there, but not ours to look at
        }
    }
}
