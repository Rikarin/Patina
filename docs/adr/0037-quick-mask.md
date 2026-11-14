# ADR 0037: A quick mask is a journaled canvas the session holds, kept to in the atlas, and never saved

**Status:** accepted, 2026-11-13, for issue #127.

A quick mask is a mask the artist paints, shown red over the model, which later strokes are then kept to: inside what
was painted, or outside it once inverted. It is a selection, not part of the material.

- **It is a canvas the journal paints**, like a layer's: a stroke with `quickMask: paint` paints the session's quick mask
  instead of a layer, by the red of its colour, and is an undo step like any other. A Worker that dies paints it again
  from the journal, so nothing new has to survive a Worker (plan §3.1).
- **No layer names it, and no save writes it.** Its file is named beside the stack (`<stack>.<set>.quick-mask-<n>`), so
  a stroke can reach it as it reaches any canvas, but the Worker leaves every quick mask out of a save. A checkpoint
  keeps it, since a Worker that takes over has to read it. Files stay the truth: a quick mask is lost when its session
  ends, as a selection is.
- **A stroke is kept to it in the atlas.** Vixen's `IPaintKeep` scales each texel's weight and the stroke's cap by the
  quick mask's red at that texel, as a stencil does on the picture. The journal entry names the quick mask it was kept
  to, so a replay reads that one as it stood when the stroke began, which the journal's order brings back.
- **Clearing moves to a new file** rather than emptying the old one. A stroke kept to the old one still names it, and an
  entry that emptied it in place would be a step of history that undoing an earlier stroke could not see past. A quick
  mask nothing has painted keeps nothing back, so a client that keeps to an empty one paints as if it had none.
- **The viewport shows it** in a slot of its own beside the material's maps, sampled by the forward shader in every view
  and laid over what is shown in red. It never reaches a map of the stack, so no export can include it.

## Not yet

- A quick mask is the texture set's; a stroke in one set is not kept by one painted in another.
- A fill layer, a decal or a path is not kept to it: only strokes are.
