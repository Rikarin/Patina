using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using Patina.Server.Graphs;
using Patina.Server.Sessions;
using System.Security.Cryptography;
using System.Text;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>What the evaluation loop has to do for a stack.</summary>
/// <param name="Revision">The revision this is.</param>
/// <param name="Document">The document as text.</param>
/// <param name="Standing">The strokes that stand, in order.</param>
/// <param name="Forget">Strokes that can never stand again.</param>
/// <param name="Forced">Whether to evaluate even if the Worker already has this.</param>
public sealed record StackWork(
    long Revision,
    string Document,
    IReadOnlyList<StackJournalEntry> Standing,
    IReadOnlyList<long> Forget,
    bool Forced
);

/// <summary>
///     One open stack. The Server owns the document, its history, its numbered events and the journal of strokes; none
///     of it depends on a Worker, so edits work with none running and the pixels catch up when one connects.
/// </summary>
public sealed class StackSession : FollowedSession<StackEvent>, IFileSession {
    readonly StackEditor editor;
    readonly Dictionary<string, StackPreview> previews = new(StringComparer.Ordinal);
    readonly HashSet<string> found = new(StringComparer.Ordinal);
    readonly Action wake;
    string fileHash;
    long revision = 1;
    long settled;
    bool forced;
    bool closed;
    StackEvaluation? evaluation;
    volatile LayerStackAsset published;

    /// <param name="path">Its file.</param>
    /// <param name="text">The document as text.</param>
    /// <param name="wake">Tells the evaluation loop to look.</param>
    public StackSession(string path, string text, Action wake) : base(static item => item.Sequence) {
        Id = Guid.NewGuid().ToString("N");
        Path = path;
        fileHash = Hash(text);
        editor = new(path, StackDocuments.Parse(text));
        published = editor.Document;
        this.wake = wake;
        MeshMaps = DocumentBakes.OfFile(path);
        foreach (var paint in PaintFiles.Where(paint => File.Exists(System.IO.Path.GetFullPath(paint, Folder!)))) {
            found.Add(paint);
        }
    }

    public string Id { get; }

    public string Path { get; }

    /// <summary>The folder its compounds come from.</summary>
    public string? Folder => System.IO.Path.GetDirectoryName(Path);

    /// <summary>The folders a relative image reference is read from, in order.</summary>
    public IReadOnlyList<string> ImageRoots {
        get;
        init {
            field = value;
            editor.Library = value.Count > 0 ? value[0] : null;
        }
    } = [];

    /// <summary>The copies a paste reads from.</summary>
    public StackClips? Clips {
        get;
        init {
            field = value;
            editor.Clip = id => value?.Find(id);
        }
    }

    /// <summary>Whether this history copies a canvas out of a folder, which then has to stay.</summary>
    public bool Reads(string folder) {
        lock (Changing) {
            return editor.Standing.Concat(editor.Redoable)
                .Any(entry => entry.From is { } from
                    && System.IO.Path.IsPathRooted(from)
                    && from.StartsWith(folder, StringComparison.Ordinal)
                );
        }
    }

    /// <summary>
    ///     Some layers as a copy holds them, and the canvases the Worker writes for it, read under the lock.
    /// </summary>
    /// <exception cref="StackEditRefusedException">A layer is not in this stack.</exception>
    public (string Text, int Count, IReadOnlyList<string> Canvases, int Width, int Height) Copied(
        IReadOnlyList<string> layers
    ) {
        lock (Changing) {
            var (text, count, canvases) = editor.Copied(layers);
            return (text, count, canvases, editor.Document.BaseWidth, editor.Document.BaseHeight);
        }
    }

    /// <summary>
    ///     This session's texture set as the last edit left it, read without the lock: what an instance in another
    ///     session's set reads of it, from under that session's own lock.
    /// </summary>
    public TextureSetAsset? Published => LayerStackEdit.SetFor(published, string.Empty);

    /// <summary>The model's other texture sets, by name, which instances here may stand for layers of (#108).</summary>
    public Func<string, TextureSetAsset?> Siblings {
        set {
            lock (Changing) {
                editor.Sibling = value;
            }
        }
    }

    /// <summary>
    ///     Told after every edit, undo or redo, so that instances of this set's layers elsewhere catch up.
    /// </summary>
    public Action<StackSession>? Changed { get; set; }

    /// <summary>Whether a layer here stands for a layer of another texture set.</summary>
    public bool ReadsOtherSets => Foreign(published).Count > 0;

    /// <summary>The texture set this session paints.</summary>
    public string Set {
        get {
            lock (Changing) {
                return editor.Set.Name;
            }
        }
    }

