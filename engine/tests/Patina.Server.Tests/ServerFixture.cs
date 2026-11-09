using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Patina.Server.Tests;

/// <summary>An in-memory Server with its own socket and library folder.</summary>
public sealed class ServerFixture : WebApplicationFactory<Program> {
    public const string AccessToken = "test-token";

    readonly DirectoryInfo scratch = Directory.CreateTempSubdirectory("patina-server-");

    public string Library => Directory.CreateDirectory(Path.Combine(scratch.FullName, "Game.ptlib")).FullName;

    public string SocketPath { get; } = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sock");

    /// <summary>The preferences file this Server keeps, in its own folder.</summary>
    public string PreferencesFile => Path.Combine(scratch.FullName, "preferences.json");

    /// <summary>A client that presents the token.</summary>
    public HttpClient AuthorisedClient() {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseSetting("library", Library);
        builder.UseSetting("Patina:Token", AccessToken);
        builder.UseSetting("Patina:Worker:SocketPath", SocketPath);
        // Never the real per-user file: the test server has an address too, so the Server would list itself there.
        builder.UseSetting("Patina:DiscoveryFile", Path.Combine(scratch.FullName, "servers.json"));
        // Nor the person's own preferences, which a test that sets them would write over.
        builder.UseSetting("Patina:PreferencesFile", PreferencesFile);
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        // WebApplicationFactory comes through here twice: from Dispose and from DisposeAsync.
        scratch.Refresh();
        if (scratch.Exists) {
            try {
                scratch.Delete(true);
            } catch (IOException) {
                // The Server's last write can land while the folder is going, and a temp folder left
                // behind is not a test failure.
            }
        }
    }
}
