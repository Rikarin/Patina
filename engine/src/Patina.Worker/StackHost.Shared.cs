using Patina.Library;
using Vixen.Texturing.Painting;

namespace Patina.Worker;

/// <summary>
///     Canvases one session paints that another reads (#108). An instance in one texture set of a paint layer in
///     another reads the source's canvas, and each session holds its own store of canvases, so it read the source's
///     file and showed only what was saved. A session now reads the canvas another session of this Worker has painted,
///     the same object, so its tiles repair as the source's strokes land and an undo there is an undo here. What it
///     borrowed goes back to the file when the painting session closes.
/// </summary>
sealed partial class StackHost {
    /// <summary>What every other session holds painted, which a session reads in place of the file.</summary>
    IEnumerable<(string File, PaintCanvas Canvas)> PaintedElsewhere(Session asking) =>
        Held.Where(session => !ReferenceEquals(session, asking)).SelectMany(static session => session.Painted());

    internal sealed partial class Session {
        /// <summary>
        ///     The canvases this session reads from another, by file, as they were lent at its last prepare.
        /// </summary>
        readonly Dictionary<string, PaintCanvas> borrowed = new(StringComparer.Ordinal);

        /// <summary>What the other sessions of this Worker hold painted, which <see cref="Borrow" /> reads.</summary>
        public Func<IEnumerable<(string File, PaintCanvas Canvas)>> Elsewhere { get; set; } = static () => [];

        /// <summary>
        ///     The canvases this session has painted, copied or made, with the file each will be saved to: what a
        ///     session of another texture set reads in place of the file (#108). A quick mask is this session's alone.
        /// </summary>
        public IEnumerable<(string File, PaintCanvas Canvas)> Painted() {
            foreach (var name in touched.Where(static name => !IsQuickMask(name))) {
                var file = LibraryLayout.Beside(folder, name);
                if (canvases.Open(file) is { } canvas) {
                    yield return (file, canvas);
                }
            }
        }

        /// <summary>
        ///     Reads every canvas another session has painted in place of its file, and the file again for one no
        ///     session holds any more. Lent again at every prepare, since a save there stamps the file anew and this
        ///     store would otherwise read the file back over the object it was lent.
        /// </summary>
        void Borrow() {
            HashSet<string> own = new(
                touched.Select(name => LibraryLayout.Beside(folder, name)),
                StringComparer.Ordinal
            );
            Dictionary<string, PaintCanvas> lent = new(StringComparer.Ordinal);
            foreach (var (file, canvas) in Elsewhere().Where(pair => !own.Contains(pair.File))) {
                lent.TryAdd(file, canvas);
            }

            foreach (var file in borrowed.Keys.Where(file => !lent.ContainsKey(file)).ToList()) {
                borrowed.Remove(file);
                // ⚠ One this session has painted since is its own now, strokes and all: forgetting it read the file
                // back over them, and the stroke never showed.
                if (!own.Contains(file)) {
                    canvases.Forget(file);
                }
            }

            foreach (var (file, canvas) in lent) {
                canvases.Adopt(file, canvas);
                borrowed[file] = canvas;
            }
        }
    }
}
