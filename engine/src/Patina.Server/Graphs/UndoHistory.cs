using Patina.Contracts;

namespace Patina.Server.Graphs;

/// <summary>A jump through an undo history: which way it went, and the last step it passed.</summary>
/// <param name="Kind"><see cref="GraphEventKind.Undone" /> or <see cref="GraphEventKind.Redone" />.</param>
/// <param name="Label">What the last step passed is called.</param>
public sealed record HistoryMove(GraphEventKind Kind, string Label);

/// <summary>A jump to a step the history no longer holds: another client's edit dropped it. Nothing has changed.</summary>
public sealed class HistoryStepGoneException : Exception {
    public HistoryStepGoneException() { }

    public HistoryStepGoneException(string message) : base(message) { }

    public HistoryStepGoneException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///     What a graph's and a stack's histories share: the list a history pane shows, and a jump through it. Each editor
///     keeps its steps its own way, so this is told how to read one and how to undo and redo.
/// </summary>
static class UndoHistory {
    /// <summary>The steps that stand, oldest first, and the ones undone, the next redo first.</summary>
    public static HistoryList List<T>(IReadOnlyList<T> undoable, IReadOnlyList<T> redoable, Func<T, HistoryStep> read) =>
        new([..undoable.Select(read)], [..redoable.Reverse().Select(read)]);

    /// <summary>Undoes or redoes until <paramref name="step" /> is the newest that stands, or nothing does if null.</summary>
    /// <returns>Which way it went and the last step it passed, or null when the history already stood there.</returns>
    /// <exception cref="HistoryStepGoneException">The history does not hold the step.</exception>
    public static HistoryMove? Jump<T>(
        IReadOnlyList<T> undoable,
        IReadOnlyList<T> redoable,
        Func<T, long> id,
        long? step,
        Func<string?> undo,
        Func<string?> redo
    ) {
        var back = step is null || undoable.Any(held => id(held) == step);
        if (!back && !redoable.Any(held => id(held) == step)) {
            throw new HistoryStepGoneException($"The history no longer holds step {step}.");
        }

        HistoryMove? moved = null;
        while ((undoable.Count > 0 ? id(undoable[^1]) : (long?)null) != step) {
            var label = back ? undo() : redo();
            if (label is null) {
                break; // cannot happen: the step was found on the way
            }

            moved = new(back ? GraphEventKind.Undone : GraphEventKind.Redone, label);
        }

        return moved;
    }
}
