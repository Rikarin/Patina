using Patina.Contracts;
using System.Globalization;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace Patina.Library;

/// <summary>
///     Reads and writes the library's documents. Writing is canonical: keys in schema order with the version first,
///     lists sorted by what identifies their items. So two people adding a reference touch their own lines, and
///     saving an unchanged document changes no byte. Lists are always written, empty or not, so a document has one
///     shape.
/// </summary>
public static class DocumentCodec {
    const string IdKey = "id";
    const string NameKey = "name";
    const string TagsKey = "tags";
    const string MeshKey = "mesh";
    const string RoleKey = "role";
    const string MaterialKey = "material";
    const string MeshesKey = "meshes";
    const string AutoUnwrapKey = "autoUnwrap";
    const string SourceKey = "source";
    const string HashKey = "hash";
    const string PreservePaintKey = "preservePaint";
    const string UnwrapKey = "unwrap";
    const string BakeKey = "bake";
    const string ViewportKey = "viewport";
    const string UnwrappedKey = "unwrapped";
    const string ExportKey = "export";
    const string DocumentsKey = "documents";
    const string FileKey = "file";
    const string HighKey = "high";
    const string ColorKey = "color";
    const string ToolKey = "tool";
    const string FilesKey = "files";
    const string UsesKey = "uses";
    const string SettingsKey = "settings";
    const string KindKey = "kind";
    const string ReferencesKey = "references";
    const string DependenciesKey = "dependencies";
    const string OutputsKey = "outputs";
    const string AssetKey = "asset";
    const string ModeKey = "mode";
    const string RevisionKey = "revision";
    const string Floating = "floating";
    const string Pinned = "pinned";

    public static string Write(LibraryDocument document) =>
        YamlText.Write(
            new(
                Pair(SchemaChain.VersionKey, YamlText.Plain(SchemaChain.Library.Current)),
                Pair(IdKey, YamlText.Plain(document.Id.ToString())),
                Pair(NameKey, YamlText.Text(document.Name)),
                Pair("texelDensity", YamlText.Plain(document.TexelDensity)),
                Pair("channels", YamlText.Texts(document.Channels)),
                Pair("defaultShader", YamlText.Text(document.DefaultShader)),
                Pair("exportTargets", YamlText.Texts(document.ExportTargets))
            )
        );

    public static string Write(ProjectDocument document) =>
        YamlText.Write(
            new(
                [
                    Pair(SchemaChain.VersionKey, YamlText.Plain(SchemaChain.Project.Current)),
                    Pair(IdKey, YamlText.Plain(document.Id.ToString())),
                    Pair(NameKey, YamlText.Text(document.Name)),
                    Pair(TagsKey, SortedTexts(document.Tags)),
                    ..Listed(MeshesKey, document.Meshes.Select(static m => Mesh(m))),
                    ..Listed(DocumentsKey, document.Documents.Select(static d => Item(d))),
                    ..document.AutoUnwrap ? [] : new[] { Pair(AutoUnwrapKey, YamlText.Plain("false")) },
                    ..document.PreservePaint ? [] : new[] { Pair(PreservePaintKey, YamlText.Plain("false")) },
                    ..Named(UnwrapKey, document.Unwrap != new UnwrapOptions(), Unwrap(document.Unwrap)),
                    ..Named(ExportKey, document.Export is not null, ExportOf(document.Export)),
                    ..Bake(document.Bake),
                    ..Named(ViewportKey, document.Viewport is not null, Viewport(document.Viewport)),
                    Pair(ReferencesKey, References(document.References)),
                    Pair(
                        OutputsKey,
                        new YamlSequenceNode(
                            document.Outputs.OrderBy(static o => o.Name, StringComparer.Ordinal).Select(Output)
                        )
                    )
                ]
            )
        );

    static YamlMappingNode ExportOf(ProjectExportSettings? settings) =>
        settings is null ? new YamlMappingNode() : ExportCodec.Write(settings);

