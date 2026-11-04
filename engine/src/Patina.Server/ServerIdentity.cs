namespace Patina.Server;

/// <summary>What this Server was started as, settled before it starts listening.</summary>
/// <param name="Library">The full path of the library it owns, or null.</param>
/// <param name="Token">The access token in force, or null.</param>
public sealed record ServerIdentity(string? Library, string? Token);
