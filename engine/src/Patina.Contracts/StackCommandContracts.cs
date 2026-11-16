using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>One edit of a stack. A batch of these is one undo step.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
// One or two to a section, in no repeating pattern. SK7020 honours no suppression, and a run of sections all alike here
// and on GraphCommand is a clone to it whatever order they take, once there are enough of them.
[JsonDerivedType(typeof(StackAddLayer), "addLayer"), JsonDerivedType(typeof(StackRemoveLayer), "removeLayer")]
[JsonDerivedType(typeof(StackMoveLayer), "moveLayer")]
[JsonDerivedType(typeof(StackSetLayer), "setLayer")]
[JsonDerivedType(typeof(StackStroke), "stroke"), JsonDerivedType(typeof(StackSetModel), "setModel")]
[JsonDerivedType(typeof(StackAddChannel), "addChannel"), JsonDerivedType(typeof(StackNestLayer), "nestLayer")]
[JsonDerivedType(typeof(StackRemoveChannel), "removeChannel")]
[JsonDerivedType(typeof(StackSetChannelFormat), "setChannelFormat")]
[JsonDerivedType(typeof(StackSetUserChannel), "setUserChannel")]
[JsonDerivedType(typeof(StackSetSeed), "setSeed"), JsonDerivedType(typeof(StackDuplicateLayers), "duplicateLayers")]
[JsonDerivedType(typeof(StackPasteLayers), "pasteLayers")]
[JsonDerivedType(typeof(StackFlattenLayer), "flattenLayer"),
 JsonDerivedType(typeof(StackUnflattenLayer), "unflattenLayer")]
[JsonDerivedType(typeof(StackSetHeightNormal), "setHeightNormal")]
[JsonDerivedType(typeof(StackSetSettings), "setSettings"), JsonDerivedType(typeof(StackCarryPaint), "carryPaint")]
[JsonDerivedType(typeof(StackAddMaskLayer), "addMaskLayer")]
[JsonDerivedType(typeof(StackRemoveMaskLayer), "removeMaskLayer"),
 JsonDerivedType(typeof(StackMoveMaskLayer), "moveMaskLayer")]
[JsonDerivedType(typeof(StackSetMaskLayer), "setMaskLayer"), JsonDerivedType(typeof(StackAddInstance), "addInstance")]
[JsonDerivedType(typeof(StackSetInstance), "setInstance")]
[JsonDerivedType(typeof(StackApplyMask), "applyMask")]
[JsonDerivedType(typeof(StackKeepQuickMask), "keepQuickMask")]
public abstract record StackCommand;

/// <summary>Adds a layer at the top of the stack.</summary>
/// <param name="Kind">fill, paint, filter or group.</param>
/// <param name="Name">What it is called, with a number after it when a layer has that name already.</param>
/// <param name="Values">
///     What a fill writes to each channel, as four numbers, or null for the default of each one.
///     Ignored for other kinds.
/// </param>
/// <param name="Parent">The group it goes at the top of, or null for the stack itself.</param>
/// <param name="Filter">What a filter layer does: Levels, Hsl, Blur, Invert or Grayscale.</param>
/// <param name="Material">
///     A material to unfold into the stack as layers: a shipped one as <c>smart:Name</c>, or a stack below the library.
///     Then <paramref name="Kind" /> and <paramref name="Name" /> say nothing.
/// </param>
/// <param name="FilterNode">The node or compound a filter layer runs, instead of a named filter.</param>
public sealed record StackAddLayer(
    string Kind,
    string Name,
    IReadOnlyDictionary<string, IReadOnlyList<float>>? Values = null,
    string? Parent = null,
    string? Filter = null,
    string? Material = null,
    string? FilterNode = null
) : StackCommand;

/// <summary>Moves a layer to the top of a group, or of the stack.</summary>
/// <param name="Layer">Which.</param>
/// <param name="Parent">The group, or null for the stack itself.</param>
public sealed record StackNestLayer(string Layer, string? Parent) : StackCommand;

/// <summary>Removes a layer.</summary>
/// <param name="Layer">Which.</param>
public sealed record StackRemoveLayer(string Layer) : StackCommand;

/// <summary>Moves a layer up or down its list.</summary>
/// <param name="Layer">Which.</param>
/// <param name="Delta">How many places, up the stack when positive.</param>
public sealed record StackMoveLayer(string Layer, int Delta) : StackCommand;

// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005