    static KeyValuePair<YamlNode, YamlNode>[] Named(string key, bool present, YamlNode value) =>
        present ? [Pair(key, value)] : [];

    static KeyValuePair<YamlNode, YamlNode>[] Present(string key, string? text) =>
        text is null ? [] : [Pair(key, YamlText.Text(text))];

    static KeyValuePair<YamlNode, YamlNode>[] Listed(string key, IEnumerable<YamlMappingNode> nodes) {
        var list = new YamlSequenceNode(nodes);
        return list.Children.Count == 0 ? [] : [Pair(key, list)];
    }

    static YamlMappingNode Mesh(ProjectMesh mesh) =>
        new(
            [
                Pair(IdKey, YamlText.Plain(mesh.Id.ToString())),
                Pair(FileKey, YamlText.Text(mesh.File)),
                ..Present(HighKey, mesh.High),
                ..Present(SourceKey, mesh.Source),
                ..Present(HashKey, mesh.Hash),
                ..Named(UnwrappedKey, mesh.Unwrapped is not null, Unwrapped(mesh.Unwrapped))
            ]
        );

    /// <summary>The project's bake settings, whole, when it has any.</summary>
    /// <remarks>
    ///     Written whole rather than as what differs from the defaults, so a default that moves in a later version
    ///     does not move a project's bakes with it.
    /// </remarks>
    static KeyValuePair<YamlNode, YamlNode>[] Bake(ProjectBakeSettings? bake) =>
        bake is null
            ? []
            : [
                Pair(
                    BakeKey,
                    YamlRecords.Write(
                        JsonSerializer.SerializeToNode(bake, LibraryTreeJsonContext.Default.ProjectBakeSettings)!
                            .AsObject()
                    )
                )
            ];

    static ProjectBakeSettings? ReadBake(YamlFields fields) {
        if (fields.Map(BakeKey) is not { } map) {
            return null;
        }

        var shape = JsonSerializer.SerializeToNode(
            new(),
            LibraryTreeJsonContext.Default.ProjectBakeSettings
        )!
            .AsObject();
        var bake = YamlRecords.Read(map, shape).Deserialize(LibraryTreeJsonContext.Default.ProjectBakeSettings)!;
        return bake.Options.Problem() is { } problem ? throw fields.Problem($"'{BakeKey}': {problem}") : bake;
    }

    /// <summary>The viewport's settings as a flat map, every one of them, so a project file has one shape.</summary>
    static YamlMappingNode Viewport(ViewportDisplay? display) {
        if (display is null) {
            return new();
        }

        (string Key, double Value)[] numbers = [
            ("environmentRotation", display.EnvironmentRotation), ("backgroundBlur", display.BackgroundBlur),
            ("backgroundOpacity", display.BackgroundOpacity), ("lightRotation", display.LightRotation),
            ("exposure", display.Exposure), ("shadows", display.Shadows), ("fieldOfView", display.FieldOfView),
            ("contrast", display.Contrast), ("saturation", display.Saturation), ("vignette", display.Vignette),
            ("grain", display.Grain), ("aberration", display.Aberration), ("sharpen", display.Sharpen)
        ];
        return new(
            [
                Pair("environment", YamlText.Text(display.Environment)),
                Pair("toneMapping", YamlText.Text(display.ToneMapping)),
                ..Present("look", display.Look),
                Pair("view", YamlText.Text(display.View)),
                ..Present("picture", display.Picture),
                Pair("wireframe", Flag(display.Wireframe)),
                Pair("orthographic", Flag(display.Orthographic)),
                ..numbers.Select(static number => Pair(number.Key, YamlText.Plain(number.Value)))
            ]
        );
    }

    static YamlScalarNode Flag(bool on) => YamlText.Plain(on ? "true" : "false");

