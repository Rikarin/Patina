# ADR 0002: Kernel compile time and the SPIR-V cache (spike S2)

**Status:** accepted, 2026-11-05. **Pass criterion (plan §6):** cold start tolerable with a progress UI, cached under
200 ms.

## Decision

Keep compiling kernels at run time. Cache SPIR-V on disk, keyed by a hash of the sources and the compiler. Create pipelines
when a plan first needs them, never all 49 up front, and show progress when a first use has to compile.

S2 passes for a warm machine: 49 kernels reach ready pipelines in about 80 ms. The plan's fallbacks (precompiled kernels,
uber-kernels) are not needed for Raven, which turned out to be cheap. The surprise is on the other side of it: **the
expensive step on macOS is Metal, not Raven**, and a SPIR-V cache cannot help with that.

## Numbers

Apple M1 Max, 10 cores, macOS 27.2, MoltenVK 1.4.2, .NET 10.0.14, Release build, unpublished (no ReadyToRun). Each row is
a fresh process, so JIT is in the figures. All 49 kernels, `Rgba8` variant, 330,704 bytes of SPIR-V, 432 KB on disk.

| Step | Time | Runs |
|---|---|---|
| Raven to SPIR-V, serial | 864, 892, 927 ms (first kernel 180 ms with JIT, median 14 ms) | 3 |
| Raven to SPIR-V, `Parallel.For` | 607, 632, 643 ms | 3 |
| SPIR-V from the disk cache | 14, 15, 16 ms serial; 18, 18 ms parallel | 5 |
| 49 pipelines, Metal has the shaders cached | 63 to 74 ms | 10+ |
| 49 pipelines, Metal has never seen them, serial | 10,353, 10,856 ms | 2 |
| 49 pipelines, never seen, `Parallel.For` | 10,329, 10,349 ms | 2 |
| 49 pipelines, seeded `VkPipelineCache`, Metal cached | 37 to 40 ms | 4 |
| Vulkan device, usual / first after a pause | 63 to 74 ms / 597 to 651 ms | 12 / 2 |

So a Worker's kernel start is about 15 ms + 65 ms on a warm machine. On a machine that has never compiled these shaders it
is about 0.9 s + 10.5 s if everything is built at once, or 230 ms per kernel on first use.

Reproduce with `dotnet tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll kernels <folder> --pipelines`. Add
`--salt=<new number>` for a cold Metal compile.

## What the numbers mean

- **Metal keeps its own on-disk cache and it hides the cold number.** The first attempts read 65 ms and looked like a
  success. A new process name, a new code signature, a new Mach-O UUID and new Vulkan formats all stayed warm, because
  Metal texture types do not encode the storage format, so those variants produce the same MSL. Only a change to the
  shader's code forced a recompile, which is what `--salt` does. Anyone timing this must salt.
- **Threads do not help.** Threaded creation took the same 10.3 s. Something below `vkCreateComputePipelines` serialises,
  and I did not find out what.
- **A `VkPipelineCache` on MoltenVK saves little.** It holds MSL, not Metal binaries: 65 ms down to 39 ms when warm. I did
  not measure it against a cold Metal compile. Not worth adopting until a measurement says otherwise.
- **The cache's key is sound.** The folder name hashes the Raven and ShaderCompiler assemblies' module ids plus every source
  text, unparsed, so a hit never loads the parser. Vixen's `EffectDiskCache` stores the entry and handles torn writes.

## Consequences

- `Patina.Viewport.ShaderCache` is in use for the viewport's own shaders. `.ptcache/kernels/` (plan §5) is the folder once
  a library is open; without one the Worker uses a per-user folder, or `--shader-cache <folder>`.
- **Not wired yet:** `TexturePlanEvaluator.VariantFor` calls the compiler directly. It needs an `IEffectSource` seam in
  Vixen, which belongs with the evaluator rewrite in P2. Until then `patina bake` pays the 0.9 s.
- The first launch on a Mac has a cost that a user will notice in P2, and S8's packaging work should consider warming the
  commonest kernels on install.
- A compiler change invalidates every entry. That is correct and costs one 0.9 s.

## Not measured

Windows, Linux and lavapipe. MoltenVK is the only driver here, and other drivers' pipeline costs will differ. ReadyToRun
should reduce the JIT share of the cold Raven figure, and the first kernel's 180 ms is the upper limit of that gain.
