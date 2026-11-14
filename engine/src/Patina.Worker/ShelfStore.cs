using Patina.Library;
using Vixen.TextureGraph;

namespace Patina.Worker;

/// <summary>The evaluator's disk tier over a library's <see cref="ResultShelf" /> (#208).</summary>
sealed class ShelfStore(ResultShelf shelf) : ITextureResultStore {
    public ResultShelf Shelf => shelf;

    public bool Has(TextureResultKey key) => shelf.Has(key.Value);

    public byte[]? Read(TextureResultKey key, TexturePoolSlot shape) =>
        shelf.Read(key.Value, (int)shape.Format, shape.Width, shape.Height, Bytes(shape));

    public void Write(TextureResultKey key, TexturePoolSlot shape, ReadOnlySpan<byte> texels) =>
        shelf.Write(key.Value, (int)shape.Format, shape.Width, shape.Height, texels);

    static long Bytes(TexturePoolSlot shape) =>
        (long)shape.Width * shape.Height * TextureFormats.BytesPerTexel(shape.Format);
}
