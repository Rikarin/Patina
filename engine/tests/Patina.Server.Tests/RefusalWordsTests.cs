using Patina.Server.Api;
using System.Reflection;

namespace Patina.Server.Tests;

/// <summary>
///     Every refusal code the Server sends has the app's words, so a new code never reaches a person as the Server's
///     neutral sentence with an id in it (#198).
/// </summary>
public sealed class RefusalWordsTests {
    /// <summary>The one code whose detail is meant to show: a stack command no client is expected to explain.</summary>
    static readonly HashSet<string> ShownAsSent = [Refusals.StackRefused];

    static readonly string Messages = Path.Combine(
        Engine.Root,
        "..",
        "editor",
        "apps",
        "studio",
        "src",
        "app",
        "core",
        "messages.ts"
    );

    public static TheoryData<string> Codes() {
        TheoryData<string> codes = [];
        foreach (var field in typeof(Refusals).GetFields(BindingFlags.Public | BindingFlags.Static)) {
            if (field is { IsLiteral: true, FieldType.Name: nameof(String) }
                && field.GetRawConstantValue() is string code
                && !ShownAsSent.Contains(code)) {
                codes.Add(code);
            }
        }

        return codes;
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void The_app_has_words_for_each_refusal_code(string code) {
        var words = File.ReadAllText(Messages);

        Assert.True(
            words.Contains($"'{code}':", StringComparison.Ordinal),
            $"Refusals.cs sends '{code}', and editor/apps/studio/src/app/core/messages.ts has no words for it."
        );
    }

    /// <summary>
    ///     How many refusals still go out with no code, and so show the Server's own sentence. Each that gains a code
    ///     takes one off; none may be added, since a client can only say a refusal in its words by its code (#198).
    /// </summary>
    const int Uncoded = 42;

    /// <summary>
    ///     A new refusal goes out through <see cref="Refusals.Problem" />, with a code the app has words for.
    /// </summary>
    [Fact]
    public void No_new_refusal_goes_out_without_a_code() {
        var server = Path.Combine(Engine.Root, "src", "Patina.Server");
        var found = Directory.EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(static file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(static file => Path.GetFileName(file) != "Refusals.cs")
            .Sum(static file => File.ReadAllText(file).Split("TypedResults.Problem(").Length - 1);

        Assert.True(
            found <= Uncoded,
            $"{found} refusals go out with no code, more than the {Uncoded} there were: send a new one through "
            + "Refusals.Problem with a code, and give the code words in messages.ts."
        );
        Assert.True(
            found >= Uncoded,
            $"{found} refusals go out with no code, fewer than the {Uncoded} there were: lower Uncoded to {found}."
        );
    }
}
