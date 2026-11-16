# ADR 0052: A graph reads the canvas being painted, and a texture set's layers as a node wait on a choice

**Status:** accepted for a graph reading paint as it is painted, 2026-11-16, built for issue #205; proposed for a
texture set's layers as a node of a graph, which is not built.

Issue #205 asks for paint, layers and graphs to be one document. Open as Graph (wave 17) writes a set's layers as a graph
beside them, and the graph reads each paint layer's `.vxpaint` through a `vxpaint:` reference. Two things were left: a
painted mask feeding a graph live, and a set's layers as a node inside a graph.

## Decided: a graph reads the canvas being painted

- **The Worker reads the painting session's canvas in place of the file.** A stack session already lends its canvases
  to the other sessions of its Worker, so an instance in another texture set follows the strokes (#108). A graph session
  now asks the same question: when a stack session of this Worker paints the `.vxpaint` a graph names, the graph uploads
  that canvas's channel, unsaved strokes and all, and stamps it with the channel's revision. Nothing new is held: the
  canvas is the stack session's, the Server tells a new Worker everything again, and a graph that loses its stack
  session reads the file.
- **A stroke is noticed as a picture that moved, not as a value.** A stamp from a canvas goes stale when the channel's
  revision changes, when the painting session ends, and a file stamp goes stale when somebody begins to paint the file.
  A stale stamp makes the graph compile again, exactly as a picture file changed on disk did; the plan rule (plan §3.2)
  is about values, and a stroke is pixels.
- **The Server says when to look.** When a set's strokes reach the Worker, and when its session closes, the Server
  evaluates every open graph in the same folder that reads paint (a node's text is a `vxpaint:` reference). A graph
  reads paint only from beside itself, so no other graph can be reading it.
- **No pixels move outside the Worker.** The canvas is read on the device's turn, as strokes are laid, and the graph's
  previews go through the cache as before.

The cost is a full upload of the channel at each evaluation while painting. Tiles are not carried across yet; a graph
reading a canvas the size of a 4K set uploads 64 MiB a pass, which is the first thing to measure if it shows.

## Proposed: a texture set's layers as a node

A node that stands for a set's layers, with an output per channel, can be built three ways. Each needs an owner's
choice, so none is built:

1. **Expanded at compile, like a compound.** The node names a `.vxlayers` and a set; the compiler asks Vixen's
   `LayerStackGraph.Build` for that set's graph and inlines it as it inlines a compound. One evaluator, no new plan
   kind, and value changes stay values. Open questions: the stack's paint is relative to the stack, so a graph in
   another folder needs paint read from the stack's folder, which today a graph refuses (a paint file is read from beside
   the graph only); the stack's mesh maps are its bake's, and the graph has a bake of its own; and a stack's own knobs
   (a fill's colour, a layer's opacity) need a way to reach the node as parameters, or the node has none.
2. **A picture the stack's session renders.** The node reads the open stack's composite as a picture, as a graph reads
   paint now. Cheap and live, but the graph then depends on a session being open, and a bake or an export of the graph
   has to open the stack itself.
3. **A stack written as a compound.** Open as Graph's output saved as a compound and used as a node. This exists today
   by hand, and it is a copy: an edit of the layers does not reach it.

The recommendation is the first, with paint read from the stack's folder for this node alone and the graph's bake used
for the stack's mesh maps, and the stack's knobs left out until a layer can publish one. That widens where a graph may
read a painted file from, and that is the decision the owner is asked for.
