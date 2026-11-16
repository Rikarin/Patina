# ADR 0059: An atlas is texture sets sharing one UV square, baked set by set and written as one

**Status:** proposed, 2026-11-16, for issue #212. Nothing here is built yet beyond what already stands; the owner's
choices are under "Left to the owner".

#212 asks for an atlas bake: "consolidate materials or meshes into one texture". Wave 19 left it needing a design: where
an atlas's maps live, how each set reads one atlas, and whether export writes one texture across sets. Most of what an
atlas needs is already in Patina, and this ADR says so before anything new is made:

- **A set bakes on any UV set** (`BakeOptions.UvSet`, `BakeRequest.UvSet`), and a document paints on the one it chose,
  carrying its paint across when it changes (#210, wave 14).
- **A set's bake covers only its own faces** (`BakeRequest.Material`), into `bakes/maps/<set>`, keyed by what those faces
  read (ADR 0008's table), so a change to one set rebakes that set alone.
- **Export merges sets that share one UV square** (ADR 0011, #211): with `merge`, each set's maps go where its own
  coverage puts them, at the largest set's size, overlap reported, UDIM tiles refused.

## Decisions (proposed)

1. **An atlas is not a document and not a bake of its own.** It is texture sets of one mesh that share one UV square on
   one UV set, which the mesh's author laid out, as a game's atlas always is. Patina stores nothing called an atlas: no
   `atlases:` block, no folder, no manifest. What makes sets an atlas is their layout, which the mesh already says.
2. **Its maps live in each set's own bake folder**, `bakes/maps/<set>`, baked on the atlas's UV set. One folder per
   atlas was weighed and refused: a set's bake key, its staleness, Bake Again on one set and Maps baked elsewhere
   (#144) all work per set, and one folder would make each of them answer for the others. What a set's bake writes
   outside its own islands (padding over a neighbour's) is never read: the viewport lays a set on its own faces and a
   merged export takes each texel from the set that covers it.
3. **Each set reads its own maps; the atlas is assembled where texels meet, at export.** A set's layers, masks and
   generators read its own bake as now. Nothing reads "the atlas" while painting, so a stack never evaluates another
   set's texels, and an atlas costs no more to paint than its sets did.
4. **Export writes one texture across sets through `merge`**, as ADR 0011 already decided. An atlas is the case `merge`
   was made for; this ADR adds nothing to it. #252 (a merged export drops per-set sizes) is the one gap there, and it is
   its own issue.
5. **A bake of sets on a shared UV square is the same rays as one bake of the whole square**, since each set's rays
   start only from its own faces. A whole-square bake (`Material` null) would save only the per-bake set-up, and would
   lose per-set keys, so the bake plan keeps one request per set.

## Left to the owner

- **Atlases across meshes or overlapping layouts.** Packing sets whose layouts overlap, or several meshes, into a new
  layout needs a UV set Patina writes and a mesh Patina exports. Patina writes no mesh today, and whether it should is a
  product decision. Until then the way to such an atlas is to lay the UV set out in the modelling tool (UV1 for the
  atlas, UV0 for unique detail), point the sets at it, and merge at export.
- **Whether an atlas is remembered as one.** A project could say "these sets are an atlas" so Export merges them by
  default and Checks warn when their layouts overlap. Decision 1 says not, because the layout already says it; the
  owner may prefer the explicit word.
- **Painting across sets in one atlas** waits on #211's two questions (which layer a stroke lands on in each set, and
  how one undo spans sets).
- **Decal sheets** (a page of decals with masks and metadata) are not atlases of a mesh and are not decided here;
  whether a decal sheet is a document of its own was filed separately.

## Consequences

- No code, file or API changes with this ADR. `SetMergeTests` hold that two sets on one UV square are written as one
  set of files; no test yet bakes such sets first and merges their bakes, which an accepted ADR should add.
- #212 keeps open only the owner's choices above and decal sheets.
