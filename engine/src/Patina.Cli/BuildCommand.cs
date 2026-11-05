using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Graphs;
using Patina.Library;
using System.Globalization;
using Vixen.Graphics.Vulkan;
using Vixen.TextureGraph;

namespace Patina.Cli;

/// <summary>
///     <c>patina build</c>: the Server's pipeline in this process, with no Server and no Worker, for a build farm (plan
///     §5). It opens a device only if a step wants one, so a preview and an export run on a machine with no GPU.
/// </summary>
static class BuildCommand {
    public const string Usage = """
                                  patina build --library <path> [--changed] [--asset <guid>] [--project <guid>]...
                                               [--preset <id>]... [--no-export] [--folder <path>] [--dry-run]
                                """;

    public static async Task<int> RunAsync(string[] options) {
        if (Parse(options) is not { } parsed) {
            await Console.Error.WriteLineAsync(Usage);
            return 2;
        }

        var (root, request, dryRun) = parsed;
        if (!OpenLibrary.Exists(root)) {
            await Console.Error.WriteLineAsync($"There is no library at '{root}'.");
            return 2;
        }

        using var library = OpenLibrary.Open(root);
        using var executor = new LocalExecutor();
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, press) => {
            press.Cancel = true;
            stop.Cancel();
        };

        try {
            if (dryRun) {
                var preview = BuildPipeline.Plan(library, request, ToolCatalog.Default, stop.Token).Preview(0, 50);
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{preview.Projects} projects: {preview.Rebuilds} outputs to build ({preview.Shared} shared), {preview.Fresh} fresh, {preview.Pinned} pinned, {preview.Exports} exports, {preview.Unresolved} unresolved."
                    )
                );
                return 0;
            }

            var name = $"cli-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..28];
            var journal = new BuildJournal(
                Path.Combine(LibraryLayout.ReportsPath(root), name),
                name,
                DateTimeOffset.UtcNow
            );
            var report = await new BuildPipeline(library, executor, ToolCatalog.Default).RunAsync(
                journal,
                request,
                null,
                null,
                TimeProvider.System,
                stop.Token
            );
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{report.Projects} projects touched: {report.Built} built, {report.Reused} reused, {report.Fresh} fresh, {report.Exported} exported, {report.ExportsFresh} exports fresh, {report.Failed} failed."
                )
            );
            Console.WriteLine(report.Folder);
            return report.Failed > 0 ? 1 : 0;
        } catch (BuildRefusedException e) {
            await Console.Error.WriteLineAsync(e.Message);
            return 2;
        } catch (OperationCanceledException) {
            await Console.Error.WriteLineAsync("Stopped. What finished stays finished, and the next build carries on.");
            return 130;
        }
    }

    static (string Root, BuildRequest Request, bool DryRun)? Parse(string[] options) {
        string? root = null;
        Guid? asset = null;
        string? folder = null;
        List<Guid> projects = [];
        List<string> presets = [];
        var none = false;
        var dryRun = false;
        for (var at = 0; at < options.Length; at++) {
            var value = at + 1 < options.Length ? options[at + 1] : null;
            switch (options[at]) {
                case "--changed":
                    break; // What every build does: it only ever builds what changed.
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--no-export":
                    none = true;
                    break;
                case "--library" when value is not null:
                    root = Path.GetFullPath(value);
                    at++;
                    break;
                case "--folder" when value is not null:
                    folder = Path.GetFullPath(value);
                    at++;
                    break;
                case "--preset" when value is not null:
                    presets.Add(value);
                    at++;
                    break;
                case "--asset" when Guid.TryParse(value, out var id):
                    asset = id;
                    at++;
                    break;
                case "--project" when Guid.TryParse(value, out var id):
                    projects.Add(id);
                    at++;
                    break;
                default:
                    return null;
            }
        }

        return root is null
            ? null
            : (root, new() {
                Asset = asset,
                Projects = projects.Count > 0 ? projects : null,
                Presets = none ? [] : presets.Count > 0 ? presets : null,
                Folder = folder
            }, dryRun);
    }

    /// <summary>Steps in this process, one at a time, off the caller's thread.</summary>
    sealed class LocalExecutor : IBuildExecutor, IDisposable {
        readonly OutputBuilders builders;
        VulkanDevice? device;
        TexturePlanEvaluator? evaluator;

        public LocalExecutor() {
            builders = new(Evaluator, static work => work());
        }

        public Task<BuildManifest> OutputAsync(OutputStep work, CancellationToken cancellationToken) =>
            Task.Run(() => builders.Build(work, 0, cancellationToken), cancellationToken);

        public Task<ExportManifest> ExportAsync(ExportStep work, CancellationToken cancellationToken) =>
            Task.Run(() => OutputBuilders.Export(work, cancellationToken), cancellationToken);

        public void Dispose() {
            evaluator?.Dispose();
            device?.Dispose();
        }

        TexturePlanEvaluator Evaluator() {
            if (evaluator is null) {
                if (!VulkanDevice.TryCreate(new(), out device, out var refusal)) {
                    throw new BuildStepFailedException($"No Vulkan device: {refusal}");
                }

                evaluator = new(device);
            }

            return evaluator;
        }
    }
}
