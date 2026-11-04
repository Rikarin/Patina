// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Immutable;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     The viewport's three shaders, through the disk cache. Patina's forward pass is compiled beside the Vixen library
///     files it imports; the sky and the tonemap are the library's own. A compilation binds every file it is given, so
///     each set holds only what its shader's imports reach.
/// </summary>
sealed class ViewportShaders(ShaderCache cache) {
    const string LibraryPrefix = "Patina.Viewport.Library.";
    const string ShaderPrefix = "Patina.Viewport.Shaders.";

    static readonly ImmutableArray<(string Name, string Text)> Core = Read(static name => name.Contains(".Core.", StringComparison.Ordinal));

    static readonly ImmutableArray<(string Name, string Text)> Shading =
        [.. Core, .. Read(static name => name.Contains(".Shading.", StringComparison.Ordinal))];

    static readonly ImmutableArray<(string Name, string Text)> PostFx = [
        .. Core,
        .. Read(static name => name.Contains(".Geometry.", StringComparison.Ordinal)),
        .. Read(static name => name.EndsWith("PostFx.Fullscreen.rvn", StringComparison.Ordinal))
    ];

    public EffectData Forward() =>
        cache.Get(
            EffectKey.Of("ViewportForward"),
            [.. Shading, .. Read(static name => name.StartsWith(ShaderPrefix, StringComparison.Ordinal))]
        );

    public EffectData Sky() =>
        cache.Get(
            EffectKey.Of("Sky", [new("Soften", "true")]),
            [.. PostFx, .. Read(static name => name.EndsWith("PostFx.Sky.rvn", StringComparison.Ordinal))]
        );

    public EffectData Tonemap() =>
        cache.Get(
            EffectKey.Of("Tonemap", [new("EncodeSrgb", "true")]),
            [.. PostFx, .. Read(static name => name.EndsWith("PostFx.Tonemap.rvn", StringComparison.Ordinal))]
        );

    static ImmutableArray<(string Name, string Text)> Read(Func<string, bool> wanted) {
        var assembly = typeof(ViewportShaders).Assembly;
        var builder = ImmutableArray.CreateBuilder<(string, string)>();
        foreach (var resource in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal)) {
            var isShader = resource.StartsWith(LibraryPrefix, StringComparison.Ordinal)
                || resource.StartsWith(ShaderPrefix, StringComparison.Ordinal);
            if (!isShader || !wanted(resource)) {
                continue;
            }

            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            builder.Add((resource, reader.ReadToEnd()));
        }

        return builder.ToImmutable();
    }
}
