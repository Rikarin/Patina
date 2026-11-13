namespace Patina.Contracts;

/// <summary>The sample projects Patina ships, which a client offers to start from (#190).</summary>
/// <param name="Names">Each sample's name, which is also the project it makes.</param>
public sealed record SampleList(IReadOnlyList<string> Names);

/// <summary>Makes a project from a sample.</summary>
/// <param name="Name">Which sample.</param>
/// <param name="Folder">The folder below <c>projects/</c> to make it in, or null for the top.</param>
public sealed record SampleRequest(string Name, string? Folder = null);
