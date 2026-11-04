namespace Patina.Library;

/// <summary>
///     One open library: one game's worth of projects and shared assets. Nothing loads a whole library, so this hands
///     out identities and a project loads on open (plan §5). No implementation exists yet: it needs library.yaml (P3).
/// </summary>
public interface ILibrary {
    LibraryId Id { get; }

    /// <summary>The library's folder.</summary>
    string RootPath { get; }

    /// <summary>The projects in the library.</summary>
    ValueTask<IReadOnlyList<ProjectId>> ListProjectsAsync(CancellationToken cancellationToken = default);
}
