using Patina.Contracts;
using Patina.Ipc;
using Patina.Library;
using System.Security.Cryptography;
using System.Text;
using Vixen.NodeGraph;

namespace Patina.Server.Graphs;

/// <summary>The file changed behind an open session. Saving would lose someone's work.</summary>
public sealed class GraphFileChangedException : Exception {
    public GraphFileChangedException() { }

    public GraphFileChangedException(string message) : base(message) { }

    public GraphFileChangedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>What the evaluation loop has to do for a session.</summary>
/// <param name="Revision">The revision this document is.</param>
/// <param name="Document">The document.</param>
/// <param name="Forced">Whether to evaluate even if the Worker already has this document.</param>
public sealed record GraphWork(long Revision, NodeGraphAsset Document, bool Forced);

/// <summary>
///     One open graph. The Server owns the document, its history and its numbered events, and none of it depends on a
///     Worker: edits work with none running, and evaluation catches up when one connects. Every client sees the same
///     events, and the log lets a client that dropped its socket catch up from the number it last saw.
/// </summary>
public sealed class GraphSession {
    const int LogCapacity = 1024;

    readonly Lock changing = new();
    readonly GraphEditor editor;
    readonly Queue<GraphEvent> log = new();
    readonly Dictionary<(int Node, string Port), GraphPreview> previews = [];
    readonly Action wake;
    TaskCompletionSource next = NewSignal();
    string fileHash;
    long revision = 1;
    long sequence;
    long settled;
    bool forced;
    bool closed;
    GraphEvaluation? evaluation;

    public GraphSession(string path, string text, NodeTypeRegistry registry, string? folder, Action wake) {
        Id = Guid.NewGuid().ToString("N");
        Path = path;
        Folder = folder;
        Registry = registry;
        fileHash = Hash(text);
        editor = new(GraphDocuments.Parse(text), registry);
        this.wake = wake;
    }

    public string Id { get; }

    public string Path { get; }

    /// <summary>The folder its compounds come from.</summary>
    public string? Folder { get; }

    public NodeTypeRegistry Registry { get; }

    public GraphSummary Summary {
        get {
            lock (changing) {
                return new(Id, Path, editor.Document.Name, revision, editor.History.Dirty);
            }
        }
    }

    /// <summary>The whole document, the number of the last event it includes, and the newest previews.</summary>
    public GraphState State {
        get {
            lock (changing) {
                var document = editor.Document;
                HashSet<int> nodes = [..document.Nodes.Select(static node => node.Id)];
                return new(
                    Id,
                    Path,
                    document.Name,
                    revision,
                    sequence,
                    editor.History,
                    [..document.Nodes.Select(GraphDocuments.State)],
                    [..document.Edges.Select(GraphDocuments.State)],
                    document.Settings,
                    [..document.Parameters.Select(GraphDocuments.Definition)],
                    [..previews.Values.Where(preview => nodes.Contains(preview.Node))],
                    [..editor.Repairs.Select(GraphDocuments.Diagnostic)],
                    evaluation
                );
            }
        }
    }

    public IReadOnlyList<string> PreviewHashes {
        get {
            lock (changing) {
                return [..previews.Values.Select(static preview => preview.Hash)];
            }
        }
    }

    /// <summary>Applies a batch whole. A refused batch changes nothing and sends no event.</summary>
    /// <exception cref="GraphEditRefusedException">A command cannot be applied.</exception>
    public GraphEditResult Apply(GraphCommandBatch batch) {
        GraphEditResult result;
        lock (changing) {
            var before = editor.Document;
            var created = editor.Apply(batch);
            result = Announce(GraphEventKind.Edited, editor.History.UndoLabel, before, created);
        }

        wake();
        return result;
    }

    /// <returns>Null when there was nothing to undo.</returns>
    public GraphEditResult? Undo() => Walk(static e => e.Undo(), GraphEventKind.Undone);

    /// <returns>Null when there was nothing to redo.</returns>
    public GraphEditResult? Redo() => Walk(static e => e.Redo(), GraphEventKind.Redone);

    /// <summary>Writes the document to its file.</summary>
    /// <param name="force">Write even though the file changed behind this session.</param>
    /// <exception cref="GraphFileChangedException">The file is not what this session read or wrote.</exception>
    public GraphEditResult Save(bool force) {
        lock (changing) {
            if (!force && File.Exists(Path) && Hash(File.ReadAllText(Path)) != fileHash) {
                throw new GraphFileChangedException($"'{Path}' changed on disk after it was opened.");
            }

            var text = GraphDocuments.Text(editor.Document);
            LibraryFiles.Replace(Path, text);
            fileHash = Hash(text);
            editor.MarkSaved();
            return Announce(GraphEventKind.Saved, null, null, []);
        }
    }

