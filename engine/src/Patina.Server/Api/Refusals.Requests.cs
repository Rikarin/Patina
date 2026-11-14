using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Server.Viewport;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Patina.Server.Api;

/// <summary>
///     Refusals of a request as asked: a page, a size, a file of the wrong kind, a setting out of range, something no
///     longer kept. Each has a code, so a client says it in its own words and never shows a path or an id (#198).
/// </summary>
public static partial class Refusals {
    /// <summary>
    ///     A page of a list with a negative offset, or a limit past what one page holds. Values: <c>most</c>.
    /// </summary>
    public const string PageRefused = "page-refused";

    /// <summary>A page of a list asked for outside its bounds: 400.</summary>
    public static ProblemHttpResult BadPage(int most) =>
        Problem(
            $"The offset cannot be negative and the limit is 1 to {most}.",
            StatusCodes.Status400BadRequest,
            PageRefused,
            new Dictionary<string, string> { ["most"] = most.ToString(CultureInfo.InvariantCulture) }
        );

    /// <summary>
    ///     A size outside what can be made: a document, a project's texture sets or a screenshot. Values:
    ///     <c>least</c> and <c>most</c>, as a person writes them.
    /// </summary>
    public const string SizeRefused = "size-refused";

    /// <summary>A size outside <paramref name="least" /> to <paramref name="most" />: 400.</summary>
    public static ProblemHttpResult BadSize(string what, string least, string most, int status) =>
        Problem(
            $"{what} is {least} to {most}.",
            status,
            SizeRefused,
            new Dictionary<string, string> { ["least"] = least, ["most"] = most }
        );

    /// <summary>
    ///     A file given where a file of another kind was asked for, such as a picture for a color table. Values:
    ///     <c>file</c>, its name alone, and <c>wanted</c>, the extensions that would do.
    /// </summary>
    public const string FileKindWrong = "file-kind-wrong";

    /// <summary>A file that is not one of <paramref name="wanted" />: named by its name, never where it is.</summary>
    public static ProblemHttpResult WrongKind(string file, IEnumerable<string> wanted, int status) {
        var name = Path.GetFileName(file);
        var kinds = string.Join(" or ", wanted);
        return Problem(
            $"'{name}' is not a {kinds} file.",
            status,
            FileKindWrong,
            new Dictionary<string, string> { ["file"] = name.Length > 0 ? name : file, ["wanted"] = kinds }
        );
    }

    /// <summary>A file a request named that is not there, by its name alone: where it was is the asker's.</summary>
    public static ProblemHttpResult NoFile(string file, int status = StatusCodes.Status404NotFound) =>
        Problem(
            $"There is no file '{Path.GetFileName(file)}'.",
            status,
            FileMissing,
            new Dictionary<string, string> { ["file"] = Path.GetFileName(file) }
        );

    /// <summary>
    ///     Settings a project, the viewport or a bake setup cannot hold, such as a number out of range. Values:
    ///     <c>reason</c>, which is the Server's own sentence.
    /// </summary>
    public const string SettingsRefused = "settings-refused";

    /// <summary>Settings that cannot be kept as asked, with the check's sentence as the reason.</summary>
    public static ProblemHttpResult BadSettings(string problem, int status) =>
        Problem(problem, status, SettingsRefused, Reason(problem));

    /// <summary>A project a request named that the library does not have. Values: <c>project</c>.</summary>
    public const string ProjectMissing = "project-missing";

    /// <summary>A project the library does not have: 404.</summary>
    public static ProblemHttpResult NoProject(string? project) =>
        Problem(
            $"There is no project '{project}'.",
            StatusCodes.Status404NotFound,
            ProjectMissing,
            new Dictionary<string, string> { ["project"] = project ?? string.Empty }
        );

    /// <summary>
    ///     A picture made for a preview that is no longer kept: a graph's, a bake's or a compression preview's. Asking
    ///     for the preview again makes it.
    /// </summary>
    public const string PreviewMissing = "preview-missing";

