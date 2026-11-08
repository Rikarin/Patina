using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>Opens a texture graph for editing. Opening a file that is already open joins its session.</summary>
/// <param name="Path">The graph file on the Server's machine, absolute or relative to the library.</param>
public sealed record GraphOpenRequest(string Path);

/// <summary>A node as a document holds it.</summary>
/// <param name="Id">The node's identity, which is never reused within a session.</param>
/// <param name="Type">The node type's path, from <c>/node-types</c>.</param>
/// <param name="X">Its position on the canvas.</param>
/// <param name="Y">Its position on the canvas.</param>
/// <param name="Values">The numbers set on its inputs, by port. An input left out takes the type's default.</param>
/// <param name="Texts">The text settings set on it, by name.</param>
/// <param name="Bypassed">
///     True when it is switched off: what feeds its first input of an output's kind passes straight through, and a
///     node with no such input passes nothing.
/// </param>
public sealed record GraphNodeState(
    int Id,
    string Type,
    float X,
    float Y,
    IReadOnlyDictionary<string, float[]> Values,
    IReadOnlyDictionary<string, string> Texts,
    bool Bypassed = false
);

/// <summary>One wire, from an output to an input.</summary>
public sealed record GraphEdgeState(int FromNode, string FromPort, int ToNode, string ToPort);

/// <summary>A value a node type or a graph lets an author set.</summary>
/// <param name="Name">What it is called.</param>
/// <param name="Default">Its default, as text.</param>
/// <param name="Summary">One line about it.</param>
/// <param name="Kind">Text, Bool, Int or Float.</param>
/// <param name="Minimum">Its lower bound, or null for none.</param>
/// <param name="Maximum">Its upper bound, or null for none.</param>
/// <param name="Group">The heading it sits under.</param>
/// <param name="Accepted">The values it may take; empty for any.</param>
/// <param name="Labels">
///     What a person calls each of <paramref name="Accepted" />, in its order. The Server fills it in; a client that
///     sends a definition leaves it out.
/// </param>
/// <param name="Label">
///     What a person calls it, apart from <paramref name="Name" />, which expressions write. Null or empty
///     for the name.
/// </param>
/// <param name="Widget">
///     The control an inspector draws: <c>Default</c>, <c>Slider</c> (a number with a range), <c>Angle</c> (a number
///     in degrees) or <c>Toggle</c> (a flag). Null for <c>Default</c>.
/// </param>
/// <param name="VisibleIf">When it is shown, or null for always. Hiding never changes its value.</param>
// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005
public sealed record GraphSettingDefinition(
    string Name,
    string Default,
    string Summary,
    string Kind,
    float? Minimum,
    float? Maximum,
    string Group,
    IReadOnlyList<string> Accepted,
    IReadOnlyList<string>? Labels = null,
    string? Label = null,
    string? Widget = null,
    GraphSettingCondition? VisibleIf = null
);
#pragma warning restore SK7005

/// <summary>When a parameter or a setting is shown: while another of the same graph or node holds a value.</summary>
/// <param name="Setting">The other one, by name.</param>
/// <param name="Value">
///     What it is compared with, as text. Numbers compare as numbers and anything else as a name, ignoring case.
/// </param>
/// <param name="Negated">True to show it while the other one does not hold the value.</param>
public sealed record GraphSettingCondition(string Setting, string Value, bool Negated = false);

/// <summary>A named set of values for a graph's parameters, kept in the graph's file.</summary>
/// <param name="Name">What it is called. Unique within the graph.</param>
/// <param name="Values">What each parameter is set to, by parameter name, as text.</param>
public sealed record GraphPresetState(string Name, IReadOnlyDictionary<string, string> Values);

/// <summary>One port of a node type.</summary>
/// <param name="Name">What an edge calls it.</param>
/// <param name="Direction">Input or Output.</param>
/// <param name="Kind">What it carries: Image, Float, Float4 and so on.</param>
/// <param name="Default">The numbers an unset input takes.</param>
/// <param name="Summary">One line about it.</param>
/// <param name="Range">What numbers mean anything on it, or null when it declares none.</param>
public sealed record GraphPortDefinition(
    string Name,
    string Direction,
    string Kind,
    IReadOnlyList<float> Default,
    string Summary,
    GraphPortRange? Range
);

