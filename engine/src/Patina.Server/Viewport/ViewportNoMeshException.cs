namespace Patina.Server.Viewport;

/// <summary>
///     A pick, a marquee, a cast or a screenshot asked of a viewport with no mesh open. The Server finds it before the
///     GPU engine is asked anything, so it is no failure of the engine (#198).
/// </summary>
public sealed class ViewportNoMeshException : InvalidOperationException {
    public ViewportNoMeshException() : base("Open a mesh first: the viewport has none.") { }

    public ViewportNoMeshException(string message) : base(message) { }

    public ViewportNoMeshException(string message, Exception innerException) : base(message, innerException) { }
}
