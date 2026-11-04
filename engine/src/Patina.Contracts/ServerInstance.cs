namespace Patina.Contracts;

/// <summary>One running Server, as the per-user discovery file lists it (plan §8).</summary>
/// <param name="Library">The full path of the library the Server owns.</param>
/// <param name="ProcessId">The Server's process id; an entry whose process has gone is stale.</param>
/// <param name="Port">The loopback port it listens on.</param>
/// <param name="Token">The bearer token every request must present.</param>
/// <param name="Version">The Server's release version.</param>
public sealed record ServerInstance(string Library, int ProcessId, int Port, string Token, string Version);

/// <summary>The discovery file's content.</summary>
/// <param name="Instances">The running Servers, at most one per library.</param>
public sealed record DiscoveryDocument(IReadOnlyList<ServerInstance> Instances);
