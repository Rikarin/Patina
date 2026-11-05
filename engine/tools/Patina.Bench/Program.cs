// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Bench;
using System.Globalization;
using Vixen.TextureGraph;

// patina-bench: scale tooling. Not shipped.
//   generate <path> <projects> [seed]   writes a library
//   s6 <path> [projects] [seed]         generates into an empty folder if needed, then measures spike S6
//   cold-open <path>                    one cold open, as its own process; s6 starts it
//   graph <library> <nodes> <name>      adds a real texture graph to a library, for the editor and spike S7
switch (args) {
    case ["generate", var path, var projects, .. var rest]:
        var generator = new GeneratorOptions(
            int.Parse(projects, CultureInfo.InvariantCulture),
            rest is [var seed] ? int.Parse(seed, CultureInfo.InvariantCulture) : 42
        );
        LibraryGenerator.Generate(Path.GetFullPath(path), generator);
        return 0;
    case ["s6", var path, .. var rest]:
        return S6.Run(
            Path.GetFullPath(path),
            new(
                rest is [var count, ..] ? int.Parse(count, CultureInfo.InvariantCulture) : 10_000,
                rest is [_, var chosen] ? int.Parse(chosen, CultureInfo.InvariantCulture) : 42
            )
        );
    case ["cold-open", var path]:
        S6.ColdOpen(Path.GetFullPath(path));
        return 0;
    case ["kernels", var folder, .. var options]:
        KernelBench.Salt = Option(options, "--salt=") is { } salt
            ? int.Parse(salt, CultureInfo.InvariantCulture)
            : null;
        return KernelBench.Run(
            folder,
            Enum.Parse<TextureFormat>(Option(options, "--format=") ?? nameof(TextureFormat.Rgba8)),
            options.Contains("--parallel"),
            options.Contains("--pipelines"),
            Option(options, "--pipeline-cache="),
            options.Contains("--pipeline-threads")
        );
    case ["mesh", var path]:
        return MeshFixture.Write(path);
    case ["graph", var library, var nodes, var name]:
        return GraphFixture.Write(library, int.Parse(nodes, CultureInfo.InvariantCulture), name);
    case ["render", var mesh, var png, var width, var height]:
        return RenderBench.Run(
            mesh,
            png,
            int.Parse(width, CultureInfo.InvariantCulture),
            int.Parse(height, CultureInfo.InvariantCulture)
        );
    default:
        await Console.Error.WriteLineAsync(
            """
            Usage: patina-bench kernels <cache folder> [--parallel] [--pipelines] [--pipeline-threads]
                       [--pipeline-cache=<file>] [--format=<format>] [--salt=<n>]
                   patina-bench mesh <file.glb>
                   patina-bench graph <library> <nodes> <name>
                   patina-bench render <mesh> <out.png> <width> <height>
                   patina-bench generate <path> <projects> [seed]
                   patina-bench s6 <path> [projects] [seed]
            """
        );
        return 2;
}

static string? Option(string[] options, string prefix) =>
    options.FirstOrDefault(o => o.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
