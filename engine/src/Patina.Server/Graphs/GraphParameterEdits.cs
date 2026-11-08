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
        List<SettingDefinition> next = [..Following(set.Name, parameter.Name)];
        next[index] = parameter;
        return new(graph, next, $"Set {set.Name}", set.Name);
    }

    public SetParametersCommand Remove(GraphRemoveParameter remove) {
        var index = ParameterAt(remove.Name);
        List<SettingDefinition> next = [..Following(remove.Name, null)];
        next.RemoveAt(index);
        return new(graph, next, $"Remove {remove.Name}", null);
    }

    /// <summary>
    ///     The parameters, with every condition on one of them following its rename, or dropped with it: a condition
    ///     naming nothing would hide a parameter for good.
    /// </summary>
    IEnumerable<SettingDefinition> Following(string name, string? renamed) =>
        graph.Parameters.Select(parameter =>
            parameter.VisibleIf is { } condition && string.Equals(condition.Setting, name, StringComparison.Ordinal)
                ? parameter with { VisibleIf = renamed is null ? null : condition with { Setting = renamed } }
                : parameter
        );

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

        var widget = SettingWidget.Default;
        if (parameter.Widget is { Length: > 0 } named
            && (!Enum.TryParse(named, out widget) || !Enum.IsDefined(widget))) {
            throw new GraphEditRefusedException($"'{named}' is not a widget a parameter can have.");
        }

        SettingDefinition definition = new(
            parameter.Name,
            parameter.Default,
            parameter.Summary,
            kind,
            parameter.Minimum ?? float.NegativeInfinity,
            parameter.Maximum ?? float.PositiveInfinity,
            parameter.Group,
            [..parameter.Accepted],
            parameter.Label?.Trim() ?? "",
            widget,
            Condition(parameter, replacing)
        );
        Suits(definition);
        return Holds(definition)
            ? definition
            : throw new GraphEditRefusedException($"'{parameter.Default}' is not a value for {parameter.Name}.");
    }

    /// <summary>A widget that cannot draw this parameter is refused, so that no client has to guess a fallback.</summary>
    static void Suits(SettingDefinition parameter) {
        var fits = parameter.Widget switch {
            SettingWidget.Slider => parameter.IsBounded,
            SettingWidget.Angle => parameter.Kind is SettingKind.Float or SettingKind.Int,
            SettingWidget.Toggle => parameter.Kind == SettingKind.Bool,
            _ => true
        };
        if (!fits) {
            throw new GraphEditRefusedException(
                parameter.Widget == SettingWidget.Slider
                    ? $"A slider needs a number with a range, and {parameter.Name} has none."
                    : $"A {parameter.Widget} does not suit a {parameter.Kind} parameter."
            );
        }
    }

    /// <summary>The condition a client sent, which must name another parameter the graph has.</summary>
    SettingCondition? Condition(GraphSettingDefinition parameter, string? replacing) {
        if (parameter.VisibleIf is not { Setting.Length: > 0 } condition) {
            return null;
        }

        if (string.Equals(condition.Setting, parameter.Name, StringComparison.Ordinal)
            || string.Equals(condition.Setting, replacing, StringComparison.Ordinal)) {
            throw new GraphEditRefusedException($"{parameter.Name} cannot be shown or hidden by its own value.");
        }

        return graph.Parameters.Exists(p => string.Equals(p.Name, condition.Setting, StringComparison.Ordinal))
            ? new(condition.Setting, condition.Value, condition.Negated)
            : throw new GraphEditRefusedException($"There is no parameter '{condition.Setting}' to show {parameter.Name} by.");
    }

    /// <summary>Saves the defaults as they are, or the values sent, under a name, replacing one of that name.</summary>
    public SetPresetsCommand SavePreset(GraphSavePreset save) {
        var name = save.Name.Trim();
        if (name.Length == 0) {
            throw new GraphEditRefusedException("A preset needs a name.");
        }

        GraphPreset preset = new() { Name = name };
        foreach (var parameter in graph.Parameters) {
            preset.Values[parameter.Name] = parameter.Default;
        }

        if (save.Values is { } values) {
            preset.Values.Clear();
            foreach (var (key, value) in values) {
                if (!graph.Parameters.Exists(p => string.Equals(p.Name, key, StringComparison.Ordinal))) {
                    throw new GraphEditRefusedException($"There is no parameter '{key}' for a preset to set.");
                }

                preset.Values[key] = value;
            }
        }

        List<GraphPreset> next = [..graph.Presets];
        var index = next.FindIndex(p => string.Equals(p.Name, name, StringComparison.Ordinal));
        if (index >= 0) {
            next[index] = preset;
        } else {
            next.Add(preset);
        }

        return new(graph, next, $"Save Preset {name}");
    }

    public SetPresetsCommand RemovePreset(GraphRemovePreset remove) {
        List<GraphPreset> next = [..graph.Presets];
        return next.RemoveAll(p => string.Equals(p.Name, remove.Name, StringComparison.Ordinal)) > 0
            ? new(graph, next, $"Remove Preset {remove.Name}")
            : throw new GraphEditRefusedException($"There is no preset '{remove.Name}'.");
    }

    /// <summary>Every parameter the preset names takes its value as its default, if it can hold it.</summary>
    public SetParametersCommand ApplyPreset(GraphApplyPreset apply) {
        var preset = graph.Presets.Find(p => string.Equals(p.Name, apply.Name, StringComparison.Ordinal))
            ?? throw new GraphEditRefusedException($"There is no preset '{apply.Name}'.");
        List<SettingDefinition> next = [
            ..graph.Parameters.Select(parameter =>
                preset.Values.TryGetValue(parameter.Name, out var value)
                && parameter with { Default = value } is var changed
                && Holds(changed)
                    ? changed
                    : parameter
            )
        ];
        return new(graph, next, $"Preset {preset.Name}", null);
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

/// <summary>Replaces the list of presets.</summary>
sealed class SetPresetsCommand(NodeGraphModel graph, IReadOnlyList<GraphPreset> next, string name) : IAuthoringCommand {
    readonly IReadOnlyList<GraphPreset> previous = [..graph.Presets];

    public string Name => name;

    public void Do(ICommandContext context) => Write(next);

    public void Undo(ICommandContext context) => Write(previous);

    void Write(IReadOnlyList<GraphPreset> presets) {
        graph.Presets.Clear();
        graph.Presets.AddRange(presets);
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
