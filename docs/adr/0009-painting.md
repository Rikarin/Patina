# ADR 0009: Painting (spike S4, and who holds a stroke)

**Status:** S4 passed in one process, 2026-11-05, on a machine under load and at the median, and **does not yet pass through a real window**
(below). S5's painting half passed, with a journal. **Pass
criteria (plan §6):** S4, a stamp through to the frame shown, 4K, 20 layers, tiled store, under 50 ms, and if not, an overlay during
the stroke composited on pause. S5, kill the Worker in a stroke: session intact, Worker back in under 3 s, and if not, a journal for
unflushed strokes.

## S4: the plan composites the stroke, and no overlay is needed

`patina-bench s4 [resolution] [layers] [channels] [stamps] [radius] [painted layer] [--whole] [--every-channel] [--blur]` runs the
real path in one process. A 20-layer stack of fills, paint layers, painted masks and HSL filters is compiled by `LayerStackCompiler`
to 280 ops on 323 images, half-float at 4096². Each stamp goes through `PaintStroke` and `LayerStackEvaluator`, which uploads its
tiles and repairs the outputs, and `TextureBake.ReadTexels` reads the dirty tiles of the output. What is timed is from the stamp to
those bytes in the Worker's memory. Apple M1 Max, MoltenVK, Release, 6 GiB of result cache. Other builds ran throughout, load
average 25 to 215, so these are numbers from a loaded machine and no number from an idle one exists.

| Case | Composite p50 | Stamp to bytes p50 | p95 | Ops |
|---|---|---|---|---|
| **Radius 48, bottom layer, one channel** | **4.1 ms** | **5.6 ms** | **11.4 ms** | 37 over tiles |
| The same near the top of the stack | 4.5 ms | 6.0 ms | 12.3 ms | 40 over tiles |
| Radius 256 | 6.7 ms | 13.0 ms | 29.5 ms | 37 over tiles |
| Five channels at once | 25.0 ms | 33.9 ms | 42.3 ms | 185 over tiles |
| A blur layer near the top | 15.6 ms | 17.4 ms | 24.1 ms | 21 over tiles, 4 whole |
| A clean evaluation of the plan, the old way | 764 ms | 766 ms | 815 ms | 280 whole |

**End to end through the real processes** (`A_stamp_reaches_a_client_as_a_preview`, `PATINA_S4_SIZE=4096`, Release): from POSTing one
stamp to holding the changed map, a 512² PNG, through the REST call, the journal, the socket, the Worker, the WebSocket event and
`GET /previews/{hash}`, on 20 layers: **p50 38 ms, p95 65 ms**, load average about 150. So the median passes and the 95th percentile,
on a machine doing other work, does not. The preview PNG is most of what is left beyond the composite, and a raw frame stream would
take it out. ADR 0003's 19 ms stream is the alternative if the p95 turns out to matter on a quiet machine.

The difference is dirty regions (plan §3.2). A kernel derives its texel from its invocation id, so `vkCmdDispatchBase` runs it over
a rectangle with no change to any kernel, and MoltenVK supports it. The alternative was a uniform origin in about fifty kernels.
Four findings came out of measuring it:

- **Keeping the chain does not fit.** The first version repaired every image above the stroke in place, which meant keeping them,
  and took 24 GiB of cache. But an image between two ops is only ever read over the rectangles it was written. So only outputs
  stay whole pictures, the images between them rent pooled textures, and a stroke needs two or three of those.
- **A filter that reads its neighbours costs a whole dispatch.** `TextureLocality` lists the kernels that were read and found to
  depend on the same texel, and anything else above the stroke runs whole, and so does everything above that. That is the blur
  row, where the blur sat near the top. Under the bottom of a stack of five channels it would be several hundred milliseconds.
- **The cache must hold a stack's outputs and its pool.** At Vixen's default 2 GiB with five outputs the idle pool is trimmed after
  each stamp and recreated: 174 ms against 26 ms. The Worker therefore sets 6 GiB.
- **The first version of this spike measured a brush that painted nothing.** `new PaintBrush { Radius = r }` has a flow of zero. A
  test that a stroke changes pixels caught it, and every number here is from `PaintBrush.Default`.

**Decision:** no overlay and no second composite. The plan is the only compositor, painted layers are external images, a stroke
updates their dirty tiles, and `EvaluateIncremental` repairs the outputs. What the artist watches is what a clean evaluation
produces, by construction and by test. The overlay stays the plan's answer for a stack with a non-local layer above the stroke, and
is not built: those strokes run whole and a slow composite sends bigger parts rather than a queue.

## Who holds a stroke: a journal on the Server

A Worker that dies must lose nothing, and a stroke's pixels are made in the Worker. So S5's fallback is needed, and it is the shape
of the design rather than an addition to it. **A stroke is a list of stamps, and the Server keeps that list.** The pixels are a
function of the files the session opened and the journal, in order. `StackEditor` holds one entry per stroke; the Worker holds
canvases, tiles and undo records, all of which it can rebuild.

