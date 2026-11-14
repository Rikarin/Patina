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
/// <param name="Format">
///     How wide the texels are that a stroke paints it at (#19): one of the vocabulary's formats, Rgba8 unless the set
///     says more.
/// </param>
public sealed record StackChannel(string Usage, IReadOnlyList<float> Default, string Format = "Rgba8");

// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005

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
/// <param name="Fill">What a fill reads: Constant, Texture, Graph or Anchor.</param>
/// <param name="Values">What a constant fill writes to each channel.</param>
/// <param name="Textures">What a texture fill reads for each channel.</param>
/// <param name="Graph">The graph a graph fill reads.</param>
/// <param name="Projection">How a fill lands: Uv, Triplanar, Planar, Spherical, Cylindrical, Decal or Path.</param>
/// <param name="Filter">The named filter a file holds: Levels, Hsl, Blur, Invert or Grayscale.</param>
/// <param name="Settings">A filter's numbers.</param>
/// <param name="Mask">Its mask.</param>
/// <param name="PlanarAxis">The world axis a planar projection looks along or a cylinder wraps: X, Y or Z.</param>
/// <param name="Placement">Where a fill sits on the surface.</param>
/// <param name="Parameters">What a graph fill sets the graph's knobs to, as text.</param>
/// <param name="FilterNode">The node or compound a filter layer runs, whichever way the file names it.</param>
/// <param name="AnchorPoint">The name of the anchor point this layer publishes, or empty for none.</param>
/// <param name="Anchor">The anchor point an Anchor fill reads.</param>
/// <param name="Inputs">Where the image inputs of a graph fill or a filter read, by port.</param>
/// <param name="Flattened">
///     Whether this is a group flattened into a canvas: it composites that canvas, and its children are kept, unread,
///     as the way back.
/// </param>
/// <param name="Instance">
///     For an instance layer, the layer it stands for. It keeps its own name, switch, opacity, blend and anchor point,
///     and its own mask entries narrow the source's mask; everything else is the source's.
/// </param>
/// <param name="Decal">The box a Decal fill is projected through, or null when none has been placed.</param>
/// <param name="Path">The curve a Path fill is laid along, or null when none has been drawn.</param>
/// <param name="Tag">The colour the layer is marked with in a list, from the vocabulary's tags, or None.</param>
public sealed record StackLayer(
    string Id,
    string Name,
    string Kind,
    bool Enabled,
    float Opacity,
    string Blend,
    IReadOnlyList<string> Channels,
    bool Painted,
    IReadOnlyList<StackLayer> Children,
    string Fill,
    IReadOnlyDictionary<string, IReadOnlyList<float>> Values,
    IReadOnlyDictionary<string, string> Textures,
    string Graph,
    string Projection,
    string Filter,
    IReadOnlyDictionary<string, IReadOnlyList<float>> Settings,
    StackMask Mask,
    string PlanarAxis = "Y",
    StackPlacement? Placement = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    string FilterNode = "",
    string AnchorPoint = "",
    StackAnchorRef? Anchor = null,
    IReadOnlyDictionary<string, StackInput>? Inputs = null,
    bool Flattened = false,
    StackInstanceRef? Instance = null,
    StackDecal? Decal = null,
    StackSurfacePath? Path = null,
    string Tag = "None"
);

/// <summary>The layer an instance stands for.</summary>
/// <param name="Layer">The source layer's id.</param>
/// <param name="Set">The texture set it is in, by name, or empty for the instance's own.</param>
public sealed record StackInstanceRef(string Layer, string Set = "");

/// <summary>An instance layer and the layer it stands for, across two texture sets of one model (#108).</summary>
/// <param name="Set">The other texture set, by name.</param>
/// <param name="Layer">The layer's id in that set.</param>
/// <param name="Name">The layer's name there.</param>
/// <param name="Source">
///     For an instance in the other set, the id of the layer of this set it stands for; empty for a source.
/// </param>
public sealed record StackInstanceLink(string Set, string Layer, string Name, string Source = "");