    /// <summary>The bake generators and bake masks read, kept in a file beside the stack.</summary>
    public string? MeshMaps { get; private set; }

    /// <summary>Counts how often the compounds this stack reads have changed, so the Worker reads them again.</summary>
    public int CompoundEdits { get; private set; }

    /// <summary>A graph this stack reads was saved, and the picture has to follow.</summary>
    public void CompoundsChanged() {
        CompoundEdits++;
        Evaluate();
    }

    /// <summary>
    ///     Counts how often a bake was chosen, for one baked again into the same folder to be read again.
    /// </summary>
    public int BakeEdits { get; private set; }

    /// <summary>Chooses a bake: a new Worker copy loads it, since the images are external to the plan.</summary>
    public void UseMeshMaps(string? folder) {
        lock (Changing) {
            MeshMaps = folder;
            BakeEdits++;
            var file = GraphMeshMaps.FileOf(Path);
            if (folder is null) {
                File.Delete(file);
            } else {
                // Relative, so that a library can move and take its bakes with it.
                LibraryFiles.Replace(
                    file,
                    System.IO.Path.GetRelativePath(System.IO.Path.GetDirectoryName(Path)!, folder)
                );
            }

            forced = true;
            revision++;
        }

        wake();
    }

    /// <summary>The mesh's file, or null when the stack names none.</summary>
    public string? ModelFile {
        get {
            lock (Changing) {
                var model = editor.Document.Model.Trim();
                return model.Length == 0 ? null : System.IO.Path.GetFullPath(model, Folder!);
            }
        }
    }

    /// <summary>The paint files the document names, for keeping a copy of what a save overwrites.</summary>
    public IReadOnlyList<string> PaintFiles {
        get {
            lock (Changing) {
                return [..Named(editor.Document.Sets.SelectMany(static set => set.Layers))];
            }
        }
    }

    /// <summary>
    ///     The paint files strokes made, standing or undone, that were not there when the session
    ///     opened.
    /// </summary>
    public IReadOnlyList<string> Created {
        get {
            lock (Changing) {
                return [
                    ..editor.Standing.Concat(editor.Redoable)
                        .Select(static stroke => stroke.Paint)
                        .Distinct(StringComparer.Ordinal)
                        .Where(paint => !found.Contains(paint))
                ];
            }
        }
    }

    public StackSummary Summary {
        get {
            lock (Changing) {
                return new(Id, Path, editor.Document.Name, revision, editor.History.Dirty);
            }
        }
    }

    public StackState State {
        get {
            lock (Changing) {
                var set = editor.Set;
                return new(
                    Id,
                    Path,
                    editor.Document.Name,
                    set.Name,
                    editor.Document.BaseWidth,
                    editor.Document.BaseHeight,
                    revision,
                    Log.Sequence,
                    editor.History,
                    [..set.Channels.Select(static channel => new StackChannel(channel.Usage, channel.Default))],
                    StackDocuments.Layers(set.Layers),
                    [..previews.Values],
                    evaluation,
                    settled,
                    editor.Document.Model,
                    MeshMaps,
                    editor.Document.Seed,
                    set.HeightNormal.ToString(),
                    StackDocuments.Anchors(set)
                );
            }
        }
    }

    public IReadOnlyList<string> PreviewHashes {
        get {
            lock (Changing) {
                return [..previews.Values.Select(static preview => preview.Hash)];
            }
        }
    }

    /// <summary>Applies a batch whole. A refused batch changes nothing and sends no event.</summary>
    /// <exception cref="StackEditRefusedException">A command cannot be applied.</exception>
    public StackEditResult Apply(StackCommandBatch batch) {
        StackEditResult result;
        lock (Changing) {
            var painted = editor.Apply(batch);
            if (editor.CompoundsArrived) {
                editor.CompoundsArrived = false;
                CompoundEdits++;
            }

            result = Announce(GraphEventKind.Edited, editor.History.UndoLabel, painted);
        }

        wake();
        Changed?.Invoke(this);
        return result;
    }

    /// <returns>Null when there was nothing to undo.</returns>
    public StackEditResult? Undo() => Walk(static held => UndoHistory.Moved(held.Undo(), GraphEventKind.Undone));

    /// <returns>Null when there was nothing to redo.</returns>
    public StackEditResult? Redo() => Walk(static held => UndoHistory.Moved(held.Redo(), GraphEventKind.Redone));

