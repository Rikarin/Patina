using System.Text.Json.Serialization;

namespace Patina.Ipc;

/// <summary>
///     Message types. Every message other than <see cref="Ok" /> and <see cref="Error" /> is a request, which gets one
///     of those two as its reply with the same <see cref="IpcMessage.Id" />. Either side may send requests.
/// </summary>
public static class IpcTypes {
    /// <summary>Worker to Server, first on a connection: who it is (<see cref="IpcMessage.Worker" />).</summary>
    public const string Hello = "hello";

    /// <summary>Server to Worker: answers with <see cref="IpcMessage.Health" />.</summary>
    public const string Health = "health";

    public const string Ok = "ok";
    public const string Error = "error";
}

/// <summary>One envelope for every message, so the protocol stays a single source-generated type.</summary>
public sealed class IpcMessage {
    /// <summary>The wire format's version; a hello from another version is refused.</summary>
    public const int ProtocolVersion = 1;

    public string Type { get; set; } = string.Empty;

    public long Id { get; set; }

    public string? Error { get; set; }

    /// <summary>hello: the Worker.</summary>
    public WorkerHello? Worker { get; set; }

    /// <summary>The reply to health.</summary>
    public WorkerHealth? Health { get; set; }

    public static IpcMessage Ok(long id) => new() { Type = IpcTypes.Ok, Id = id };

    public static IpcMessage Fail(long id, string error) => new() { Type = IpcTypes.Error, Id = id, Error = error };
}

/// <summary>The Worker's hello.</summary>
public sealed record WorkerHello(int Protocol, string Version, int ProcessId);

/// <summary>The Worker's health: enough for a client to tell that it is alive and what it costs.</summary>
public sealed record WorkerHealth(double UptimeSeconds, long WorkingSetBytes);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(IpcMessage))]
public sealed partial class IpcJsonContext : JsonSerializerContext;
