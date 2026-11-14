namespace Patina.Contracts;

/// <summary>How a file or an output of one build stands against another's.</summary>
public static class BuildChanges {
    /// <summary>Only the later build has it.</summary>
    public const string Added = "added";

    /// <summary>Only the earlier build has it.</summary>
    public const string Removed = "removed";

    /// <summary>Both have it, and its bytes or its key differ.</summary>
    public const string Changed = "changed";

    /// <summary>Both have it, and it is byte for byte the same.</summary>
    public const string Same = "same";

    /// <summary>Both have it, and one build kept no digest of it, so whether it changed is not known.</summary>
    public const string Unknown = "unknown";
}

/// <summary>
///     What differs between two builds (#217): their outputs' keys and their files' bytes, project by project.
/// </summary>
/// <param name="Before">The earlier build.</param>
/// <param name="After">The later build.</param>
/// <param name="Projects">Each project either build touched, by path, with what differs.</param>
public sealed record BuildComparison(string Before, string After, IReadOnlyList<ProjectComparison> Projects) {
    /// <summary>Files whose bytes differ or that only one build wrote.</summary>
    public int ChangedFiles => Projects.Sum(static p => p.Files.Count(static f => f.Change != BuildChanges.Same));

    /// <summary>Files both builds wrote byte for byte the same.</summary>
    public int SameFiles => Projects.Sum(static p => p.Files.Count(static f => f.Change == BuildChanges.Same));
}

/// <summary>What differs in one project between two builds.</summary>
/// <param name="Id">The project.</param>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its path below <c>projects/</c>.</param>
/// <param name="Touched">
///     Which builds touched it: both, before or after. A build that leaves a project alone writes nothing of it, so a
///     project only one build touched has its files listed as that build left them.
/// </param>
/// <param name="Outputs">Its outputs, with their keys in each build.</param>
/// <param name="Files">Its exported files, with their change.</param>
public sealed record ProjectComparison(
    Guid Id,
    string Name,
    string Path,
    string Touched,
    IReadOnlyList<OutputComparison> Outputs,
    IReadOnlyList<FileComparison> Files
);

/// <summary>One output in two builds.</summary>
/// <param name="Name">The output.</param>
/// <param name="Before">Its build key in the earlier build, or null when that build did not touch it.</param>
/// <param name="After">Its build key in the later build, or null.</param>
/// <param name="Change">One of <see cref="BuildChanges" />: the same key is the same result.</param>
public sealed record OutputComparison(string Name, string? Before, string? After, string Change);

/// <summary>One exported file in two builds.</summary>
/// <param name="Preset">The preset that wrote it.</param>
/// <param name="File">Where it is, below the export's folder, with forward slashes.</param>
/// <param name="Change">One of <see cref="BuildChanges" />.</param>
public sealed record FileComparison(string Preset, string File, string Change);
