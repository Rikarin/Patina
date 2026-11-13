namespace Patina.Contracts;

/// <summary>What the documents below one folder or project cost (#207).</summary>
/// <param name="Name">The folder or project, by its name in the folder asked about.</param>
/// <param name="Documents">How many stacks are below it, at any depth.</param>
/// <param name="Bytes">What their maps hold together, uncompressed at each document's size.</param>
public sealed record FolderCost(string Name, int Documents, long Bytes);

/// <summary>
///     What a folder of the library costs, and each folder or project directly in it, costliest first (#207). A
///     document's cost is its maps as an export writes them uncompressed: its size, by four bytes a texel, by a map for
///     each channel of each texture set. Compression and mip levels are a preset's, and are not counted.
/// </summary>
/// <param name="Folder">The folder below <c>projects/</c>, empty for the whole library.</param>
/// <param name="Documents">How many stacks are below it.</param>
/// <param name="Bytes">What they cost together.</param>
/// <param name="Items">Each folder or project directly in it.</param>
public sealed record LibraryCosts(string Folder, int Documents, long Bytes, IReadOnlyList<FolderCost> Items);

/// <summary>One document and what it costs.</summary>
/// <param name="Path">Its file, below the library.</param>
/// <param name="Width">Its width in texels.</param>
/// <param name="Height">Its height in texels.</param>
/// <param name="Maps">How many maps its texture sets export.</param>
/// <param name="Bytes">What they hold together, uncompressed.</param>
public sealed record DocumentCost(string Path, int Width, int Height, int Maps, long Bytes);

/// <summary>One page of the documents that cost more than a budget, costliest first.</summary>
/// <param name="Items">The documents on this page.</param>
/// <param name="Total">How many cost more than the budget, on all pages.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record DocumentCostPage(IReadOnlyList<DocumentCost> Items, int Total, int Offset, int Limit);