/// <summary>What an input port says about its numbers. Any end may be open, as null.</summary>
/// <param name="Minimum">The smallest number that means anything.</param>
/// <param name="Maximum">The largest number that means anything.</param>
/// <param name="SoftMinimum">Where a slider starts, when the port's meaning gives that. Numbers go on past it.</param>
/// <param name="SoftMaximum">Where a slider ends, the same way.</param>
public sealed record GraphPortRange(float? Minimum, float? Maximum, float? SoftMinimum, float? SoftMaximum);

/// <summary>A node a client can add, for a palette.</summary>
/// <param name="Path">What a document stores, such as <c>Colour/Levels</c>.</param>
/// <param name="Title">The last segment of the path.</param>
/// <param name="Category">The path without its title.</param>
/// <param name="Summary">One line about it.</param>
/// <param name="HasPreview">Whether the type declares that its picture is worth showing.</param>
/// <param name="Ports">Its inputs and outputs.</param>
/// <param name="Settings">Its text settings, set with <see cref="GraphSetText" />.</param>
/// <param name="Presets">A compound's presets, which set its settings; empty for every other node.</param>
public sealed record GraphNodeType(
    string Path,
    string Title,
    string Category,
    string Summary,
    bool HasPreview,
    IReadOnlyList<GraphPortDefinition> Ports,
    IReadOnlyList<GraphSettingDefinition> Settings,
    IReadOnlyList<GraphPresetState>? Presets = null
);

/// <summary>The nodes a search found.</summary>
/// <param name="Paths">Their paths, best first.</param>
public sealed record GraphNodeMatches(IReadOnlyList<string> Paths);

/// <summary>A frame round some nodes. It holds no geometry: a client draws it from where its nodes are.</summary>
/// <param name="Id">What a command names it by. It never changes and is never reused while a session lasts.</param>
/// <param name="Title">What it is called.</param>
/// <param name="Nodes">Its members.</param>
/// <param name="Color">
///     The colour its frame is drawn in, as <c>#rrggbb</c>, or empty for the client's own choice. A client that
///     follows a theme maps the colours it offers to its own.
/// </param>
/// <param name="Description">What its nodes do together.</param>
public sealed record GraphGroupState(
    int Id,
    string Title,
    IReadOnlyList<int> Nodes,
    string Color = "",
    string Description = ""
);

/// <summary>A note on the canvas.</summary>
/// <param name="Id">What a command names it by. It never changes and is never reused while a session lasts.</param>
/// <param name="Text">What it says.</param>
/// <param name="X">Its left edge on the canvas.</param>
/// <param name="Y">Its top edge on the canvas.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public sealed record GraphCommentState(int Id, string Text, float X, float Y, float Width, float Height);

/// <summary>One port a graph has when another graph holds it as a node.</summary>
/// <param name="Name">What an edge in the holding graph calls it.</param>
/// <param name="Direction">Input or Output, as the holding graph sees it.</param>
/// <param name="Kind">What it carries: Image, Float, Float4 and so on.</param>
/// <param name="Default">What an unwired input takes, one number per lane, or empty for the kind's zero.</param>
/// <param name="Summary">One line about it.</param>
public sealed record GraphInterfacePort(
    string Name,
    string Direction,
    string Kind,
    IReadOnlyList<float> Default,
    string Summary
);

/// <summary>A compound a graph can hold, and the graph behind it.</summary>
/// <param name="Type">Its node type's path, as <c>/node-types</c> lists it.</param>
/// <param name="Path">What <c>POST /graphs</c> takes to open it.</param>
/// <param name="ReadOnly">True for a compound that ships with Patina, which opens to be read.</param>
public sealed record GraphCompound(string Type, string Path, bool ReadOnly);