/// <summary>Changes what a layer's row shows. What is null stays.</summary>
/// <param name="Layer">Which.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Enabled">Whether it composites.</param>
/// <param name="Opacity">How much of it composites, 0 to 1.</param>
/// <param name="Blend">Its blend mode.</param>
/// <param name="Channels">Which channels it writes. Empty means all of them.</param>
/// <param name="Fill">What a fill reads: Constant, Texture or Graph.</param>
/// <param name="Values">What a constant fill writes, replacing what these channels held.</param>
/// <param name="Textures">What a texture fill reads, replacing what these channels held.</param>
/// <param name="Graph">The graph a graph fill reads.</param>
/// <param name="Projection">How a fill lands: Uv, Triplanar, Planar, Spherical, Cylindrical, Decal or Path.</param>
/// <param name="Filter">What a filter does, one of the five named filters.</param>
/// <param name="Settings">
///     A filter's numbers, replacing what these names held. Choosing another filter starts it from its own defaults.
/// </param>
/// <param name="Mask">
///     Its mask, which replaces the old one and keeps any painted canvas. A source of None takes the mask away, whole.
/// </param>
/// <param name="MeshMask">The named meshes it is kept to, or null to leave that as it is.</param>
/// <param name="PlanarAxis">The world axis a planar projection looks along or a cylinder wraps: X, Y or Z.</param>
/// <param name="Placement">Where a fill sits on the surface, which replaces where it sat.</param>
/// <param name="Parameters">What a graph fill sets the graph's knobs to, replacing what these names held.</param>
/// <param name="FilterNode">The node or compound a filter runs, or empty to go back to the named filter.</param>
/// <param name="AnchorPoint">
///     The anchor point it publishes, or empty for none. A new name rewrites every reference to the old one, in the
///     same step.
/// </param>
/// <param name="Anchor">The anchor point an Anchor fill reads.</param>
/// <param name="Inputs">
///     Where the image inputs of a graph fill or a filter read, by port, replacing all of them.
/// </param>
/// <param name="Decal">The box a Decal fill is projected through, replacing where it was.</param>
/// <param name="Path">The curve a Path fill is laid along, replacing the curve it had.</param>
/// <param name="Tag">The colour it is marked with, from the vocabulary's tags: None takes the mark away.</param>
public sealed record StackSetLayer(
    string Layer,
    string? Name = null,
    bool? Enabled = null,
    float? Opacity = null,
    string? Blend = null,
    IReadOnlyList<string>? Channels = null,
    string? Fill = null,
    IReadOnlyDictionary<string, IReadOnlyList<float>>? Values = null,
    IReadOnlyDictionary<string, string>? Textures = null,
    string? Graph = null,
    string? Projection = null,
    string? Filter = null,
    IReadOnlyDictionary<string, IReadOnlyList<float>>? Settings = null,
    StackMask? Mask = null,
    StackMeshMask? MeshMask = null,
    string? PlanarAxis = null,
    StackPlacement? Placement = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    string? FilterNode = null,
    string? AnchorPoint = null,
    StackAnchorRef? Anchor = null,
    IReadOnlyDictionary<string, StackInput>? Inputs = null,
    StackDecal? Decal = null,
    StackSurfacePath? Path = null,
    string? Tag = null
) : StackCommand {
    /// <summary>
    ///     How a texture fill's normal and coat normal pictures are read, <c>y-up</c> or <c>y-down</c> (#310). Null
    ///     keeps it, unless the textures it lays put a picture in the normal or coat normal channel: then it is the
    ///     project's.
    /// </summary>
    public string? NormalFormat { get; init; }
}
#pragma warning restore SK7005

/// <summary>Where a fill sits on the surface, in whichever projection it uses.</summary>
/// <param name="Scale">How many times the picture repeats. Above zero.</param>
/// <param name="Rotation">How far it turns, in radians.</param>
/// <param name="OffsetX">How far it moves along its first axis, in repeats.</param>
/// <param name="OffsetY">How far it moves along its second axis, in repeats.</param>
/// <param name="Sharpness">How hard the seam between a tri-planar's planes is.</param>
public sealed record StackPlacement(
    float Scale = 1f,
    float Rotation = 0f,
    float OffsetX = 0f,
    float OffsetY = 0f,
    float Sharpness = 4f
);

/// <summary>Names the mesh a stack is painted on, which is undone like any other edit.</summary>
/// <param name="Model">A model file, absolute or relative to the stack. Empty takes the mesh away.</param>
public sealed record StackSetModel(string Model) : StackCommand;

/// <summary>Adds a channel to the stack, so that layers can write it and a preset can export it.</summary>
/// <param name="Usage">Which, one of the vocabulary's usages.</param>
public sealed record StackAddChannel(string Usage) : StackCommand;

