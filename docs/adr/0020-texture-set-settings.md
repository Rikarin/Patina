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
- **A canvas never shrinks.** Painting into one larger than its set is refused with a sentence that says to set the
  resolution back: painting it at the smaller size would throw its detail away.
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

A part of the set without the chosen UV set is refused by name, never painted in the first. Picks on the whole model and
Find Layer still read the first set; painting across a UV set boundary and set-to-set projection are not done.

## What a mesh is (#146, #231)

- **Side files.** A model is read from its bytes, and every file it names beside itself (an OBJ's `mtllib`, a glTF's
  `.bin`) through a resolver, `SharedFile.Beside`, which reads relative to the mesh while sharing write and delete.
  Before, Assimp looked for them in the process's working directory, so an OBJ's materials were all its default grey.
- **One list of formats.** `MeshFormats.Meshes` is what a project's folder, Import and every file dialog take: glTF,
  FBX, OBJ, COLLADA, PLY and STL. A test reads a triangle of every one.
- **Facts.** `GET /library/mesh?path=` asks the Worker (Assimp may crash) for a mesh's parts, materials with the UV sets
  all their parts share, triangles, vertices, box, and metres per unit where the format or file says: glTF by its
  specification, FBX by its `UnitScaleFactor`. OBJ, PLY and STL never say. Shown in Project Settings and New Project.
