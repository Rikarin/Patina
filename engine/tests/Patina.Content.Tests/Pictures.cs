using Vixen.Core.Imaging;

namespace Patina.Content.Tests;

/// <summary>
///     The pictures the content tests draw, kept for a person to look at (#169, #273): <c>PATINA_CONTENT_PICTURES</c>
///     names a folder, and each compound drawn is written there as a PNG under its path, the slash a space. Without
///     the variable nothing is written; the oracles are the tests', and this is the picture they are not.
/// </summary>
static class Pictures {
    /// <summary>The variable that names the folder.</summary>
    public const string Variable = "PATINA_CONTENT_PICTURES";

    /// <summary>Writes one picture, when a folder is named.</summary>
    /// <param name="path">The compound's path, such as <c>Grunges/Grunge 01</c>.</param>
    /// <param name="picture">What it drew.</param>
    /// <param name="suffix">What tells this picture from another of the same compound, if anything.</param>
    public static void Keep(string path, in Bitmap picture, string suffix = "") {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 } folder) {
            return;
        }

        Directory.CreateDirectory(folder);
        PngCodec.Save(Path.Combine(folder, $"{path.Replace('/', ' ')}{suffix}.png"), picture);
    }
}
