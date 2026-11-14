using Patina.Library;

namespace Patina.Server;

/// <summary>
///     Where autosave and letting its work go meet. A save, a discard or an answer to an offer lets go of a document's
///     autosaves through here, and an autosave begun before that can never finish after it: one in flight while the
///     edits are thrown away would otherwise land a moment later and offer back what was refused (#162).
///     Autosaves go where the person chose (#162), which <paramref name="away" /> says for each library, or to its
///     cache.
/// </summary>
/// <param name="away">The folder outside the cache that a library's autosaves go to, or null for its cache.</param>
public sealed class AutosaveGate(Func<string, string?>? away = null) {
    readonly Func<string, string?> place = away ?? (static _ => null);
    readonly Lock gate = new();

    /// <summary>When each document's autosaves were last let go, by their folder, as a count of lettings go.</summary>
    readonly Dictionary<string, long> forgotten = new(StringComparer.Ordinal);

    long count;

    /// <summary>The autosaves begun and not yet finished or abandoned, which a Worker may be writing into.</summary>
    readonly HashSet<string> writing = new(StringComparer.Ordinal);

    /// <summary>
    ///     Begins an autosave of a document, a folder for its paint and then its text. Until it is finished or
    ///     abandoned, letting the document go leaves this folder to whoever is writing it.
    /// </summary>
    public string Begin(string root, string file) {
        lock (gate) {
            var generation = Recovery.Begin(root, file, place(root));
            writing.Add(generation);
            return generation;
        }
    }

    /// <summary>
    ///     A mark for autosave to take before it looks at what is open: whatever is let go after this is let go of
    ///     everything autosave began since.
    /// </summary>
    public long Mark() {
        lock (gate) {
            return count;
        }
    }

    /// <summary>Lets go of a document's autosaves, those still being written included.</summary>
    public void Forget(string root, string file) {
        lock (gate) {
            ForgetHeld(root, file);
        }
    }

    /// <summary>When a document was autosaved last, if that is later than its last save.</summary>
    public DateTimeOffset? Newer(string root, string file) => Recovery.Newer(root, file, place(root));

    /// <summary>Puts a document's newest autosave in place of its file, then lets go of its autosaves.</summary>
    public void Restore(string root, string file) {
        lock (gate) {
            Recovery.Restore(root, file, place(root));
            ForgetHeld(root, file);
        }
    }

    /// <summary>
    ///     Moves the autosaves kept in a place chosen before to where they go now (#297), all but those being written.
    /// </summary>
    /// <param name="from">The place chosen before.</param>
    /// <param name="to">The place chosen now, or null for each library's cache.</param>
    /// <param name="open">The library open now, if any.</param>
    public int Move(string from, string? to, string? open) {
        lock (gate) {
            return Recovery.Move(from, to, open, writing);
        }
    }

    void ForgetHeld(string root, string file) {
        count++;
        forgotten[Recovery.Folder(root, file)] = count;
        // ⚠ Not the autosave being written: deleting its folder under the Worker fails the save that asked.
        Recovery.Clear(root, file, writing, place(root));
    }

    /// <summary>
    ///     Finishes an autosave with the document's text and lets go of all but the newest <paramref name="keep" />,
    ///     unless the document was let go since <paramref name="mark" />: then what was begun goes too, and this is
    ///     false.
    /// </summary>
    public bool Finish(string root, string file, string generation, long mark, string text, int keep) {
        lock (gate) {
            writing.Remove(generation);
            if (Abandoned(root, file, generation, mark)) {
                return false;
            }

            Recovery.Finish(generation, text);
            Recovery.Trim(root, file, keep, place(root));
            return true;
        }
    }

    /// <summary>Lets go of an autosave that did not finish, if its document was let go since it began.</summary>
    public void Abandon(string root, string file, string generation, long mark) {
        lock (gate) {
            writing.Remove(generation);
            Abandoned(root, file, generation, mark);
        }
    }

    bool Abandoned(string root, string file, string generation, long mark) {
        if (!forgotten.TryGetValue(Recovery.Folder(root, file), out var at) || at <= mark) {
            return false;
        }

        if (Directory.Exists(generation)) {
            Directory.Delete(generation, true);
        }

        // A Worker that wrote after the letting go made the document's folder again, and nothing else is in it.
        var folder = Path.GetDirectoryName(generation)!;
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) {
            Directory.Delete(folder);
        }

        return true;
    }
}
