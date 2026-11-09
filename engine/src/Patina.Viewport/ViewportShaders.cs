// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Immutable;
using System.Globalization;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     The viewport's shaders, through the disk cache. Patina's forward pass is compiled beside the Vixen library
///     files it imports; the sky and the tonemap are the library's own. A compilation binds every file it is given, so
///     each set holds only what its shader's imports reach.
/// </summary>
sealed class ViewportShaders(ShaderCache cache) {
    const string LibraryPrefix = "Patina.Viewport.Library.";
    const string ShaderPrefix = "Patina.Viewport.Shaders.";

    static readonly ImmutableArray<(string Name, string Text)> Core =
        Read(static name => name.Contains(".Core.", StringComparison.Ordinal));

    static readonly ImmutableArray<(string Name, string Text)> Shading =
        [..Core, ..Read(static name => name.Contains(".Shading.", StringComparison.Ordinal))];

    static readonly ImmutableArray<(string Name, string Text)> PostFx = [
        ..Core,
        ..Read(static name => name.Contains(".Geometry.", StringComparison.Ordinal)),
        ..Read(static name => name.EndsWith("PostFx.Fullscreen.rvn", StringComparison.Ordinal))
    ];

    public EffectData Forward() =>
        cache.Get(
            EffectKey.Of("ViewportForward"),
            [..Shading, ..Read(static name => name.EndsWith("Shaders.ViewportForward.rvn", StringComparison.Ordinal))]
        );

    public EffectData Shadow() =>
        cache.Get(
            EffectKey.Of("ViewportShadow"),
            [..Read(static name => name.EndsWith("Shaders.ViewportShadow.rvn", StringComparison.Ordinal))]
        );

    public EffectData Sky() =>
        cache.Get(
            EffectKey.Of("Sky", [new("Soften", "true")]),
            [..PostFx, ..Read(static name => name.EndsWith("PostFx.Sky.rvn", StringComparison.Ordinal))]
        );

    /// <summary>Lines over the model.</summary>
    public EffectData Wire() =>
        cache.Get(
            EffectKey.Of("ViewportWire"),
            [..Core, ..Read(static name => name.EndsWith("Shaders.ViewportWire.rvn", StringComparison.Ordinal))]
        );

    /// <summary>The floor's grid and the world's axes.</summary>
    public EffectData Grid() =>
        cache.Get(
            EffectKey.Of("ViewportGrid"),
            [..Core, ..Read(static name => name.EndsWith("Shaders.ViewportGrid.rvn", StringComparison.Ordinal))]
        );

    /// <summary>One of Vixen's four bloom passes: 0 keeps the bright light, 1 and 2 halve, 3 adds a level back.</summary>
    public EffectData Bloom(int mode) =>
        cache.Get(
            EffectKey.Of("Bloom", [new("Mode", mode.ToString(CultureInfo.InvariantCulture)), new("KarisAverage", "true")]),
            [..PostFx, ..Read(static name => name.EndsWith("PostFx.Bloom.rvn", StringComparison.Ordinal))]
        );

    /// <summary>The curve to the screen, one variant per operator, in the order Vixen numbers them.</summary>
    public EffectData Tonemap(ViewportToneMapping curve) =>
        cache.Get(
            EffectKey.Of(
                "Tonemap",
                [new("EncodeSrgb", "true"), new("Operator", ((int)curve).ToString(CultureInfo.InvariantCulture))]
            ),
            [..PostFx, ..Read(static name => name.EndsWith("PostFx.Tonemap.rvn", StringComparison.Ordinal))]
        );

    /// <summary>Vixen's lens pass with only the effects asked for, since a zero vignette is not no vignette.</summary>
    public EffectData Lens(bool vignette, bool aberration, bool grain) =>
        cache.Get(
            EffectKey.Of(
                "Vignette",
                [
                    new("UseVignette", Flag(vignette)),
                    new("UseChromaticAberration", Flag(aberration)),
                    new("UseGrain", Flag(grain))
                ]
            ),
            [..PostFx, ..Read(static name => name.EndsWith("PostFx.Vignette.rvn", StringComparison.Ordinal))]
        );

    /// <summary>AMD's contrast-adaptive sharpening.</summary>
    public EffectData Sharpen() =>
        cache.Get(
            EffectKey.Of("Sharpen"),
            [..PostFx, ..Read(static name => name.EndsWith("PostFx.Sharpen.rvn", StringComparison.Ordinal))]
        );

    static string Flag(bool on) => on ? "true" : "false";

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
