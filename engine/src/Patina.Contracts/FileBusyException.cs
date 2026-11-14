namespace Patina.Contracts;

/// <summary>
///     A file or folder that could not be replaced or moved because another program still had it open once
///     <see cref="SharedFile.Patience" /> had passed. Trying again later usually succeeds.
/// </summary>
public sealed class FileBusyException : IOException {
    public FileBusyException() { }

    public FileBusyException(string message) : base(message) { }

    public FileBusyException(string message, Exception inner) : base(message, inner) { }

    /// <summary>A busy file or folder, named by its path, with the last refusal of the move.</summary>
    /// <param name="message">What is busy, as a person reads it.</param>
    /// <param name="path">The file or folder that is in use.</param>
    /// <param name="inner">The last refusal of the move.</param>
    public FileBusyException(string message, string path, Exception inner) : base(message, inner) {
        Path = path;
    }

    /// <summary>The refusal of a move of <paramref name="path" /> that its holder never let go of.</summary>
    /// <param name="path">The file or folder that is in use.</param>
    /// <param name="inner">The last refusal of the move.</param>
    public static FileBusyException Of(string path, Exception inner) =>
        new(
            $"'{System.IO.Path.GetFileName(path)}' is in use by another program, so it could not be changed. "
            + "Try again.",
            path,
            inner
        );

    /// <summary>The file or folder that is in use, when it is known.</summary>
    public string? Path { get; }
}
