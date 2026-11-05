# ADR 0007: What a graph's references mean to Patina

**Status:** accepted, 2026-11-05.

## Decision

Vixen leaves a host to resolve what a graph names. Patina resolves three things, each so that **the graph's file stays the
truth** and a Worker can be replaced at any moment. `Patina.Graphs` holds the first two's code, and the Worker and
`patina bake` both read a graph through it, so a graph that previews also bakes.

- **Compounds come from a folder called `Compounds` beside the graph.** A graph in `shared/graphs` reads
  `shared/graphs/Compounds`; a compound reads the folder it is in. This is Vixen's own convention and replaces reading the
  graph's folder, which made every sibling a node in every palette and read them all at open.
- **A bitmap's reference is a path:** relative to the graph, then to the library. It is decoded with Vixen's
  `StbImageDecoder`, the PNG, JPEG, BMP, TGA, PSD and GIF the engine reads. A file that will not read is a `PT0001` on its
  node, and the graph does not evaluate. Decoded pixels are kept up to 512 MiB under each file's length and time, because
  compiling loads them again after every structural edit, and everything kept can be read again.
- **A ramp or a curve is written inline, in the node's text setting**, where Vixen expects a reference:
  `ramp:0 000000ff,1 ffffffff` and `curve:r=0 0,1 1;g=...`, a channel left out being straight. There is no asset to
  track, a diff shows the edit, and two graphs cannot share one by accident. An empty setting means what it means in
  Vixen: black to white, the identity. **Vixen reads these, not Patina:** this began as a host's resolver and turned out
  to be general, so a compilation bakes the table itself through `TextureRamp`, and every host, `vixen texture bake`
  included, gets it for nothing. A wrong one is a diagnostic on its node, and stops were always mixed in the stored values.
- **A shipped compound has no file**, so `POST /graphs` takes `shipped:<type>`, and the session refuses edits and saves.
  `GET /graphs/{id}/compounds` says which path opens which compound, so a client never builds these strings.

Boundary nodes stand for a graph holding this one. Opened on its own, a compound loses them in the evaluating copy. A
number they fed takes its default. An image has none, so a noise stands in, or a compound with an image input would show
its author nothing. That copy is the only place this happens: the Server's document keeps its boundary nodes, and no
client's document holds the noise.

## Authoring a compound

- **The interface is commands on a batch**, `addPort`, `setPort` and `removePort`, beside the parameter commands that were
  already a compound's knobs. They undo, reach clients as a `delta.interface`, and any client can drive them. A rename
  keeps the wires on the boundary node, a removal takes them until an undo, and a kind they cannot carry is refused.
- **A boundary node's ports are read from the interface as it stands**, not from a registry, which only knew them as the
  graph opened. A graph that is in `Compounds`, or has an interface, is offered the two boundary nodes in `node-types`.
- **`POST /graphs/{id}/compounds` saves a selection** as a new file in the folder the graph reads, through Vixen's
  `SubGraphs.Extract`, and replaces it with a node as one undo step. An undo leaves the file: it is the author's by then,
  and other graphs may hold it. A name that is taken or ships is refused before anything moves.
- **`POST /library/compounds` copies a shipped compound** into a file under a name of its own, because Vixen refuses a file
  that shadows one. It names the open graph to sit beside, so that a client never parses a path to work out a folder.

A group or note is named by the identity Vixen's file now gives it, shared between the two and never handed out twice to one
model. A file from before it gets one on load. A batch's `ref` on `addGroup` or `addComment` says which it became.

## Patina and Vixen bake the same bytes

S1's comparison, `patina bake` against `vixen texture bake`, with each pinned to the same Vixen commit. M1 Max, MoltenVK, one
machine, three graphs: 11 maps of 11 are byte-identical by SHA-256.

| Graph | Holds | Maps |
|---|---|---|
| 512², 16-bit | a noise through a blur and a file compound, an inline ramp and curve, a bitmap | 3 |
| 1024², 16-bit | Brick, Grunge Rust, Blend, Height to Normal, Ambient Occlusion, Distance | 4 |
| 1024², 32-bit | the same | 4 |

The 16 and 32-bit maps differ from each other, so the comparison can fail. The bitmap's reference had to be spelled
differently, since Vixen's is an asset path, and the picture is the same file. Vixen packs roughness, metallic and occlusion
into one file and Patina writes each, so those usages were not compared. Windows and lavapipe were not tried.

Saving a compound republishes the node types of every open graph that reads its folder: clients get a `typesChanged`
event, and the Worker takes the document again.

## Costs and open questions

- **The curve editor draws its own Hermite** in `libs/studio-ui`, mirroring `CurveEvaluation`'s automatic slopes. The
  picture comes from the bake, so a difference could only misdraw the line.
- **A rename of a port breaks the wires of graphs that hold the compound**, in their files. Only its own boundary node
  follows, and a holding graph reports the input as unwired.
- **Saving a selection copies every parameter across**, as Vixen's `Extract` does, so one the selection does not use is a
  knob to delete. A saved compound's expressions then read its own copy, so changing the host's parameter stops reaching
  them.
- **`Extract` gives ports only to wires that cross.** A selection with nothing after it has no output until one is added.
- **Unsaved edits to a compound do not reach the graphs holding it,** and a compound changed on disk by another program is
  noticed only when a graph opens. There is no watcher on the folder.
- **A rename of a parameter leaves expressions naming the old one,** which then report a diagnostic.
