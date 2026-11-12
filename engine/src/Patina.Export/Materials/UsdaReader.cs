using Patina.Contracts;
using Patina.Export.Presets;
using System.Text;

namespace Patina.Export.Materials;

/// <summary>
///     Reads the materials of a USD layer in its text form (<c>.usda</c>) as maps (#157): each <c>Material</c> prim's
///     <c>UsdPreviewSurface</c>, whose inputs are a value or a connection to a <c>UsdUVTexture</c>'s output. It reads
///     what <see cref="UsdMaterialFile" /> writes and what other tools write of the same schema: a texture's
///     <c>scale</c> and <c>bias</c> say a lane holds one minus the map, a normal's green runs down or a factor
///     multiplies it. USD's binary forms (<c>.usdc</c>, <c>.usdz</c>) need USD's own library, which Patina does not
///     carry; a <c>.usd</c> file is read when it is text.
/// </summary>
public sealed class UsdaReader {
    readonly string folder;
    readonly Dictionary<string, Prim> prims = new(StringComparer.Ordinal);
    readonly List<string> warnings = [];

    UsdaReader(string path) {
        folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
    }

    /// <summary>A prim: its type and its properties by name, each value as the file spells it.</summary>
    sealed record Prim(string Path, string Type, string Name, Dictionary<string, string> Properties);

    /// <summary>The materials of a USD text layer, in its order.</summary>
    /// <exception cref="InvalidDataException">It is not a USD text layer, or holds no material.</exception>
    public static IReadOnlyList<ImportedMaterial> Read(string path) {
        var text = SharedFile.ReadAllText(path);
        if (!text.TrimStart().StartsWith("#usda", StringComparison.Ordinal)) {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' is not a USD text layer. A .usdc or .usdz needs USD's own library; "
                + "save it as .usda to bring its materials in."
            );
        }

        var reader = new UsdaReader(path);
        reader.Parse(text);
        var materials = reader.prims.Values.Where(static p => p.Type == "Material").ToList();
        if (materials.Count == 0) {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' has no Material prim.");
        }

