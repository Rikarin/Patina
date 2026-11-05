using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>Opens a layer stack.</summary>
/// <param name="Path">A <c>.vxlayers</c> file, absolute or relative to the library.</param>
public sealed record StackOpenRequest(string Path);

/// <summary>Creates a layer stack: one base fill, and one paint layer over it.</summary>
/// <param name="Path">The file to write, absolute or relative to the library.</param>
/// <param name="Width">Its width in texels.</param>
/// <param name="Height">Its height in texels.</param>
public sealed record StackCreateRequest(string Path, int Width, int Height);

/// <summary>A channel a stack writes.</summary>
/// <param name="Usage">Its usage: baseColor, roughness and so on.</param>
/// <param name="Default">What it starts as.</param>
public sealed record StackChannel(string Usage, IReadOnlyList<float> Default);

/// <summary>One layer, without its pixels.</summary>
/// <param name="Id">Its identifier, stable across renames.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Kind">fill, paint, filter or group.</param>
/// <param name="Enabled">Whether it composites.</param>
/// <param name="Opacity">How much of it composites.</param>
/// <param name="Blend">Its blend mode.</param>
/// <param name="Channels">Which channels it writes. Empty means all of them.</param>
/// <param name="Painted">Whether it names a paint file yet.</param>
/// <param name="Children">What a group holds.</param>
public sealed record StackLayer(
    string Id,
    string Name,
    string Kind,
    bool Enabled,
    float Opacity,
    string Blend,
    IReadOnlyList<string> Channels,
    bool Painted,
    IReadOnlyList<StackLayer> Children
);

/// <summary>A map as a small picture, by the hash an event carries.</summary>
/// <param name="Usage">Which channel.</param>
/// <param name="Hash">Its name under <c>/previews/{hash}</c>.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public sealed record StackPreview(string Usage, string Hash, int Width, int Height);

/// <summary>How an evaluation went.</summary>
/// <param name="Revision">The revision it was of.</param>
/// <param name="Compiled">Whether the plan had to compile.</param>
/// <param name="Ran">How many ops ran.</param>
/// <param name="Repaired">How many of those ran over the tiles a stroke changed rather than the whole image.</param>
/// <param name="Reused">How many came from the cache.</param>
/// <param name="Milliseconds">How long the Worker took.</param>
/// <param name="Previews">The maps that changed.</param>
/// <param name="Diagnostics">Why it could not evaluate, or what it warned about.</param>
/// <param name="Error">Why the evaluation failed, or null.</param>
public sealed record StackEvaluation(
    long Revision,
    bool Compiled,
    int Ran,
    int Repaired,
    int Reused,
    double Milliseconds,
    IReadOnlyList<StackPreview> Previews,
    IReadOnlyList<string> Diagnostics,
    string? Error
);

// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005

/// <summary>A whole stack: everything a client needs to draw it, and no pixels.</summary>
/// <param name="Id">The session.</param>
/// <param name="Path">Its file.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Set">The texture set this session paints.</param>
/// <param name="Width">Its width in texels.</param>
/// <param name="Height">Its height in texels.</param>
/// <param name="Revision">The revision of the document.</param>
/// <param name="Sequence">The number of the last event it includes.</param>
/// <param name="History">Its undo history.</param>
/// <param name="Channels">The channels it writes.</param>
/// <param name="Layers">Its layers, bottom first.</param>
/// <param name="Previews">The newest maps.</param>
/// <param name="Evaluation">The last evaluation, or null.</param>
/// <param name="Settled">The newest revision an evaluation has reached.</param>
public sealed record StackState(
    string Id,
    string Path,
    string Name,
    string Set,
    int Width,
    int Height,
    long Revision,
    long Sequence,
    GraphHistoryState History,
    IReadOnlyList<StackChannel> Channels,
    IReadOnlyList<StackLayer> Layers,
    IReadOnlyList<StackPreview> Previews,
    StackEvaluation? Evaluation,
    long Settled
);
#pragma warning restore SK7005

/// <summary>An open stack, for a list.</summary>
/// <param name="Id">The session.</param>
/// <param name="Path">Its file.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Revision">The revision of the document.</param>
/// <param name="Dirty">Whether it has edits no file holds.</param>
public sealed record StackSummary(string Id, string Path, string Name, long Revision, bool Dirty);

/// <summary>One edit of a stack. A batch of these is one undo step.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(StackAddLayer), "addLayer")]
[JsonDerivedType(typeof(StackRemoveLayer), "removeLayer")]
[JsonDerivedType(typeof(StackMoveLayer), "moveLayer")]
[JsonDerivedType(typeof(StackSetLayer), "setLayer")]
[JsonDerivedType(typeof(StackStroke), "stroke")]
public abstract record StackCommand;

/// <summary>Adds a layer at the top of the stack.</summary>
/// <param name="Kind">fill, paint, filter or group.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Values">What a fill writes to each channel, as four numbers. Ignored for other kinds.</param>
public sealed record StackAddLayer(
    string Kind,
    string Name,
    IReadOnlyDictionary<string, IReadOnlyList<float>>? Values = null
) : StackCommand;