/// <summary>Something a graph's compiler or its file's reader has to say.</summary>
/// <param name="Id">A stable code, such as <c>TG0005</c>.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Severity">Warning or Error.</param>
/// <param name="Node">The node it concerns, or null.</param>
/// <param name="Port">The port it concerns, or null.</param>
public sealed record GraphDiagnostic(string Id, string Message, string Severity, int? Node = null, string? Port = null);

/// <summary>Where a node's picture is: <c>GET /api/v1/previews/{hash}</c>. Pixels never cross JSON.</summary>
/// <param name="Node">The node.</param>
/// <param name="Port">Its output port, or the usage of an Output node.</param>
/// <param name="Hash">The SHA-256 of the PNG's bytes, in lower-case hex.</param>
/// <param name="Width">The PNG's size in pixels.</param>
/// <param name="Height">The PNG's size in pixels.</param>
public sealed record GraphPreview(int Node, string Port, string Hash, int Width, int Height);

/// <summary>What the undo history offers.</summary>
/// <param name="CanUndo">Whether there is an edit to undo.</param>
/// <param name="CanRedo">Whether there is an edit to redo.</param>
/// <param name="UndoLabel">What undo would take back.</param>
/// <param name="RedoLabel">What redo would put back.</param>
/// <param name="Dirty">Whether the document differs from the file.</param>
/// <param name="Step">
///     The newest step that stands, or null before the first. Each new step, undo and redo changes it, so a client that
///     lists the history (<see cref="HistoryList" />) reads it again when this changes.
/// </param>
public sealed record GraphHistoryState(
    bool CanUndo,
    bool CanRedo,
    string? UndoLabel,
    string? RedoLabel,
    bool Dirty,
    long? Step = null
);

/// <summary>One undo step of a graph or a stack.</summary>
/// <param name="Id">What a jump names it by. Ids only grow: a step that has gone is never taken for another.</param>
/// <param name="Label">What it is called, as undo and redo call it.</param>
public sealed record HistoryStep(long Id, string Label);

/// <summary>An undo history as a list, which is what a history pane shows.</summary>
/// <param name="Done">The steps that stand, oldest first. Undo takes back the last.</param>
/// <param name="Undone">The steps undone, the next one redo puts back first.</param>
public sealed record HistoryList(IReadOnlyList<HistoryStep> Done, IReadOnlyList<HistoryStep> Undone);

/// <summary>Where to take an undo history: undoing or redoing until a step is the newest that stands.</summary>
/// <param name="Step">The step to stand at, or null to undo everything.</param>
public sealed record HistoryJump(long? Step);

/// <summary>What one evaluation did.</summary>
/// <param name="Revision">The document revision it shows; during a drag this trails the newest.</param>
/// <param name="Compiled">True if the plan was compiled; false if the numbers were written into the last one.</param>
/// <param name="Ran">How many operations ran. The rest came from the result cache.</param>
/// <param name="Reused">How many operations already had a result.</param>
/// <param name="Milliseconds">How long the Worker took, previews included.</param>
/// <param name="Previews">The previews that differ from the last evaluation's. All of them are in the state.</param>
/// <param name="Diagnostics">Everything the compiler reported.</param>
/// <param name="Error">Why it did not finish, or null. "No Vulkan" means the Worker has no GPU.</param>
public sealed record GraphEvaluation(
    long Revision,
    bool Compiled,
    int Ran,
    int Reused,
    double Milliseconds,
    IReadOnlyList<GraphPreview> Previews,
    IReadOnlyList<GraphDiagnostic> Diagnostics,
    string? Error
);

// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005
/// <summary>The reply to <c>GET /api/v1/graphs/{id}</c>: the whole document as the Server holds it.</summary>
/// <param name="Id">The session.</param>
/// <param name="Path">The file.</param>
/// <param name="Name">The graph's name.</param>
/// <param name="Revision">Counts changes to the document, undo included.</param>
/// <param name="Sequence">The last event's number: pass it as <c>since</c> to follow from exactly here.</param>
/// <param name="History">The undo history.</param>
/// <param name="Nodes">The nodes, by identity.</param>
/// <param name="Edges">The wires, by the input they arrive at.</param>
/// <param name="Settings">Its settings: <c>baseWidth</c>, <c>baseHeight</c>, <c>seed</c> and any others.</param>
/// <param name="Parameters">The parameters the graph exposes.</param>
/// <param name="Presets">Named sets of values for them.</param>
/// <param name="Interface">The ports it has when another graph holds it.</param>
/// <param name="Groups">Its group frames, in file order.</param>
/// <param name="Comments">Its notes, in file order.</param>
/// <param name="Previews">The newest preview of each node.</param>
/// <param name="Repairs">What reading the file had to drop or fix.</param>
/// <param name="Evaluation">The last evaluation, or null before the first.</param>
/// <param name="Settled">
///     The newest revision with nothing left to evaluate. Below <paramref name="Revision" />, evaluation is behind.
/// </param>
/// <param name="ReadOnly">True when this graph cannot be edited or saved: a compound that ships with Patina.</param>
public sealed record GraphState(
    string Id,
    string Path,
    string Name,
    long Revision,
    long Sequence,
    GraphHistoryState History,
    IReadOnlyList<GraphNodeState> Nodes,
    IReadOnlyList<GraphEdgeState> Edges,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<GraphSettingDefinition> Parameters,
    IReadOnlyList<GraphPresetState> Presets,
    IReadOnlyList<GraphInterfacePort> Interface,
    IReadOnlyList<GraphGroupState> Groups,
    IReadOnlyList<GraphCommentState> Comments,
    IReadOnlyList<GraphPreview> Previews,
    IReadOnlyList<GraphDiagnostic> Repairs,
    GraphEvaluation? Evaluation,
    long Settled,
    bool ReadOnly
);
#pragma warning restore SK7005

/// <summary>An open graph, for a list.</summary>
public sealed record GraphSummary(string Id, string Path, string Name, long Revision, bool Dirty);

/// <summary>One edit. Commands in a batch run in order, and every command sees the ones before it.</summary>
[JsonDerivedType(typeof(GraphAddNode), "addNode")]
[JsonDerivedType(typeof(GraphRemoveNodes), "removeNodes")]
[JsonDerivedType(typeof(GraphMoveNodes), "moveNodes")]
[JsonDerivedType(typeof(GraphConnect), "connect"), JsonDerivedType(typeof(GraphWire), "wire")]
[JsonDerivedType(typeof(GraphDisconnect), "disconnect")]
[JsonDerivedType(typeof(GraphSetValue), "setValue")]
[JsonDerivedType(typeof(GraphSetText), "setText")]
[JsonDerivedType(typeof(GraphSetSetting), "setSetting")]
// Here and not first: SK7020 honours no suppression, and reads an unbroken list of twenty attributes as a clone.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(GraphAddParameter), "addParameter")]
[JsonDerivedType(typeof(GraphSetParameter), "setParameter")]
[JsonDerivedType(typeof(GraphRemoveParameter), "removeParameter")]
[JsonDerivedType(typeof(GraphAddPort), "addPort")]
[JsonDerivedType(typeof(GraphSetPort), "setPort")]
[JsonDerivedType(typeof(GraphRemovePort), "removePort")]
[JsonDerivedType(typeof(GraphAddGroup), "addGroup")]
[JsonDerivedType(typeof(GraphSetGroup), "setGroup")]
[JsonDerivedType(typeof(GraphRemoveGroup), "removeGroup")]
[JsonDerivedType(typeof(GraphAddComment), "addComment")]
[JsonDerivedType(typeof(GraphSetComment), "setComment")]
[JsonDerivedType(typeof(GraphRemoveComment), "removeComment")]
[JsonDerivedType(typeof(GraphSetBypass), "setBypass")]
[JsonDerivedType(typeof(GraphArrange), "arrange")]
[JsonDerivedType(typeof(GraphSavePreset), "savePreset")]
[JsonDerivedType(typeof(GraphRemovePreset), "removePreset")]
[JsonDerivedType(typeof(GraphApplyPreset), "applyPreset")]
public abstract record GraphCommand;

