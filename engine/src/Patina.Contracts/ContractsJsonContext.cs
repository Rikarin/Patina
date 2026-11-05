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
[JsonSerializable(typeof(LibraryInfo))]
[JsonSerializable(typeof(ProjectPage))]
[JsonSerializable(typeof(AssetPage))]
[JsonSerializable(typeof(AssetImpact))]
[JsonSerializable(typeof(ProjectCreateRequest))]
[JsonSerializable(typeof(ProjectSummary))]
[JsonSerializable(typeof(TagPage))]
[JsonSerializable(typeof(ViewportState))]
[JsonSerializable(typeof(ViewportMeshRequest))]
[JsonSerializable(typeof(ViewportCameraState))]
[JsonSerializable(typeof(ViewportInput))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
