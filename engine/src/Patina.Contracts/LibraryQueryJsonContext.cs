using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>What a document's snapshots (#209), the samples (#190) and the library's costs (#207) send.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default
)]
[JsonSerializable(typeof(DocumentSnapshot))]
[JsonSerializable(typeof(DocumentSnapshotList))]
[JsonSerializable(typeof(SnapshotTakeRequest))]
[JsonSerializable(typeof(SnapshotRequest), TypeInfoPropertyName = nameof(SnapshotRequest))]
[JsonSerializable(typeof(SampleList))]
[JsonSerializable(typeof(SampleRequest))]
[JsonSerializable(typeof(LibraryCosts))]
[JsonSerializable(typeof(DocumentCostPage), TypeInfoPropertyName = nameof(DocumentCostPage))]
public sealed partial class LibraryQueryJsonContext : JsonSerializerContext;