    /// <summary>A preview's picture no longer kept: 404.</summary>
    public static ProblemHttpResult NoPreview(string detail) =>
        Problem(detail, StatusCodes.Status404NotFound, PreviewMissing);

    /// <summary>
    ///     A build or a compression preview the library's export settings cannot make. Values: <c>reason</c>, which
    ///     is the export's own sentence.
    /// </summary>
    public const string BuildRefused = "build-refused";

    /// <summary>A build refused, with the export's sentence as the reason: 422.</summary>
    public static ProblemHttpResult BadBuild(Exception refusal) =>
        Problem(refusal.Message, StatusCodes.Status422UnprocessableEntity, BuildRefused, Reason(refusal.Message));

    /// <summary>A build whose report is no longer kept, or that never finished. Values: <c>id</c>.</summary>
    public const string BuildReportMissing = "build-report-missing";

    /// <summary>
    ///     A bake that cannot run as set up, for a reason with no code of its own. Values: <c>reason</c>, which is
    ///     the bake's own sentence.
    /// </summary>
    public const string BakeRefused = "bake-refused";

    /// <summary>
    ///     A bake shown on the mesh, or a map of one, that its job has not finished yet. Values: <c>id</c>.
    /// </summary>
    public const string JobUnfinished = "job-unfinished";

    /// <summary>
    ///     A map asked of a bake that did not make it. Values: <c>map</c>, the map's name as a person reads it.
    /// </summary>
    public const string BakeMapMissing = "bake-map-missing";

    /// <summary>A map asked of a bake that did not make it, named by its label.</summary>
    public static ProblemHttpResult NoBakeMap(string job, string map) =>
        Problem(
            $"Job '{job}' made no '{map}' map.",
            StatusCodes.Status404NotFound,
            BakeMapMissing,
            new Dictionary<string, string> { ["map"] = Terms.Label(map) }
        );

    /// <summary>An object-space bake shown on the material, which reads tangent-space normals.</summary>
    public const string BakeObjectSpace = "bake-object-space";

    /// <summary>A bake setup with no high mesh, which may be the low mesh itself.</summary>
    public const string BakeNeedsHigh = "bake-needs-high";

    /// <summary>A node shown on the mesh before the graph has evaluated it.</summary>
    public const string NodeUnevaluated = "node-unevaluated";

    /// <summary>
    ///     A question about the viewport's mesh, or a picture of it, when it has none
    ///     (<see cref="ViewportNoMeshException" />).
    /// </summary>
    public const string ViewportNoMesh = "viewport-no-mesh";

    /// <summary>A question or a picture of a viewport with no mesh: the Server's own check, 409.</summary>
    public static ProblemHttpResult Of(ViewportNoMeshException none) =>
        Problem(none.Message, StatusCodes.Status409Conflict, ViewportNoMesh);

    /// <summary>A preference out of range. Values: <c>reason</c>, the Server's own sentence.</summary>
    public const string PreferenceRefused = "preference-refused";

    /// <summary>
    ///     A request no client of this build sends: a value the API does not know. Values: <c>reason</c>, which is
    ///     the Server's own sentence, for whoever wrote the client.
    /// </summary>
    public const string RequestInvalid = "request-invalid";

    /// <summary>A sentence with each quoted full path in it cut to the file's name, quotes kept.</summary>
    public static string Plain(string sentence) =>
        QuotedPath()
            .Replace(
                sentence,
                static match => match.Groups[1].Value
                    + Path.GetFileName(match.Groups[2].Value.TrimEnd('\\', '/'))
                    + match.Groups[1].Value
            );

    /// <summary>A path in quotes that starts at a drive, a share or the root, and has a folder in it.</summary>
    [GeneratedRegex("""(['"])((?:[A-Za-z]:[\\/]|\\\\|/)[^'"]*[\\/][^'"]*)\1""")]
    private static partial Regex QuotedPath();
}