/// <summary>
///     How a texture set's layers are tied to the model's other sets (#108): the instances elsewhere of its layers, and
///     the layers elsewhere its own instances stand for, each with its name, which a client of one set cannot read.
/// </summary>
/// <param name="Instances">Layers of other sets that stand for one of this set's.</param>
/// <param name="Sources">Layers of other sets that one of this set's instances stands for.</param>
public sealed record StackInstanceLinks(
    IReadOnlyList<StackInstanceLink> Instances,
    IReadOnlyList<StackInstanceLink> Sources
);

/// <summary>A reference to an anchor point, by its name, in one channel.</summary>
/// <param name="Anchor">The anchor point's name.</param>
/// <param name="Channel">
///     The channel read, by usage. Empty, for a fill or a filter's input only, means the channel being written.
/// </param>
public sealed record StackAnchorRef(string Anchor, string Channel = "");

/// <summary>An anchor point and every place that reads it.</summary>
/// <param name="Name">The anchor point's name.</param>
/// <param name="Layer">The id of the layer that publishes it.</param>
/// <param name="Uses">Who reads it, and in which channel.</param>
public sealed record StackAnchor(string Name, string Layer, IReadOnlyList<StackAnchorUse> Uses);

/// <summary>One place a stack reads an anchor point.</summary>
/// <param name="Layer">The id of the layer that reads it.</param>
/// <param name="Where">
///     What part of that layer reads it, in words to show: Fill, Mask, Mask layer 2, Input Warp.
/// </param>
/// <param name="Channel">The channel it reads, or empty for the channel being written.</param>
public sealed record StackAnchorUse(string Layer, string Where, string Channel);

/// <summary>What decides where a layer shows.</summary>
/// <param name="Source">None, Constant, Texture, Anchor, Generator, Bake or Paint.</param>
/// <param name="Value">A constant mask's value.</param>
/// <param name="Asset">The image a texture mask reads.</param>
/// <param name="Anchor">The anchor point an anchor mask reads, and the channel it reads it in.</param>
/// <param name="Generator">The compound a generator runs, such as Generators/Dirt.</param>
/// <param name="Map">The mesh map a bake mask reads.</param>
/// <param name="Colours">
///     The colours a bake or texture mask selects from its picture, each three numbers, or null for all of it. A texel
///     near any of them is selected.
/// </param>
/// <param name="Tolerance">How far from a colour still counts.</param>
/// <param name="Softness">How much further the selection takes to reach the background.</param>
/// <param name="Effects">What runs on the mask, in order, or null to keep them.</param>
/// <param name="Parameters">What a generator's knobs are set to, as text, or null to keep them.</param>
/// <param name="MeshParts">The named meshes the layer is limited to, or null for the whole model.</param>
/// <param name="Inputs">The image inputs of a generator that anchor points feed, by port, or null to keep them.</param>
/// <param name="Layers">
///     The mask's own stack over this base, bottom first. Ignored in a change: the mask layer commands edit it, so that
///     a painted canvas stays with its entry.
/// </param>
/// <param name="Lane">
///     Which lane of its picture the base reads: Red, Green, Blue or Alpha. A mesh map that measures a direction keeps
///     its X, Y and Z in the first three.
/// </param>
/// <param name="Enabled">
///     Whether the mask applies. Switched off, the layer composites unmasked, keeping every source, layer and effect of
///     its mask for when it is switched on again, and still keeps to the meshes it is limited to.
/// </param>
/// <param name="Inside">With colours, what the mask is where they are: its output value, 0 to 1.</param>
/// <param name="Outside">With colours, what the mask is everywhere else: its background, 0 to 1.</param>
public sealed record StackMask(
    string Source,
    float Value = 1f,
    string Asset = "",
    StackAnchorRef? Anchor = null,
    string Generator = "",
    string Map = "",
    IReadOnlyList<IReadOnlyList<float>>? Colours = null,
    float Tolerance = 0.02f,
    float Softness = 0f,
    IReadOnlyList<StackMaskEffect>? Effects = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    IReadOnlyList<string>? MeshParts = null,
    IReadOnlyDictionary<string, StackAnchorRef>? Inputs = null,
    IReadOnlyList<StackMaskLayer>? Layers = null,
    string Lane = "Red",
    bool Enabled = true,
    float Inside = 1f,
    float Outside = 0f
);

