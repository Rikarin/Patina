using Patina.Ipc;
using Patina.Server.Graphs;
using Patina.Testing;
using System.Globalization;

namespace Patina.Server.Tests;

/// <summary>What trimming the preview cache takes: the oldest files of every kind, and never one in use.</summary>
public sealed class PreviewTrimTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-preview-trim-");

    public void Dispose() => TempFolders.Delete(folder.FullName);

    /// <summary>
    ///     A sky graph lighting the viewport is drawn again as a half-float panorama on every save, and the trim only
    ///     looked at pictures, so the panoramas stayed forever (#300). Every kind the cache holds now shares the
    ///     budget, oldest first, and a file in use stays whatever its age.
    /// </summary>
    [Fact]
    public void Panoramas_and_drawings_are_trimmed_with_the_pictures_and_kept_while_in_use() {
        var store = new PreviewStore(new(folder.FullName, null));
        var start = DateTime.UtcNow.AddHours(-1);
        var files = new[] {
                GraphPreviewFiles.Radiance, GraphPreviewFiles.Drawing, GraphPreviewFiles.Radiance,
                GraphPreviewFiles.Extension, GraphPreviewFiles.Radiance
            }.Select((extension, i) => Write(store.Root, Hash(i), extension, start.AddMinutes(i)))
            .ToList();
        var keep = Hash(0); // the oldest, but the viewport is lit by it

        store.Trim([keep], 3 * Size);

        Assert.Equal(
            [true, false, false, true, true],
            files.Select(File.Exists)
        );
    }

    /// <summary>A file someone else wrote, under no hash, is not the cache's and is left alone.</summary>
    [Fact]
    public void Other_files_are_left_alone() {
        var store = new PreviewStore(new(folder.FullName, null));
        var picture = Write(store.Root, Hash(1), GraphPreviewFiles.Extension, DateTime.UtcNow.AddHours(-1));
        var other = Path.Combine(store.Root, "readme.txt");
        File.WriteAllBytes(other, new byte[Size]);

        store.Trim([], 0);

        Assert.False(File.Exists(picture));
        Assert.True(File.Exists(other));
    }

    const int Size = 1000;

    static string Hash(int i) => i.ToString("x64", CultureInfo.InvariantCulture);

    static string Write(string root, string hash, string extension, DateTime written) {
        var path = GraphPreviewFiles.PathOf(root, hash, extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[Size]);
        File.SetLastWriteTimeUtc(path, written);
        return path;
    }
}