/// <summary>Adds a node.</summary>
/// <param name="Type">The node type's path, or <c>Reroute</c> for a dot that only bends a wire.</param>
/// <param name="X">Where it goes.</param>
/// <param name="Y">Where it goes.</param>
/// <param name="Ref">
///     A negative number the client picks. Later commands in this batch may use it wherever they name a node, and the
///     reply says which identity it became.
/// </param>
public sealed record GraphAddNode(string Type, float X, float Y, int? Ref = null) : GraphCommand;

/// <summary>Removes nodes, and the wires that touch them.</summary>
/// <param name="Nodes">The nodes.</param>
/// <param name="Relink">
///     True to keep the chain joined: each input a removed node fed is wired to what fed that node's first input of
///     the same kind, the way a bypassed node passes it on.
/// </param>
public sealed record GraphRemoveNodes(IReadOnlyList<int> Nodes, bool Relink = false) : GraphCommand;

/// <summary>
///     Switches nodes off or back on. A switched-off node keeps its wires and values; see
///     <see cref="GraphNodeState" />.
/// </summary>
public sealed record GraphSetBypass(IReadOnlyList<int> Nodes, bool Bypassed) : GraphCommand;

/// <summary>Lays nodes out in columns, left to right in the order data flows, with Vixen's layout.</summary>
/// <param name="Nodes">
///     The nodes, which keep their top-left corner as a block and are laid out among themselves; empty for the whole
///     graph.
/// </param>
public sealed record GraphArrange(IReadOnlyList<int> Nodes) : GraphCommand;

/// <summary>Saves a preset, replacing one of the same name where it stands.</summary>
/// <param name="Name">What it is called.</param>
/// <param name="Values">What each parameter is set to, by name; null for every parameter's default now.</param>
public sealed record GraphSavePreset(string Name, IReadOnlyDictionary<string, string>? Values = null) : GraphCommand;

/// <summary>Takes a preset away.</summary>
public sealed record GraphRemovePreset(string Name) : GraphCommand;

/// <summary>
///     Sets the parameters a preset names to its values, as their defaults. A value a parameter cannot hold, or a
///     name the graph no longer declares, is left out. A compound's preset on a node of another graph is that node's
///     settings, which <see cref="GraphSetText" /> sets.
/// </summary>
public sealed record GraphApplyPreset(string Name) : GraphCommand;

/// <summary>Where one node goes.</summary>
public sealed record GraphNodePosition(int Node, float X, float Y);

/// <summary>Moves nodes. A move never evaluates anything.</summary>
public sealed record GraphMoveNodes(IReadOnlyList<GraphNodePosition> Positions) : GraphCommand;

/// <summary>Wires an output to an input, replacing what was on that input.</summary>
public sealed record GraphConnect(int FromNode, string FromPort, int ToNode, string ToPort) : GraphCommand;

/// <summary>
///     Wires two nodes through the first ports that fit, for a node just made from a selection or a dropped wire, whose
///     client cannot know its kinds. Nothing happens when none fit: a generator takes no input, and it is still wanted.
/// </summary>
/// <param name="FromNode">The node the wire leaves.</param>
/// <param name="ToNode">The node the wire reaches.</param>
/// <param name="FromPort">The output, or null for the first that fits.</param>
/// <param name="ToPort">The input, or null for the first that fits.</param>
public sealed record GraphWire(int FromNode, int ToNode, string? FromPort = null, string? ToPort = null) : GraphCommand;

/// <summary>Takes the wire off an input.</summary>
public sealed record GraphDisconnect(int ToNode, string ToPort) : GraphCommand;

/// <summary>Sets the numbers on a node's input. This is what a slider sends.</summary>
/// <param name="Node">The node.</param>
/// <param name="Port">The input.</param>
/// <param name="Value">One number per lane of the port.</param>
public sealed record GraphSetValue(int Node, string Port, IReadOnlyList<float> Value) : GraphCommand;

/// <summary>Sets a text setting on a node, such as a blend mode.</summary>
public sealed record GraphSetText(int Node, string Port, string Value) : GraphCommand;

