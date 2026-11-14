namespace Patina.Library.Tests;

/// <summary>Evaluation results kept on disk between sessions (#208).</summary>
public sealed class ResultShelfTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-results-");

    public void Dispose() => folder.Delete(true);

    static byte[] Texels(int length, byte seed) {
        var texels = new byte[length];
        for (var index = 0; index < length; index++) {
            texels[index] = (byte)(index * 7 + seed);
        }

        return texels;
    }

    /// <summary>A result written by one shelf reads back from another over its folder, as after a restart.</summary>
    [Fact]
    public void A_result_reads_back_on_a_later_shelf() {
        var texels = Texels(4 * 4 * 8, 3);
        new ResultShelf(folder.FullName).Write(42, 5, 4, 4, texels);

        var later = new ResultShelf(folder.FullName);
        Assert.True(later.Has(42));
        Assert.False(later.Has(43));
        Assert.Equal(texels, later.Read(42, 5, 4, 4, texels.Length));
    }

    /// <summary>A result of another shape, or a file that is not one, is a miss, never a wrong picture.</summary>
    [Fact]
    public void Another_shape_or_a_broken_file_is_a_miss() {
        var shelf = new ResultShelf(folder.FullName);
        shelf.Write(7, 5, 4, 4, Texels(128, 1));

        Assert.Null(shelf.Read(7, 6, 4, 4, 128));
        Assert.Null(shelf.Read(7, 5, 8, 2, 128));
        Assert.Null(shelf.Read(7, 5, 4, 4, 256));

        var file = Assert.Single(folder.GetFiles());
        File.WriteAllBytes(file.FullName, [1, 2, 3]);
        Assert.Null(shelf.Read(7, 5, 4, 4, 128));
        Assert.False(shelf.Has(7));
    }

    /// <summary>Over its budget the shelf lets the results least recently used go, and keeps the newest.</summary>
    [Fact]
    public void Over_its_budget_the_oldest_result_goes() {
        new ResultShelf(folder.FullName).Write(1, 5, 4, 4, Texels(128, 1));
        var first = Assert.Single(folder.GetFiles());
        first.LastWriteTimeUtc = DateTime.UtcNow.AddHours(-1);

        var shelf = new ResultShelf(folder.FullName, first.Length * 3 / 2);
        shelf.Write(2, 5, 4, 4, Texels(128, 1));

        Assert.False(shelf.Has(1));
        Assert.True(shelf.Has(2));
        Assert.Single(folder.GetFiles());
        Assert.False(new ResultShelf(folder.FullName).Has(1));
    }

    /// <summary>Each adapter has a shelf of its own, since a key names no device.</summary>
    [Fact]
    public void Each_adapter_has_its_own_shelf() {
        var one = ResultShelf.For(folder.FullName, "GPU A|1.0|0.1.0");
        var other = ResultShelf.For(folder.FullName, "GPU B|1.0|0.1.0");

        Assert.NotEqual(one.Folder, other.Folder);
        Assert.Equal(one.Folder, ResultShelf.For(folder.FullName, "GPU A|1.0|0.1.0").Folder);
        one.Write(9, 5, 4, 4, Texels(128, 9));
        Assert.False(other.Has(9));
    }
}