/// <summary>
///     Takes a channel off the texture set (#141): no layer writes it and no export finds it. What the layers held for
///     it, their values and paint, stays in them, so adding the channel again brings it back.
/// </summary>
/// <param name="Usage">Which, one the set has. A set keeps at least one channel.</param>
public sealed record StackRemoveChannel(string Usage) : StackCommand;

/// <summary>
///     How wide the texels are that strokes paint a channel at (#19): eight bits a lane, or sixteen or thirty-two bit
///     floats, which keep what eight bits round away, for a height above all. One undo step, <i>Channel depth</i>. A
///     layer painted before is widened, exactly, by its next stroke, unless a stroke of the session already painted it,
///     which holds the pixels it painted over; one wider than the set asks keeps its width, so paint is never rounded.
/// </summary>
/// <param name="Usage">Which channel, one the set has.</param>
/// <param name="Format">One of the vocabulary's formats.</param>
public sealed record StackSetChannelFormat(string Usage, string Format) : StackCommand;

/// <summary>
///     Names a user channel and says whether it holds a color (#141), as one undo step, <i>User channel</i>. A user
///     channel, <c>user0</c> to <c>user7</c>, means nothing to the material: its name is what a person reads, and its
///     usage stays what a preset and a graph name it by. What is left out stays as it is.
/// </summary>
/// <param name="Usage">Which channel, a user channel the set has.</param>
/// <param name="Name">What it is called; empty for its usage's own label.</param>
/// <param name="Colour">Whether it holds a color, painted and chosen as one, rather than one number.</param>
public sealed record StackSetUserChannel(string Usage, string? Name = null, bool? Colour = null) : StackCommand;

/// <summary>
///     Duplicates layers, each just above itself, with everything inside it. A painted canvas is copied, never shared:
///     a stroke on the copy leaves the original alone.
/// </summary>
/// <param name="Layers">Which. A layer inside another of them goes with that one.</param>
public sealed record StackDuplicateLayers(IReadOnlyList<string> Layers) : StackCommand;

/// <summary>
///     Pastes what a copy holds, as new layers with canvases of their own, in this stack or another. Limits to named
///     meshes stay only in the stack they were made for.
/// </summary>
/// <param name="Clip">What <c>POST /stacks/{id}/clip</c> answered.</param>
/// <param name="Above">The layer they go just above, or null for the top of the stack.</param>
/// <param name="AsInstances">
///     Whether each comes in as an instance of the layer that was copied rather than as a copy, so that an edit to the
///     original shows in it. The original has to be in this stack or another texture set of its model.
/// </param>
/// <param name="Into">
///     A layer to paste part of the first copied layer into, rather than adding layers: its mask, or its content.
/// </param>
/// <param name="Part">
///     With <paramref name="Into" />: Mask takes the copied layer's mask, entries, effects and painted canvases, and
///     keeps the layer's own limit to some meshes; Content takes what the copied layer puts on top (its kind, its fill
///     or canvas, its filter, a folder's layers) and keeps the layer's name, mask, blend, opacity and anchor point.
/// </param>
public sealed record StackPasteLayers(
    string Clip,
    string? Above = null,
    bool AsInstances = false,
    string? Into = null,
    string Part = "Mask"
) : StackCommand;

/// <summary>
///     Flattens a group into a canvas that composites what its children did, keeping them, unread, as the way back. The
///     whole stack is flattened by gathering every layer into one group first. Undoing it takes the canvas away.
/// </summary>
/// <param name="Layer">The group, or null for the whole stack.</param>
public sealed record StackFlattenLayer(string? Layer = null) : StackCommand;

/// <summary>Takes a flattened group back to its children, which composite again.</summary>
/// <param name="Layer">The group.</param>
public sealed record StackUnflattenLayer(string Layer) : StackCommand;

/// <summary>
///     Adds an entry to a layer's mask stack. A layer with no mask gets a white one under it first, so that the entry
///     has something to composite over.
/// </summary>
/// <param name="Layer">The layer whose mask it is.</param>
/// <param name="Source">Constant, Texture, Anchor, Generator, Bake or Paint.</param>
/// <param name="Index">Where it goes, counted from the bottom of the mask's stack, or null for the top.</param>
/// <param name="Generator">The compound, for a generator entry, or null for the first the vocabulary offers.</param>
public sealed record StackAddMaskLayer(string Layer, string Source, int? Index = null, string? Generator = null)
    : StackCommand;