/// <summary>Removes a layer.</summary>
/// <param name="Layer">Which.</param>
public sealed record StackRemoveLayer(string Layer) : StackCommand;

/// <summary>Moves a layer up or down its list.</summary>
/// <param name="Layer">Which.</param>
/// <param name="Delta">How many places, up the stack when positive.</param>
public sealed record StackMoveLayer(string Layer, int Delta) : StackCommand;

/// <summary>Changes what a layer's row shows. What is null stays.</summary>
/// <param name="Layer">Which.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Enabled">Whether it composites.</param>
/// <param name="Opacity">How much of it composites, 0 to 1.</param>
/// <param name="Blend">Its blend mode.</param>
public sealed record StackSetLayer(
    string Layer,
    string? Name = null,
    bool? Enabled = null,
    float? Opacity = null,
    string? Blend = null
) : StackCommand;

/// <summary>Where the brush goes, in texels from the image's top left.</summary>
/// <param name="X">Its column.</param>
/// <param name="Y">Its row.</param>
public sealed record StackStamp(float X, float Y);

/// <summary>A brush. Everything but the radius has the value a new brush has.</summary>
/// <param name="Radius">In texels.</param>
/// <param name="Flow">How much one stamp lays down, 0 to 1.</param>
/// <param name="Opacity">How much a whole stroke can lay down, 0 to 1.</param>
/// <param name="Spacing">How far apart stamps are, as a share of the radius.</param>
/// <param name="Falloff">How soft its edge is, 0 to 1.</param>
public sealed record StackBrush(
    float Radius,
    float Flow = 1f,
    float Opacity = 1f,
    float Spacing = 0.15f,
    float Falloff = 0.5f
);

/// <summary>
///     Part of a stroke: where the brush went, never what it left behind. A drag sends several of these with one
///     <paramref name="Stroke" /> and they make one undo step, ended by one with <paramref name="Open" /> false.
/// </summary>
/// <param name="Stroke">A name the client chose for this drag.</param>
/// <param name="Layer">The paint layer.</param>
/// <param name="Channel">Which of its channels. Ignored for a mask.</param>
/// <param name="Brush">The brush, which the first part decides.</param>
/// <param name="Colour">Red, green, blue and alpha, 0 to 1, which the first part decides.</param>
/// <param name="Stamps">Where the brush went since the last part.</param>
/// <param name="Open">Whether more of this drag is coming.</param>
/// <param name="Mask">Whether this paints the layer's mask.</param>
public sealed record StackStroke(
    string Stroke,
    string Layer,
    string Channel,
    StackBrush Brush,
    IReadOnlyList<float> Colour,
    IReadOnlyList<StackStamp> Stamps,
    bool Open = false,
    bool Mask = false
) : StackCommand;

/// <summary>Edits applied whole or not at all, as one undo step.</summary>
/// <param name="Commands">The edits, in order.</param>
/// <param name="Label">What the undo step says.</param>
public sealed record StackCommandBatch(IReadOnlyList<StackCommand> Commands, string? Label = null);

/// <summary>Where an edit left the session.</summary>
/// <param name="Revision">The revision of the document.</param>
/// <param name="Sequence">The event it produced.</param>
/// <param name="History">The undo history.</param>
public sealed record StackEditResult(long Revision, long Sequence, GraphHistoryState History);

/// <summary>What an edit changed, so that a client need not read the whole stack again.</summary>
/// <param name="Layers">The stack's layers now.</param>
/// <param name="Painted">Each channel a stroke touched, as layer and usage.</param>
public sealed record StackDelta(IReadOnlyList<StackLayer> Layers, IReadOnlyList<StackPainted> Painted);

/// <summary>A channel a stroke touched.</summary>
/// <param name="Layer">The layer.</param>
/// <param name="Channel">The channel.</param>
public sealed record StackPainted(string Layer, string Channel);

/// <summary>Something that happened to a stack, with the same kinds and numbering as a graph's events.</summary>
/// <param name="Sequence">Its number.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Revision">The revision of the document after it.</param>
/// <param name="History">The undo history after it.</param>
/// <param name="Label">What the undo step says.</param>
/// <param name="Delta">What an edit changed.</param>
/// <param name="Evaluation">How an evaluation went.</param>
public sealed record StackEvent(
    long Sequence,
    GraphEventKind Kind,
    long Revision,
    GraphHistoryState History,
    string? Label = null,
    StackDelta? Delta = null,
    StackEvaluation? Evaluation = null
);

/// <summary>The events after a number.</summary>
/// <param name="Sequence">The newest event's number.</param>
/// <param name="Events">The events.</param>
public sealed record StackEventPage(long Sequence, IReadOnlyList<StackEvent> Events);

/// <summary>Writes each channel of a stack to a file.</summary>
/// <param name="Folder">Where, absolute or relative to the library.</param>
public sealed record StackExportRequest(string Folder);

/// <summary>A digest of a map's pixels, so that two pictures can be compared with neither crossing the API.</summary>
/// <param name="Usage">Which channel.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Digest">SHA-256 of its pixels as 8-bit RGBA, row by row.</param>
/// <param name="File">Where it was written, for an export.</param>
public sealed record StackPixels(string Usage, int Width, int Height, string Digest, string? File = null);
