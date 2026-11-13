namespace Patina.Ipc.Tests;

public sealed class GraphPreviewFilesTests : IDisposable {
    readonly string folder = Path.Combine(Path.GetTempPath(), $"patina-previews-{Guid.NewGuid():N}");

    public void Dispose() {
        if (Directory.Exists(folder)) {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    ///     Two nodes with one picture store it at once under one hash, while the Server serves it (#250). On Windows a
    ///     rename onto a file somebody has open is denied, which failed an evaluation with "Access to the path is
    ///     denied" on a busy machine. Every writer starts together, round after round, beside a reader that keeps the
    ///     file open, so the rename onto the file is attempted while it is held.
    /// </summary>
    [Fact]
    public async Task Writers_of_one_picture_at_once_beside_a_reader_all_succeed() {
        byte[] bytes = [..Enumerable.Range(0, 4096).Select(static at => (byte)at)];
        const string hash = "51c5f4bb88ad70c91491f1482c6d25acf3937c9111dba9d5cf876bc55c94bbb2";
        var path = GraphPreviewFiles.PathOf(folder, hash);

        for (var round = 0; round < 40; round++) {
            await RaceAsync(path, hash, bytes);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }

        Assert.Empty(Directory.GetFiles(folder, "*.tmp", SearchOption.AllDirectories));
    }

    /// <summary>One round: every writer stores the picture at once while a reader keeps opening it.</summary>
    async Task RaceAsync(string path, string hash, byte[] bytes) {
        const int writers = 8;

        if (File.Exists(path)) {
            File.Delete(path);
        }

        using var reading = new CancellationTokenSource();
        var reader = Task.Run(
            () => {
                while (!reading.IsCancellationRequested) {
                    try {
                        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        Thread.SpinWait(2000);
                    } catch (IOException) {
                        // Not there yet, or a writer is renaming it in: a reader tries again, as the Server does.
                    }
                }
            },
            TestContext.Current.CancellationToken
        );
        using var start = new Barrier(writers);
        var stores = Enumerable.Range(0, writers)
            .Select(_ => Task.Run(
                    () => {
                        start.SignalAndWait(TestContext.Current.CancellationToken);
                        GraphPreviewFiles.Store(folder, hash, bytes);
                    },
                    TestContext.Current.CancellationToken
                )
            )
            .ToArray();
        try {
            await Task.WhenAll(stores);
        } finally {
            await reading.CancelAsync();
            await reader;
        }
    }
}
