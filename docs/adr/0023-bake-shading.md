# ADR 0023: A bake shades its texture set, and a set says when its bake no longer fits

**Status:** accepted, 2026-11-10, for issues #86, #238, #89 and #151.

## The bake is beneath the layers (#86)

A bake used to reach masks and nothing else. A stack's default channels are base color, roughness and metalness, so a
low mesh baked well still showed flat in the viewport, and a set with a height channel exported the height's flat
slope as its normal, with the bake's normal nowhere. Show on Low Mesh laid a bake over the mesh, and any tab change
took it off again.

A texture set now reads its bake beneath its layers, in Vixen's stack builder (`LayerBakeMix`, on
`TextureSetAsset.BakedNormal` and `BakedOcclusion`), so the one evaluator does it for the viewport, the checks and the
export alike:

| Mix | Normal | Occlusion |
|---|---|---|
| Combine, the default | the set's normal (its channel, with its height's slope) reoriented into the baked one | the set's occlusion multiplied by the baked |
| Replace | the normal channel starts from the bake, and a layer that paints normal covers it | the occlusion channel starts from the bake |
| Off | the layers' alone | the layers' alone |

A set with no such channel writes the bake as that map. These are the set's settings, so they live in its stack file
(ADR 0020) and change through `setSettings`.

- **Only a bake the host has.** A mesh map no bake has given reads as grey, so the host tells the builder which of
  `normal` and `ao` its bake holds (`LayerStackEvaluator.Baked`), and a set never baked compiles as it always did. The
  Worker's session and an export's stack step read it from the bake's manifests (`BakedMapPicture.Shading`).
- **Tangent space only.** A normal baked in object space cannot be combined with a tangent-space one, so it is left
  out. The manifest records the space.
- **One convention.** A bake's normal points green up, as glTF reads it, and a set's green down, as `Height to Normal`
  writes it, so the bake is turned over on its way in and the set writes one convention; the export and the viewport
  already turn a set's normal for what reads it.
- **Pictures only.** Coverage, one layer's amount and a group's own composite read no bake: they are the layers' and
  nothing beneath them.

An export already read a bake as its floor, a map the stack did not write. That stays for every other map, and the
stack's normal and occlusion now carry the bake in them, so the floor no longer wins over a stack that has either.

## A bake fits a set while its mesh and its UV set are the ones it was made for (#238)

A delivered bake's manifest records the mesh's digest, as it did, and now the UV set its maps are laid out in. A set
whose mesh is another, or whose UV set is another, reads its maps in the wrong place: its shading, and the decals,
paths and mesh-map masks that read the bake per texel (ADR 0021). `BakeFit` says which; nothing rebakes on its own.

- `StackState.bakeStale` is the open set's, recomputed with each state, the mesh's digest kept while its file stays the
  same. Layers' banner reads it before the project's.
- `ProjectDocumentInfo.bakeStale` is each document's, for the Bake dialog, which marks each set to bake again.
- `bake-stale` is a check, an error, whose target opens the Bake dialog.

A set's bake was already keyed by its UV set, since the low mesh is read in it (#151), so baking again after a change
of UV set is a new bake, and changing it back finds the old one in the cache.

A set whose mesh changed elsewhere keeps a fitting bake. The manifest also records the material baked onto and the
revision of its faces in the bake's UV set, read as the bake key reads them (`BakeKeyReads.Geometry`), and a bake of
another mesh digest still fits while the mesh file has those faces (`BakeFit.SameFaces`). Library cannot read a mesh,
so the Server hands it the reader (`BakeFit.Geometry`); unset, and for a bake made before the faces were recorded, any
change to the mesh is a change to every set, as before. The project's own word on a bake folder narrows the same way,
and Layers takes the open set's word over the project's.

## What is not done

- The bake still has one side, `BakeOptions.Resolution` (#147).
