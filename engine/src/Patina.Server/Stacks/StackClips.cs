using Patina.Library;

namespace Patina.Server.Stacks;

/// <summary>Copied layers, as a paste reads them.</summary>
/// <param name="Id">What a paste names it by.</param>
/// <param name="Text">
///     The layers, as a stack file holds them, so that every paste starts from objects of its own.
/// </param>
/// <param name="Folder">Where their canvases were written as they stood when they were copied.</param>
/// <param name="Stack">The stack they came from.</param>
/// <param name="Width">The width of that stack's canvases.</param>
/// <param name="Height">Their height.</param>
/// <param name="Set">The texture set they came from, which an instance pasted from them reads.</param>
public sealed record StackClipContents(
    string Id,
    string Text,
    string Folder,
    string Stack,
    int Width,
    int Height,
    string Set = ""
);

/// <summary>
///     What has been copied out of a stack. A copy is the layers and their pixels as they stood, so a cut layer still
///     pastes and a stroke after the copy is not in it. A paste's journal reads the canvases from here, so a folder is
///     kept while any open stack's history names it. All of it is in the cache.
/// </summary>
public sealed class StackClips {
    /// <summary>How many copies stay to be pasted. A history reading an older one keeps its folder anyway.</summary>
    const int Kept = 16;

    readonly Lock changing = new();
    readonly List<StackClipContents> clips = [];
    readonly string root;

    public StackClips(ServerIdentity identity) {
        root = Path.Combine(
            identity.Library is { } library
                ? LibraryLayout.PaintPath(library)
                : Path.Combine(Path.GetTempPath(), $"patina-paint-{Environment.ProcessId}"),
            "clips"
        );

        // What a Server that stopped left: no session it had can read it now.
        if (Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }

    /// <summary>A new folder for a copy's canvases.</summary>
    public string NewFolder() => Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>Keeps a copy, and lets go of the oldest that nothing reads.</summary>
    /// <param name="clip">The copy.</param>
    /// <param name="reads">Whether an open stack's history reads a folder.</param>
    public void Add(StackClipContents clip, Func<string, bool> reads) {
        lock (changing) {
            clips.Add(clip);
            while (clips.Count > Kept) {
                var oldest = clips[0];
                clips.RemoveAt(0);
                if (!reads(oldest.Folder) && Directory.Exists(oldest.Folder)) {
                    Directory.Delete(oldest.Folder, true);
                }
            }
        }
    }

    public StackClipContents? Find(string id) {
        lock (changing) {
            return clips.Find(clip => clip.Id == id);
        }
    }
}
