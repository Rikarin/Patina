using System.Globalization;
using Patina.Contracts;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     A session's quick mask (#127): a canvas the journal paints like any other, which no layer names and no save
///     writes. Strokes kept to it read it in the Worker as they reach each texel, so a replay keeps them where they
///     were kept.
/// </summary>
/// <remarks>
///     ⚠ <b>Cleared by moving to a new file, not by an entry that empties the old one.</b> A stroke kept to the old
///     one names it in the journal, and a replay has to find it as it stood; emptying it in place would be a step of
///     history that undoing a stroke painted before it could not see past.
/// </remarks>
/// <param name="stem">The stack's file name without its extension.</param>
/// <param name="set">The texture set.</param>
sealed class StackQuickMask(string stem, string set) {
    int generation = 1;

    /// <summary>Whether the viewport lays it over the model.</summary>
    public bool Shown { get; set; }

    /// <summary>The paint file, relative to the stack, of the quick mask strokes paint now.</summary>
    public string Paint =>
        LayerPaint.NameFor(
            stem,
            set,
            string.Create(CultureInfo.InvariantCulture, $"{StackQuickMasks.FileMarker[1..]}{generation}")
        );

    /// <summary>Starts a new, empty quick mask.</summary>
    public void Clear() => generation++;

    /// <summary>What a client is told: shown or not, and whether a stroke that stands has painted it.</summary>
    public StackQuickMaskState State(IEnumerable<StackJournalEntry> standing) => new(Shown, Painted(standing));

    /// <summary>Whether a stroke that stands has painted the quick mask, so keeping to it means anything.</summary>
    public bool Painted(IEnumerable<StackJournalEntry> standing) =>
        standing.Any(entry => string.Equals(entry.Paint, Paint, StringComparison.Ordinal));
}
