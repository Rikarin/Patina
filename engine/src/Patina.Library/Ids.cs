using System.Globalization;

namespace Patina.Library;

/// <summary>A GUID identity. References use GUIDs, not paths, so moving files breaks nothing (plan §5).</summary>
public interface IGuidId<out TSelf> where TSelf : IGuidId<TSelf> {
    Guid Value { get; }

    static abstract TSelf From(Guid value);
}

/// <summary>A library's identity.</summary>
public readonly record struct LibraryId(Guid Value) : IGuidId<LibraryId> {
    public static LibraryId From(Guid value) => new(value);

    public static LibraryId New() => new(Guid.CreateVersion7());

    public override string ToString() => GuidId.Format(this);
}

/// <summary>A project's identity inside a library.</summary>
public readonly record struct ProjectId(Guid Value) : IGuidId<ProjectId> {
    public static ProjectId From(Guid value) => new(value);

    public static ProjectId New() => new(Guid.CreateVersion7());

    public override string ToString() => GuidId.Format(this);
}

/// <summary>The one text form of an identity in Patina's files: lower case, with dashes.</summary>
public static class GuidId {
    public static string Format<T>(T id) where T : IGuidId<T> => id.Value.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>Reads an identity; the empty GUID is not one.</summary>
    public static T Parse<T>(string text) where T : IGuidId<T> =>
        TryParse(text, out T? id) ? id! : throw new FormatException($"'{text}' is not an identity.");

    /// <summary>Reads an identity; the empty GUID is not one.</summary>
    public static bool TryParse<T>(string? text, out T? id) where T : IGuidId<T> {
        var parsed = Guid.TryParse(text, CultureInfo.InvariantCulture, out var value) && value != Guid.Empty;
        id = parsed ? T.From(value) : default;
        return parsed;
    }
}