/// <summary>
///     One entry of a mask's own stack: a source, composited over the base and the entries beneath it with its own
///     blend and opacity. Paint over a generator is a Paint entry over a Generator base.
/// </summary>
/// <param name="Source">Constant, Texture, Anchor, Generator, Bake or Paint.</param>
/// <param name="Value">A constant entry's value.</param>
/// <param name="Asset">The image a texture entry reads.</param>
/// <param name="Anchor">The anchor point an anchor entry reads, and the channel it reads it in.</param>
/// <param name="Generator">The compound a generator entry runs.</param>
/// <param name="Map">The mesh map a bake entry reads.</param>
/// <param name="Parameters">What a generator entry's knobs are set to, as text.</param>
/// <param name="Inputs">The image inputs of a generator entry that anchor points feed, by port.</param>
/// <param name="Painted">Whether a paint entry has a canvas yet.</param>
/// <param name="Blend">How it composites over what is beneath it.</param>
/// <param name="Opacity">How much of it composites, 0 to 1.</param>
/// <param name="Enabled">Whether it composites at all.</param>
/// <param name="Lane">Which lane of its picture it reads: Red, Green, Blue or Alpha.</param>
public sealed record StackMaskLayer(
    string Source,
    float Value = 1f,
    string Asset = "",
    StackAnchorRef? Anchor = null,
    string Generator = "",
    string Map = "",
    IReadOnlyDictionary<string, string>? Parameters = null,
    IReadOnlyDictionary<string, StackAnchorRef>? Inputs = null,
    bool Painted = false,
    string Blend = "Copy",
    float Opacity = 1f,
    bool Enabled = true,
    string Lane = "Red"
);

/// <summary>One step run on a mask.</summary>
/// <param name="Node">The node type or compound it is.</param>
/// <param name="Enabled">Whether it runs.</param>
/// <param name="Values">Its numbers, by port.</param>
/// <param name="Texts">Its settings, by name.</param>
/// <param name="Inputs">
///     Where its other image inputs read, by port. The mask always goes into its first image input.
/// </param>
/// <param name="Blend">How its result composites over the mask it read. Copy replaces it.</param>
/// <param name="Opacity">How much of its result composites, 0 to 1.</param>
public sealed record StackMaskEffect(
    string Node,
    bool Enabled = true,
    IReadOnlyDictionary<string, IReadOnlyList<float>>? Values = null,
    IReadOnlyDictionary<string, string>? Texts = null,
    IReadOnlyDictionary<string, StackInput>? Inputs = null,
    string Blend = "Copy",
    float Opacity = 1f
);

/// <summary>One colour of a stack's id map.</summary>
/// <param name="Colour">Red, green and blue, 0 to 1, as the map stores them.</param>
/// <param name="Share">How much of the map's covered area it is, 0 to 1.</param>
public sealed record StackIdColour(IReadOnlyList<float> Colour, float Share);

/// <summary>The colours of a stack's id map, and the one at a place on it if one was asked about.</summary>
/// <param name="Colours">Its colours, the largest first.</param>
/// <param name="At">The colour at the place asked about, or null.</param>
public sealed record StackIdColours(IReadOnlyList<StackIdColour> Colours, IReadOnlyList<float>? At = null);

