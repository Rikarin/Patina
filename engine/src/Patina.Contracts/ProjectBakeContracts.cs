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
