namespace Patina.Server.Tests;

/// <summary>Finds things in the repository from a test's output folder.</summary>
static class Engine {
    /// <summary>The engine folder.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>The configuration these tests were built in, which the executables under test share.</summary>
    public static string Configuration { get; } = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    /// <summary>The built entry assembly of a project under <c>src</c>.</summary>
    public static string Dll(string project, string assembly) =>
        Path.Combine(Root, "src", project, "bin", Configuration, "net11.0", $"{assembly}.dll");

    static string FindRoot() {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) {
            if (File.Exists(Path.Combine(dir.FullName, "Patina.slnx"))) {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the engine folder.");
    }
}
