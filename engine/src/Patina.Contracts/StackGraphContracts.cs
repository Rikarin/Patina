namespace Patina.Contracts;

/// <summary>
///     Writes the graph a layer stack stands for into its project, beside it, as a new graph document (#205). The stack
///     is left as it is: the graph is a copy to take further, and nothing keeps the two in step.
/// </summary>
/// <param name="Name">What the graph is called. A name the project already has gets a number after it.</param>
public sealed record StackGraphRequest(string Name);

/// <summary>What writing a stack's graph made.</summary>
/// <param name="Path">The graph document, below the library, as a project lists it.</param>
/// <param name="Name">The name it was given, with a number when the one asked for was taken.</param>
/// <param name="Problems">
///     What the stack had to say as it became a graph, each said once, such as a layer left out because it has
///     nothing to show: the graph has the rest.
/// </param>
public sealed record StackGraphMade(string Path, string Name, IReadOnlyList<string> Problems);