/// <summary>
///     An adjustment a filter layer or a mask can run, or a generator an input can read: any node or shipped compound
///     a stack can use there. A project's own compounds reach a stack from the shelf.
/// </summary>
/// <param name="Node">The node type or compound, as <see cref="StackLayer.FilterNode" /> names it.</param>
/// <param name="Name">What a person calls it.</param>
/// <param name="Category">The heading it sits under.</param>
/// <param name="Summary">One line about it.</param>
/// <param name="Ports">Its numbers, each saying what it is and what it starts as. Never an image it reads.</param>
/// <param name="Settings">What it takes as text.</param>
/// <param name="Inputs">The images it reads, its subject first. Empty for a generator.</param>
public sealed record StackFilterKind(
    string Node,
    string Name,
    string Category,
    string Summary,
    IReadOnlyList<GraphPortDefinition> Ports,
    IReadOnlyList<GraphSettingDefinition> Settings,
    IReadOnlyList<StackFilterInput>? Inputs = null
);

/// <summary>One image a filter reads, as Properties offers a slot for it.</summary>
/// <param name="Port">Its name, which a layer's <c>inputs</c> are keyed by.</param>
/// <param name="Summary">What the node says it reads there.</param>
/// <param name="Subject">
///     Whether it is the picture the filter changes. It reads the layers beneath in the channel being written until the
///     layer says otherwise, and on a mask it is always the mask.
/// </param>
/// <param name="Optional">Whether the filter runs with nothing there.</param>
/// <param name="Channel">The channel it is named after, which it reads beneath until told otherwise, or empty.</param>
public sealed record StackFilterInput(string Port, string Summary, bool Subject, bool Optional, string Channel);

/// <summary>Where one image input of a filter, a mask step or a graph fill reads.</summary>
/// <param name="Source">Beneath, Anchor, Generator, Texture or Bake.</param>
/// <param name="Channel">The channel read beneath, by usage, or empty for the channel being written.</param>
/// <param name="Anchor">The anchor point read, and in which channel, for Anchor.</param>
/// <param name="Generator">The node or compound read, for Generator: Source/Noise, Noise/Clouds.</param>
/// <param name="Values">The generator's numbers, by port.</param>
/// <param name="Parameters">The generator's settings, by name.</param>
/// <param name="Asset">The image read, for Texture.</param>
/// <param name="Map">The mesh map read, for Bake.</param>
public sealed record StackInput(
    string Source,
    string Channel = "",
    StackAnchorRef? Anchor = null,
    string Generator = "",
    IReadOnlyDictionary<string, IReadOnlyList<float>>? Values = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    string Asset = "",
    string Map = ""
);

/// <summary>A word a stack's field takes, and what a person calls it.</summary>
/// <param name="Value">What a file and a command hold.</param>
/// <param name="Label">What to show. A client shows this, never the value.</param>
public sealed record StackTerm(string Value, string Label);

/// <summary>The words a stack's fields take, each with its label, so that a client holds no list of its own.</summary>
/// <param name="Blends">The thirty blend modes.</param>
/// <param name="Filters">The filters a layer can be.</param>
/// <param name="Masks">The mask sources.</param>
/// <param name="Fills">What a fill layer reads.</param>
/// <param name="Projections">How a fill's pictures land on the mesh.</param>
/// <param name="Generators">The compounds that read a mesh's bakes.</param>
/// <param name="MeshMaps">The maps a bake mask or a generator can read.</param>
/// <param name="Alphas">The named brush alphas.</param>
/// <param name="MaskEffects">The steps a mask can run.</param>
/// <param name="Usages">The channels a stack can have.</param>
/// <param name="GeneratorParameters">The knobs each generator has, by its path.</param>
/// <param name="Brush">What a brush's fields take, and the brush a new one starts as.</param>
/// <param name="Inputs">
///     Where an image input can read: the layers beneath, an anchor point, a generator, an image or a
///     mesh map.
/// </param>
/// <param name="Sources">The generators an image input can read: noises, patterns, grunges and the rest.</param>
/// <param name="Lanes">The lanes of a picture a mask can read.</param>
/// <param name="Tags">The colors a layer can be marked with, None first.</param>
/// <param name="Kinds">The kinds a graph's ports and parameters take: Number for Float, and the rest.</param>
/// <param name="Formats">How wide a channel's painted texels can be (#19): 8-bit, and 16-bit and 32-bit float.</param>
public sealed record StackVocabulary(
    IReadOnlyList<StackTerm> Blends,
    IReadOnlyList<StackFilterKind> Filters,
    IReadOnlyList<StackTerm> Masks,
    IReadOnlyList<StackTerm> Fills,
    IReadOnlyList<StackTerm> Projections,
    IReadOnlyList<StackTerm> Generators,
    IReadOnlyList<StackTerm> MeshMaps,
    IReadOnlyList<StackTerm> Alphas,
    IReadOnlyList<StackFilterKind> MaskEffects,
    IReadOnlyList<StackTerm> Usages,
    IReadOnlyDictionary<string, IReadOnlyList<GraphSettingDefinition>> GeneratorParameters,
    StackBrushTerms Brush,
    IReadOnlyList<StackTerm> Inputs,
    IReadOnlyList<StackFilterKind> Sources,
    IReadOnlyList<StackTerm>? Lanes = null,
    IReadOnlyList<StackTerm>? Tags = null,
    IReadOnlyList<StackTerm>? Kinds = null,
    IReadOnlyList<StackTerm>? Formats = null
);

