using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>Every type that crosses the API or lives in a file the Server writes.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
)]
[JsonSerializable(typeof(SystemInfo))]
[JsonSerializable(typeof(DiscoveryDocument))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
