# ADR 0052: A graph is built at 16K only when its plan fits the device, measured before anything is made

**Status:** proposed, 2026-11-16, for issue #218. Nothing of it is built yet.

#218 left 16K graphs waiting on a GPU-memory plan. `TextureSizes.Largest` (8192) is one number that governs a graph's
size, a texturing document's canvas, a bake and an export, so raising it would raise all four at once. This is the plan,
written before any of it is built because it is a choice between designs with different costs.

## What 16K costs

A 16384² picture is 2 GiB at four half-floats a texel, 1 GiB at four bytes and 512 MiB for one half-float. Vixen pools a
plan's images by liveness (`TexturePoolSchedule`): an image lives from the op that writes it to the last op that reads
it, and a slot is reused only by an image of the same format and size. Its `Bytes` is what one evaluation allocates,
with no device, so it can be known before anything is made. Measured on Patina's own compounds, each compiled alone the
way `Patina.Content.Tests` benches them and scaled to 16384² (every image of a plan is relative to its base, so its
pool's bytes scale with the square of the side):

| Compound | Live at once | Textures | Pool at 16K |
|---|---:|---:|---:|
| `Grunges/Scuffs` | 3 | 3 | 1.5 GiB |
| `Grunges/Grunge 01` | 3 | 4 | 5 GiB |
| `Generators/Moss` | 4 | 6 | 6 GiB |
| `Generators/Surface Scratches` | 4 | 6 | 9 GiB |
| `Weathering/Peeling Paint` | 7 | 11 | 11.5 GiB |
| `Photo/Textured Fill` | 12 | 25 | 17.8 GiB |
| `Generators/Edge Select`, `Cavity Select` | 3 | 5 | 33.5 GiB |
| `Weathering/Rust` | 11 | 16 | 45 GiB |

⚠ **The large ones are not long chains but one kind of image.** Edge Select, Cavity Select and everything built on them
(Paint Chips, the worn edges, Rust) rank the curvature in two `Rgba32Float` images at twice the graph's side, a level
of -1. At 16K each of those is 32768² and 16 GiB, and 32768 is past `maxImageDimension2D` on many devices
(many stop at 16384). The same two images are 8 GiB together at an 8K graph today.

So no rule of the form "16K if the card has N GiB" holds: a grunge fits on a 16 GiB card, and a material with worn edges
fits on none. The result cache (`TextureResultCache`, half the device's memory and at most 6 GiB in the Worker) cannot
keep a 16K graph's intermediates either, and the incremental path is what an edit runs.

## Options considered

- **Raise `Largest` to 16384 everywhere.** One line, and every 8 GiB card fails mid-evaluation with an allocation error
  the Worker can only report as a lost device. Stacks would also offer 16K canvases, each paint layer 2 GiB with its
  journal. Rejected.
- **Tile the evaluation**: run the plan over 4K tiles with a halo per op. Every op would have to state its footprint, and
  the nodes that read far (blur at a large radius, warp, flood fill, distance, the erosions, any Pixel Processor that
  samples) have footprints as large as the picture, so a real graph would fall back to whole pictures anyway. This is
  Vixen work of the size of the evaluator itself, for a gain on the graphs that need it least. Not now.
- **Evaluate at 8K and upscale.** Not 16K: no detail a 16K output exists for. Rejected.
- **Whole pictures, admitted by measurement** (this ADR).

## Decisions

- **A graph's size may be 16384, and only a graph's.** `TextureSizes` gains a separate largest graph side; a texturing
  document's canvas, a bake and a stack keep 8192, since a paint layer's memory is held for the whole session and a
  graph's only for one evaluation. The list a client offers for a graph's size gains 16384; the others do not.
- **The plan is measured before it runs.** The Worker compiles the plan at the requested size, takes
  `TexturePoolSchedule.Bytes` plus the external pictures it uploads and the largest side of any slot, and compares them
  with what the device has: the side with `maxImageDimension2D`, and the bytes with the
  `VK_EXT_memory_budget` heap budget where the driver reports it, else half the device's own memory, the rule the result
  cache already follows. A plan that does not fit is refused with a coded refusal (`graph-too-large`, its bytes and the
  device's in the values) before any texture is created, and the Server passes the Worker's words on as it does for "No
  Vulkan". It never answers as if the graph had been drawn.
- **A 16K evaluation keeps nothing.** It runs through the plain evaluator, not the incremental one, and leaves the result
  cache as it was, so an export at 16K does not evict the session the artist is editing. Each output is read back and
  written to its file before the next is read, so the host side holds one picture at a time.
- **Editing stays at the session's size.** The 2D view and the viewport show a 16K graph at its preview level (the
  `BakeLevelOffset` the session already uses), so an edit costs what it costs at 8K or less; 16K is what a build and an
  export write. The graph's size setting is what they read, as now.
- **The refusal says what would fit.** Its message names the largest side the device would take for that plan (the
  bytes scale with the square of the side), so a person can pick 8K rather than guess.

## Not yet

- Tiled evaluation, kept for the day a graph that does not fit on a 24 GiB card is a real request.
- The ranked selections' two supersampled `Rgba32Float` images (Vixen's curvature ranking) are what keep worn edges
  off every card at 16K; ranking at the graph's own side, or in fewer lanes, would take most of the weathering compounds
  under 12 GiB. That is a Vixen change of its own and the first thing to measure once admission exists.
- Stacks and canvases at 16K, which are a memory plan of their own (paint layers, journals, checkpoints).
- Exports at 16K go through the same admission, but compression to block formats at 16K on the CPU is untimed.