    /// <summary>Asks for an evaluation of the document as it stands, as after deleting the cache.</summary>
    public void Evaluate() {
        lock (changing) {
            forced = true;
        }

        wake();
    }

    /// <summary>Ends the session: followers get a last event.</summary>
    public void Close() {
        lock (changing) {
            Announce(GraphEventKind.Closed, null, null, []);
            closed = true;
        }
    }

    /// <summary>What the evaluation loop should do next, or null.</summary>
    public GraphWork? Pending() {
        lock (changing) {
            return closed || (settled == revision && !forced) ? null : new(revision, editor.Document, forced);
        }
    }

    /// <summary>Says a revision needs no evaluation: only positions changed.</summary>
    public void Settle(GraphWork work) {
        lock (changing) {
            Finish(work);
        }
    }

    /// <summary>Takes an evaluation in and tells the clients, with the previews that changed.</summary>
    public void Publish(GraphWork work, GraphResult result) {
        lock (changing) {
            Finish(work);
            List<GraphPreview> changed = [];
            if (result.Evaluated) {
                var now = result.Previews
                    .Select(static p => new GraphPreview(p.Node, p.Port, p.Hash, p.Width, p.Height))
                    .ToDictionary(static preview => (preview.Node, preview.Port));
                changed.AddRange(
                    now.Where(p => previews.GetValueOrDefault(p.Key) != p.Value).Select(static p => p.Value)
                );
                previews.Clear();
                foreach (var (key, preview) in now) {
                    previews[key] = preview;
                }
            }

            GraphDiagnostic[] diagnostics = [
                ..result.Problems.Select(static p => new GraphDiagnostic(
                        p.Id,
                        p.Message,
                        p.Severity,
                        p.Node != 0 ? p.Node : null,
                        p.Port.Length > 0 ? p.Port : null
                    )
                )
            ];
            Report(
                new(
                    work.Revision,
                    result.Compiled,
                    result.Ran,
                    result.Reused,
                    result.Milliseconds,
                    changed,
                    diagnostics,
                    null
                )
            );
        }
    }

    /// <summary>Tells the clients why an evaluation failed. The document is untouched.</summary>
    public void Fail(GraphWork work, string reason) {
        lock (changing) {
            Finish(work);
            Report(new(work.Revision, false, 0, 0, 0, [], [], reason));
        }
    }

    /// <summary>The events after a number; a single resync event when they are no longer held.</summary>
    public GraphEventPage EventsAfter(long since) {
        lock (changing) {
            var oldest = log.Count > 0 ? log.Peek().Sequence : sequence + 1;
            if (since > sequence || since < oldest - 1) {
                return new(sequence, [new(sequence, GraphEventKind.Resync, revision, editor.History)]);
            }

            return new(sequence, [..log.Where(e => e.Sequence > since)]);
        }
    }

    /// <summary>Completes when there is an event after <paramref name="since" />.</summary>
    public Task WaitAsync(long since, CancellationToken cancellationToken) {
        lock (changing) {
            return sequence > since ? Task.CompletedTask : next.Task.WaitAsync(cancellationToken);
        }
    }

    static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    GraphEditResult? Walk(Func<GraphEditor, string?> move, GraphEventKind kind) {
        GraphEditResult? result;
        lock (changing) {
            var before = editor.Document;
            if (move(editor) is not { } label) {
                return null;
            }

            result = Announce(kind, label, before, []);
        }

        wake();
        return result;
    }

    GraphEditResult Announce(
        GraphEventKind kind,
        string? label,
        NodeGraphAsset? before,
        IReadOnlyList<GraphCreatedNode> created
    ) {
        if (before is not null) {
            revision++;
        }

        var delta = before is null ? null : GraphDocuments.Delta(before, editor.Document);
        var history = editor.History;
        Append(new(sequence + 1, kind, revision, history, label, delta));
        return new(revision, sequence, history, created);
    }

    void Finish(GraphWork work) {
        settled = work.Revision;
        forced = forced && !work.Forced;
    }

    void Report(GraphEvaluation report) {
        evaluation = report;
        Append(new(sequence + 1, GraphEventKind.Evaluated, revision, editor.History, null, null, report));
    }

    void Append(GraphEvent item) {
        sequence = item.Sequence;
        log.Enqueue(item);
        if (log.Count > LogCapacity) {
            log.Dequeue();
        }

        var signal = next;
        next = NewSignal();
        signal.TrySetResult();
    }
}