/// <summary>Sets a setting of the graph: <c>baseWidth</c>, <c>baseHeight</c> or <c>seed</c>.</summary>
public sealed record GraphSetSetting(string Key, string Value) : GraphCommand;

/// <summary>Exposes a new parameter. Its name must be free, and its default must suit its kind and range.</summary>
public sealed record GraphAddParameter(GraphSettingDefinition Parameter) : GraphCommand;

/// <summary>Replaces a parameter where it stands. Dragging its default sends this on every step.</summary>
/// <param name="Name">The parameter as it is called now.</param>
/// <param name="Parameter">What it becomes, which may rename it. Expressions keep the old name until edited.</param>
public sealed record GraphSetParameter(string Name, GraphSettingDefinition Parameter) : GraphCommand;

/// <summary>Stops exposing a parameter.</summary>
public sealed record GraphRemoveParameter(string Name) : GraphCommand;

/// <summary>Gives the graph a new port. Its boundary node shows it at once, and a holding graph after a save.</summary>
public sealed record GraphAddPort(GraphInterfacePort Port) : GraphCommand;

/// <summary>Changes a port where it stands, and keeps the wires on its boundary node under a new name.</summary>
/// <param name="Name">The port as it is called now.</param>
/// <param name="Direction">Input or Output, which cannot change.</param>
/// <param name="Port">What it becomes. A kind its wires cannot carry is refused until they are gone.</param>
public sealed record GraphSetPort(string Name, string Direction, GraphInterfacePort Port) : GraphCommand;

/// <summary>Takes a port away, with the wires on its boundary node. Undoing it puts them back.</summary>
public sealed record GraphRemovePort(string Name, string Direction) : GraphCommand;

/// <summary>Draws a group frame round some nodes.</summary>
/// <param name="Title">What it is called.</param>
/// <param name="Nodes">Its members.</param>
/// <param name="Ref">A negative number the client picks. The reply says which identity it became.</param>
public sealed record GraphAddGroup(string Title, IReadOnlyList<int> Nodes, int? Ref = null) : GraphCommand;

/// <summary>Changes a group: anything left null stays as it is.</summary>
/// <param name="Id">The group.</param>
/// <param name="Title">Its new title.</param>
/// <param name="Nodes">Its new members.</param>
/// <param name="Color">
///     Its frame's colour as <c>#rrggbb</c>, or empty for the client's own choice. Anything else is refused, so the
///     file stays readable by any client.
/// </param>
/// <param name="Description">What its nodes do together.</param>
public sealed record GraphSetGroup(
    int Id,
    string? Title = null,
    IReadOnlyList<int>? Nodes = null,
    string? Color = null,
    string? Description = null
) : GraphCommand;

/// <summary>Takes a group frame away. Its nodes stay.</summary>
public sealed record GraphRemoveGroup(int Id) : GraphCommand;

/// <summary>Adds a note.</summary>
/// <param name="Text">What it says.</param>
/// <param name="X">Its left edge.</param>
/// <param name="Y">Its top edge.</param>
/// <param name="Width">Its width, or null for the standard size.</param>
/// <param name="Height">Its height, or null for the standard size.</param>
/// <param name="Ref">A negative number the client picks. The reply says which identity it became.</param>
public sealed record GraphAddComment(
    string Text,
    float X,
    float Y,
    float? Width = null,
    float? Height = null,
    int? Ref = null
) : GraphCommand;

/// <summary>Changes a note: anything left null stays as it is. A note moved by hand sends this on every step.</summary>
/// <param name="Id">The note.</param>
/// <param name="Text">What it says.</param>
/// <param name="X">Its left edge.</param>
/// <param name="Y">Its top edge.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public sealed record GraphSetComment(
    int Id,
    string? Text = null,
    float? X = null,
    float? Y = null,
    float? Width = null,
    float? Height = null
) : GraphCommand;

/// <summary>Removes a note.</summary>
public sealed record GraphRemoveComment(int Id) : GraphCommand;

