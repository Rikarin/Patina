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
}
