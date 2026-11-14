using Patina.Contracts;

namespace Patina.Server.Tests;

/// <summary>
///     <c>GET /library/mesh</c> answers from what the Worker last said of a file until the file's bytes change (#292),
///     where it used to send the Worker another parse of the same file for every question.
/// </summary>
public sealed class MeshFactsCacheTests : IDisposable {
    readonly string folder = Directory.CreateTempSubdirectory("patina-mesh-facts-").FullName;
    readonly List<string> asked = [];

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public async Task A_file_is_read_once_while_its_bytes_stay_and_again_once_they_change() {
        var file = Path.Combine(folder, "crate.obj");
        File.WriteAllText(file, "v 0 0 0\n");
        var cache = Cache();

        var first = await cache.GetAsync(file, Token);
        var again = await cache.GetAsync(file, Token);
        Assert.Same(first, again);
        Assert.Single(asked);

        // Written again with the same bytes, as an exporter does: still the same mesh.
        File.WriteAllText(file, "v 0 0 0\n");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        Assert.Same(first, await cache.GetAsync(file, Token));
        Assert.Single(asked);

        File.WriteAllText(file, "v 0 0 0\nv 1 0 0\n");
        Assert.NotSame(first, await cache.GetAsync(file, Token));
        Assert.Equal(2, asked.Count);
    }

    [Fact]
    public async Task A_file_the_reader_said_nothing_of_is_asked_about_again() {
        var file = Path.Combine(folder, "broken.obj");
        File.WriteAllText(file, "nothing");
        MeshFactsCache cache = new((path, _) => {
                asked.Add(path);
                return Task.FromResult<MeshFacts?>(null);
            }
        );

        Assert.Null(await cache.GetAsync(file, Token));
        Assert.Null(await cache.GetAsync(file, Token));
        Assert.Equal(2, asked.Count);
    }

    MeshFactsCache Cache() =>
        new((path, _) => {
                asked.Add(path);
                return Task.FromResult<MeshFacts?>(
                    new(Path.GetFileName(path), "OBJ", asked.Count, 3, [], [], 1, [0, 0, 0], [1, 1, 1], null, false)
                );
            }
        );
}
