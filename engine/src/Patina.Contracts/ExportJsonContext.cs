using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>What the preset editor and the compression preview put on the wire (ADR 0019).</summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(ExportPresetSource))]
[JsonSerializable(typeof(ExportPresetWrite))]
[JsonSerializable(typeof(ExportPresetCheckRequest))]
[JsonSerializable(typeof(ExportPresetCheck))]
[JsonSerializable(typeof(ExportPresetImport))]
[JsonSerializable(typeof(CompressionPreviewRequest))]
[JsonSerializable(typeof(CompressionPreviewInfo))]
public sealed partial class ExportJsonContext : JsonSerializerContext;
