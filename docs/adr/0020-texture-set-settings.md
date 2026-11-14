# ADR 0020: A texture set's settings live in its stack, a resolution change rewrites no paint, and a set chooses its UV set

**Status:** accepted, 2026-11-09, for issues #19, #141, #146, #210 and #231.

## One place for a set's settings

A texture set is a texturing document on one material of the project's mesh (ADR 0012), and its settings live in that
document's stack file, the `.vxlayers`, and nowhere else. `project.yaml` says which material a document paints; it holds
no setting of the set.

| Setting | In the stack file | In `StackState` | Changed by |
|---|---|---|---|
| Name | `sets[].name`, the material | `set` | the mesh (a renamed material is a reload) |
| Resolution | `baseWidth`, `baseHeight` | `width`, `height` | `setSettings` |
| UV set | `sets[].uvSet`, written when not 0 | `uvSet` | `setSettings` |
| Height to normal | `sets[].heightNormal` | `heightNormal` | `setSettings`, or `setHeightNormal` |
| Channels and their defaults | `sets[].channels` | `channels` | `addChannel` |

`setSettings` takes any of width, height, UV set and height to normal, and is one undo step, *Texture set settings*.
Export, the bake and the checks read these from the stack, so a client never sends them twice. A width and a height are
each one of `TextureSizes.Sides`, so a set may be 2048 × 512. This is the set's size, the one it is authored at; an
export's size for the set (ADR 0019, `perSet`) is the longest side its files are written at, keeps the set's shape and
changes nothing here. The checks measure the set at its own size and count its memory at the size the export writes.

## Resolution: canvases resampled reversibly, not strokes replayed (#19)

Changing a set's resolution rewrites no canvas. The two ways to keep painting non-destructive were to keep every stroke
and draw it again at the new size, or to keep the pixels and never lose any of them. We keep the pixels:

- **A stroke is not a self-contained thing to replay.** It reads a brush preset, an alpha picture, a projected picture,
  a clone's composite, the mesh and its layout, all of which change after it; and the GPU kernels that stamp it change
  with Vixen. A replay would paint something else as often as it painted the same thing, and the journal (ADR 0009) is
  a session's, rebuilt from the canvases, not a history that outlives them.
- **The evaluator samples an image at its own size.** A canvas of any size is uploaded as it is and read at the set's,
  so a set made smaller shows and exports its paint filtered down, and made larger again shows it exactly as before.
- **A canvas grows, exactly, when it is next painted.** `PaintCanvas.Enlarged` repeats each texel over the texels it
  covers, so at a whole factor (every change between the offered sides) each block averages back to the texel it came
  from, and no value appears that was not painted.
- **A canvas never shrinks.** A stroke on the model paints one larger than its set, of the set's shape, at the canvas's
  own size (#19): it is placed by the picture, not by texels, so its stroke entry carries the canvas's size and a
  brush sized in texels is scaled to keep its size. A stroke in the 2D View, whose stamps are the set's texels, is
  carried onto the canvas's texels by the Server before it is journalled (`StackStrokeScale`, #281): its stamps, the
  rest of its drag, where it resumes, a brush sized on the map or in texels, a stencil's and a picture's place and
  scale and a clone's offset are multiplied by the canvas's width over the set's. A texel's middle is at its index and
  a half on both, so it lands where the brush went. A canvas of another shape, larger than the set on either side, is
  first enlarged to the smallest size of the set's shape that holds it (`StackCanvasSides.Holding`: the set's size
  times the larger of the two ratios, canvas over set), each texel repeated as any enlargement is, and is then painted
  as a canvas of the set's shape. Scaling a stroke by two factors instead would keep the canvas smaller, but a brush's
  turn, an alpha, a stencil and a projected picture would all shear, which no stroke field can say. Only a canvas that
  would pass the largest side that way is refused (`stroke-canvas-larger`).
- **A stroke's undo holds the pixels it painted over**, so a canvas a stroke of the open session began on is not
  enlarged under it; that stroke is refused, saying to save and open the document again. Each stroke carries the set's
  size it was made at (`StackPaintPart.Width`, `Height`), so a Worker replaying a session after a crash paints an old
  stroke at the size it was painted at.

What this costs: a layer painted at 1024 and enlarged to 4096 has 1024's detail until it is painted again, where a
replay would have redrawn it sharp. And a layer painted at 4096 is painted again only at 4096.

### Bit depth

Paint stays RGBA8 (`PaintImage`). A 16-bit or float channel needs a second texel width through the tile cache, the
stroke kernels, the undo snapshots and the `.vxpaint` format, and the evaluator already holds 16F and 32F results, so
the decision is to add depth to a *channel* in Vixen's paint image, chosen per channel in the set's settings, and to
treat today's 8-bit canvases as that channel's first format. Nothing in Patina depends on the width of a texel beyond
`PaintImage.BytesPerTexel`, which is where it starts. Not done in this ADR.

