using System.Text.Json.Serialization;

namespace Patina.Contracts;

/// <summary>What the Library's tree and a project's contents put on the wire.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(ProjectContents))]
[JsonSerializable(typeof(ProjectMeshRequest))]
[JsonSerializable(typeof(ProjectDocumentRequest))]
[JsonSerializable(typeof(ProjectDocumentMeshRequest), TypeInfoPropertyName = nameof(ProjectDocumentMeshRequest))]
[JsonSerializable(typeof(ProjectRenameRequest))]
[JsonSerializable(typeof(ProjectDuplicateRequest))]
[JsonSerializable(typeof(FolderList))]
[JsonSerializable(typeof(FolderSummary))]
[JsonSerializable(typeof(FolderCreateRequest), TypeInfoPropertyName = nameof(FolderCreateRequest))]
[JsonSerializable(typeof(FolderUpdateRequest))]
[JsonSerializable(typeof(LibraryMoveRequest))]
[JsonSerializable(typeof(LibraryMoved))]
[JsonSerializable(typeof(MaterialList), TypeInfoPropertyName = nameof(MaterialList))]
[JsonSerializable(typeof(MaterialUseRequest))]
[JsonSerializable(typeof(MaterialUse))]
[JsonSerializable(typeof(StackIdColours), TypeInfoPropertyName = nameof(StackIdColours))]
[JsonSerializable(typeof(ProjectPictureRequest))]
[JsonSerializable(typeof(ProjectPicture), TypeInfoPropertyName = nameof(ProjectPicture))]
[JsonSerializable(typeof(ProjectDocumentRoleRequest))]
[JsonSerializable(typeof(MaterialBrush), TypeInfoPropertyName = nameof(MaterialBrush))]
[JsonSerializable(typeof(StackThumbnails), TypeInfoPropertyName = nameof(StackThumbnails))]
[JsonSerializable(typeof(ProjectDocumentCopyRequest))]
[JsonSerializable(typeof(DocumentRecovery), TypeInfoPropertyName = nameof(DocumentRecovery))]
[JsonSerializable(typeof(DocumentRecoveryRequest))]
public sealed partial class LibraryTreeJsonContext : JsonSerializerContext;
