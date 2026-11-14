# ADR 0021: Decals and paths are a fill's projection, evaluated, never stamped

**Status:** accepted, 2026-11-09 (#130, #131, #133, #134, with #135 alongside).

## Context

Four parity issues ask for things placed on the model and changed later. #133: a projection placed in 3D, moved,
scaled and turned again. #134: a warp on it before it lands. #130: a path whose points can be edited after the stroke
is seen. #131: a ribbon, a strip of a material or a picture laid along a path. Before this, a path was points on a
picture rasterised when the stroke finished, gone into the canvas; a projection was the brush painting a picture on the
screen, also gone into the canvas; and the fill projections (triplanar, planar, spherical, cylindrical) were fixed to
world axes or the model's bounds.

There were three places such a thing could live:

1. **A layer type of its own**, with a canvas the Worker stamps from the decal or the path whenever it changes.
2. **A mask entry**, so a path or a decal says where a layer shows.
3. **A fill's projection**, beside the four the stack already has.

## Decision

**A decal and a path are two more members of `LayerProjection`: `Decal` and `Path`.** A fill layer reads its box from
`LayerAsset.Decal` (a `LayerDecal`) or its curve from `LayerAsset.SurfacePath` (a `LayerSurfacePath`), and Vixen's
stack compiler wires the fill's content through one node, `Space/Decal` or `Space/Surface Path`, over the same
`position` and `world` bakes a triplanar fill reads. The node's output is transparent wherever the decal or the path
does not reach, so the layer composites nothing there, under its own blend, opacity and mask.

- **Nothing is stamped.** Moving a decal or a point is a `setLayer` with the new numbers: an edit to the document that
  undoes, redoes, saves, syncs to every client and replays after a lost Worker like any other, with no journal entry and
  no canvas. Undo and history need nothing new: the History list names the step *Move decal* or *Edit path*.
- **It is evaluated on the device, by the one evaluator** (plan §3.2). A decal is a box projection: a texel's place on
  the model, read from the `position` bake, is taken into the box's frame, culled by its normal against the box's
  facing and faded through the depth. A path covers each texel by its distance in space from the polyline, against the
  width there, with the path's normal as the guard against the far side of a thin wall; a ribbon reads its picture along
  the arc length and across the side. The curve is `PaintPath`'s centripetal Catmull-Rom, now evaluated in three
  dimensions, and reaches the kernel as a small 32-bit table carried in the plan, so no host resolves anything.
- **The fill decides what lands.** A constant fill along a path is a stroke of that colour, a texture fill a picture, a
  graph fill a material in every channel: #131's "a strip of a material or a picture" is a graph or texture fill with
  Ribbon on. A constant fill is meaningful here, unlike under the axis projections, because a decal and a path decide
  where the layer covers.
- **The warp is part of the decal**: a 3 by 3 grid of where the picture's grid lands on the decal's square, inverted
  cell by cell in the kernel with the four-corner warp's quadratic.

### Why not a layer type with a canvas

A canvas stamped from data is a cache of a document, and every host that evaluates a stack would have to make it: the
Worker, `patina build` and a Blender client's export alike. It would be resolution-bound (#19), stale whenever the
mesh or its bake changes (#204), and an edit to a point would replay strokes. The projection is a pure function of the
document and the bake, which every host already has.

**What it costs:** a path is a distance field and not a stamped brush. It has a radius, a hardness, pressure per point
and an angle limit, but no brush alpha, spacing or jitter. A stamped path with the full brush engine is still the
`path` curve of a stroke, which rasterises into a paint layer as before. Distance is in space, not along the surface,
so on a sharply curved part a wide path cuts across rather than following the surface; the normal guard keeps it off
faces turned away.

### Why not a mask entry

A mask entry has no content of its own, and #131 asks for a material laid along the path. As a projection the same
path is a mask too: a fill with a path projection in a layer's mask stack is not built, but the node is a graph node, so
a mask generator can use it.

## Later additions (wave 11)

- **A picture dropped on the mesh** is a decal on a new fill: the picture is brought into the project as Choose… brings
  one, the fill is a Texture fill writing only the base color, and the decal is placed as Place on Mesh places one, as
  tall as the picture's shape. Two edits, as any new fill's content is.
- **Warp to Geometry** is the 3 by 3 warp computed, not a new kind of projection: three rows and three columns across the
  decal's square are found on the mesh through the viewport's pick, and the middle point of each moves to where half
  the surface's length along it is reached. A 3 by 3 grid cannot undo a symmetric stretch (both halves of a cylinder
  turn away alike), only an uneven one, such as a decal over an edge; a finer grid would be a change to the decal's
  kernel. ⚠ The surface is found along the camera's rays, not the decal's facing, so it is measured best looking at
  the decal; a pick outside the box's depth or facing away counts that stretch as flat.
- **A ribbon over itself**: the kernel walks the polyline in passes, each run of segments that reaches a texel, and lays
  each pass over the ones before it; within a pass the nearest segment wins as before. A stroke still takes the nearest
  segment alone.

## Later additions (wave 14)

- **Stamps along a path** (#130): a stroke's `Spacing`, a share of the path's full width, makes it a row of round stamps
  rather than one band, still evaluated and never stamped into a canvas. Each texel finds the nearest place on each
  segment as before and is measured in the path's own frame there (how far along, across, and what is left over: off
  the surface, or past the path's end), against the stamps either side; stamps stand at whole spacings from the start
  and none past the end. `Jitter` moves each stamp by a hash of its number, so a path jitters the same way on every
  host. ⚠ The spacing is never closer than a tenth of the width, which keeps the stamps one segment weighs for a texel
  within the kernel's loop of 32. A ribbon takes no stamps: its picture runs along the whole path.
- Still not done: a brush alpha on the stamps (it needs a second picture input on the node and a picture on the
  path), erase and smudge along a path, a filled path, snapping, a Paths panel, and distance along the surface.

## The box

`position` is 0 to 1 along each axis of the bake's own bounds, so a decal written in that space is stretched on any
model that is not a cube. **A decal and a path are written in the model's own units, with the box the bake is
normalised in beside them** (`Box`, six numbers). The client sends the model's bounds as `POST /viewport/pick` gives
them, which is the bake's box when the maps are baked from the model itself.

**The bake's own box wins** (wave 11). A bake from a high mesh whose bounds differ from the low mesh's, a model swapped
for one with other bounds, and a position map written with another range (`cube`, `sphere`, `none`, ADR 0008) all left
the stored box wrong and moved the decal by the difference. The baker knows the box exactly, so the bake's manifest
records it as `PositionBox`, and every host that evaluates a stack with that bake (the Worker, `patina build`) reads every
decal and path through it (Vixen's `LayerStackEvaluator.PositionBox`). Nothing in the document is rewritten: the box is a
fact about the bake, not the decal, and the stored one is only the fallback for a bake made before the manifest said it.

## UV sets

A decal and a path never read a texture coordinate. They read the `position` and `world` bakes, and a set's bake is
made in the set's own UV set (ADR 0020), so a set laid out in a mesh's second UV set gets its decal there. ⚠ A bake
made before the set's UV set changed is still in the old layout and nothing marks it out of date, so a decal reads it
until the set is baked again.

## #204 and the mesh

#204 (strokes bound to the surface surviving mesh edits) is the owner's to decide. This keeps it open and makes it no
harder: a decal and a path are stored in the model's space, not in the atlas, so a new unwrap or a new resolution
changes nothing about them, and a remesh that keeps the shape keeps them where they were. The stored box is the one
thing a mesh change must update, and a future `StackSetModel` could do that when it binds a model with other bounds.

## Smart materials and masks (#135)

Saving a layer, a folder or a mask goes through Vixen's `SmartMaterial.Extract`, the one place that decides what
survives a change of model (painted pixels do not; a mask that read them becomes a zero). A smart mask is a layered
material document with the role `mask` (a role a layered material may now carry), holding one fill layer whose mask is
the one saved. Applying one to a layer that has a mask merges by default: its base multiplies over the layer's mask as
one more entry, its entries follow with their own operators, and its effects run after the layer's own, over the whole
mask. That last part is a compromise: a mask's effects apply to the whole composited mask and an entry carries none, so
a merged smart mask's blur also blurs the mask it joined. A decal or a path saved in a smart material goes with it,
placed where it was on the model it was saved from.
