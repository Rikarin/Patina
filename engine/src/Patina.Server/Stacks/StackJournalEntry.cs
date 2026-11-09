using Patina.Contracts;

namespace Patina.Server.Stacks;

/// <summary>A stroke as the journal holds it: where the brush went, in order, which is all a Worker needs.</summary>
public sealed class StackJournalEntry(
    long id,
    StackStroke first,
    string paint,
    string usage,
    uint colour,
    string model,
    uint? start
) {
    public long Id { get; } = id;

    public string Stroke { get; } = first.Stroke;

    public string Layer { get; } = first.Layer;

    public string Paint { get; } = paint;

    public string Usage { get; } = usage;

    public StackBrush Brush { get; } = first.Brush;

    public uint Colour { get; } = colour;

    /// <summary>The mesh that stood when this was painted: a later binding must not move it.</summary>
    public string Model { get; } = model;

    /// <summary>What a canvas this stroke was the first on starts as, or null to leave it as it is.</summary>
    public uint? Start { get; } = start;

    public StackView? View { get; } = first.View;

    public string Curve { get; } = first.Curve;

    public StackSymmetry? Symmetry { get; } = first.Symmetry;

    public StackStrokeSource? Source { get; } = first.Source;

    public StackPicture? Stencil { get; } = first.Stencil;

    public string? Fill { get; } = first.Fill;

    /// <summary>
    ///     What a clone or a material reads, as a stack written out: the stack as it stood when the stroke began, or a
    ///     stack of the material alone. In the journal so a replay reads the same whatever was edited since.
    /// </summary>
    public string? SourceDocument { get; init; }

    public List<StackStamp> Stamps { get; } = [..first.Stamps];

    public bool Open { get; set; } = first.Open;

    /// <summary>
    ///     The canvas this entry makes <see cref="Paint" /> a copy of, relative to the stack or absolute, or null for a
    ///     stroke. In the journal so that a Worker copies it after the strokes that made it and before any on the copy.
    /// </summary>
    public string? From { get; private init; }

    /// <summary>
    ///     A canvas that starts as a copy of another, which is how a duplicate or a paste gets pixels of its own.
    /// </summary>
    public static StackJournalEntry Copy(long id, string paint, string from, string model) =>
        new(
            id,
            new(string.Empty, string.Empty, string.Empty, new(1f), [0f, 0f, 0f, 0f], []),
            paint,
            string.Empty,
            0u,
            model,
            null
        ) { From = from };

    /// <summary>
    ///     What this entry makes <see cref="Paint" /> hold instead of strokes: one group of a document, composited,
    ///     which is how a group is flattened. The document is the one that stood then, so a Worker that replays it
    ///     later writes the same canvas. Null for a stroke.
    /// </summary>
    public StackCaptureEntry? Capture { get; private init; }

    /// <summary>A canvas that starts as what one group of a document composites: a flattened group's cache.</summary>
    public static StackJournalEntry Captured(long id, string paint, StackCaptureEntry capture, string model) =>
        new(
            id,
            new(string.Empty, string.Empty, string.Empty, new(1f), [0f, 0f, 0f, 0f], []),
            paint,
            string.Empty,
            0u,
            model,
            null
        ) { Capture = capture };
}

/// <summary>What a flattened group's canvas is made of.</summary>
/// <param name="Document">The stack as text, as it stood when the group was flattened.</param>
/// <param name="Layer">The group.</param>
/// <param name="Backdrop">
///     Whether the canvas holds what is beneath the group, which only the whole stack's does.
/// </param>
public sealed record StackCaptureEntry(string Document, string Layer, bool Backdrop);
