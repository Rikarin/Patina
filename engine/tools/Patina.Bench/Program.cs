// SPDX-License-Identifier: GPL-3.0-or-later

using Patina.Bench;
using System.Globalization;
using Vixen.TextureGraph;

switch (args) {
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
            Usage: patina-bench kernels <cache folder> [--parallel] [--pipelines] [--pipeline-threads] [--pipeline-cache=<file>] [--format=<format>] [--salt=<n>]
                   patina-bench mesh <file.glb>
                   patina-bench render <mesh> <out.png> <width> <height>
            """
        );
        return 2;
}

static string? Option(string[] options, string prefix) =>
    options.FirstOrDefault(o => o.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
