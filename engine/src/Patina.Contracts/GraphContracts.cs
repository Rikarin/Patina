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
public sealed record GraphNodeState(
    int Id,
    string Type,
    float X,
    float Y,
    IReadOnlyDictionary<string, float[]> Values,
    IReadOnlyDictionary<string, string> Texts
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
public sealed record GraphSettingDefinition(
    string Name,
    string Default,
    string Summary,
    string Kind,
    float? Minimum,
    float? Maximum,
    string Group,
    IReadOnlyList<string> Accepted
);

/// <summary>One port of a node type.</summary>
/// <param name="Name">What an edge calls it.</param>
/// <param name="Direction">Input or Output.</param>
/// <param name="Kind">What it carries: Image, Float, Float4 and so on.</param>
/// <param name="Default">The numbers an unset input takes.</param>
/// <param name="Summary">One line about it.</param>
/// <param name="Minimum">The smallest number that means anything, or null when open.</param>
/// <param name="Maximum">The largest number that means anything, or null when open.</param>
/// <param name="SoftMinimum">Where a slider starts, when the port's meaning gives that; numbers go on past it.</param>
/// <param name="SoftMaximum">Where a slider ends, the same way.</param>
public sealed record GraphPortDefinition(
    string Name,
    string Direction,
    string Kind,
    IReadOnlyList<float> Default,
    string Summary,
    float? Minimum,
    float? Maximum,
    float? SoftMinimum,
    float? SoftMaximum
);

/// <summary>A node a client can add, for a palette.</summary>
/// <param name="Path">What a document stores, such as <c>Colour/Levels</c>.</param>
/// <param name="Title">The last segment of the path.</param>
/// <param name="Category">The path without its title.</param>
/// <param name="Summary">One line about it.</param>
/// <param name="HasPreview">Whether the type declares that its picture is worth showing.</param>
/// <param name="Ports">Its inputs and outputs.</param>
/// <param name="Settings">Its text settings, set with <see cref="GraphSetText" />.</param>
public sealed record GraphNodeType(
    string Path,
    string Title,
    string Category,
    string Summary,
    bool HasPreview,
    IReadOnlyList<GraphPortDefinition> Ports,
    IReadOnlyList<GraphSettingDefinition> Settings
);

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
public sealed record GraphHistoryState(
    bool CanUndo,
    bool CanRedo,
    string? UndoLabel,
    string? RedoLabel,
    bool Dirty
);

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
/// <param name="Previews">The newest preview of each node.</param>
/// <param name="Repairs">What reading the file had to drop or fix.</param>
/// <param name="Evaluation">The last evaluation, or null before the first.</param>
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
    IReadOnlyList<GraphPreview> Previews,
    IReadOnlyList<GraphDiagnostic> Repairs,
    GraphEvaluation? Evaluation
);
#pragma warning restore SK7005

/// <summary>An open graph, for a list.</summary>
public sealed record GraphSummary(string Id, string Path, string Name, long Revision, bool Dirty);

/// <summary>One edit. Commands in a batch run in order, and every command sees the ones before it.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(GraphAddNode), "addNode")]
[JsonDerivedType(typeof(GraphRemoveNodes), "removeNodes")]
[JsonDerivedType(typeof(GraphMoveNodes), "moveNodes")]
[JsonDerivedType(typeof(GraphConnect), "connect")]
[JsonDerivedType(typeof(GraphDisconnect), "disconnect")]
[JsonDerivedType(typeof(GraphSetValue), "setValue")]
[JsonDerivedType(typeof(GraphSetText), "setText")]
[JsonDerivedType(typeof(GraphSetSetting), "setSetting")]
public abstract record GraphCommand;

/// <summary>Adds a node.</summary>
/// <param name="Type">The node type's path.</param>
/// <param name="X">Where it goes.</param>
/// <param name="Y">Where it goes.</param>
/// <param name="Ref">
///     A negative number the client picks. Later commands in this batch may use it wherever they name a node, and the
///     reply says which identity it became.
/// </param>
public sealed record GraphAddNode(string Type, float X, float Y, int? Ref = null) : GraphCommand;

/// <summary>Removes nodes, and the wires that touch them.</summary>
public sealed record GraphRemoveNodes(IReadOnlyList<int> Nodes) : GraphCommand;

/// <summary>Where one node goes.</summary>
public sealed record GraphNodePosition(int Node, float X, float Y);

/// <summary>Moves nodes. A move never evaluates anything.</summary>
public sealed record GraphMoveNodes(IReadOnlyList<GraphNodePosition> Positions) : GraphCommand;

/// <summary>Wires an output to an input, replacing what was on that input.</summary>
public sealed record GraphConnect(int FromNode, string FromPort, int ToNode, string ToPort) : GraphCommand;

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

/// <summary>Some edits, applied whole or not at all, and undone as one step.</summary>
/// <param name="Commands">What to do, in order.</param>
/// <param name="Label">What the undo history calls it, or null for a name from the commands.</param>
/// <param name="Merge">
///     True to fold this batch into the last one when both change the same things, so that a whole drag is one undo.
///     A client leaves it false on the first batch of a drag.
/// </param>
public sealed record GraphCommandBatch(IReadOnlyList<GraphCommand> Commands, string? Label = null, bool Merge = false);

/// <summary>Which identity a client's <see cref="GraphAddNode.Ref" /> became.</summary>
public sealed record GraphCreatedNode(int Ref, int Node);

/// <summary>The reply to an edit, an undo, a redo or a save.</summary>
/// <param name="Revision">The document's revision after it.</param>
/// <param name="Sequence">The event that announced it.</param>
/// <param name="History">The undo history after it.</param>
/// <param name="Created">The nodes a batch created under a <c>ref</c>.</param>
public sealed record GraphEditResult(
    long Revision,
    long Sequence,
    GraphHistoryState History,
    IReadOnlyList<GraphCreatedNode> Created
);

/// <summary>What changed in the document, the same for an edit, an undo and a redo.</summary>
/// <param name="Nodes">Nodes that are new or differ, whole.</param>
/// <param name="RemovedNodes">Nodes that have gone.</param>
/// <param name="Edges">Wires that are new.</param>
/// <param name="RemovedEdges">Wires that have gone.</param>
/// <param name="Settings">Graph settings that are new or differ.</param>
/// <param name="RemovedSettings">Graph settings that have gone.</param>
public sealed record GraphDelta(
    IReadOnlyList<GraphNodeState> Nodes,
    IReadOnlyList<int> RemovedNodes,
    IReadOnlyList<GraphEdgeState> Edges,
    IReadOnlyList<GraphEdgeState> RemovedEdges,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<string> RemovedSettings
);

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

/// <summary>The events after a number, for a client that does not hold a WebSocket or has missed some.</summary>
/// <param name="Sequence">The newest event's number.</param>
/// <param name="Events">The events after the number asked for, oldest first.</param>
public sealed record GraphEventPage(long Sequence, IReadOnlyList<GraphEvent> Events);
