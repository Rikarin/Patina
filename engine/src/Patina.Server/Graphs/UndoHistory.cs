using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;
using Patina.Server.Api;

namespace Patina.Server.Graphs;

/// <summary>A jump through an undo history: which way it went, and the last step it passed.</summary>
/// <param name="Kind"><see cref="GraphEventKind.Undone" /> or <see cref="GraphEventKind.Redone" />.</param>
/// <param name="Label">What the last step passed is called.</param>
public sealed record HistoryMove(GraphEventKind Kind, string Label);

/// <summary>
///     A jump to a step the history no longer holds: another client's edit dropped it. Nothing has changed.
/// </summary>
public sealed class HistoryStepGoneException : Exception {
    public HistoryStepGoneException() { }

    public HistoryStepGoneException(string message) : base(message) { }

    public HistoryStepGoneException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///     What a graph's and a stack's histories share: the list a history pane shows, a jump through it, and the
///     endpoint's answer. Each editor keeps its steps its own way, so this reads the list and is told how to step.
/// </summary>
public static class UndoHistory {
    /// <summary>The steps that stand, oldest first, and the ones undone, the next redo first.</summary>
    /// <param name="done">The steps that stand, oldest first.</param>
    /// <param name="undone">The steps undone, the next redo last, as an editor keeps them.</param>
    /// <param name="read">What a step is called and named by.</param>
    public static HistoryList List<T>(IReadOnlyList<T> done, IReadOnlyList<T> undone, Func<T, HistoryStep> read) =>
        new([..done.Select(read)], [..undone.Reverse().Select(read)]);

    /// <summary>
    ///     Undoes or redoes until <paramref name="step" /> is the newest that stands, or nothing does if null.
    /// </summary>
    /// <param name="list">The history as it stands.</param>
    /// <param name="step">The step to stand at.</param>
    /// <param name="undo">Takes back one step and says what it was called.</param>
    /// <param name="redo">Puts back one step and says what it was called.</param>
    /// <returns>Which way it went and the last step it passed, or null when the history already stood there.</returns>
    /// <exception cref="HistoryStepGoneException">The history does not hold the step.</exception>
    public static HistoryMove? Jump(HistoryList list, long? step, Func<string?> undo, Func<string?> redo) {
        var done = step is null ? -1 : IndexOf(list.Done, step.Value);
        var undone = step is null ? -1 : IndexOf(list.Undone, step.Value);
        if (step is not null && done < 0 && undone < 0) {
            throw new HistoryStepGoneException($"The history no longer holds step {step}.");
        }

        var back = undone < 0;
        var count = back ? list.Done.Count - 1 - done : undone + 1;
        HistoryMove? moved = null;
        for (var i = 0; i < count; i++) {
            if ((back ? undo() : redo()) is { } label) {
                moved = new(back ? GraphEventKind.Undone : GraphEventKind.Redone, label);
            }
        }

        return moved;
    }

    /// <summary>A step that undo or redo passed, or null when there was none.</summary>
    public static HistoryMove? Moved(string? label, GraphEventKind kind) => label is null ? null : new(kind, label);

    /// <summary>A jump's answer for an endpoint: the result, or the refusal a step that has gone gets.</summary>
    public static Results<Ok<T>, ProblemHttpResult> Answer<T>(Func<T> jump, long? step) {
        try {
            return TypedResults.Ok(jump());
        } catch (HistoryStepGoneException e) {
            return Refusals.Of(e, step);
        }
    }

    static int IndexOf(IReadOnlyList<HistoryStep> steps, long id) {
        for (var i = 0; i < steps.Count; i++) {
            if (steps[i].Id == id) {
                return i;
            }
        }

        return -1;
    }
}
