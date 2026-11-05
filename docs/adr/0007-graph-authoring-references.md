# ADR 0007: What a graph's references mean to Patina

**Status:** accepted, 2026-11-05.

## Decision

Vixen leaves a host to resolve what a graph names. Patina resolves four things, each so that **the graph's file stays the
truth** and a Worker can be replaced at any moment.

- **Compounds come from a folder called `Compounds` beside the graph.** A graph in `shared/graphs` reads
  `shared/graphs/Compounds`; a compound reads the folder it is in. This is Vixen's own convention and replaces reading the
  graph's folder, which made every sibling a node in every palette and read them all at open.
- **A bitmap's reference is a path:** relative to the graph, then to the library. The Worker decodes it with Vixen's
  `StbImageDecoder`, the PNG, JPEG, BMP, TGA, PSD and GIF the engine reads. A file that will not read is a `PT0001` on its
  node, and the graph does not evaluate. Decoded pixels are kept up to 512 MiB under each file's length and time, because
  compiling loads them again after every structural edit, and everything kept can be read again.
- **A ramp or a curve is written inline, in the node's text setting**, where Vixen expects a reference:
  `ramp:0 000000ff,1 ffffffff` and `curve:r=0 0,1 1;g=...`, a channel left out being straight. There is no asset to
  track, a diff shows the edit, and two graphs cannot share one by accident. An empty setting means what it means in
  Vixen: black to white, the identity.
- **A shipped compound has no file**, so `POST /graphs` takes `shipped:<type>`, and the session refuses edits and saves.
  `GET /graphs/{id}/compounds` says which path opens which compound, so a client never builds these strings.

Boundary nodes stand for a graph holding this one. Opened on its own, a compound loses them in the Worker's copy, and the
inputs they fed take their defaults. That copy is the only place they go: the Server's document keeps them.

Saving a compound republishes the node types of every open graph that reads its folder: clients get a `typesChanged`
event, and the Worker takes the document again.

## Costs and open questions

- **The ramp table is made in Patina.** `TextureRamp` is internal to Vixen, so `GraphTables` fills the 256-entry row
  itself: curves through the public `CurveEvaluation`, ramps by mixing stops in the stored values, with no colour space
  chosen. Making `TextureRamp` public would remove the second copy of the row's layout, and a named gradient evaluator
  would settle the colour space. It is a Vixen change and was not needed to ship this.
- **The curve editor draws its own Hermite** in `libs/studio-ui`, mirroring `CurveEvaluation`'s automatic slopes. The
  picture comes from the bake, so a difference could only misdraw the line.
- **`patina bake` does not read these.** Its `PT0001`-shaped refusal stands: the resolver is in the Worker, and the CLI has
  no reference to it. Moving it needs a library both can use.
- **A shipped compound cannot be copied into a library** from the UI, and a user cannot author a compound's interface:
  there are no commands for ports, so a new compound can only hold what is written by hand or by Vixen.
- **Unsaved edits to a compound do not reach the graphs holding it,** and a compound changed on disk by another program is
  noticed only when a graph opens. There is no watcher on the folder.
- **A rename of a parameter leaves expressions naming the old one,** which then report a diagnostic.
- **A group or note is named by its place in a list**, so two clients editing them at once can address the wrong one. A
  stale batch is refused only when the place no longer exists. Vixen's file gives them no identity.
