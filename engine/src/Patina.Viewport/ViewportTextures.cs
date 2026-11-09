// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using Vixen.Core.Imaging;
using Vixen.Graphics;
using Vixen.Graphics.Vulkan;
using Vixen.Interchange;

namespace Patina.Viewport;

/// <summary>
///     The textures on the GPU, by what they are: a file's hash or an embedded image's, and what its numbers mean,
///     since one file may be read as colour and as a measurement. All of it is rebuilt from files after a crash, and
///     after any change only the textures still in use are kept.
/// </summary>
sealed class ViewportTextures : IDisposable {
    readonly VulkanDevice device;
    readonly Dictionary<(string Identity, MapMeaning Meaning), Entry> entries = [];

    public ViewportTextures(VulkanDevice device) {
        this.device = device;
        White = Upload(ViewportImage.Solid(255, 255, 255, MapMeaning.Colour), "viewport white");
        Open = Upload(ViewportImage.Solid(255, 255, 255, MapMeaning.Measurement), "viewport open");
        Black = Upload(ViewportImage.Solid(0, 0, 0, MapMeaning.Colour), "viewport black");
    }

    /// <summary>Base colour for a slot nothing fills: the factor stands alone.</summary>
    public TextureViewHandle White { get; }

    /// <summary>Emissive for a slot nothing fills: no light.</summary>
    public TextureViewHandle Black { get; }

    /// <summary>Roughness, metallic or occlusion for a slot nothing fills.</summary>
    public TextureViewHandle Open { get; }

    public void Dispose() {
        foreach (var entry in entries.Values) {
            Destroy(entry);
        }

        entries.Clear();
    }

    /// <summary>A file, read once for each hash.</summary>
    /// <exception cref="InvalidDataException">It is not an image this reads.</exception>
    /// <exception cref="IOException">It cannot be read.</exception>
    public TextureViewHandle Of(ViewportMapFile file, MapMeaning meaning) =>
        Of(
            file.Hash,
            meaning,
            () => ViewportImage.Decode(File.ReadAllBytes(file.Path), Path.GetExtension(file.Path), meaning),
            file.Path
        );

    /// <summary>An image a material names, beside the model or inside it.</summary>
    /// <exception cref="InvalidDataException">It is not an image this reads.</exception>
    /// <exception cref="IOException">It cannot be read.</exception>
    public TextureViewHandle Of(ModelTexture texture, string meshFolder, MapMeaning meaning) {
        var bytes = texture.Embedded ?? File.ReadAllBytes(Path.GetFullPath(texture.Path!, meshFolder));
        return Of(
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            meaning,
            () => ViewportImage.Decode(bytes, texture.Extension, meaning),
            "material image"
        );
    }

    /// <summary>A cube map for the life of the renderer, released through <paramref name="cleanup" />.</summary>
    public TextureViewHandle UploadCube(TextureData data, List<Action> cleanup) {
        var texture = device.CreateTexture(
            new(
                data.Format,
                data.Width,
                data.Height,
                TextureUsage.Sampled | TextureUsage.CopyDestination,
                MipLevels: data.LevelCount,
                ArrayLayers: CubeMap.Faces,
                Dimension: TextureDimension.TextureCube,
                Name: "viewport environment"
            )
        );
        var view = device.CreateTextureView(texture);
        cleanup.Add(() => {
                device.Destroy(view);
                device.Destroy(texture);
            }
        );

        var staging = device.CreateBuffer(
            new(data.ByteLength, BufferUsage.CopySource, MemoryAccess.HostUpload, "viewport environment staging")
        );
        device.Write(staging, 0, data.Pixels);
        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport environment")) {
            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.Undefined, ResourceState.CopyDestination)])
            );
            CopyFaces(commands, data, staging, texture);
            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.CopyDestination, ResourceState.ShaderRead)])
            );
            commands.Finish();
            device.GraphicsQueue.Submit([commands]);
        }

        device.WaitIdle();
        device.Destroy(staging);
        return view;
    }

    static void CopyFaces(ICommandList commands, TextureData data, BufferHandle staging, TextureHandle texture) {
        foreach (var level in data.Levels) {
            var faceBytes = level.Length / CubeMap.Faces;
            for (var face = 0; face < CubeMap.Faces; face++) {
                commands.CopyBufferToTexture(
                    staging,
                    level.Offset + face * faceBytes,
                    new(texture, level.Level, face),
                    new(level.Width, level.Height, 1)
                );
            }
        }
    }

    /// <summary>
    ///     The width of the texture behind a view, or zero for one of the stand-ins with no picture of its own and for
    ///     a view this did not make.
    /// </summary>
    public int WidthOf(TextureViewHandle view) {
        foreach (var entry in entries.Values) {
            if (entry.View == view) {
                return entry.Permanent ? 0 : entry.Width;
            }
        }

        return 0;
    }

    /// <summary>Lets go of everything not used since <see cref="BeginUse" />.</summary>
    public void EndUse() {
        device.WaitIdle();
        foreach (var (key, entry) in entries.Where(static pair => pair.Value is { Used: false, Permanent: false })
                     .ToArray()) {
            Destroy(entry);
            entries.Remove(key);
        }
    }

    /// <summary>Starts counting which textures a new set of materials reaches.</summary>
    public void BeginUse() {
        foreach (var key in entries.Keys.ToArray()) {
            entries[key] = entries[key] with { Used = false };
        }
    }

    TextureViewHandle Of(string identity, MapMeaning meaning, Func<TextureData> decode, string name) {
        var key = (identity, meaning);
        if (entries.TryGetValue(key, out var entry)) {
            entries[key] = entry with { Used = true };
            return entry.View;
        }

        var created = Create(decode(), name);
        entries.Add(key, created);
        return created.View;
    }

    TextureViewHandle Upload(TextureData data, string name) {
        var entry = Create(data, name) with { Permanent = true };
        entries.Add((name, MapMeaning.Measurement), entry);
        return entry.View;
    }

    Entry Create(TextureData data, string name) {
        var texture = device.CreateTexture(
            new(
                data.Format,
                data.Width,
                data.Height,
                TextureUsage.Sampled | TextureUsage.CopyDestination,
                MipLevels: data.LevelCount,
                Name: name
            )
        );
        var staging = device.CreateBuffer(
            new(data.ByteLength, BufferUsage.CopySource, MemoryAccess.HostUpload, "viewport texture staging")
        );
        device.Write(staging, 0, data.Pixels);
        using (var commands = device.BeginCommandList(QueueKind.Graphics, "viewport texture")) {
            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.Undefined, ResourceState.CopyDestination)])
            );
            foreach (var level in data.Levels) {
                commands.CopyBufferToTexture(
                    staging,
                    level.Offset,
                    new(texture, level.Level),
                    new(level.Width, level.Height, 1)
                );
            }

            commands.Barrier(
                new BarrierGroup([], [new(texture, ResourceState.CopyDestination, ResourceState.ShaderRead)])
            );
            commands.Finish();
            device.GraphicsQueue.Submit([commands]);
        }

        device.WaitIdle();
        device.Destroy(staging);
        return new(texture, device.CreateTextureView(texture), true, false, data.Width);
    }

    void Destroy(Entry entry) {
        device.Destroy(entry.View);
        device.Destroy(entry.Texture);
    }

    readonly record struct Entry(TextureHandle Texture, TextureViewHandle View, bool Used, bool Permanent, int Width);
}
