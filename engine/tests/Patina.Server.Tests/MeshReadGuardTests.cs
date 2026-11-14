namespace Patina.Server.Tests;

/// <summary>
///     The Server never parses a mesh (plan §3.1, #285): a bad FBX may crash the native parser, and that must take down
///     the Worker, which can die at any time, never the Server, which holds every session.
/// </summary>
public sealed class MeshReadGuardTests {
    /// <summary>
    ///     Nothing the Server's code can reach, through Patina's own libraries, calls Vixen's model reader. What the
    ///     Server needs of a mesh it asks the Worker for, and keeps.
    /// </summary>
    [Fact]
    public void Nothing_the_Server_runs_reads_a_mesh() {
        var found = MethodReach.Forbidden(
            AppContext.BaseDirectory,
            typeof(Api.Refusals).Assembly.GetName().Name!,
            static (assembly, type) => assembly == "Vixen.Interchange" && type == "Vixen.Interchange.ModelReader"
        );

        Assert.True(found.Count == 0, "Ask the Worker instead:\n" + string.Join('\n', found));
    }
}