    /// <summary>
    ///     What the document says now, and that everything the Worker has been told is about to be on
    ///     disk.
    /// </summary>
    /// <exception cref="GraphFileChangedException">The file changed behind this session.</exception>
    /// <returns>The text to write, and the revision it is of.</returns>
    public (string Text, long Revision) PrepareSave(bool force) {
        lock (Changing) {
            if (!force && File.Exists(Path) && Hash(SharedFile.ReadAllText(Path)) != fileHash) {
                throw new GraphFileChangedException($"'{Path}' changed on disk after it was opened.");
            }

            return (StackDocuments.Text(editor.Document), revision);
        }
    }

    /// <summary>The document as it stands.</summary>
    public string Text {
        get {
            lock (Changing) {
                return StackDocuments.Text(editor.Document);
            }
        }
    }

    /// <summary>The evaluation that last failed and has not been followed by a good one, if there is one.</summary>
    public string? EvaluationError {
        get {
            lock (Changing) {
                return evaluation?.Error;
            }
        }
    }

    /// <summary>
    ///     Writes the document, once the Worker has written its pixels. An edit made since the text was taken is not in
    ///     the file, so the stack stays unsaved however this ends.
    /// </summary>
    public StackEditResult FinishSave(string text, long taken) {
        lock (Changing) {
            LibraryFiles.Replace(Path, text);
            fileHash = Hash(text);
            if (taken == revision) {
                editor.MarkSaved();
            }

            return Announce(GraphEventKind.Saved, null, null);
        }
    }

    /// <summary>Asks for an evaluation of the stack as it stands.</summary>
    public void Evaluate() {
        lock (Changing) {
            forced = true;
        }

        wake();
    }

    /// <summary>Ends the session: followers get a last event.</summary>
    public void Close() {
        lock (Changing) {
            Log.Append(new(Log.Sequence + 1, GraphEventKind.Closed, revision, editor.History));
            closed = true;
        }
    }

    /// <summary>The undo history as a list.</summary>
    public HistoryList Steps => Read(static held => Listed(held));

    /// <summary>
    ///     Undoes or redoes until a step is the newest that stands, as one event: followers see the stack jump.
    /// </summary>
    /// <param name="step">The step, or null to undo everything.</param>
    /// <exception cref="HistoryStepGoneException">The history does not hold the step, and nothing changed.</exception>
    public StackEditResult JumpTo(long? step) =>
        Walk(held => UndoHistory.Jump(Listed(held), step, held.Undo, held.Redo))
        ?? Read<StackEditResult>(held => new(revision, Log.Sequence, held.History));

    /// <summary>What the evaluation loop should do next, or null.</summary>
    public StackWork? Pending() {
        StackWork work;
        Func<string, TextureSetAsset?> siblings;
        lock (Changing) {
            if (closed || (settled == revision && !forced)) {
                return null;
            }

            work = new(revision, string.Empty, editor.Standing, [..editor.Forgotten], forced);
            siblings = editor.Sibling;
            var document = editor.Document;
            work = work with { Document = StackDocuments.Text(Composed(document, siblings)) };
        }

        return work;
    }

    /// <summary>
    ///     The document with every other texture set its instances read, transitively, after its own: what the Worker
    ///     compiles, so that it resolves an instance across sets from the set as it stands in its own session (#108).
    /// </summary>
    static LayerStackAsset Composed(LayerStackAsset document, Func<string, TextureSetAsset?> siblings) {
        if (Foreign(document).Count == 0) {
            return document;
        }

        List<TextureSetAsset> sets = [..document.Sets];
        Queue<string> wanted = new(Foreign(document));
        while (wanted.TryDequeue(out var name)) {
            if (sets.Exists(set => set.Name == name) || siblings(name) is not { } sibling) {
                continue;
            }

            sets.Add(sibling);
            foreach (var next in Foreign(document with { Sets = [sibling] })) {
                wanted.Enqueue(next);
            }
        }

        return document with { Sets = sets };
    }

    /// <summary>The other texture sets a document's instances name.</summary>
    static HashSet<string> Foreign(LayerStackAsset document) {
        HashSet<string> named = new(
            document.Sets.SelectMany(static set => StackEditor.Flattened(set.Layers)
                .Where(layer => layer.Kind == LayerKind.Instance
                    && layer.Instance.Set.Length > 0
                    && layer.Instance.Set != set.Name
                )
                .Select(static layer => layer.Instance.Set)
            ),
            StringComparer.Ordinal
        );

        named.ExceptWith(document.Sets.Select(static set => set.Name));
        return named;
    }

