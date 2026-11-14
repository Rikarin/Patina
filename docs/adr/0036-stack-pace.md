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
  save waiting. The Worker compiles the full-size plan for each of them whatever the proxy, and goes back to the proxy for
  the next edit. That costs a compile each way, which is cheap beside the evaluation and keeps one rule: what is written
  is what the stack is, at its size.
- **A proxy repairs nothing in tiles.** A canvas at full size feeding a plan at a quarter is of another extent than the
  op that reads it, and the evaluator already runs such an op whole rather than guess which texels moved. The proxy wins
  on everything that is not under the brush; under the brush it costs a whole run at a fraction of the texels.
- **Not per layer.** A layer at its own proxy would make its composite with the layers around it a resample at every
  boundary, and the evaluator's content keys would differ by layer. The document-wide divisor gives most of the win.

## Not yet

- A cache of each layer's result that survives a save and a restart, kept out of the project file. The evaluator's
  results live in GPU memory and go with the Worker; keeping them needs the plan's content keys written beside the
  result in `.ptcache`, which is evaluator work in Vixen.
