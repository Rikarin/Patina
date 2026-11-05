using Patina.Contracts;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Vixen.Authoring;
using Vixen.NodeGraph;

namespace Patina.Server.Graphs;

/// <summary>Turns parameter commands into edits of the list a graph exposes, and refuses a bad one.</summary>
sealed class GraphParameterEdits(NodeGraphModel graph) {
    public SetParametersCommand Add(GraphAddParameter add) {
        var parameter = Checked(add.Parameter, null);
        return new(graph, [..graph.Parameters, parameter], $"Expose {parameter.Name}", parameter.Name);
    }

    public SetParametersCommand Set(GraphSetParameter set) {
        var index = ParameterAt(set.Name);
        var parameter = Checked(set.Parameter, set.Name);
        List<SettingDefinition> next = [..graph.Parameters];
        next[index] = parameter;
        return new(graph, next, $"Set {set.Name}", set.Name);
    }

    public SetParametersCommand Remove(GraphRemoveParameter remove) {
        var index = ParameterAt(remove.Name);
        List<SettingDefinition> next = [..graph.Parameters];
        next.RemoveAt(index);
        return new(graph, next, $"Remove {remove.Name}", null);
    }

    int ParameterAt(string name) {
        var index = graph.Parameters.FindIndex(p => string.Equals(p.Name, name, StringComparison.Ordinal));
        return index >= 0 ? index : throw new GraphEditRefusedException($"There is no parameter '{name}'.");
    }

    /// <summary>A parameter as the model holds it, or a refusal a client can show.</summary>
    /// <param name="parameter">What a client sent.</param>
    /// <param name="replacing">The name it replaces, which it may keep.</param>
    SettingDefinition Checked(GraphSettingDefinition parameter, string? replacing) {
        if (string.IsNullOrWhiteSpace(parameter.Name)) {
            throw new GraphEditRefusedException("A parameter needs a name.");
        }

        if (!string.Equals(parameter.Name, replacing, StringComparison.Ordinal)
            && graph.Parameters.Exists(p => string.Equals(p.Name, parameter.Name, StringComparison.Ordinal))) {
            throw new GraphEditRefusedException($"There is a parameter called '{parameter.Name}' already.");
        }

        if (!Enum.TryParse<SettingKind>(parameter.Kind, out var kind) || !Enum.IsDefined(kind)) {
            throw new GraphEditRefusedException($"'{parameter.Kind}' is not a kind of parameter.");
        }

        SettingDefinition definition = new(
            parameter.Name,
            parameter.Default,
            parameter.Summary,
            kind,
            parameter.Minimum ?? float.NegativeInfinity,
            parameter.Maximum ?? float.PositiveInfinity,
            parameter.Group,
            [..parameter.Accepted]
        );
        return Holds(definition)
            ? definition
            : throw new GraphEditRefusedException($"'{parameter.Default}' is not a value for {parameter.Name}.");
    }

    static bool Holds(SettingDefinition parameter) {
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
}

/// <summary>Replaces the list of exposed parameters. Vixen has no command for this either.</summary>
sealed class SetParametersCommand(
    NodeGraphModel graph,
    IReadOnlyList<SettingDefinition> next,
    string name,
    string? merges,
    IReadOnlyList<SettingDefinition>? before = null
) : IAuthoringCommand {
    readonly IReadOnlyList<SettingDefinition> previous = before ?? [..graph.Parameters];

    public string Name => name;

    string? Merges => merges;

    public void Do(ICommandContext context) => Write(next);

    public void Undo(ICommandContext context) => Write(previous);

    public bool TryMergeWith(IAuthoringCommand previousCommand, [NotNullWhen(true)] out IAuthoringCommand? merged) {
        merged = previousCommand is SetParametersCommand earlier && merges is not null && earlier.Merges == merges
            ? new SetParametersCommand(graph, next, name, merges, earlier.previous)
            : null;
        return merged is not null;
    }

    void Write(IReadOnlyList<SettingDefinition> parameters) {
        graph.Parameters.Clear();
        graph.Parameters.AddRange(parameters);
    }
}