    /// <summary>A setting left out is the viewport's own, and one that is not a number is refused.</summary>
    static ViewportDisplay? ReadViewport(IReadOnlyDictionary<string, string> map, YamlFields fields) {
        if (map.Count == 0) {
            return null;
        }

        var defaults = new ViewportDisplay();
        return new() {
            Environment = map.GetValueOrDefault("environment", defaults.Environment),
            EnvironmentRotation = Number("environmentRotation", defaults.EnvironmentRotation),
            BackgroundBlur = Number("backgroundBlur", defaults.BackgroundBlur),
            BackgroundOpacity = Number("backgroundOpacity", defaults.BackgroundOpacity),
            LightRotation = Number("lightRotation", defaults.LightRotation),
            Exposure = Number("exposure", defaults.Exposure),
            Shadows = Number("shadows", defaults.Shadows),
            ToneMapping = map.GetValueOrDefault("toneMapping", defaults.ToneMapping),
            Look = map.GetValueOrDefault("look"),
            View = map.GetValueOrDefault("view", defaults.View),
            Picture = map.GetValueOrDefault("picture"),
            Wireframe = Flag("wireframe"),
            Orthographic = Flag("orthographic"),
            FieldOfView = Number("fieldOfView", defaults.FieldOfView),
            Contrast = Number("contrast", defaults.Contrast),
            Saturation = Number("saturation", defaults.Saturation),
            Vignette = Number("vignette", defaults.Vignette),
            Grain = Number("grain", defaults.Grain),
            Aberration = Number("aberration", defaults.Aberration),
            Sharpen = Number("sharpen", defaults.Sharpen)
        };

        double Number(string key, double fallback) =>
            !map.TryGetValue(key, out var text) ? fallback
            : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value
            : throw fields.Problem($"'{ViewportKey}.{key}' is not a number.");

        bool Flag(string key) => map.GetValueOrDefault(key) == "true";
    }

    static YamlMappingNode Unwrap(UnwrapOptions options) =>
        new(
            Pair("resolution", YamlText.Plain(options.Resolution)),
            Pair("margin", YamlText.Plain(options.Margin)),
            Pair("quality", YamlText.Plain(options.Quality)),
            Pair("rotations", YamlText.Plain(options.Rotations)),
            Pair("texelDensity", YamlText.Plain(options.TexelDensity)),
            Pair("overflow", YamlText.Plain(options.Overflow))
        );

