using System.Text.Json.Nodes;

namespace Patina.Server.Tests;

public sealed class OpenApiTests : IDisposable {
    /// <summary>Committed OpenAPI document; the TypeScript and Python clients are generated from it.</summary>
    static readonly string OpenApiPath = Path.Combine(Engine.Root, "src", "Patina.Server", "openapi.json");

    readonly ServerFixture server = new();

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => server.Dispose();

    [Fact]
    public async Task OpenApi_document_matches_the_committed_copy() {
        using var client = server.AuthorisedClient();
        var current = Normalise(await client.GetStringAsync("/openapi/v1.json", Token));

        if (Environment.GetEnvironmentVariable("PATINA_UPDATE_OPENAPI") == "1" || !File.Exists(OpenApiPath)) {
            await File.WriteAllTextAsync(OpenApiPath, current, Token);
        }

        var committed = Normalise(await File.ReadAllTextAsync(OpenApiPath, Token));
        Assert.True(
            committed == current,
            "The API changed. Review the difference, then update the committed document with "
            + "PATINA_UPDATE_OPENAPI=1 dotnet test."
            + FirstDifference(committed, current)
        );
        Assert.Contains("\"/api/v1/system\"", current, StringComparison.Ordinal);
    }

    static string FirstDifference(string committed, string current) {
        var a = committed.Split('\n');
        var b = current.Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++) {
            var left = i < a.Length ? a[i] : "(end)";
            var right = i < b.Length ? b[i] : "(end)";
            if (left != right) {
                return $"\nFirst difference at line {i + 1}:\n  committed: {left}\n  current:   {right}";
            }
        }

        return string.Empty;
    }

    /// <summary>
    ///     Canonical form; XML doc text carries the platform's line endings into descriptions, so unify them.
    /// </summary>
    static string Normalise(string json) =>
        JsonNode.Parse(json)!.ToJsonString(new() { WriteIndented = true })
            .Replace("""\r\n""", """\n""", StringComparison.Ordinal)
        + "\n";
}