    /// <summary>
    ///     What a Worker has not been told about these strokes, read under the lock so an edit cannot race
    ///     it.
    /// </summary>
    /// <param name="entries">The strokes.</param>
    /// <param name="sent">
    ///     What it has been told: how many stamps of each, and whether that stroke was still
    ///     open.
    /// </param>
    /// <returns>The parts, and what it will have been told after them.</returns>
    public IReadOnlyList<StackPaintPart> PartsAfter(
        IReadOnlyList<StackJournalEntry> entries,
        Dictionary<long, (int Stamps, bool Open)> sent
    ) {
        lock (Changing) {
            List<StackPaintPart> parts = [];
            foreach (var entry in entries) {
                var known = sent.TryGetValue(entry.Id, out var told);
                if (known && told.Stamps == entry.Stamps.Count && told.Open == entry.Open) {
                    continue;
                }

                parts.Add(
                    new(
                        entry.Id,
                        entry.Paint,
                        entry.Usage,
                        entry.Brush,
                        entry.Colour,
                        [..entry.Stamps.Skip(told.Stamps)],
                        entry.Open,
                        entry.Model,
                        entry.View,
                        entry.Curve,
                        entry.Symmetry,
                        entry.Start,
                        entry.From,
                        entry.Capture is { } capture ? new(capture.Document, capture.Layer, capture.Backdrop) : null,
                        entry.Source,
                        entry.Stencil,
                        entry.Fill,
                        // Only with the first part: the Worker makes its source once, as the stroke begins.
                        known ? null : entry.SourceDocument
                    )
                );
                sent[entry.Id] = (entry.Stamps.Count, entry.Open);
            }

            return parts;
        }
    }

    /// <summary>Takes an evaluation in and tells the clients, with the maps that changed.</summary>
    public void Publish(StackWork work, StackResult result) {
        lock (Changing) {
            Finish(work);
            List<StackPreview> changed = [];
            if (result.Evaluated) {
                foreach (var preview in result.Previews.Where(p => previews.GetValueOrDefault(p.Usage) != p)) {
                    changed.Add(preview);
                    previews[preview.Usage] = preview;
                }
            }

            Report(
                new(
                    work.Revision,
                    result.Compiled,
                    result.Ran,
                    result.Repaired,
                    result.Reused,
                    result.Milliseconds,
                    changed,
                    result.Problems,
                    null,
                    result.Found
                )
            );
        }
    }

    /// <summary>Tells the clients why an evaluation failed. The document is untouched.</summary>
    public void Fail(StackWork work, string reason) {
        lock (Changing) {
            Finish(work);
            Report(new(work.Revision, false, 0, 0, 0, 0, [], [], reason, []));
        }
    }

    /// <summary>The events after a number; a single resync event when they are no longer held.</summary>
    public StackEventPage EventsAfter(long since) => new(Sequence, After(since));

    /// <inheritdoc />
    protected override StackEvent Resync() => new(Log.Sequence, GraphEventKind.Resync, revision, editor.History);

    static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    static IEnumerable<string> Named(IEnumerable<LayerAsset> layers) {
        foreach (var layer in layers) {
            if (layer.Paint.Trim().Length > 0) {
                yield return layer.Paint.Trim();
            }

            if (layer.Mask.Paint.Trim().Length > 0) {
                yield return layer.Mask.Paint.Trim();
            }

            foreach (var child in Named(layer.Children)) {
                yield return child;
            }
        }
    }

    static HistoryList Listed(StackEditor held) =>
        UndoHistory.List(held.DoneSteps, held.UndoneSteps, static step => new(step.Id, step.Label));

    /// <summary>What the stack says, read under the lock.</summary>
    T Read<T>(Func<StackEditor, T> read) {
        lock (Changing) {
            return read(editor);
        }
    }

    StackEditResult? Walk(Func<StackEditor, HistoryMove?> move) {
        StackEditResult result;
        lock (Changing) {
            if (move(editor) is not { } moved) {
                return null;
            }

            result = Announce(moved.Kind, moved.Label, []);
        }

        wake();
        Changed?.Invoke(this);
        return result;
    }

    StackEditResult Announce(GraphEventKind kind, string? label, IReadOnlyList<StackPainted>? painted) {
        published = editor.Document;
        if (painted is not null) {
            revision++;
        }

        var delta = painted is null
            ? null
            : new StackDelta(StackDocuments.Layers(editor.Set.Layers), painted, StackDocuments.Anchors(editor.Set));
        var history = editor.History;
        Log.Append(new(Log.Sequence + 1, kind, revision, history, label, delta));
        return new(revision, Log.Sequence, history);
    }

    void Finish(StackWork work) {
        settled = work.Revision;
        forced = forced && !work.Forced;
        editor.Forgotten.RemoveAll(work.Forget.Contains);
    }

    void Report(StackEvaluation report) {
        evaluation = report;
        Log.Append(new(Log.Sequence + 1, GraphEventKind.Evaluated, revision, editor.History, null, null, report));
    }
}