    static UnwrapOptions ReadUnwrap(IReadOnlyDictionary<string, string> map, YamlFields fields) {
        var defaults = new UnwrapOptions();

        int Whole(string key, int fallback) =>
            !map.TryGetValue(key, out var text) ? fallback
            : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value
            : throw fields.Problem($"'{UnwrapKey}.{key}' is not a whole number.");

        var options = new UnwrapOptions {
            Resolution = Whole("resolution", defaults.Resolution),
            Margin = Whole("margin", defaults.Margin),
            Quality = map.GetValueOrDefault("quality", defaults.Quality),
            Rotations = Whole("rotations", defaults.Rotations),
            TexelDensity = map.TryGetValue("texelDensity", out var density)
                && double.TryParse(density, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    ? number
                    : defaults.TexelDensity,
            Overflow = map.GetValueOrDefault("overflow", defaults.Overflow)
        };
        return options.Problem() is { } problem ? throw fields.Problem(problem) : options;
    }

    static YamlMappingNode Unwrapped(UnwrapReport? report) =>
        report is null
            ? new()
            : new(
                Pair("from", YamlText.Text(report.From)),
                Pair(
                    "sets",
                    new YamlSequenceNode(
                        report.Sets.Select(static set => new YamlMappingNode(
                                Pair(MaterialKey, YamlText.Text(set.Material)),
                                Pair("charts", YamlText.Plain(set.Charts)),
                                Pair("flipped", YamlText.Plain(set.Flipped)),
                                Pair("stretch", YamlText.Plain(set.Stretch)),
                                Pair("worstStretch", YamlText.Plain(set.WorstStretch)),
                                Pair("packed", YamlText.Plain(set.Packed)),
                                Pair("warnings", YamlText.Texts(set.Warnings))
                            )
                        )
                    )
                )
            );

    static UnwrapReport? ReadUnwrapped(YamlFields mesh) {
        if (mesh.Map(UnwrappedKey) is not { } held) {
            return null;
        }

        return new(
            held.Require("from"),
            [
                ..held.Maps("sets")
                    .Select(static set => new UnwrapSet(
                            set.Require(MaterialKey),
                            (int)set.Number("charts", 0),
                            (int)set.Number("flipped", 0),
                            set.Number("stretch", 1),
                            set.Number("worstStretch", 1),
                            set.Number("packed", 0),
                            set.Texts("warnings")
                        )
                    )
            ]
        );
    }

    static YamlMappingNode Item(ProjectItem item) =>
        new(
            [
                Pair(IdKey, YamlText.Plain(item.Id.ToString())),
                Pair(KindKey, YamlText.Plain(item.Kind)),
                Pair(NameKey, YamlText.Text(item.Name)),
                Pair(FileKey, YamlText.Text(item.File)),
                ..Present(MeshKey, item.Mesh?.ToString()),
                ..Present(RoleKey, item.Role),
                ..Present(MaterialKey, item.Material),
                ..Named(TagsKey, item.Tags.Count > 0, SortedTexts(item.Tags)),
                ..Named(DependenciesKey, item.Dependencies.Count > 0, References(item.Dependencies))
            ]
        );

    public static string Write(FolderDocument document) =>
        YamlText.Write(
            new(
                [
                    Pair(SchemaChain.VersionKey, YamlText.Plain(SchemaChain.Folder.Current)),
                    ..Present(ColorKey, document.Color)
                ]
            )
        );

    public static string Write(AssetDocument document) =>
        YamlText.Write(
            new(
                Pair(SchemaChain.VersionKey, YamlText.Plain(SchemaChain.Asset.Current)),
                Pair(IdKey, YamlText.Plain(document.Id.ToString())),
                Pair(KindKey, YamlText.Text(document.Kind)),
                Pair(NameKey, YamlText.Text(document.Name)),
                Pair(TagsKey, SortedTexts(document.Tags)),
                Pair(DependenciesKey, References(document.Dependencies))
            )
        );

    public static ReadDocument<LibraryDocument> ReadLibrary(string text, string path, SchemaChain? chain = null) =>
        Read(
            text,
            path,
            chain ?? SchemaChain.Library,
            static fields => new LibraryDocument(
                fields.Id<LibraryId>(IdKey),
                fields.Require(NameKey),
                fields.Number("texelDensity", 1024),
                fields.Texts("channels"),
                fields.Optional("defaultShader") ?? string.Empty,
                fields.Texts("exportTargets")
            )
        );

    public static ReadDocument<ProjectDocument> ReadProject(string text, string path, SchemaChain? chain = null) =>
        Read(
            text,
            path,
            chain ?? SchemaChain.Project,
            static fields => {
                var references = ReadReferences(fields, ReferencesKey);
                return new ProjectDocument(
                    fields.Id<ProjectId>(IdKey),
                    fields.Require(NameKey),
                    fields.Texts(TagsKey),
                    references,
                    ReadOutputs(fields, [..references.Select(static r => r.Asset)])
                ) {
                    Meshes = ReadMeshes(fields),
                    Documents = ReadItems(fields),
                    AutoUnwrap = fields.Optional(AutoUnwrapKey) != "false",
                    PreservePaint = fields.Optional(PreservePaintKey) != "false",
                    Unwrap = ReadUnwrap(fields.TextMap(UnwrapKey), fields),
                    Export = fields.Map(ExportKey) is { } export ? ExportCodec.Read(export) : null,
                    Bake = ReadBake(fields),
                    Viewport = ReadViewport(fields.TextMap(ViewportKey), fields)
                };
            }
        );

    public static ReadDocument<FolderDocument> ReadFolder(string text, string path) =>
        Read(
            text,
            path,
            SchemaChain.Folder,
            static fields => new FolderDocument(Named(fields.Optional(ColorKey)))
        );

    static string? Named(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    static List<ProjectMesh> ReadMeshes(YamlFields fields) {
        List<ProjectMesh> meshes = [
            ..fields.Maps(MeshesKey)
                .Select(static mesh => new ProjectMesh(
                        mesh.Id<AssetId>(IdKey),
                        Inside(mesh, mesh.Require(FileKey)),
                        mesh.Optional(HighKey) is { } high ? Inside(mesh, high) : null,
                        mesh.Optional(SourceKey),
                        mesh.Optional(HashKey)
                    ) { Unwrapped = ReadUnwrapped(mesh) }
                )
        ];
        return meshes.DistinctBy(static m => m.Id).Count() == meshes.Count
            ? meshes
            : throw fields.Problem("two meshes share an id.");
    }

    static List<ProjectItem> ReadItems(YamlFields fields) {
        var meshes = fields.Maps(MeshesKey).Select(static mesh => mesh.Id<AssetId>(IdKey)).ToHashSet();
        List<ProjectItem> items = [..fields.Maps(DocumentsKey).Select(item => ReadItem(item, meshes))];
        return items.DistinctBy(static d => d.Id).Count() == items.Count
            ? items
            : throw fields.Problem("two documents share an id.");
    }

    static ProjectItem ReadItem(YamlFields fields, HashSet<AssetId> meshes) {
        var kind = fields.Require(KindKey);
        if (!DocumentKinds.Known(kind)) {
            throw fields.Problem($"'{kind}' is no kind of document.");
        }

        AssetId? mesh = fields.Optional(MeshKey) is null ? null : fields.Id<AssetId>(MeshKey);
        if (mesh is { } named && !meshes.Contains(named)) {
            throw fields.Problem($"it is on the mesh {named}, which the project does not list.");
        }

        if (kind == DocumentKinds.Texturing != mesh.HasValue) {
            throw fields.Problem("a texturing document is on a mesh, and no other kind is.");
        }

        var role = fields.Optional(RoleKey);
        if (role is not null && (kind != DocumentKinds.Graph || !GraphRoles.Known(role))) {
            throw fields.Problem($"'{role}' is no role of a {kind} document.");
        }

        return new(
            fields.Id<AssetId>(IdKey),
            kind,
            Inside(fields, fields.Require(FileKey)),
            fields.Require(NameKey),
            mesh,
            role,
            fields.Optional(MaterialKey)
        ) { Tags = fields.Texts(TagsKey), Dependencies = ReadReferences(fields, DependenciesKey) };
    }

    static string Inside(YamlFields fields, string file) =>
        IsInsideProject(file) ? file : throw fields.Problem($"'{file}' is outside the project.");

    public static ReadDocument<AssetDocument> ReadAsset(string text, string path, SchemaChain? chain = null) =>
        Read(
            text,
            path,
            chain ?? SchemaChain.Asset,
            static fields => new AssetDocument(
                fields.Id<AssetId>(IdKey),
                fields.Require(KindKey),
                fields.Require(NameKey),
                fields.Texts(TagsKey),
                ReadReferences(fields, DependenciesKey)
            )
        );

    static ReadDocument<T> Read<T>(string text, string path, SchemaChain chain, Func<YamlFields, T> read) {
        var root = YamlText.Parse(text, path);
        var version = chain.Upgrade(root, path);
        var fields = new YamlFields(root, path);
        _ = fields.Optional(SchemaChain.VersionKey);
        var document = read(fields);
        return new(document, version, fields.Unread);
    }

    static List<AssetReference> ReadReferences(YamlFields fields, string key) {
        List<AssetReference> references = [..fields.Maps(key).Select(ReadReference)];
        if (references.DistinctBy(static r => r.Asset).Count() != references.Count) {
            // Two references with different modes would leave "which bytes does this use" without an answer.
            throw fields.Problem($"'{key}' names one asset twice.");
        }

        return references;
    }

    static AssetReference ReadReference(YamlFields fields) {
        var asset = fields.Id<AssetId>(AssetKey);
        var revisionText = fields.Optional(RevisionKey);
        var mode = fields.Optional(ModeKey) ?? Floating;
        switch (mode) {
            case Floating when revisionText is null:
                return AssetReference.Floating(asset);
            case Floating:
                throw fields.Problem($"the floating reference to {asset} has a revision.");
            case Pinned:
                return Revision.TryParse(revisionText, out var revision)
                    ? AssetReference.Pinned(asset, revision)
                    : throw fields.Problem($"the pinned reference to {asset} has no valid revision.");
            default:
                throw fields.Problem($"'{mode}' is no reference mode.");
        }
    }

    static List<OutputDeclaration> ReadOutputs(YamlFields fields, HashSet<AssetId> references) {
        List<OutputDeclaration> outputs = [..fields.Maps(OutputsKey).Select(output => ReadOutput(output, references))];
        if (outputs.DistinctBy(static o => o.Name).Count() != outputs.Count) {
            throw fields.Problem("two outputs share a name.");
        }

        return outputs;
    }

    static OutputDeclaration ReadOutput(YamlFields fields, HashSet<AssetId> references) {
        var name = fields.Require(NameKey);
        var files = fields.Texts(FilesKey);
        if (files.FirstOrDefault(static f => !IsInsideProject(f)) is { } escaping) {
            throw fields.Problem($"output '{name}' reads '{escaping}', which is outside the project.");
        }

        List<AssetId> uses = [];
        foreach (var text in fields.Texts(UsesKey)) {
            if (!GuidId.TryParse<AssetId>(text, out var asset) || !references.Contains(asset)) {
                throw fields.Problem($"output '{name}' uses '{text}', which the project does not reference.");
            }

            uses.Add(asset);
        }

        return new(name, fields.Require(ToolKey), files, uses, fields.TextMap(SettingsKey));
    }

    /// <summary>A key is computed from these paths, so one that left the project would tie it to the machine.</summary>
    static bool IsInsideProject(string file) =>
        file.Length > 0
        && !file.Contains('\\', StringComparison.Ordinal)
        && !Path.IsPathRooted(file)
        && !file.Split('/').Any(static part => part is "" or "." or "..");

    static KeyValuePair<YamlNode, YamlNode> Pair(string key, YamlNode value) => new(new YamlScalarNode(key), value);

    static YamlSequenceNode SortedTexts(IEnumerable<string> values) =>
        YamlText.Texts(values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    static YamlSequenceNode References(IEnumerable<AssetReference> references) =>
        new(references.OrderBy(static r => r.Asset.ToString(), StringComparer.Ordinal).Select(Reference));

    static YamlMappingNode Reference(AssetReference reference) {
        var node = new YamlMappingNode(
            Pair(AssetKey, YamlText.Plain(reference.Asset.ToString())),
            Pair(ModeKey, YamlText.Plain(reference.Mode == ReferenceMode.Pinned ? Pinned : Floating))
        );
        if (reference.Revision is { } revision) {
            node.Add(RevisionKey, YamlText.Plain(revision.Text));
        }

        return node;
    }

    static YamlMappingNode Output(OutputDeclaration output) =>
        new(
            Pair(NameKey, YamlText.Text(output.Name)),
            Pair(ToolKey, YamlText.Text(output.Tool)),
            Pair(FilesKey, SortedTexts(output.Files)),
            Pair(
                UsesKey,
                new YamlSequenceNode(
                    output.Uses.Select(static u => u.ToString())
                        .Order(StringComparer.Ordinal)
                        .Select(static u => YamlText.Plain(u))
                )
            ),
            Pair(
                SettingsKey,
                new YamlMappingNode(
                    output.Settings.OrderBy(static s => s.Key, StringComparer.Ordinal)
                        .Select(static s => Pair(s.Key, YamlText.Text(s.Value)))
                )
            )
        );
}
