namespace Patina.Ipc.Tests;

public sealed class IpcSocketSweepTests : IDisposable {
    readonly string folder = Path.Combine(Path.GetTempPath(), $"patina-sockets-{Guid.NewGuid():N}");

    public IpcSocketSweepTests() {
        Directory.CreateDirectory(folder);
    }

    public void Dispose() => Patina.Testing.TempFolders.Delete(folder);

    [Fact]
    public void A_day_old_socket_of_a_process_gone_goes_and_a_running_ones_stays() {
        var gone = Socket($"patina-{int.MaxValue}.sock", TimeSpan.FromDays(2));
        var running = Socket($"patina-{Environment.ProcessId}.sock", TimeSpan.FromDays(2));
        var fresh = Socket($"patina-{int.MaxValue - 1}.sock", TimeSpan.FromHours(1));
        var other = Socket("someone-else.sock", TimeSpan.FromDays(2));

        Assert.Equal(1, IpcSocket.SweepAbandoned(folder, TimeSpan.FromDays(1)));

        Assert.False(File.Exists(gone));
        Assert.True(File.Exists(running));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(other));
    }

    string Socket(string name, TimeSpan age) {
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, []);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }
}
