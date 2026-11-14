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

    /// <summary>Whether a fill's stamps are one outline rather than clicks.</summary>
    public bool Outline { get; } = first.Outline;

    /// <summary>Whether an outline fills what its camera cannot see too.</summary>
    public bool Through { get; } = first.Through;

    /// <summary>Where the brush already stood when this stroke carries the last one on, or null.</summary>
    public IReadOnlyList<float>? Resume { get; } = first.Resume;

    /// <summary>
    ///     What a clone or a material reads, as a stack written out: the stack as it stood when the stroke began, or a
    ///     stack of the material alone. In the journal so a replay reads the same whatever was edited since.
    /// </summary>
    public string? SourceDocument { get; init; }

    /// <summary>
    ///     The texture set's size when the stroke began, which its stamps are texels of (#19), or the size a copy was
    ///     made at. In the journal so a replay after the set's resolution changed still lands where the brush went.
    ///     Zero for one with no stamps.
    /// </summary>
    public int Width { get; init; }

    /// <summary>The texture set's height when the stroke began.</summary>
    public int Height { get; init; }

    /// <summary>
    ///     How wide the texels are that the stroke paints its channel at (#19), or null for eight bits a lane. In the
    ///     journal so a replay paints at the width the stroke did, whatever the set asks since.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    ///     The colour in full, four numbers, for a stroke at a width the packed colour would round; or null.
    /// </summary>
    public IReadOnlyList<float>? Lanes { get; init; }

    /// <summary>
    ///     What the stamps of a stroke in the 2D view are multiplied by to land on a canvas larger than the set (#19),
    ///     which the rest of its drag is carried by too, or null when they are the canvas's texels already.
    /// </summary>
    public float? StampScale { get; init; }

    /// <summary>Adds the next part of the drag's stamps, carried onto the canvas as the first part was (#19).</summary>
    /// <param name="stamps">Where the brush went since the last part, as sent.</param>
    public void Extend(IReadOnlyList<StackStamp> stamps) =>
        Stamps.AddRange(StampScale is { } by ? StackStrokeScale.Stamps(stamps, by) : stamps);

    /// <summary>
    ///     With <see cref="From" />, the UV sets a copy is carried between (#210), or null for a copy laid out as what
    ///     it copies.
    /// </summary>
    public Ipc.StackCarry? Carry { get; private init; }

    /// <summary>
    ///     A canvas that starts as a copy of another laid out in another UV set (#210), made at the set's size then as
    ///     any copy is.
    /// </summary>
    /// <param name="id">Its number in the journal.</param>
    /// <param name="canvas">The new canvas, then the one it copies.</param>
    /// <param name="model">The mesh both layouts are read on.</param>
    /// <param name="size">The set's size.</param>
    /// <param name="carry">The UV sets it is carried between.</param>
    public static StackJournalEntry CarriedCopy(
        long id,
        (string Paint, string From) canvas,
        string model,
        (int Width, int Height) size,
        Ipc.StackCarry carry
    ) =>
        new(id, NoStroke, canvas.Paint, string.Empty, 0u, model, null) {
            From = canvas.From, Width = size.Width, Height = size.Height, Carry = carry
        };

    /// <summary>
    ///     What the viewport drew alone when a stroke on the mesh began (#136), or null for the whole model: a stroke
    ///     reaches only what was shown. In the journal so a replay reaches the same faces whatever is shown since.
    /// </summary>
    public ViewportIsolation? Isolation { get; init; }

    /// <summary>
    ///     The quick mask this stroke is kept to, as it was when the stroke began (#127), or null. In the journal so a
    ///     replay reads the same quick mask whatever has been cleared since.
    /// </summary>
    public string? Keep { get; init; }

    /// <summary>Whether the stroke is kept to where that quick mask is not painted.</summary>
    public bool KeepOutside { get; init; }

    public List<StackStamp> Stamps { get; } = [..first.Stamps];

    public bool Open { get; set; } = first.Open;

    /// <summary>
    ///     The canvas this entry makes <see cref="Paint" /> a copy of, relative to the stack or absolute, or null for a
    ///     stroke. In the journal so that a Worker copies it after the strokes that made it and before any on the copy.
    /// </summary>
    public string? From { get; private init; }

    /// <summary>
    ///     A canvas that starts as a copy of another, which is how a duplicate or a paste gets pixels of its own. It is
    ///     made at the set's size then, whatever the size of what it copies (#106), and the journal keeps that size so
    ///     a replay after the set's size changed makes it the same.
    /// </summary>
    public static StackJournalEntry Copy(long id, string paint, string from, string model, int width, int height) =>
        new(id, NoStroke, paint, string.Empty, 0u, model, null) { From = from, Width = width, Height = height };

    /// <summary>
    ///     A copy laid over a colour, which makes it opaque where the colour is: a quick mask copied into a mask entry
    ///     (#282), which a mask then reads by what it holds and not by what its strokes covered.
    /// </summary>
    public static StackJournalEntry CopyOver(
        long id,
        (string Paint, string From) canvas,
        string model,
        (int Width, int Height) size,
        uint under
    ) =>
        new(id, NoStroke, canvas.Paint, string.Empty, 0u, model, under) {
            From = canvas.From, Width = size.Width, Height = size.Height
        };

    /// <summary>What an entry that is no stroke is made from: no brush, no colour and nowhere it went.</summary>
    static StackStroke NoStroke => new(string.Empty, string.Empty, string.Empty, new(1f), [0f, 0f, 0f, 0f], []);

    /// <summary>
    ///     What this entry makes <see cref="Paint" /> hold instead of strokes: one group of a document, composited,
    ///     which is how a group is flattened. The document is the one that stood then, so a Worker that replays it
    ///     later writes the same canvas. Null for a stroke.
    /// </summary>
    public StackCaptureEntry? Capture { get; private init; }

    /// <summary>A canvas that starts as what one group of a document composites: a flattened group's cache.</summary>
    public static StackJournalEntry Captured(long id, string paint, StackCaptureEntry capture, string model) =>
        new(id, NoStroke, paint, string.Empty, 0u, model, null) { Capture = capture };
}

/// <summary>What a flattened group's canvas is made of.</summary>
/// <param name="Document">The stack as text, as it stood when the group was flattened.</param>
/// <param name="Layer">The group.</param>
/// <param name="Backdrop">
///     Whether the canvas holds what is beneath the group, which only the whole stack's does.
/// </param>
public sealed record StackCaptureEntry(string Document, string Layer, bool Backdrop);
