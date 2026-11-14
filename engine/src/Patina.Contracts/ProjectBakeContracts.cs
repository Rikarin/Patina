namespace Patina.Contracts;

/// <summary>
///     The bakes a project's own settings ask for (#186): one request per texture set to bake, or one for the whole
///     mesh, as the Bake dialog sends them. <c>GET /api/v1/library/project/bake/plan</c> answers it, and
///     <c>patina bake-maps --project … --dry-run</c> prints it, so a script bakes a project the way the editor does
///     without working the requests out itself.
/// </summary>
/// <param name="Project">The project's folder.</param>
/// <param name="Requests">What to send to <c>POST /api/v1/jobs/bakes</c>, one at a time or all at once.</param>
public sealed record ProjectBakePlan(string Project, IReadOnlyList<BakeRequest> Requests);

/// <summary>
///     Maps baked in another program, brought in as a texturing document's bake (#144): the body of
///     <c>POST /api/v1/library/project/bake/import</c>.
/// </summary>
/// <param name="Project">The project, below <c>projects/</c>.</param>
/// <param name="Document">The texturing document, by its id, whose texture set the maps are of.</param>
/// <param name="Maps">Each map and the PNG of it, on the Server's machine.</param>
/// <param name="Space">Whether the normal is in <c>tangent</c> or <c>object</c> space, as a bake's options say.</param>
public sealed record BakeImportRequest(
    string Project,
    Guid Document,
    IReadOnlyList<BakeImportMap> Maps,
    string Space = "tangent"
);

/// <summary>One map brought in.</summary>
/// <param name="Map">One of the maps a bake makes, such as <c>normal</c>, <c>ao</c> or <c>curvature</c>.</param>
/// <param name="File">
///     The PNG, absolute or below the library. A normal map's green runs up the picture, as Patina's bakes do.
/// </param>
public sealed record BakeImportMap(string Map, string File);

/// <summary>Where maps brought in now are, which the document reads its bake from.</summary>
/// <param name="Folder">The bake's folder, below the library.</param>
public sealed record BakeImported(string Folder);