/// <summary>What a layer looks like alone.</summary>
/// <param name="Layer">The layer.</param>
/// <param name="Hash">Its name under <c>/previews/{hash}</c>.</param>
public sealed record StackThumbnail(string Layer, string Hash);

/// <summary>
///     Asks which layer is on top at a place on a stack's map, as a click on the mesh or the map finds it.
/// </summary>
/// <param name="U">Across the map, 0 to 1: the texture coordinate <c>POST /viewport/pick</c> gives.</param>
/// <param name="V">Down it, 0 to 1.</param>
public sealed record StackFindRequest(float U, float V);

/// <summary>The layer on top at a place on a stack's map.</summary>
/// <param name="Layer">
///     The topmost layer that covers the place, inside its folders, or null when only the stack's defaults are there.
///     A filter covers nothing of its own, and a flattened folder is one layer.
/// </param>
public sealed record StackFound(string? Layer);

/// <summary>The fills of a stack, each drawn alone.</summary>
/// <param name="Items">Them.</param>
public sealed record StackThumbnails(IReadOnlyList<StackThumbnail> Items);

/// <summary>Chooses the bake a stack's generators and bake masks read.</summary>
/// <param name="Folder">The bake's folder, or null for none.</param>
public sealed record StackMeshMapsRequest(string? Folder);

/// <summary>A map as a small picture, by the hash an event carries.</summary>
/// <param name="Usage">Which channel.</param>
/// <param name="Hash">Its name under <c>/previews/{hash}</c>.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public sealed record StackPreview(string Usage, string Hash, int Width, int Height);

