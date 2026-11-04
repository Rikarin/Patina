using System.Collections.Immutable;
using Vixen.ShaderCompiler;
using Vixen.Shaders;

namespace Patina.Viewport.Tests;

/// <summary>The disk cache, counted in compiler runs rather than milliseconds.</summary>
public sealed class ShaderCacheTests : IDisposable {
    const string Source = """
                          package Patina.Viewport.Tests

                          shader Probe {
                              [VertexShader]
                              [Semantic("SV_Position")]
                              func Vertex(position: float3): float4 {
                                  return float4(position, 1f)
                              }

                              [FragmentShader]
                              [Semantic("SV_Target")]
                              func Fragment(): float4 {
                                  return float4(1f, 0f, 0f, 1f)
                              }
                          }
                          """;

    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-shaders-");

    public void Dispose() => folder.Delete(true);

    static ImmutableArray<(string Name, string Text)> Sources(string text) => [("Probe.rvn", text)];

    static EffectData Get(ShaderCache cache, string text, ref int compiles) {
        var count = 0;
        var effect = cache.Get(
            EffectKey.Of("Probe"),
            Sources(text),
            () => {
                count++;
                return RavenEffectCompiler.FromSources(Sources(text));
            }
        );
        compiles += count;
        return effect;
    }

    [Fact]
    public void A_second_process_finds_the_compiled_shader_and_never_touches_the_compiler() {
        var compiles = 0;

        var first = Get(new(folder.FullName), Source, ref compiles);
        Assert.Equal(1, compiles);

        var cache = new ShaderCache(folder.FullName);
        var second = Get(cache, Source, ref compiles);

        Assert.Equal(1, compiles);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(0, cache.Misses);
        Assert.Equal(first.Stages[0].Bytecode, second.Stages[0].Bytecode);
    }

    [Fact]
    public void An_edited_source_misses_even_though_the_shader_keeps_its_name() {
        var compiles = 0;
        var cache = new ShaderCache(folder.FullName);

        var red = Get(cache, Source, ref compiles);
        var green = Get(
            cache,
            Source.Replace("1f, 0f, 0f, 1f", "0f, 1f, 0f, 1f", StringComparison.Ordinal),
            ref compiles
        );

        Assert.Equal(2, compiles);
        Assert.Equal(2, cache.Misses);
        Assert.NotEqual(red.Stages[1].Bytecode, green.Stages[1].Bytecode);
    }

    [Fact]
    public void A_truncated_entry_costs_a_compile_and_nothing_else() {
        var compiles = 0;
        Get(new(folder.FullName), Source, ref compiles);
        foreach (var entry in folder.EnumerateFiles("*", SearchOption.AllDirectories)) {
            File.WriteAllBytes(entry.FullName, [1, 2, 3]);
        }

        var effect = Get(new(folder.FullName), Source, ref compiles);

        Assert.Equal(2, compiles);
        Assert.NotEmpty(effect.Stages);
    }

    [Fact]
    public void A_compiler_version_names_both_assemblies() =>
        Assert.Matches("^[0-9a-f]{32}-[0-9a-f]{32}$", ShaderCache.CompilerVersion);
}
