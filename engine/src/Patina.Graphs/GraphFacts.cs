using Patina.Contracts;
using Patina.Library;
using System.Globalization;
using Vixen.Core.Yaml;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Graphs;

/// <summary>
///     A graph as the contract describes it, for every process that has to say what one is without opening it in a
///     session: the Server, and <c>patina graph info</c> and <c>patina bake</c>. One place, so the two cannot disagree
///     about what a parameter or a value is.
/// </summary>
public static class GraphFactsOf {
    const string OutputNode = "Output/Output";
    const string UsageSetting = "Usage";
    const string DefaultUsage = "baseColor";

    /// <summary>A graph's file, read. A package's folder reads its graph.</summary>
    /// <exception cref="FileNotFoundException">There is no graph there.</exception>
    /// <exception cref="LibraryFormatException">A package's manifest is not one this reads.</exception>
    public static (string File, NodeGraphAsset Graph, PackageManifest? Package) Read(string path) {
        var package = PackageManifests.IsPackage(path) ? PackageManifests.Read(path) : null;
        var file = package is null ? path : PackageManifests.GraphOf(path);
        package ??= PackageManifests.PackageOf(file) is { } folder ? PackageManifests.Read(folder) : null;
        if (!File.Exists(file)) {
            throw new FileNotFoundException($"There is no graph at '{path}'.", path);
        }

        return (file, YamlSerializer.Parse<NodeGraphAsset>(SharedFile.ReadAllText(file)), package);
    }

    /// <summary>What a graph or a package is, as <c>patina graph info</c> prints it.</summary>
    public static GraphFacts Of(NodeGraphAsset graph, PackageManifest? package = null) {
        var model = NodeGraphDocument.Load(graph, out _);
        return new(
            graph.Name,
            Info(graph),
            new SortedDictionary<string, string>(graph.Settings, StringComparer.Ordinal),
            [..model.Parameters.Select(parameter => Definition(parameter))],
            [..graph.Presets.Select(static preset => new GraphPresetState(preset.Name, preset.Values))],
            [
                ..graph.Interface.Where(static port => port.Direction == PortDirection.Input)
                    .Select(static port => new GraphInterfacePort(
                            port.Name,
                            port.Direction.ToString(),
                            port.Kind.ToString(),
                            port.Default,
                            port.Summary
                        )
                    )
            ],
            Usages(graph),
            package
        );
    }