Painting on the Server instead was the alternative: its pixels would then survive anything. It was turned down because a stamp is
the Worker's work in the plan (§3.1), and the Server would hold gigabytes of tiles and have to send them across shared memory for no
gain over stamping again. Stamps are deterministic: `PaintStroke` takes its jitter from a seed, and the digest test holds that a
replay makes the same bytes.

- **The Worker is told the state, not the steps.** `stack.sync` carries the strokes that should stand, in order, and the stamps it
  has not been sent. The Worker undoes back to what it shares and goes forward, so an undo, a redo and a replay are one code path.
- **A checkpoint bounds a replay.** After 2048 stamps, between strokes, the Worker writes the canvases it touched to
  `.ptcache/paint/<session>/<guid>`, and a new Worker starts from the newest one on the way to the strokes that stand. A checkpoint
  holding an undone stroke is not used, so undo reaches behind one, at the cost of a longer replay.
- **A save keeps what it overwrites.** The journal starts from what the session opened, so before the first save or checkpoint those
  files are copied to `.ptcache/paint/<session>/base`. Deleting that cache costs the undo past a save after a lost Worker, and
  nothing a file holds, which is the rule (plan §3.1).
- **A file a stroke made is not a base.** A first stroke creates its paint file and a save writes it, so a replay that started from
  that file painted the stroke twice. `stack.load` names the files this session's strokes created, and a new Worker starts those
  blank. `A_worker_lost_after_a_save_undoes_to_before_the_file_existed` was red before that.
- **A Server that dies loses unsaved strokes**, as it loses unsaved graph edits. The journal is in memory. S5 is about the Worker.

`A_worker_killed_mid_stroke_loses_nothing` kills the Worker with `SIGKILL` between two parts of a drag, sends a third with nobody
there, and starts a new one. The pixels were back 0.8 to 2.8 s later on a 1024² stack, cold process and first evaluation included,
and their digest equals a session that was never interrupted, before and after an undo. Believing a new Worker knows what the old one
knew fails it. `A_worker_lost_after_a_checkpoint_can_still_undo_past_it` undoes five strokes behind a checkpoint on a new Worker.
Replay is CPU time per stamp, about a millisecond at this brush, so 2048 stamps is the worst a stroke boundary costs at about
2 to 10 s on top of the Worker's own start. That is beyond S5's 3 s for the pixels; the session itself is never down, since edits land
on the Server meanwhile.

## What you paint is what exports

A map's digest is SHA-256 of its pixels as 8-bit RGBA, which is what an export writes. `GET /stacks/{id}/pixels` gives the digests of
what the strokes left, and `POST /stacks/{id}/export` evaluates from nothing and writes PNGs. `What_you_paint_is_what_exports` requires
the two digests and the decoded files to agree, for an Overlay layer under a Multiply one, which `PaintComposite` could not show. A
repair that covered half its rectangle fails it.

`What_the_viewport_samples_is_what_exports` extends this to the viewport. Viewport pixels are lit and tonemapped, so they can never equal
a file, and what the test compares is **the source pixels the draw call samples**: the top level of the texture bound to each material
slot, read back from the viewport's own device as 8-bit RGBA, by SHA-256, against the PNG an export wrote from a clean evaluation of the
whole stack. Lighting, tonemapping, filtering and the mips above that level are not covered. It holds after strokes through a camera
across the mesh's seams, a stroke in the atlas, an undone first stroke (which compiles the plan from before it) and a redo. Dropping a
repair, or stopping one eight texels short of its right edge, fails it.

`Every_kind_of_layer_stays_what_exports` holds the same across fills, filters, groups and masks. The Python-shaped path is untouched: all
of it is `GET /stacks/{id}/pixels?sampled=true`.

**This overturns the plan this section used to state,** that the viewport would sample `TextureBake.TextureOf(output)` directly with no
second copy. The stack's outputs are half-float, so sampling one filters sRGB numbers without decoding them, shows precision no export
has, and has no mips. So the viewport holds an 8-bit texture in the formats a file of the same bytes gets, and a repair sends it the
bytes an export writes (`TexturePixels.ToBitmap`, the same function). `A_live_map_draws_what_a_file_of_the_same_bytes_draws_and_a_repair_keeps_it_so`
holds the frame within 2 or 3 levels of the file's, near and far, before and after a repair. A 4K map costs the GPU its usual 89 MB, and
the Worker 28 MB for the mips, since each level is made from the rectangle below it.

The two devices stay two. The bytes cross inside the Worker, so nothing about the evaluator's frame can touch the viewport's, and no pixel
reaches a socket or a JSON body: a stroke is positions, an evaluation is a revision, and a frame carries the revision it shows.

Vixen's panes composited with `PaintComposite` until this change, which is its #849. They now read the plan's output and that class is
gone, so there is one implementation.

## A stroke on a mesh