/// <summary>Some edits, applied whole or not at all, and undone as one step.</summary>
/// <param name="Commands">What to do, in order.</param>
/// <param name="Label">What the undo history calls it, or null for a name from the commands.</param>
/// <param name="Merge">
///     True to fold this batch into the last one when both change the same things, so that a whole drag is one undo.
///     A client leaves it false on the first batch of a drag.
/// </param>
public sealed record GraphCommandBatch(IReadOnlyList<GraphCommand> Commands, string? Label = null, bool Merge = false);

/// <summary>Which identity a client's <c>ref</c> became, on a node, a group or a note.</summary>
/// <param name="Ref">The number the client picked.</param>
/// <param name="Node">The identity.</param>
public sealed record GraphCreatedNode(int Ref, int Node);

/// <summary>The reply to an edit, an undo, a redo or a save.</summary>
/// <param name="Revision">The document's revision after it.</param>
/// <param name="Sequence">The event that announced it.</param>
/// <param name="History">The undo history after it.</param>
/// <param name="Created">What a batch created under a <c>ref</c>.</param>
public sealed record GraphEditResult(
    long Revision,
    long Sequence,
    GraphHistoryState History,
    IReadOnlyList<GraphCreatedNode> Created
);

// SK7005: as on GraphState, one constructor parameter per JSON field.
#pragma warning disable SK7005
/// <summary>What changed in the document, the same for an edit, an undo and a redo.</summary>
/// <param name="Nodes">Nodes that are new or differ, whole.</param>
/// <param name="RemovedNodes">Nodes that have gone.</param>
/// <param name="Edges">Wires that are new.</param>
/// <param name="RemovedEdges">Wires that have gone.</param>
/// <param name="Settings">Graph settings that are new or differ.</param>
/// <param name="RemovedSettings">Graph settings that have gone.</param>
/// <param name="Parameters">The whole list of exposed parameters, or null when it did not change.</param>
/// <param name="Interface">The whole list of interface ports, or null when it did not change.</param>
/// <param name="Groups">The whole list of groups, or null when it did not change.</param>
/// <param name="Comments">The whole list of notes, or null when it did not change.</param>
/// <param name="Presets">The whole list of presets, or null when it did not change.</param>
public sealed record GraphDelta(
    IReadOnlyList<GraphNodeState> Nodes,
    IReadOnlyList<int> RemovedNodes,
    IReadOnlyList<GraphEdgeState> Edges,
    IReadOnlyList<GraphEdgeState> RemovedEdges,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<string> RemovedSettings,
    IReadOnlyList<GraphSettingDefinition>? Parameters = null,
    IReadOnlyList<GraphGroupState>? Groups = null,
    IReadOnlyList<GraphCommentState>? Comments = null,
    IReadOnlyList<GraphInterfacePort>? Interface = null,
    IReadOnlyList<GraphPresetState>? Presets = null
);
#pragma warning restore SK7005

/// <summary>What an event reports.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GraphEventKind>))]
public enum GraphEventKind {
    /// <summary>A batch was applied: <see cref="GraphEvent.Delta" /> says what changed.</summary>
    [JsonStringEnumMemberName("edited")]
    Edited,

    /// <summary>An edit was taken back: <see cref="GraphEvent.Delta" /> says what changed.</summary>
    [JsonStringEnumMemberName("undone")]
    Undone,

    /// <summary>An edit was put back: <see cref="GraphEvent.Delta" /> says what changed.</summary>
    [JsonStringEnumMemberName("redone")]
    Redone,

    /// <summary>The document reached the file.</summary>
    [JsonStringEnumMemberName("saved")]
    Saved,

    /// <summary>An evaluation ended: <see cref="GraphEvent.Evaluation" /> carries the preview hashes.</summary>
    [JsonStringEnumMemberName("evaluated")]
    Evaluated,

    /// <summary>A revision needed no evaluation, as after a move: the last evaluation still stands.</summary>
    [JsonStringEnumMemberName("settled")]
    Settled,

    /// <summary>A compound was saved: <c>/node-types</c> may have changed, and the graph evaluates again.</summary>
    [JsonStringEnumMemberName("typesChanged")]
    TypesChanged,

    /// <summary>The session has ended.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,

