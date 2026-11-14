using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     Looking after an open library's room (#162, #297): its bin, and Clean, which takes away the copies of moved paint
///     and puts the pictures and uploads nothing uses in the bin. Each runs under the library's own write lock, as every
///     other change to its files does, so a Clean never meets a project being moved or a picture being brought in.
/// </summary>
public sealed class LibraryCare {
    readonly string root;
    readonly LibraryNameIndex names;
    readonly Lock gate;
    readonly Action wrote;

    internal LibraryCare(string root, IndexDatabase database, Lock gate, Action wrote) {
        this.root = root;
        names = new(database);
        this.gate = gate;
        this.wrote = wrote;
    }

    /// <summary>What the bin holds.</summary>
    public BinList Bin() => LibraryBin.List(root);

    /// <summary>Puts something back from the bin. The index sees it at the next rescan.</summary>
    /// <returns>Where it is now, below the library.</returns>
    /// <exception cref="FileNotFoundException">The bin holds no such thing.</exception>
    /// <exception cref="ProjectRefusedException">It cannot go back where it was.</exception>
    public string Restore(string id) {
        lock (gate) {
            var path = ProjectFiles.Restore(root, id);
            wrote();
            return path;
        }
    }

    /// <summary>Deletes one thing in the bin for good.</summary>
    /// <exception cref="FileNotFoundException">The bin holds no such thing.</exception>
    public void DeleteFromBin(string id) {
        lock (gate) {
            LibraryBin.Delete(root, id);
        }
    }

    /// <summary>Deletes everything in the bin for good.</summary>
    public int EmptyBin() {
        lock (gate) {
            return LibraryBin.Empty(root);
        }
    }

    /// <summary>
    ///     Deletes the copies of moved paint, and moves the pictures nothing reads into the bin as one entry. The files
    ///     are read for names only when they changed since the last time (#297).
    /// </summary>
    /// <param name="open">The open documents' text by their full path, which may name what no file does yet.</param>
    public LibraryCleaned Clean(IReadOnlyDictionary<string, string> open) {
        lock (gate) {
            var (folders, bytes) = PaintBackups.Remove(root);
            var (item, pictures, pictureBytes) = UnusedPictures.Bin(root, open, names.Read);
            return new(folders, bytes, pictures, pictureBytes, item?.Id);
        }
    }

    /// <summary>The files sent from another computer that nothing names any more (#297).</summary>
    /// <param name="open">The open documents' text by their full path.</param>
    public UnusedUploadList UnusedUploads(IReadOnlyDictionary<string, string> open) {
        lock (gate) {
            var found = Library.UnusedUploads.Find(root, open, names.Read);
            return new(found, found.Sum(static file => file.Bytes));
        }
    }

    /// <summary>Moves the files sent from another computer that nothing names into the bin, as one entry (#297).</summary>
    /// <param name="open">The open documents' text by their full path.</param>
    public UploadsCleared ClearUploads(IReadOnlyDictionary<string, string> open) {
        lock (gate) {
            var (item, count, bytes) = Library.UnusedUploads.Bin(root, open, names.Read);
            return new(count, bytes, item?.Id);
        }
    }
}