A stack names a model. **A stroke with a view is a list of positions on a picture and the camera it was seen through** (`StackView`, three
axes and an eye in the mesh's own space), so the journal needs no Worker, a drag survives one dying, and a client with any camera can
say it: Blender's is not Patina's. `POST /viewport/pick` asks the Worker what is under a position and hands back the camera, once, at
pointer-down; everything after it needs no answer. A stroke keeps the model that stood when it was painted, so binding another later
cannot move it.

Each stamp finds the surface with `PaintProjection.TryHit` on the Worker, clips the triangles near the hit to the square it makes on the
picture and weighs each texel where it projects (Vixen's `PaintMeshSurface`). A seam is two places in the atlas that are one place on the
mesh, so a stroke in the viewport crosses one without knowing, and lands on a separate shell as well. A stroke in the atlas floods across
edges the mesh really shares, which keeps it off the far side of a thin wall. Only covered texels are painted, each island is dilated 4
texels on its own, and a stroke reports one rectangle per island since their union is everything between them.

The radius of a stroke with a view is in the units of its picture, the client's layout pixels, so a brush is the same size on screen whatever
the render scale or the chart under it. On the housing at the default camera, 4K and a device pixel ratio of 2, 12 such pixels is about 48
texels of radius, and 24 about 96.

### Stamp to frame shown

`pnpm exec playwright test --config apps/desktop/e2e/bench.config.ts paint`, in a real Electron window (565×260 layout pixels, 1130×520
frames), S4's stack of 20 layers at 4096² on the housing's low mesh, painting the second layer through a soft brush, one pointer move per
16 ms. Time runs from a stamp leaving the pointer to the paint call of the first frame whose header carries its revision, so it includes
rAF. Debug-free Release, not published, so no ReadyToRun. Apple M1 Max, with other agents' builds running throughout.

| Brush | Load average | p50 | p95 | Worst |
|---|---|---|---|---|
| Radius 12 px, about 48 texels, four strokes | 21 to 61 | 99 to 100 ms | 299 to 368 ms | 620 to 736 ms |
| Radius 12 px, six strokes, the final build | 38 to 39 | 67 ms | 283 ms | 701 ms |
| Radius 24 px, about 96 texels, four strokes | 17 to 50 | 1.5 to 2.4 s | 2.0 to 3.2 s | 2.2 to 3.4 s |
| Radius 24 px, six strokes, the final build | 44 to 57 | 10.9 s | 12.6 s | 12.8 s |

**This does not meet S4's 50 ms in a real window, and the larger brush does not keep up with the pointer on this machine under this load.**
No idle run exists. The parts, from a probe inside the Worker on the larger brush: composite 8 ms, reading back and laying rectangles in the
viewport's texture 30 ms, a frame 5 ms, all per cycle and steady under load because the GPU is not what is contended. The rest is stamps: one
stamp at 96 texels is 6.4 ms alone in `patina-bench stamps` at load 26, and 50 ms inside a Worker at load 55, at the same counters. Stamps are
CPU on one thread, and a machine with eight performance cores under a load of 50 divides them. Strokes are never skipped, so a drag that
outruns them queues, and the queue grows with the length of the drag, which is the 2 s and the 11 s. Whether that is the load or a Worker-only cost that I failed to find is not settled: the 9 times between
those two numbers fell as the load fell, but the two were not measured at the same moment. The first thing to do is run the bench on an idle
machine, and the second is stamps across threads.

Three findings came out of it. Bounding a stamp's scan by a ball round the hit cost 30 ms a stamp at a hard edge, on a mesh of thirty
triangles, because a face seen side on is ten radii deep under the brush; a triangle is now clipped by the pyramid through the stamp. A dilation
scanned the stamp's rectangle a few times over to find nothing, so it now looks only at blocks that can hold a gutter. And a repair is on whole
256² tiles, most of which a stamp did not touch, so the viewport is sent only what the strokes reached plus a margin of two texels. A repair's
ops read at most one texel away or it would not be a repair, and an undo, which says nothing about where, sends the whole rectangle.

## What it costs

- A 4K stack holds a dense 67 MB texture per painted channel, because the kernels read one image. `PaintTileCache` saves uploads and
  not memory, and it has no budget.
- A non-local filter above the stroke is slow in proportion to the channels it paints.
- An export and a viewport both depend on `TextureLocality` being right, and a kernel listed wrongly leaves stale texels. It is short
  for that reason, and `LayerStackEvaluatorDeviceTests` goes red the moment Blur is added to it.
- Depth under a brush is a limit, not an occlusion test, so a texel hidden by other geometry close under the brush is painted.
- A UV outside the unit square is not wrapped, so a mesh across UDIM tiles paints only the first.
- A stack paints one texture set, and `TextureSet.Mesh` is not read: the whole model is the surface.
- While a viewport-worn drag runs, previews stop and the channels' pictures catch up when it ends, since an encode per map per stamp is what
  S4's end-to-end p95 was spent on. A drag in the atlas keeps them, since they are what it watches.