    /// <summary>The events after the asked number are gone: read the state again.</summary>
    [JsonStringEnumMemberName("resync")]
    Resync
}

/// <summary>
///     One numbered event of a session. Sequence numbers rise by one, so a client that sees a gap has missed something
///     and reads <c>/events</c> or the state. Every client gets every event, its own edits included.
/// </summary>
/// <param name="Sequence">Its number within the session.</param>
/// <param name="Kind">What it reports.</param>
/// <param name="Revision">The document's revision when it was sent.</param>
/// <param name="History">The undo history when it was sent.</param>
/// <param name="Label">What the edit, undo or redo was called.</param>
/// <param name="Delta">What changed in the document.</param>
/// <param name="Evaluation">What an evaluation did.</param>
public sealed record GraphEvent(
    long Sequence,
    GraphEventKind Kind,
    long Revision,
    GraphHistoryState History,
    string? Label = null,
    GraphDelta? Delta = null,
    GraphEvaluation? Evaluation = null
);

/// <summary>Asks for a node's picture at the size the graph computes it.</summary>
/// <param name="Node">The node.</param>
/// <param name="Port">Its output port, or the usage of an Output node, as a preview names them.</param>
public sealed record GraphImageRequest(int Node, string Port);

/// <summary>Where a full-size picture is: <c>GET /api/v1/previews/{hash}</c>. Pixels never cross JSON.</summary>
/// <param name="Hash">The SHA-256 of the PNG's bytes, in lower-case hex.</param>
/// <param name="Width">The PNG's size in pixels.</param>
/// <param name="Height">The PNG's size in pixels.</param>
public sealed record GraphImage(string Hash, int Width, int Height);

/// <summary>The body of <c>POST /api/v1/library/graphs</c>.</summary>
/// <param name="Name">The graph's name. The Server makes its file name from it.</param>
/// <param name="Project">
///     A project's path below <c>projects/</c> to put it in, or null for a new project of its own.
/// </param>
/// <param name="Resolution">The side it evaluates at. Null is 2048. Its authoring base does not depend on this.</param>
public sealed record GraphCreateRequest(string Name, string? Project = null, int? Resolution = null);

/// <summary>A new graph's file.</summary>
/// <param name="Path">Relative to the library, with forward slashes, as <c>POST /graphs</c> takes it.</param>
public sealed record GraphCreated(string Path);

/// <summary>Moves some nodes into a new compound beside the graph and puts a node holding it in their place.</summary>
/// <param name="Name">
///     Its node type's path, such as <c>Masks/Rust Edge</c>, which is also its file below <c>Compounds</c>.
/// </param>
/// <param name="Nodes">The nodes. The wires that cross into them and out of them become its ports.</param>
/// <param name="X">Where the node holding it goes.</param>
/// <param name="Y">Where the node holding it goes.</param>
public sealed record GraphExtractRequest(string Name, IReadOnlyList<int> Nodes, float X, float Y);

/// <summary>A selection saved as a compound. The edit undoes as one step, and the file stays.</summary>
/// <param name="Compound">The new compound.</param>
/// <param name="Node">The node holding it.</param>
/// <param name="Edit">The edit to the graph the selection came from.</param>
public sealed record GraphExtraction(GraphCompound Compound, int Node, GraphEditResult Edit);

/// <summary>The body of <c>POST /api/v1/library/compounds</c>.</summary>
/// <param name="Type">The shipped compound's node type.</param>
/// <param name="Name">
///     The copy's node type. It cannot be a shipped one's, since a shipped compound is never shadowed. Null for the
///     shipped name and " Copy".
/// </param>
/// <param name="Graph">
///     An open graph whose compounds folder takes it, so that the graph can hold it. Null is refused.
/// </param>
public sealed record GraphCompoundCopy(string Type, string? Name = null, string? Graph = null);

/// <summary>The events after a number, for a client that does not hold a WebSocket or has missed some.</summary>
/// <param name="Sequence">The newest event's number.</param>
/// <param name="Events">The events after the number asked for, oldest first.</param>
public sealed record GraphEventPage(long Sequence, IReadOnlyList<GraphEvent> Events);
