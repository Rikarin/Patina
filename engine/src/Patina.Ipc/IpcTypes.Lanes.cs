namespace Patina.Ipc;

/// <summary>Message types that came after the file grew long, and which of them go ahead of a Worker's queue.</summary>
public static partial class IpcTypes {
    /// <summary>
    ///     Server to Worker: one texel of one node's picture, as the graph stored it (#184); answers with
    ///     <see cref="IpcMessage.GraphSampled" />.
    /// </summary>
    public const string GraphSample = "graph.sample";

    /// <summary>
    ///     Whether a Worker answers this request ahead of its queue (<see cref="IpcConnection.Urgent" />): what a
    ///     frame needs, and questions about where things are on the mesh, none of which depends on work queued before
    ///     it. The maps are files the Server had written before it named them.
    /// </summary>
    /// <remarks>
    ///     ⚠ Never <see cref="ViewportStack" /> (#295): wearing a stack keeps its place behind the
    ///     <c>stack.load</c> and <c>stack.sync</c> sent before it, or the viewport wears a stack the Worker does not
    ///     hold yet, or one as it was before its last change. The Server does not hold a frame for it.
    /// </remarks>
    public static bool Urgent(string type) =>
        type is ViewportOpen
            or ViewportMaps
            or ViewportCamera
            or ViewportDisplay
            or ViewportRender
            or ViewportPick
            or ViewportCast
            or ViewportMarquee
            or ViewportBakePreview;
}