## UV sets (#210)

`ModelReader` keeps every coordinate set beside the meshes when asked (`ReadModel.TexCoordSets`), and `MeshData` does not
change shape. A set's `uvSet` is the file's channel number; wherever Patina reads a set's layout it swaps that set into
the place of the first:

- the Worker's strokes, its UV-tile check and the set's checks (`MeshShelf.Of(path, set, uvSet)`);
- a bake's low mesh and cage (`BakeRequest.UvSet`; left out, the Server reads it from the project's set on that
  material, so a client baking several sets need not open each, and for a bake that names no material, from the one
  document on that mesh, which is how a mesh of one material is baked);
- through that bake, a decal and a path (ADR 0021), which read the `position` and `world` maps and so land in the
  set's own layout;
- the export's coverage, which its padding grows from;
- Preserve Paint's move to a new mesh;
- the viewport, which reads the mesh again with each worn set's layout (`viewport.open` carries `UvSets`).

A part of the set without the chosen UV set is refused by name, never painted in the first. A pick on the model says its
place on the map in the UV set of the material it met (`viewport.pick` carries the worn sets' `UvSets`, wire version 17),
so Find Layer, the color picker and the clone source read the set's own layout.

**Carrying paint to another UV set.** A set's paint is texels, so a set moved to another UV set would show it wherever
the new layout puts those texels. `carryPaint` (one undo step with the `setSettings` that moves it, wire version 19)
makes every canvas of the set a new canvas the Worker fills as a copy of the old one laid out again on the surface
(`StackCarry` on a copy's journal entry, `PaintTransfer.Move` between the set's triangles in the two UV sets), as
Preserve Paint moves paint to a new mesh. New canvases rather than the old ones rewritten, because an undo then only
names the old canvases again, and a replay after a crash makes the copies the same way. The same command projects
paint laid out in any of the mesh's UV sets onto the set's own. Painting across a UV set boundary is not done.

## What a mesh is (#146, #231)

- **Side files.** A model is read from its bytes, and every file it names beside itself (an OBJ's `mtllib`, a glTF's
  `.bin`) through a resolver, `SharedFile.Beside`, which reads relative to the mesh while sharing write and delete.
  Before, Assimp looked for them in the process's working directory, so an OBJ's materials were all its default grey.
- **One list of formats.** `MeshFormats.Meshes` is what a project's folder, Import and every file dialog take: glTF,
  FBX, OBJ, COLLADA, PLY and STL. A test reads a triangle of every one.
- **Facts.** `GET /library/mesh?path=` asks the Worker (Assimp may crash) for a mesh's parts, materials with the UV sets
  all their parts share, triangles, vertices, box, and metres per unit where the format or file says: glTF by its
  specification, FBX by its `UnitScaleFactor`. OBJ, PLY and STL never say. Shown in Project Settings and New Project.

## UDIM: a texture set paints one tile (#20)

A texture set paints the one UDIM tile all its triangles sit in, which need not be 1001. Its layout is moved home onto
1001 by whole squares (`UvTile.Home`, in Vixen) wherever Patina reads it: the viewport's mesh and so every stroke, pick,
part mask and 2D view (`ViewportMesh.Read`, per material across all its parts), and every bake, coverage and check
(`MeshMapBakeRun.Placed`). A set's maps are therefore always one square, and nothing below the readers knows a tile.
The tile travels with a stack's build object (`BuildManifest.Udim`) to its export set and files, where `{udim}` and
`{tile}` name it.

- **Why one tile per set, and not a set of maps per tile.** A set with several tiles needs a canvas, a coverage, an
  evaluation and an export per tile, through paint, the evaluator, the viewport's live maps and every check. One tile
  per set reuses all of that unchanged, and the common UDIM layout of a game or film asset already gives each tile a
  material. A material that reaches several tiles stays as it was: said out loud when its set opens, its bake refused,
  and its strokes past the first tile landing nowhere, with "give each tile a material of its own" as the way out.
- **Merging** (#211) is of sets sharing one square, so sets on different tiles merge into one set of files per tile,
  and only when every file the preset writes names its tile with `{udim}` or `{tile}`: otherwise the files of two tiles
  would be one file, and the sets are written one by one with a sentence saying what to add (#281).
- **Before a build.** The Server reads a set's tile from its mesh in its UV set (`SetUdim`, once per file while it stays
  as it was), as it reads a set's faces for its bake, so a stack's state carries `udim`, the Texture Sets pane shows it,
  and a preset's file list names each set by its tile (#281).
- **A build object made before a change to what it holds** keeps its key, since a tool's version moves only with the
  product's. `OutputKeys.Generation` is in every output's key and is raised with such a change; it was raised to 2 for
  the tile in the manifest.
- **Not done:** a set across several tiles.