        return [..materials.Select(reader.Material)];
    }

    ImportedMaterial Material(Prim material) {
        warnings.Clear();
        var surface = material.Properties.TryGetValue("outputs:surface.connect", out var target)
            ? Target(target)
            : prims.Values.FirstOrDefault(p => p.Path.StartsWith(material.Path + "/", StringComparison.Ordinal)
                && Id(p) == SurfaceInputs.Preview
            );
        if (surface is null || Id(surface) != SurfaceInputs.Preview) {
            warnings.Add($"USD: {material.Name} has no UsdPreviewSurface, so it is brought in empty.");
            return new(material.Name, [], [..warnings]);
        }

        List<ImportedSlot> slots = [];
        foreach (var (input, usage) in SurfaceInputs.Of(SurfaceInputs.Preview)) {
            if (Slot(usage, surface, $"inputs:{input}") is { } slot) {
                slots.Add(slot);
            }
        }

        return new(material.Name, slots, [..warnings]);
    }

    ImportedSlot? Slot(string usage, Prim surface, string input) {
        if (surface.Properties.TryGetValue($"{input}.connect", out var connection)) {
            return Connected(usage, connection);
        }

        if (surface.Properties.TryGetValue(input, out var value)) {
            var numbers = MaterialXReader.Numbers(value);
            return numbers.Count == 0 ? null : new(usage, null, -1, false, MaterialXReader.Four(numbers));
        }

        return null;
    }

    /// <summary>A surface input connected to a texture's output: rgb, or one of r, g and b.</summary>
    ImportedSlot? Connected(string usage, string connection) {
        var target = connection.Trim().Trim('<', '>');
        var at = target.LastIndexOf(".outputs:", StringComparison.Ordinal);
        if (at < 0 || !prims.TryGetValue(target[..at], out var texture)) {
            return Left(usage, $"it is connected to '{target}', which is not in this file");
        }

        if (Id(texture) != "UsdUVTexture") {
            return Left(usage, $"it is made by a {Id(texture) ?? "shader"}, which a graph would have to compute");
        }

        var lane = target[(at + ".outputs:".Length)..] switch {
            "rgb" => -1,
            "r" => 0,
            "g" => 1,
            "b" => 2,
            _ => 3
        };

        if (!texture.Properties.TryGetValue("inputs:file", out var asset) || Asset(asset) is not { Length: > 0 } uri) {
            return Left(usage, "its texture names no file");
        }

        var scale = Lanes(texture, "inputs:scale", 1f);
        var bias = Lanes(texture, "inputs:bias", 0f);
        var file = Path.GetFullPath(Path.Combine(folder, uri.Replace('/', Path.DirectorySeparatorChar)));
        if (usage == ExportMaps.Normal) {
            // A normal is read as scale × stored + bias into -1 to 1: (2, 2, 2) and (-1, -1, -1) when green runs up.
            return new(usage, file, -1, false, [1f, 1f, 1f, 1f]) { GreenUp = scale[1] >= 0 };
        }

        var read = lane < 0 ? 0 : lane;
        var inverted = scale[read] < 0 && bias[read] > 0;
        float[] factor = [..scale.Select(static s => Math.Abs(s))];
        return new(usage, file, lane, inverted, factor);
    }

    ImportedSlot? Left(string usage, string why) {
        warnings.Add($"USD: {usage} is left out, because {why}.");
        return null;
    }

    Prim? Target(string connection) => prims.GetValueOrDefault(Before(connection.Trim().Trim('<', '>'), ".outputs:"));

    static string Before(string text, string mark) {
        var at = text.LastIndexOf(mark, StringComparison.Ordinal);
        return at < 0 ? text : text[..at];
    }

    static string? Id(Prim prim) => prim.Properties.TryGetValue("info:id", out var id) ? id.Trim().Trim('"') : null;

    static string? Asset(string value) {
        var start = value.IndexOf('@', StringComparison.Ordinal);
        var end = value.LastIndexOf('@');
        return start >= 0 && end > start ? value[(start + 1)..end].Trim('@') : null;
    }

    static float[] Lanes(Prim prim, string name, float fallback) {
        var numbers = prim.Properties.TryGetValue(name, out var text) ? MaterialXReader.Numbers(text) : [];
        return [..Enumerable.Range(0, 4).Select(at => at < numbers.Count ? numbers[at] : fallback)];
    }

    /// <summary>
    ///     Walks the layer's prims, keeping each one's type and the right-hand side of each property. Metadata in
    ///     brackets, comments and dictionaries are passed over: a material's surface needs none of them.
    /// </summary>
    void Parse(string text) {
        var tokens = new Tokens(text);
        Stack<string> path = [];
        while (tokens.Next() is { } token) {
            switch (token) {
                case "def" or "over" or "class":
                    Define(tokens, path);
                    break;
                case "}":
                    path.TryPop(out _);
                    break;
                case "(" or "[":
                    tokens.SkipGroup(token);
                    break;
                default:
                    if (path.Count > 0 && tokens.Peek() == "=") {
                        tokens.Next();
                        prims[PathOf(path)].Properties[token] = tokens.Value();
                    }

                    break;
            }
        }
    }

    /// <summary>A prim's head, after <c>def</c>: its type if any, its name, its metadata, then its body.</summary>
    void Define(Tokens tokens, Stack<string> path) {
        var type = tokens.Peek() is { } next && !next.StartsWith('"') ? tokens.Next()! : string.Empty;
        var name = (tokens.Next() ?? "\"\"").Trim('"');
        if (tokens.Peek() == "(") {
            tokens.SkipGroup();
        }

        if (tokens.Next() != "{") {
            throw new InvalidDataException($"A prim '{name}' has no body.");
        }

        path.Push(name);
        var full = PathOf(path);
        prims[full] = new(full, type, name, new(StringComparer.Ordinal));
    }

    static string PathOf(Stack<string> path) => "/" + string.Join('/', path.Reverse());

    /// <summary>A USD text layer as words, strings, assets, paths and brackets.</summary>
    sealed class Tokens(string text) {
        const string Stops = """()[]{}=,"@<#""";
        int at;
        string? peeked;

        public string? Peek() => peeked ??= Read();

        public string? Next() {
            var token = Peek();
            peeked = null;
            return token;
        }

        /// <summary>Passes a bracketed group and everything in it, from its opening bracket or after it.</summary>
        public void SkipGroup(string? opened = null) {
            var open = opened ?? Next();
            var close = open switch {
                "(" => ")",
                "[" => "]",
                "{" => "}",
                _ => null
            };
            if (close is null) {
                return;
            }

            for (var depth = 1; depth > 0 && Next() is { } token;) {
                depth += Depth(token, open!, close);
            }
        }

        /// <summary>A value: one token, or a bracketed tuple or list as its text.</summary>
        public string Value() {
            var first = Next() ?? string.Empty;
            if (first == "{") {
                // A dictionary or a variant set's value: nothing a surface reads.
                SkipGroup(first);
                return string.Empty;
            }

            if (first is not ("(" or "[")) {
                return first;
            }

            var close = first == "(" ? ")" : "]";
            var built = new StringBuilder(first);
            for (var depth = 1; depth > 0 && Next() is { } token;) {
                depth += Depth(token, first, close);
                built.Append(token == "," ? ", " : token);
            }

            return built.ToString();
        }

        /// <summary>How a token moves the depth of brackets: one in, one out, or neither.</summary>
        static int Depth(string token, string open, string close) {
            if (token == open) {
                return 1;
            }

            return token == close ? -1 : 0;
        }

        string? Read() {
            SkipBlank();
            if (at >= text.Length) {
                return null;
            }

            var start = at;
            var first = text[at];
            switch (first) {
                case '"':
                    at = Quoted(at);
                    break;
                case '@':
                    at = After(text.IndexOf('@', at + 1));
                    break;
                case '<':
                    at = After(text.IndexOf('>', at + 1));
                    break;
                case '(' or ')' or '[' or ']' or '{' or '}' or '=' or ',':
                    at++;
                    break;
                default:
                    while (at < text.Length && !char.IsWhiteSpace(text[at]) && !Stops.Contains(text[at])) {
                        at++;
                    }

                    break;
            }

            return text[start..at];
        }

        /// <summary>Passes white space and comments, which run to the end of their line.</summary>
        void SkipBlank() {
            while (at < text.Length && (char.IsWhiteSpace(text[at]) || text[at] == '#')) {
                if (text[at] == '#') {
                    var line = text.IndexOf('\n', at);
                    at = line < 0 ? text.Length : line;
                } else {
                    at++;
                }
            }
        }

        /// <summary>Where a string that starts here ends: one quote, or three for a string of several lines.</summary>
        int Quoted(int from) {
            if (text.AsSpan(from).StartsWith("\"\"\"")) {
                var end = text.IndexOf("\"\"\"", from + 3, StringComparison.Ordinal);
                return end < 0 ? text.Length : end + 3;
            }

            for (var i = from + 1; i < text.Length; i++) {
                if (text[i] == '\\') {
                    i++;
                } else if (text[i] == '"') {
                    return i + 1;
                }
            }

            return text.Length;
        }

        /// <summary>Just past a closing mark, or the end of the text when there is none.</summary>
        int After(int mark) => mark < 0 ? text.Length : mark + 1;
    }
}
