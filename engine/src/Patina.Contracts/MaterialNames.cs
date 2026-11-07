namespace Patina.Contracts;

/// <summary>What a model's materials are called as texture sets, which have to be told apart.</summary>
public static class MaterialNames {
    /// <summary>A name for each material, in order: its own, with a number when it has none or shares it.</summary>
    public static string[] Unique(IReadOnlyList<string> names) {
        ArgumentNullException.ThrowIfNull(names);

        HashSet<string> taken = new(StringComparer.Ordinal);
        var unique = new string[names.Count];
        for (var at = 0; at < unique.Length; at++) {
            var name = string.IsNullOrWhiteSpace(names[at]) ? $"Material {at + 1}" : names[at].Trim();
            var candidate = name;
            for (var count = 2; !taken.Add(candidate); count++) {
                candidate = $"{name} {count}";
            }

            unique[at] = candidate;
        }

        return unique;
    }
}