    /// <summary>The maps a graph makes, by usage, each once.</summary>
    public static IReadOnlyList<string> Usages(NodeGraphAsset graph) => [
        ..graph.Nodes.Where(static node => node.Type == OutputNode)
            .Select(static node => node.Texts.GetValueOrDefault(UsageSetting, DefaultUsage))
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>What a document says about itself, as clients see it.</summary>
    public static GraphInfoState Info(NodeGraphAsset document) =>
        new(
            document.Description,
            document.Author,
            document.Tags,
            document.PhysicalSize is [var width, _] ? width : null,
            document.PhysicalSize is [_, var height] ? height : null
        );

    /// <summary>What a client is told about a setting of a node, a compound or a graph.</summary>
    /// <param name="setting">The setting.</param>
    /// <param name="labels">What a person calls each accepted value, or null to leave that to the client.</param>
    public static GraphSettingDefinition Definition(
        SettingDefinition setting,
        Func<IReadOnlyList<string>, IReadOnlyList<string>?>? labels = null
    ) =>
        new(
            setting.Name,
            setting.Default,
            setting.Summary,
            setting.Kind.ToString(),
            Bound(setting.Minimum),
            Bound(setting.Maximum),
            setting.Group,
            setting.Accepted,
            labels?.Invoke(setting.Accepted),
            setting.Label.Length > 0 ? setting.Label : null,
            setting.Widget == SettingWidget.Default ? null : setting.Widget.ToString(),
            setting.VisibleIf is { } condition ? new(condition.Setting, condition.Value, condition.Negated) : null
        );

    public static GraphDiagnostic Diagnostic(NodeDiagnostic diagnostic) =>
        new(
            diagnostic.Id,
            diagnostic.Message,
            diagnostic.Severity.ToString(),
            diagnostic.Node.IsValid ? diagnostic.Node.Value : null,
            diagnostic.Port.Length > 0 ? diagnostic.Port : null
        );

    /// <summary>Whether a parameter's default is a value it can hold: of its kind, in its range, among its choices.</summary>
    public static bool Holds(SettingDefinition parameter) {
        var number = float.TryParse(
            parameter.Default,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed
        );
        return parameter.Kind switch {
            SettingKind.Float or SettingKind.Int when !number || !float.IsFinite(parsed) => false,
            SettingKind.Int when parsed != MathF.Round(parsed) => false,
            SettingKind.Float or SettingKind.Int when parsed < parameter.Minimum || parsed > parameter.Maximum => false,
            SettingKind.Bool => parameter.Default is "true" or "false",
            _ => parameter.Accepts(parameter.Default)
        };
    }

    /// <summary>
    ///     A graph with its parameters set, for an evaluation that has no session: a preset's values first, then each
    ///     value given by name, then a seed. Each value becomes the parameter's default, which is what a graph evaluated
    ///     on its own reads.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="preset">A preset to start from, or null.</param>
    /// <param name="values">Values by parameter name.</param>
    /// <param name="seed">The seed, or null for the graph's.</param>
    /// <exception cref="ArgumentException">
    ///     There is no such preset or parameter, or a value is not one its parameter can hold, said in a sentence.
    /// </exception>
    public static NodeGraphAsset WithValues(
        NodeGraphAsset graph,
        string? preset,
        IReadOnlyList<KeyValuePair<string, string>> values,
        int? seed
    ) {
        Dictionary<string, string> set = new(StringComparer.Ordinal);
        if (preset is not null) {
            var chosen = graph.Presets.FirstOrDefault(p => string.Equals(p.Name, preset, StringComparison.Ordinal))
                ?? throw new ArgumentException(
                    $"There is no preset '{preset}'. This graph has {Listed(graph.Presets.Select(static p => p.Name))}."
                );
            foreach (var (name, value) in chosen.Values) {
                set[name] = value;
            }
        }

        var model = NodeGraphDocument.Load(graph, out _);
        foreach (var (name, value) in values) {
            if (!model.Parameters.Exists(p => string.Equals(p.Name, name, StringComparison.Ordinal))) {
                throw new ArgumentException(
                    $"There is no parameter '{name}'. This graph has {Listed(model.Parameters.Select(static p => p.Name))}."
                );
            }

            set[name] = value;
        }

        GraphParameterAsset[] parameters = [..graph.Parameters];
        for (var at = 0; at < parameters.Length; at++) {
            var parameter = model.Parameters.Find(p => string.Equals(p.Name, parameters[at].Name, StringComparison.Ordinal));
            if (parameter is null || !set.TryGetValue(parameter.Name, out var value)) {
                continue;
            }

            var changed = parameter with { Default = parameter.Kind == SettingKind.Text ? parameter.Canonical(value) : value };
            if (!Holds(changed)) {
                throw new ArgumentException($"'{value}' is not a value for {parameter.Name}, a {Described(parameter)}.");
            }

            parameters[at] = parameters[at] with { Default = changed.Default };
        }

        var settings = new Dictionary<string, string>(graph.Settings, StringComparer.Ordinal);
        if (seed is { } number) {
            settings[TextureGraphSettings.Seed] = number.ToString(CultureInfo.InvariantCulture);
        }

        return graph with { Parameters = parameters, Settings = settings };
    }

    static string Described(SettingDefinition parameter) =>
        parameter switch {
            { IsChoice: true } => $"choice of {string.Join(", ", parameter.Accepted)}",
            { Kind: SettingKind.Float or SettingKind.Int, IsBounded: true } =>
                $"{parameter.Kind} from {parameter.Minimum.ToString(CultureInfo.InvariantCulture)} to "
                + parameter.Maximum.ToString(CultureInfo.InvariantCulture),
            _ => parameter.Kind.ToString()
        };

    static string Listed(IEnumerable<string> names) =>
        names.ToList() is { Count: > 0 } list ? string.Join(", ", list) : "none";

    static float? Bound(float value) => float.IsFinite(value) ? value : null;
}
