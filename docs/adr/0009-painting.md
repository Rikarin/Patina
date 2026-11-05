# ADR 0009: Painting (spike S4, and who holds a stroke)

**Status:** S4 passed, 2026-11-05, on a machine under load and at the median. S5's painting half passed, with a journal. **Pass
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

The viewport does not exist yet in this form, so the test compares the composite and the export. That is the seam for it: a textured
viewport can compare a read-back of the texture it draws against the same digest. The maps a viewport wants are
`TextureBake.TextureOf(output)`, repaired in place, so no second copy of a picture is involved.

Vixen's own paint pane still composites with `PaintComposite` and so keeps the mismatch Vixen documents (its #849). Patina does not
use that class. Moving the pane onto `LayerStackEvaluator` closes it there.

## What it costs

- A 4K stack holds a dense 67 MB texture per painted channel, because the kernels read one image. `PaintTileCache` saves uploads and
  not memory, and it has no budget.
- A non-local filter above the stroke is slow in proportion to the channels it paints.
- An export and a viewport both depend on `TextureLocality` being right, and a kernel listed wrongly leaves stale texels. It is short
  for that reason, and `LayerStackEvaluatorDeviceTests` goes red the moment Blur is added to it.
- Painting is 2D: strokes use full coverage with no gutter, since nothing here sends a mesh to a stack yet.