/// <summary>Something a stack's compile had to say.</summary>
/// <param name="Layer">The layer it is about, or empty when it is about the stack.</param>
/// <param name="Severity">Warning, or Error when nothing could evaluate.</param>
/// <param name="Message">What is wrong.</param>
public sealed record StackProblem(string Layer, string Severity, string Message);

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
/// <param name="Problems">What the compile said, by layer.</param>
public sealed record StackEvaluation(
    long Revision,
    bool Compiled,
    int Ran,
    int Repaired,
    int Reused,
    double Milliseconds,
    IReadOnlyList<StackPreview> Previews,
    IReadOnlyList<string> Diagnostics,
    string? Error,
    IReadOnlyList<StackProblem> Problems
);

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
/// <param name="Model">The mesh it is painted on, as the document names it, or empty.</param>
/// <param name="MeshMaps">The bake its generators read, or null.</param>
/// <param name="Seed">The number every procedural step derives from.</param>
/// <param name="HeightNormal">
///     How a height channel becomes the set's normal: <c>Sharp</c> or <c>Smooth</c>. Read only when the set has a
///     height channel, and then it writes a normal whether or not it declares one.
/// </param>
/// <param name="Anchors">The anchor points its set publishes, by name, and who reads each.</param>
/// <param name="UvSet">The mesh's coordinate set the set is painted, baked and drawn in, by the file's number.</param>
/// <param name="BakedNormal">How the bake's normal reaches the set: <c>Combine</c>, <c>Replace</c>, <c>Off</c>.</param>
/// <param name="BakedOcclusion">How its occlusion does: <c>Combine</c>, <c>Replace</c>, <c>Off</c>.</param>
/// <param name="BakeStale">
///     Why the bake the set reads no longer fits it, or null when it does or there is none: <c>mesh</c>, made on a mesh
///     the set no longer stands on, or <c>uvSet</c>, laid out in another of the mesh's UV sets (#238), or <c>shape</c>,
///     of another shape than the set's Width by Height (#262). Either way its maps need baking again.
/// </param>
/// <param name="Pace">How it is evaluated while it is edited: paused or not, and at what size (#208).</param>
/// <param name="Udim">
///     The UDIM tile its layout sits on in its UV set (#281), which its exported files are named by: 1001 for a set on
///     the first tile, on no mesh, or reaching several, which its problems say.
/// </param>
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
    long Settled,
    string Model,
    string? MeshMaps = null,
    uint Seed = 0,
    string HeightNormal = "Smooth",
    IReadOnlyList<StackAnchor>? Anchors = null,
    int UvSet = 0,
    string BakedNormal = "Combine",
    string BakedOcclusion = "Combine",
    string? BakeStale = null,
    StackPace? Pace = null,
    int Udim = 1001
);
#pragma warning restore SK7005

/// <summary>
///     How a stack is evaluated while it is edited, which is the session's and never the file's (#208). Saving,
///     exporting, checking and reading pixels always evaluate it whole, at full size, paused or not.
/// </summary>
/// <param name="Paused">
///     Whether edits wait to be evaluated, so that a big stack can be set up without paying for each step. Resuming
///     evaluates everything at once.
/// </param>
/// <param name="Proxy">
///     What the stack's width and height are divided by for the viewport and previews while it is edited: 1, 2, 4
///     or 8. Painted pixels stay at full size; only what they are composited into is smaller.
/// </param>
public sealed record StackPace(bool Paused = false, int Proxy = 1);

/// <summary>A change to how a stack is evaluated while it is edited (#208). What is left out stays as it is.</summary>
/// <param name="Paused">Whether edits wait to be evaluated.</param>
/// <param name="Proxy">What the stack's sides are divided by: 1, 2, 4 or 8.</param>
public sealed record StackPaceChange(bool? Paused = null, int? Proxy = null);

/// <summary>An open stack, for a list.</summary>
/// <param name="Id">The session.</param>
/// <param name="Path">Its file.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Revision">The revision of the document.</param>
/// <param name="Dirty">Whether it has edits no file holds.</param>
public sealed record StackSummary(string Id, string Path, string Name, long Revision, bool Dirty);

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
) : StackCommand;
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
    IReadOnlyDictionary<string, StackAnchorRef>? Inputs = null
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

/// <summary>Where an edit left the session.</summary>
/// <param name="Revision">The revision of the document.</param>
/// <param name="Sequence">The event it produced.</param>
/// <param name="History">The undo history.</param>
public sealed record StackEditResult(long Revision, long Sequence, GraphHistoryState History);

/// <summary>What an edit changed, so that a client need not read the whole stack again.</summary>
/// <param name="Layers">The stack's layers now.</param>
/// <param name="Painted">Each channel a stroke touched, as layer and usage.</param>
/// <param name="Anchors">The anchor points its set publishes now, and who reads each.</param>
public sealed record StackDelta(
    IReadOnlyList<StackLayer> Layers,
    IReadOnlyList<StackPainted> Painted,
    IReadOnlyList<StackAnchor>? Anchors = null
);

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
