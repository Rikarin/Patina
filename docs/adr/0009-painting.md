# ADR 0009: Painting (spike S4, and who holds a stroke)

**Status:** S4 passed for a stack of texel-local layers, 2026-11-05, and failed with a blur above the stroke. **Pass criterion
(plan §6):** a stamp through to the frame shown, 4K, 20 layers, tiled store, under 50 ms. **If it fails:** an overlay during the
stroke, composited on pause.

## S4: the plan composites the stroke, and no overlay is needed

`patina-bench s4 [resolution] [layers] [channels] [stamps] [radius] [painted layer]` runs the real path in one process. A 20-layer
stack of fills, paint layers, painted masks and HSL filters is compiled by `LayerStackCompiler` to 280 ops on 323 images, half-float
at 4096². Each stamp goes through `PaintStroke`, its tiles to the external texture, `TexturePlanEvaluator.EvaluateIncremental`, and
`TextureBake.ReadTexels` for the dirty tiles of the output. What is timed is from the stamp to those bytes in the Worker's memory.
The rest of the way, shared memory or a stream to a canvas, is ADR 0003's 19 ms. Apple M1 Max, MoltenVK, Release. The machine was
running other builds throughout, load average 60 to 140, so read these as a loaded machine.

| Case, 200 stamps | Composite p50 | Stamp to bytes p50 | p95 | Dispatches |
|---|---|---|---|---|
| **Radius 48, bottom layer, one channel** | **4.1 ms** | **6.0 ms** | **10.6 ms** | 74 of 256² tiles |
| The same with every op run whole, the old way | 41.9 ms | 44.7 ms | 52.1 ms | 37 at 4096² |
| Radius 256 | 11.5 ms | 16.7 ms | 21.2 ms | 333 |
| Every channel at once, 6 GiB cache | 25.7 ms | 32.9 ms | 43.3 ms | 185 |
| A blur layer above the stroke | 43.2 ms | 45.1 ms | 49.1 ms | 38 at 4096² |

Adding 19 ms: **25 ms for an ordinary stroke, 52 ms for the worst stroke a stack of local layers can ask for.** The cache budget was
Vixen's default of 2 GiB except where stated.

The difference is dirty regions (plan §3.2). A kernel derives its texel from its invocation id, so `vkCmdDispatchBase` runs it over
a rectangle with no change to any kernel, and MoltenVK supports it. The alternative was a uniform origin in about fifty kernels.
Three findings came out of measuring it:

- **Keeping the chain does not fit.** The first version repaired every image above the stroke in place, which meant keeping them,
  and took 24 GiB of cache. But an image between two ops is only ever read over the rectangles it was written. So only outputs
  stay whole pictures, the images between them rent pooled textures, and a stroke needs two or three of those.
- **A filter that reads its neighbours stops this.** `TextureLocality` lists the kernels that were read and found to depend on the
  same texel, and anything else above the stroke runs whole. That is the blur row, and at five channels it would be about 370 ms.
- **The cache must hold a stack's outputs and its pool.** At 2 GiB with five outputs the idle pool is trimmed after each stamp and
  recreated: 174 ms against 26 ms. The Worker therefore sets its own budget.

**Decision:** no overlay and no second composite. The plan is the only compositor, painted layers are external images, a stroke
updates their dirty tiles, and `EvaluateIncremental` repairs the outputs. What the artist watches is what a clean evaluation
produces, by construction and by test. The overlay stays the plan's answer for a stack with a non-local layer above the stroke, and
is not built: those strokes run whole and skip to the newest stamp.
