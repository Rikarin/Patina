using Patina.Ipc;
using Patina.Library;
using System.Text.RegularExpressions;

namespace Patina.Server.Graphs;

/// <summary>
///     The content-addressed preview cache (ADR 0005). The Worker writes files here and the Server serves them. It is
///     cache: deleting it costs a re-evaluation and nothing else.
/// </summary>
public sealed partial class PreviewStore(ServerIdentity identity) {
    const long Budget = 256L * 1024 * 1024;

    /// <summary>The folder, on a disk the Worker can write.</summary>
    public string Root { get; } = identity.Library is { } library
        ? LibraryLayout.PreviewsPath(library)
        : Path.Combine(Path.GetTempPath(), $"patina-previews-{Environment.ProcessId}");

    /// <summary>The file for a hash, or null when the hash is not one or the file is not there.</summary>
    public string? Find(string hash) {
        if (!HashPattern().IsMatch(hash)) {
            return null;
        }

        var path = GraphPreviewFiles.PathOf(Root, hash);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    ///     Removes the oldest files until the cache fits its budget, keeping what clients are looking at.
    /// </summary>
    public void Trim(IEnumerable<string> keep) {
        if (!Directory.Exists(Root)) {
            return;
        }

        var files = new DirectoryInfo(Root).GetFiles($"*{GraphPreviewFiles.Extension}", SearchOption.AllDirectories);
        var bytes = files.Sum(static file => file.Length);
        if (bytes <= Budget) {
            return;
        }

        HashSet<string> kept = [..keep];
        foreach (var file in files.OrderBy(static file => file.LastWriteTimeUtc)) {
            if (bytes <= Budget) {
                return;
            }

            if (!kept.Contains(Path.GetFileNameWithoutExtension(file.Name))) {
                bytes -= file.Length;
                file.Delete();
            }
        }
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex HashPattern();
}
