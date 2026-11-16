using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>
///     What a document's snapshots (#209), the samples (#190), the library's costs (#207) and material presets (#124)
///     send.
/// </summary>
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
[JsonSerializable(typeof(MaterialPreset), TypeInfoPropertyName = nameof(MaterialPreset))]
[JsonSerializable(typeof(MaterialPresetCreateRequest), TypeInfoPropertyName = nameof(MaterialPresetCreateRequest))]
public sealed partial class LibraryQueryJsonContext : JsonSerializerContext;
