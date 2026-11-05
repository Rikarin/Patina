namespace Patina.Contracts;

// SK7005: a wire record's constructor is its JSON shape, one parameter per field, so fewer parameters means a new API.
#pragma warning disable SK7005
/// <summary>The reply to <c>GET /api/v1/library</c>: what opening a library reads, and nothing more.</summary>
/// <param name="Id">The library's identity, from <c>library.yaml</c>.</param>
/// <param name="Name">The library's display name.</param>
/// <param name="Path">The library's folder on the Server's machine.</param>
/// <param name="SchemaVersion">The version of <c>library.yaml</c> as it stands on disk.</param>
/// <param name="TexelDensity">The texel density new projects start from, in texels per metre.</param>
/// <param name="Channels">The channels the library authors.</param>
/// <param name="DefaultShader">The shader new projects start from.</param>
/// <param name="ExportTargets">The export targets the library builds for.</param>
/// <param name="ProjectCount">The projects in the index.</param>
/// <param name="AssetCount">The shared assets in the index.</param>
/// <param name="Indexing">Whether a rescan is running; counts and pages may lag the files until it ends.</param>
public sealed record LibraryInfo(
    Guid Id,
    string Name,
    string Path,
    int SchemaVersion,
    double TexelDensity,
    IReadOnlyList<string> Channels,
    string DefaultShader,
    IReadOnlyList<string> ExportTargets,
    int ProjectCount,
    int AssetCount,
    bool Indexing
);
#pragma warning restore SK7005

/// <summary>One project as the browser shows it. The project itself loads on open.</summary>
/// <param name="Id">The project's identity.</param>
/// <param name="Name">The project's display name.</param>
/// <param name="Path">The project's folder, relative to <c>projects/</c>, with forward slashes.</param>
/// <param name="Tags">The project's tags, sorted.</param>
/// <param name="ReferenceCount">How many shared assets the project references directly.</param>
public sealed record ProjectSummary(Guid Id, string Name, string Path, IReadOnlyList<string> Tags, int ReferenceCount);

/// <summary>One page of projects.</summary>
/// <param name="Items">The projects on this page.</param>
/// <param name="Total">How many projects match, on all pages.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record ProjectPage(IReadOnlyList<ProjectSummary> Items, int Total, int Offset, int Limit);

/// <summary>The body of <c>POST /api/v1/library/projects</c>.</summary>
/// <param name="Name">The project's display name. The Server makes its folder from it.</param>
/// <param name="Folder">
///     Folders below <c>projects/</c> to put it in, with forward slashes, such as <c>props</c>.
/// </param>
/// <param name="Tags">The project's tags.</param>
public sealed record ProjectCreateRequest(string Name, string? Folder = null, IReadOnlyList<string>? Tags = null);

/// <summary>A tag, and how many items carry it.</summary>
/// <param name="Tag">The tag.</param>
/// <param name="Count">How many projects or assets carry it.</param>
public sealed record TagCount(string Tag, int Count);

/// <summary>One page of tags, by name.</summary>
/// <param name="Items">The tags on this page.</param>
/// <param name="Total">How many tags there are, on all pages.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record TagPage(IReadOnlyList<TagCount> Items, int Total, int Offset, int Limit);

/// <summary>One shared asset as the browser shows it.</summary>
/// <param name="Id">The asset's identity.</param>
/// <param name="Kind">The asset's kind, such as <c>smart-material</c>.</param>
/// <param name="Name">The asset's display name.</param>
/// <param name="Path">The asset's file, relative to <c>shared/</c>, with forward slashes.</param>
/// <param name="Tags">The asset's tags, sorted.</param>
/// <param name="Revision">The content hash of the asset's file, or null when the file is missing.</param>
/// <param name="UsageCount">How many projects reference the asset directly.</param>
public sealed record AssetSummary(
    Guid Id,
    string Kind,
    string Name,
    string Path,
    IReadOnlyList<string> Tags,
    string? Revision,
    int UsageCount
);

/// <summary>One page of shared assets.</summary>
/// <param name="Items">The assets on this page.</param>
/// <param name="Total">How many assets match, on all pages.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record AssetPage(IReadOnlyList<AssetSummary> Items, int Total, int Offset, int Limit);

/// <summary>A project that depends on a shared asset.</summary>
/// <param name="Id">The project's identity.</param>
/// <param name="Name">The project's display name.</param>
/// <param name="Path">The project's folder, relative to <c>projects/</c>, with forward slashes.</param>
/// <param name="Pinned">True when a pin holds the project back, so a change to the asset does not reach it.</param>
public sealed record ImpactedProject(Guid Id, string Name, string Path, bool Pinned);

/// <summary>What saving a shared asset would touch: "affects 412 projects, 3 pinned" (plan §5).</summary>
/// <param name="Asset">The asset asked about.</param>
/// <param name="Projects">The projects that depend on the asset, directly or through other assets.</param>
/// <param name="Pinned">How many of <paramref name="Projects" /> a pin holds back; the rest rebuild.</param>
/// <param name="UnreadableDocuments">
///     Project and asset documents the index could not read. They may depend on the asset too, so a value above zero
///     means the counts are a lower bound.
/// </param>
/// <param name="Items">One page of the dependent projects.</param>
/// <param name="Offset">The offset the page starts at.</param>
/// <param name="Limit">The most items a page holds.</param>
public sealed record AssetImpact(
    AssetSummary Asset,
    int Projects,
    int Pinned,
    int UnreadableDocuments,
    IReadOnlyList<ImpactedProject> Items,
    int Offset,
    int Limit
);
