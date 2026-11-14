# ADR 0043: A quick mask across texture sets (proposed)

**Status:** proposed, 2026-11-14, for issue #282. Nothing is built: clear and undo across sets are the owner's to choose
(questions at the end), and the model below does not depend on which way they go.

ADR 0037 made a quick mask the texture set's: a canvas in one stack session's journal, painted and kept to in that set's
atlas. A model with several sets is painted one set at a time, so a mask painted over a seam between two sets keeps
strokes on one side only, and switching sets loses it. A quick mask is a selection on the model, so it should follow the
model.

## Proposal

- **The Server keeps the model's quick mask strokes**, beside the stack sessions, keyed by the texturing document's
  project and mesh: the list of quick mask strokes that went through a view (`quickMask: paint` with `View`), in the
  order they were painted, each with the set it was painted in and its stroke id. It is session state, as ADR 0037's
  canvas is: nothing saves it, and it is lost when the last set of the model closes. The Worker keeps nothing new, so a
  Worker that dies loses nothing (plan §3.1).
- **Each set's quick mask is laid from that list.** A stroke through a view is a set of positions on the mesh's picture
  that the Worker finds on the surface, so the same stroke laid in another set's session paints that set's atlas
  wherever the brush's disc falls on its faces, and nothing where it falls on no face of the set. The set it was painted
  in lays it as now; every other worn set gets it as a journal entry on its own quick mask canvas, which is not an undo
  step of that set (an echo). A set opened later lays the list when it is worn, as a new generation of its quick mask.
- **A stroke in the atlas stays the set's.** UV space means nothing in another set, so a quick mask stroke with no view
  paints only the set it was painted in, as it does today, and is not on the model's list.
- **Keeping is unchanged.** A stroke or a Keep Layer to Quick Mask in a set reads that set's canvas as it stood when it
  began (ADR 0037), which already holds every echo laid before it. Fills, decals and paths keep through
  `keepQuickMask` as they do now.
- **An echo is never taken out of a journal.** ADR 0037 clears by moving to a new file because a stroke kept to the old
  canvas names it, and an entry emptied in place is history an undo cannot see past. The same holds here: when the
  model's list changes other than by a new stroke at its end, every other set's quick mask moves to a new generation and
  the list that stands is laid on it. A stroke already kept in that set keeps naming the generation it was kept to.

The wire needs nothing new: an echo is a stroke the Server already knows how to send. The cost is replay time on a set
that opens while the list is long, the same cost a set's own journal has.

## What the owner chooses

1. **Undo of a quick mask stroke.** (a) *Per set, recommended:* the stroke is an undo step of the set it was painted
   in, and undoing or redoing it there re-lays the other sets' quick masks as new generations from the list that stands.
   Undo in another set never reaches it, and history stays one per set as it is for every other edit. (b) *Model-wide:*
   quick mask strokes form one history shared by every set, so Undo in any set can take back a stroke painted in
   another. That is a second undo stack beside each set's, which Undo History would have to show and interleave.
2. **Clear.** (a) *The model's, recommended:* Clear empties the list and moves every worn set's quick mask to a new
   generation, since the mask is the model's. (b) *The set's:* Clear in one set takes back only the strokes painted in
   it, and the other sets re-lay what is left.
3. **Show and hide.** Whether showing the quick mask in one set shows it in all (recommended, as it is one mask), or
   stays each set's flag as it is now.

## Not part of this

- A quick mask across documents of different meshes, or across projects.
- A quick mask kept in the file: it stays a selection that no save writes (ADR 0037).