/// <summary>
///     Adds an instance of a layer just above the chosen place: a layer that stands for the source, in this texture set
///     or another of the model, so that an edit to the source shows in it.
/// </summary>
/// <param name="Source">The layer it stands for, in this texture set or another.</param>
/// <param name="Above">The layer it goes just above, or null for the top of the stack.</param>
public sealed record StackAddInstance(StackInstanceRef Source, string? Above = null) : StackCommand;

/// <summary>Points an instance at another layer, which it then stands for.</summary>
/// <param name="Layer">The instance.</param>
/// <param name="Source">The layer it stands for from now on, in this texture set or another.</param>
public sealed record StackSetInstance(string Layer, StackInstanceRef Source) : StackCommand;

/// <summary>Removes an entry from a layer's mask stack.</summary>
/// <param name="Layer">The layer whose mask it is.</param>
/// <param name="Index">Which, counted from the bottom.</param>
public sealed record StackRemoveMaskLayer(string Layer, int Index) : StackCommand;

/// <summary>Moves an entry up or down a layer's mask stack.</summary>
/// <param name="Layer">The layer whose mask it is.</param>
/// <param name="Index">Which, counted from the bottom.</param>
/// <param name="Delta">How many places, up the stack when positive.</param>
public sealed record StackMoveMaskLayer(string Layer, int Index, int Delta) : StackCommand;

// SK7005: a wire record's constructor is its JSON shape, one parameter per field.
#pragma warning disable SK7005

/// <summary>Changes an entry of a layer's mask stack. What is null stays.</summary>
/// <param name="Layer">The layer whose mask it is.</param>
/// <param name="Index">Which, counted from the bottom.</param>
/// <param name="Source">Where its pixels come from. A paint entry keeps its canvas whatever it becomes.</param>
/// <param name="Value">A constant entry's value, 0 to 1.</param>
/// <param name="Asset">The image a texture entry reads.</param>
/// <param name="Anchor">The anchor point an anchor entry reads.</param>
/// <param name="Generator">The compound a generator entry runs. Another compound starts from its own knobs.</param>
/// <param name="Map">The mesh map a bake entry reads.</param>
/// <param name="Parameters">A generator entry's knobs, replacing what these names held.</param>
/// <param name="Blend">How it composites.</param>
/// <param name="Opacity">How much of it composites, 0 to 1.</param>
/// <param name="Enabled">Whether it composites.</param>
/// <param name="Lane">Which lane of its picture it reads: Red, Green, Blue or Alpha.</param>
/// <param name="Inputs">
///     The image inputs of a generator entry that anchor points feed, by port, replacing all of them. Another
///     generator starts with none.
/// </param>
/// <param name="Output">
///     Which picture of a generator entry that writes several it reads, or empty for its only one. Another generator
///     starts from empty.
/// </param>
/// <param name="Projection">How a texture entry lands: Uv, Triplanar, Planar, Spherical or Cylindrical.</param>
/// <param name="Placement">Where a projected texture entry sits, which replaces where it sat.</param>
public sealed record StackSetMaskLayer(
    string Layer,
    int Index,
    string? Source = null,
    float? Value = null,
    string? Asset = null,
    StackAnchorRef? Anchor = null,
    string? Generator = null,
    string? Map = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    string? Blend = null,
    float? Opacity = null,
    bool? Enabled = null,
    string? Lane = null,
    IReadOnlyDictionary<string, StackAnchorRef>? Inputs = null,
    string? Output = null,
    string? Projection = null,
    StackPlacement? Placement = null
) : StackCommand;
#pragma warning restore SK7005

/// <summary>Copies layers out of a stack, with their pixels as they stand now.</summary>
/// <param name="Layers">Which. A layer inside another of them goes with that one.</param>
public sealed record StackClipRequest(IReadOnlyList<string> Layers);

/// <summary>Layers copied out of a stack, which a <see cref="StackPasteLayers" /> names.</summary>
/// <param name="Id">What a paste names it by. A Server that restarts has none.</param>
/// <param name="Layers">How many layers it holds at the top.</param>
/// <param name="Stack">The stack they came from.</param>
public sealed record StackClip(string Id, int Layers, string Stack);

/// <summary>Sets the number every procedural step of the stack derives from.</summary>
/// <param name="Seed">It.</param>
public sealed record StackSetSeed(uint Seed) : StackCommand;

/// <summary>Edits applied whole or not at all, as one undo step.</summary>
/// <param name="Commands">The edits, in order.</param>
/// <param name="Label">What the undo step says.</param>
public sealed record StackCommandBatch(IReadOnlyList<StackCommand> Commands, string? Label = null);
