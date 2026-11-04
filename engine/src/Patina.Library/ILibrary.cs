using Patina.Contracts;

namespace Patina.Library;

/// <summary>
///     One open library: one game's worth of projects and shared assets. Nothing loads a whole library (plan §5), so
///     every query here answers one page from the index, and a project itself loads on open.
/// </summary>
public interface ILibrary {
    LibraryId Id { get; }

    /// <summary>The library's folder.</summary>
    string RootPath { get; }

    /// <summary>One page of the projects that match.</summary>
    ProjectPage Projects(LibraryQuery query);

    /// <summary>One page of the shared assets that match.</summary>
    AssetPage Assets(LibraryQuery query);

    /// <summary>What changing an asset's bytes would touch, or null when the index does not know the asset.</summary>
    AssetImpact? Impact(AssetId asset, int offset = 0, int limit = 50);
}
