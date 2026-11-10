using Patina.Contracts;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Patina.Server.Tests;

/// <summary>
///     <c>patina</c> as a build script drives it, with no Server (#186): publishing a graph as a package, saying what
///     one takes and makes, and baking it with its knobs set from the command line.
/// </summary>
public sealed class CliPackageProcessTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-cli-package-");

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    /// <summary>A compound of its own folder's with a knob on the blend after it: enough for a package to carry.</summary>
    void WriteGraph() {
        Directory.CreateDirectory(Path.Combine(folder.FullName, "work", "Compounds"));
        File.WriteAllText(
            Path.Combine(folder.FullName, "work", "Banded.vxtexgraph"),
            """
            name: Banded
            nodes:
              - { id: 1, type: Source/Checker, values: { 'Scale X': [4], 'Scale Y': [4] } }
              - { id: 2, type: Soften }
              - { id: 3, type: Source/Uniform, values: { Colour: [1, 0, 0, 1] } }
              - { id: 4, type: Colour/Blend, texts: { '=Opacity': 'redness' } }
              - { id: 5, type: Output/Output, texts: { Usage: baseColor } }
            edges:
              - { fromNode: 1, fromPort: Out, toNode: 2, toPort: In }
              - { fromNode: 2, fromPort: Out, toNode: 4, toPort: Background }
              - { fromNode: 3, fromPort: Out, toNode: 4, toPort: Foreground }
              - { fromNode: 4, fromPort: Out, toNode: 5, toPort: Input }
            settings: { baseWidth: '64', baseHeight: '64', seed: '1' }
            parameters:
              - { name: redness, default: '0.25', kind: Float, minimum: 0, maximum: 1 }
            presets:
              - { name: Red, values: { redness: '1' } }
            description: Checks that soften.
            """
        );
        File.WriteAllText(
            Path.Combine(folder.FullName, "work", "Compounds", "Soften.vxtexgraph"),
            """
            name: Soften
            nodes:
              - { id: 1, type: Sub-graph/Input }
              - { id: 2, type: Filters/Blur, values: { Radius: [2] } }
              - { id: 3, type: Sub-graph/Output }
            edges:
              - { fromNode: 1, fromPort: In, toNode: 2, toPort: Input }
              - { fromNode: 2, fromPort: Out, toNode: 3, toPort: Out }
            interface:
              - { name: In, direction: Input, kind: Image }
              - { name: Out, direction: Output, kind: Image }
            """
        );
    }

    async Task<(int Exit, string Out, string Error)> PatinaAsync(params string[] arguments) {
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Engine.Dll("Patina.Cli", "patina"));
        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        using var cli = Process.Start(info)!;
        var error = cli.StandardError.ReadToEndAsync(Token);
        var output = await cli.StandardOutput.ReadToEndAsync(Token);
        await cli.WaitForExitAsync(Token);
        return (cli.ExitCode, output, await error);
    }

    [Fact]
    public async Task A_package_says_what_it_takes_and_bakes_where_it_is_put_with_its_knobs_set() {
        WriteGraph();
        var packages = Path.Combine(folder.FullName, "packages");
        var published = await PatinaAsync("package", Path.Combine(folder.FullName, "work", "Banded.vxtexgraph"), packages);
        Assert.True(published.Exit == 0, published.Error);

        // Moved away from the folder it came from, it still has its compound: the package carries it.
        var package = Path.Combine(folder.FullName, "elsewhere", "Banded.ptpkg");
        Directory.CreateDirectory(Path.GetDirectoryName(package)!);
        Directory.Move(Path.Combine(packages, "Banded.ptpkg"), package);
        Directory.Delete(Path.Combine(folder.FullName, "work"), true);

        var info = await PatinaAsync("graph", "info", package);
        Assert.True(info.Exit == 0, info.Error);
        var facts = JsonSerializer.Deserialize(info.Out, LibraryTreeJsonContext.Default.GraphFacts)!;
        Assert.Equal(("Banded", "Checks that soften.", "redness"), (facts.Name, facts.Info.Description, Assert.Single(facts.Parameters).Name));
        Assert.Equal(["baseColor"], facts.Outputs);
        Assert.Equal("Red", Assert.Single(facts.Presets).Name);
        Assert.Equal(["Packages/Banded/Soften"], facts.Package!.Compounds.Select(static c => c.Type));

        var refused = await PatinaAsync("bake", package, Path.Combine(folder.FullName, "bad"), "--param", "redness=2");
        Assert.Equal(2, refused.Exit);
        Assert.Contains("not a value for redness", refused.Error, StringComparison.Ordinal);
        Assert.Equal(2, (await PatinaAsync("bake", package, Path.Combine(folder.FullName, "bad"), "--preset", "Blue")).Exit);

        var help = await PatinaAsync("bake", "--help");
        Assert.Equal(0, help.Exit);
        Assert.Contains("--param <name>=<value>", help.Out, StringComparison.Ordinal);

        async Task<string> BakedAsync(string name, params string[] options) {
            var baked = await PatinaAsync(["bake", package, Path.Combine(folder.FullName, name), ..options]);
            if (baked.Exit != 0 && baked.Error.Contains("no vulkan", StringComparison.OrdinalIgnoreCase)) {
                Gpu.Skip(baked.Error);
            }

            Assert.True(baked.Exit == 0, baked.Error);
            return Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(folder.FullName, name, "baseColor.png"), Token)));
        }

        var plain = await BakedAsync("plain");
        var red = await BakedAsync("red", "--preset", "Red");
        Assert.NotEqual(plain, red);
        Assert.Equal(red, await BakedAsync("set", "--param", "redness=1"));
        Assert.Equal(plain, await BakedAsync("again", "--param", "redness=0.25"));
    }
}
