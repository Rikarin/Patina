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

    /// <summary>Every kind of file the cache keeps under a hash.</summary>
    static readonly string[] Kinds =
        [GraphPreviewFiles.Extension, GraphPreviewFiles.Drawing, GraphPreviewFiles.Radiance];

    /// <summary>The folder, on a disk the Worker can write.</summary>
    public string Root { get; } = identity.Library is { } library
        ? LibraryLayout.PreviewsPath(library)
        : Path.Combine(Path.GetTempPath(), $"patina-previews-{Environment.ProcessId}");

    /// <summary>The file for a hash, or null when the hash is not one or the file is not there.</summary>
    public string? Find(string hash, string extension = GraphPreviewFiles.Extension) {
        if (!HashPattern().IsMatch(hash)) {
            return null;
        }

        var path = GraphPreviewFiles.PathOf(Root, hash, extension);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    ///     Removes the oldest files until the cache fits its budget, keeping what clients are looking at.
    /// </summary>
    public void Trim(IEnumerable<string> keep) => Trim(keep, Budget);

    /// <summary>
    ///     Removes the oldest files of every kind the cache keeps until it fits <paramref name="budget" />, keeping
    ///     what clients are looking at. Pictures, drawings and panoramas share the one budget: a panorama lighting
    ///     the viewport is a few megabytes, and a sky edited over an afternoon left one behind per save (#300).
    /// </summary>
    /// <param name="keep">The hashes still in use.</param>
    /// <param name="budget">The bytes the cache may hold.</param>
    public void Trim(IEnumerable<string> keep, long budget) {
        if (!Directory.Exists(Root)) {
            return;
        }

        var files = new DirectoryInfo(Root).EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(static file => Kinds.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var bytes = files.Sum(static file => file.Length);
        if (bytes <= budget) {
            return;
        }

        HashSet<string> kept = [..keep];
        foreach (var file in files.OrderBy(static file => file.LastWriteTimeUtc)) {
            if (bytes <= budget) {
                return;
            }

            var length = file.Length;
            if (!kept.Contains(Path.GetFileNameWithoutExtension(file.Name)) && Deleted(file)) {
                bytes -= length;
            }
        }
    }

    /// <summary>Deletes a file unless someone has it open: one being served or read is left for next time.</summary>
    static bool Deleted(FileInfo file) {
        try {
            file.Delete();
            return true;
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex HashPattern();
}
