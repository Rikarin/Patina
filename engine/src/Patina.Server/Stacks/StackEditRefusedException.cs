namespace Patina.Server.Stacks;

/// <summary>
///     A command that cannot be applied, with a neutral reason and a stable code a client says in its own words
///     (<see cref="Api.Refusals" />). Nothing has changed.
/// </summary>
public sealed class StackEditRefusedException : Exception {
    public StackEditRefusedException() { }

    public StackEditRefusedException(string message) : base(message) { }

    public StackEditRefusedException(string message, Exception innerException) : base(message, innerException) { }

    /// <param name="message">The reason, for a reader of the API.</param>
    /// <param name="code">What a client tells it apart by.</param>
    /// <param name="values">What the reason was written from, by name.</param>
    public StackEditRefusedException(string message, string code, IReadOnlyDictionary<string, string>? values = null)
        : base(message) {
        Code = code;
        Values = values ?? new Dictionary<string, string>();
    }

    /// <summary>What a client tells this refusal apart by.</summary>
    public string Code { get; } = Api.Refusals.StackRefused;

    /// <summary>The values the reason was written from.</summary>
    public IReadOnlyDictionary<string, string> Values { get; } = new Dictionary<string, string>();
}
