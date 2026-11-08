namespace Patina.Server;

/// <summary>No GPU process is connected, so a wait for one ended. It can start or restart at any time.</summary>
public sealed class RendererMissingException : TimeoutException {
    public RendererMissingException() : base("No GPU process is connected.") { }

    public RendererMissingException(string message) : base(message) { }

    public RendererMissingException(string message, Exception innerException) : base(message, innerException) { }
}
