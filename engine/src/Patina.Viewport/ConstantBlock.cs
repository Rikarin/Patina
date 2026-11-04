// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     One uniform block's bytes, written by parameter name. A name the variant does not read was compiled out, so
///     setting it does nothing.
/// </summary>
sealed class ConstantBlock(EffectBlock block) {
    readonly byte[] bytes = new byte[block.Size];

    public ReadOnlySpan<byte> Bytes => bytes;

    public ConstantBlock Set<T>(string name, in T value) where T : unmanaged {
        foreach (var member in block.Members) {
            var key = member.Key.Name;
            if (string.Equals(key[(key.LastIndexOf('.') + 1)..], name, StringComparison.Ordinal)) {
                MemoryMarshal.Write(bytes.AsSpan(member.Offset), in value);
            }
        }

        return this;
    }
}
