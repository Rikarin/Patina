# ADR 0036: A stack's pause and proxy are the session's, and nothing that reads its pixels sees either

**Status:** accepted, 2026-11-13, for issue #208.

A big stack is slow to set up: each step of adding and arranging dozens of layers evaluates the whole picture. Two
controls help, and both are how a session evaluates, not what the document is.

- **Pause.** Edits are taken, journalled and undoable as ever, and the Server does not hand them to the Worker until
  evaluation resumes; then everything waiting is evaluated at once. The viewport and the previews keep the last picture.
- **Proxy.** The stack's plan is compiled at its width and height divided by 2, 4 or 8, so every fill, filter and blend
  runs on a quarter, a sixteenth or a sixty-fourth of the texels. Painted canvases stay at full size; the plan samples
  them down. A stroke still lands in the canvas at full resolution, so nothing painted under a proxy is lost.

## Decisions

- **The session holds both, the file neither.** A proxy is a preference about this machine's speed, and a pause is a
  moment in a session. In the file they would make diffs that say nothing about the material, and a teammate would open a
  stack paused. Both are in `StackState.pace` and set with `PUT /api/v1/stacks/{id}/pace`, and every client hears a
  `paced` event, so a second client (Blender) shows the same.
- **Anything that reads pixels is never paused and never smaller.** A save, an autosave, an export, a check, a sample
  and a read of the pixels hold the session evaluating while they wait (`StackSession.Reading`), so a pause cannot keep a
  save waiting. The Worker evaluates each of them at full size whatever the proxy, and keeps one rule: what is written
  is what the stack is, at its size. (Amended for #288.) Under a proxy the full-size stack is a second evaluator kept
  beside the proxy's, rather than the proxy's plan compiled at full size and back: that cost a compile pair per
  autosave on a big stack, and the stroke after an autosave waited for the second. The second evaluator uploads the
  canvases again, and is let go when the proxy goes back to full size.
- **A proxy repairs in tiles too** (amended for #279). A canvas at full size feeding a plan at a quarter is of
  another extent than the op that reads it. Only `Bitmap` reads a picture of another size, and it samples each texel of
  the plan at its centre scaled into the canvas, one texel across, so Vixen scales the rectangles a stroke changed into
  the plan's texels, a texel or two wider on each side, and repairs there (`TextureLocality.Resamples`). Any other op
  that meets an input of another size still runs whole. A stroke under a proxy is then a repair of a sixteenth of the
  texels, and a property test checks that the scaled rectangle covers every texel whose read reaches the change.
- **Not per layer.** A layer at its own proxy would make its composite with the layers around it a resample at every
  boundary, and the evaluator's content keys would differ by layer. The document-wide divisor gives most of the win.

## A cache per layer that survives a save and a restart (#208)

- **What each layer hands the next is kept in `.ptcache/results`, by its content key.** Vixen's result cache has a
  disk tier the host places (`ITextureResultStore`): the evaluator asks it for any result it wants and does not hold,
  and uploads what it finds instead of running the op. A save writes the stack's outputs and its handovers (each image
  one layer's op writes and another layer's op reads), at full size, so a stack opened in a later session, or on a
  Worker that replaced a lost one, runs only the layers an edit has changed since.
- **Written at a save, never per edit.** A write is a read-back per result, which is what the GPU cache exists to keep
  off a stroke. Under a proxy a save keeps nothing unless the full-size stack is already evaluated beside it.
- **One shelf per adapter, driver and build.** A key names no device, and byte identity is only proved on one, so the
  folder is named after all three. Each result is a small header and the texels, compressed; the shelf lets the least
  recently used go past 8 GiB. It is a cache: deleting it costs time and nothing else, and no project file names it.
