namespace Patina.Server.Tests;

/// <summary>
///     The Server never parses a mesh or decodes a picture (plan §3.1, #285, #293): a bad FBX may crash the native
///     parser, and a bad PSD the native decoder, and that must take down the Worker, which can die at any time, never
///     the Server, which holds every session.
/// </summary>
public sealed class MeshReadGuardTests {
    /// <summary>The picture decoders Vixen has: every member of a reader, and the decoding half of a codec.</summary>
    static readonly string[] Readers = [
        "Vixen.Interchange.TiffReader", "Vixen.Interchange.WebPReader", "Vixen.Interchange.StbImageDecoder",
        "Vixen.Interchange.ImageDecoders", "Vixen.Interchange.IImageDecoder"
    ];

    static readonly string[] Codecs = ["Vixen.Core.Imaging.ExrCodec", "Vixen.Core.Imaging.PngCodec"];

    /// <summary>
    ///     Nothing the Server's code can reach, through Patina's own libraries, calls Vixen's model reader. What the
    ///     Server needs of a mesh it asks the Worker for, and keeps.
    /// </summary>
    [Fact]
    public void Nothing_the_Server_runs_reads_a_mesh() {
        var found = MethodReach.Forbidden(
            AppContext.BaseDirectory,
            typeof(Api.Refusals).Assembly.GetName().Name!,
            static (assembly, type, _) => assembly == "Vixen.Interchange" && type == "Vixen.Interchange.ModelReader"
        );

        Assert.True(found.Count == 0, "Ask the Worker instead:\n" + string.Join('\n', found));
    }

    /// <summary>
    ///     Nothing the Server's code can reach decodes a picture (#293): a library picture's preview and check, a
    ///     bake's ID colours and a compression preview are the Worker's. Encoding is allowed: the Server writes PNGs of
    ///     numbers it holds, which parses nobody's file.
    /// </summary>
    [Fact]
    public void Nothing_the_Server_runs_decodes_a_picture() {
        var found = MethodReach.Forbidden(
            AppContext.BaseDirectory,
            typeof(Api.Refusals).Assembly.GetName().Name!,
            static (_, type, member) => Readers.Contains(type, StringComparer.Ordinal)
                || (Codecs.Contains(type, StringComparer.Ordinal)
                    && (member.StartsWith("Decode", StringComparison.Ordinal) || member == "Load"))
        );

        Assert.True(found.Count == 0, "Ask the Worker instead:\n" + string.Join('\n', found));
    }
}
