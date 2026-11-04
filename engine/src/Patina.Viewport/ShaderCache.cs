// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Vixen.Raven;
using Vixen.ShaderCompiler;
using Vixen.Shaders;

namespace Patina.Viewport;

/// <summary>
///     Compiled SPIR-V on disk, on Vixen's <see cref="EffectDiskCache" />. An entry's folder is named after the hash of
///     its sources and the compiler that compiled them, so a new compiler or an edited source misses, and the sources
///     are hashed unparsed: a hit never touches the compiler. Deleting the folder loses nothing but time (ADR 0002).
/// </summary>
public sealed class ShaderCache(string directory) {
    int hits;
    int misses;

    /// <summary>Identifies the compiler: the two assemblies whose code decides what a source compiles to.</summary>
    public static string CompilerVersion { get; } =
        $"{typeof(Compilation).Assembly.ManifestModule.ModuleVersionId:N}"
        + $"-{typeof(RavenEffectCompiler).Assembly.ManifestModule.ModuleVersionId:N}";

    /// <summary>The per-user folder, for a Worker that was not given one.</summary>
    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create
            ),
            "Patina",
            "kernels"
        );

    /// <summary>How many entries came from disk, across every thread.</summary>
    public int Hits => hits;

    /// <summary>How many entries had to be compiled, across every thread.</summary>
    public int Misses => misses;

    /// <summary>
    ///     The variant <paramref name="key" /> names out of <paramref name="sources" />, from disk if it can be.
    /// </summary>
    /// <param name="key">The shader and its permutations.</param>
    /// <param name="sources">The whole compilation, in order.</param>
    /// <param name="compile">Compiles it on a miss; null keeps the default, which is Raven with no extras.</param>
    public EffectData Get(
        EffectKey key,
        ImmutableArray<(string Name, string Text)> sources,
        Func<RavenEffectCompiler>? compile = null
    ) {
        var source = new Compiling(() => compile?.Invoke() ?? RavenEffectCompiler.FromSources(sources));
        var cache = new EffectDiskCache(Path.Combine(directory, Hash(sources)), "spirv", source);
        var effect = cache.TryGet(key)
            ?? throw new InvalidOperationException($"The shader sources declare no shader '{key.ShaderName}'.");
        if (cache.Hits > 0) {
            Interlocked.Increment(ref hits);
        } else {
            Interlocked.Increment(ref misses);
        }

        return effect;
    }

    static string Hash(ImmutableArray<(string Name, string Text)> sources) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(CompilerVersion));
        foreach (var (name, text) in sources) {
            hash.AppendData(Encoding.UTF8.GetBytes(name));
            hash.AppendData([0]);
            hash.AppendData(Encoding.UTF8.GetBytes(text));
            hash.AppendData([0]);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Parses only when a miss asks: <see cref="RavenEffectCompiler" />'s constructor is where parsing is.</summary>
    sealed class Compiling(Func<RavenEffectCompiler> compiler) : IEffectSource {
        public EffectData? TryGet(EffectKey key) => compiler().TryGet(key);
    }
}
