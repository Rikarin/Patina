using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Library;
using System.Text.Json;

namespace Patina.Server;

/// <summary>
///     The person's settings that the Server acts on, whatever library it has open: <c>preferences.json</c> beside the
///     discovery file, plain JSON that anyone can read and edit. A client changes them through the API, so Blender and
///     the editor see the same ones. A setting given on the command line or in the environment wins for that run,
///     which is how a test runs autosave every second without touching the person's file.
/// </summary>
public sealed class Preferences {
    /// <summary>The most an interval or an idle wait can be: a day.</summary>
    public const int MostSeconds = 86_400;

    /// <summary>The most autosaves of one document kept.</summary>
    public const int MostKept = 20;

    readonly string file;
    readonly int? autosaveOverride;
    readonly Lock gate = new();
    ServerPreferences current;

    public Preferences(IOptions<ServerOptions> options, ILogger<Preferences> logger) {
        file = options.Value.PreferencesFile ?? DefaultPath;
        autosaveOverride = options.Value.AutosaveSeconds;
        current = Read(file, logger);
    }

    /// <summary>Where the file is when nothing says otherwise: the per-user folder the discovery file is in.</summary>
    public static string DefaultPath => Path.Combine(Path.GetDirectoryName(InstanceRegistry.DefaultPath)!, "preferences.json");

    /// <summary>The settings as the person keeps them.</summary>
    public ServerPreferences Current {
        get {
            lock (gate) {
                return current;
            }
        }
    }

    /// <summary>How autosave runs now: the person's settings, under any given for this run.</summary>
    public AutosavePreferences Autosave {
        get {
            var kept = Current.Autosave;
            return autosaveOverride is { } seconds ? kept with { IntervalSeconds = seconds } : kept;
        }
    }

    /// <summary>Changes the settings and keeps them in the file.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A setting is out of range.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    public ServerPreferences Set(ServerPreferences preferences) {
        ArgumentNullException.ThrowIfNull(preferences);
        Check(preferences.Autosave);
        lock (gate) {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            LibraryFiles.Replace(
                file,
                JsonSerializer.SerializeToUtf8Bytes(preferences, LibraryTreeJsonContext.Default.ServerPreferences)
            );
            current = preferences;
            return current;
        }
    }

    static void Check(AutosavePreferences autosave) {
        ArgumentNullException.ThrowIfNull(autosave);
        ArgumentOutOfRangeException.ThrowIfNegative(autosave.IntervalSeconds, "intervalSeconds");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(autosave.IntervalSeconds, MostSeconds, "intervalSeconds");
        ArgumentOutOfRangeException.ThrowIfNegative(autosave.IdleSeconds, "idleSeconds");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(autosave.IdleSeconds, MostSeconds, "idleSeconds");
        ArgumentOutOfRangeException.ThrowIfLessThan(autosave.Keep, 1, "keep");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(autosave.Keep, MostKept, "keep");
    }

    /// <summary>The file, or the defaults when there is none or it cannot be used, which the log says.</summary>
    static ServerPreferences Read(string file, ILogger logger) {
        ServerPreferences defaults = new(new());
        if (!File.Exists(file)) {
            return defaults;
        }

        try {
            var read = JsonSerializer.Deserialize(File.ReadAllBytes(file), LibraryTreeJsonContext.Default.ServerPreferences);
            if (read?.Autosave is null) {
                return defaults;
            }

            Check(read.Autosave);
            return read;
        } catch (Exception e) when (e is JsonException or IOException or ArgumentException) {
            ServerLog.PreferencesUnreadable(logger, file, e.Message);
            return defaults;
        }
    }
}
