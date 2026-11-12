# ADR 0031: The viewport's lens is scaled to the model, it has no temporal filter, and a stroke reaches what was shown

**Status:** accepted, 2026-11-12 (#31, #136, #137, #138, #149).

## Context

Four viewport issues were left with parts that were less work than decision. #138 asked for depth of field and
temporal anti-aliasing beside the post effects that landed; its status comment asked whether the two should be closed
rather than kept open. #136 found that strokes still projected onto parts the viewport had isolated away, although
clicks no longer did. #137 wanted the 2D view to draw the UV layout, which only the Worker can read from the mesh. #149
wanted the skew drawn inside the faces, where `MapBaker.Reach` is silent. And #31's last status comment left nothing
open but subsurface, clearcoat and a path tracer, which are #142 and #143.

## Decision

**Depth of field is Vixen's physical gather, through a lens scaled to the model.** `DepthOfField.rvn` takes a focal
length, an f-number and a sensor in metres, which is right for a camera in a world and wrong for a viewport whose model
has no unit it trusts: a real lens on a model a metre across, at the distance that frames it, focuses deeper than the
model. So `ViewportFocus` scales the sensor with the bounding radius, as if the radius were five centimetres. The blur at
a setting is then the same on any model, and it is the shallow focus of a small thing close up, which is the shot an
artist takes of a material. Two settings and no more: Depth of field opens the lens from f/22 at nothing to f/1.4, and
Focus moves the focus from the point the camera turns round by up to a bounding radius either way. The lens's numbers
are not settings, since a focal length and a sensor in a viewport with no unit would be numbers no one could choose.

- **It runs on the light, before the bloom and the curve.** The forward pass resolves its light and its nearest depth
  apart when the lens is open, and the gather writes the frame's HDR picture, so nothing after it changes. With the lens
  closed the frame is exactly what it was, which a test holds to the byte.
- **The blur stops at ten pixels.** A gather of 64 taps wider than that shows a sharp edge as copies of itself. A large
  screenshot is therefore a little sharper than the pane, which is the honest cost of a gather. A near field composited
  apart, which would let a blurred foreground spill over the sharp, is two more passes and not worth them here.
- **A view of a stored value is never blurred**, as it has no curve or effect.

**There is no temporal anti-aliasing, and #138 closes without it.** The viewport draws one still picture of one model at
4x MSAA, and only when something changes. A temporal filter needs a run of jittered frames for every one of those
pictures, which is the cost of an animation for a picture that does not move. What TAA would smooth, a specular line
crawling under a moving camera, the viewport never shows, because it renders afresh when the camera stops.

**A stroke on the mesh reaches what the viewport drew alone when it began, and keeps that.** The Server writes the
viewport's isolation into a stroke's journal entry and sends it with the stroke's parts; the Worker's surface for the
stroke is the set's triangles that isolation shows. So a part isolated away takes no paint and hides nothing from the
brush, and a replay after a Worker restart reaches the same faces whatever is shown by then. The Server attaches it
only when the viewport shows the stack's own model, since an isolation names parts of the mesh on view and says nothing
about another. A stroke in the 2D view keeps to the whole layout.

**A drawing of lines goes through the preview folder like a picture.** The UV layout of a set with a hundred thousand
triangles is megabytes of numbers, and the socket carries hashes. The Worker draws it as an SVG over the unit square,
each edge once, under its hash beside the node pictures, and `GET /stacks/{id}/uv-wire` sends that file. SVG rather than
a picture of lines, because a line in it stays a pixel wide at any zoom of the 2D view.

**A bake's preview draws the skew with Vixen's arithmetic, not a copy of it.** `MapBaker.Leans` gives rays from a grid
inside each triangle, leaned by the skew and its map through the same `Leaned` the texel bake calls, so the preview and
the bake cannot drift apart. Missing seams on hard edges are found in Patina, since they are a fact of the low mesh's
file and not of the bake.

## Consequences

- The Worker's wire gains `mesh.uv-wire` and `StackPaintPart.Isolation`.
- #31 has nothing left that is not #142 or #143: shadows, occlusion on the light and specular occlusion landed, and
  subsurface and clearcoat need the channels the material model decision brings.
- Not decided here, and left open in #136 and #137: isolating a UV tile, which waits on painting beyond tile 1001
  (#20, #82); a highlight on the mesh of what a selection covers; renaming a texture set or giving it a description,
  which needs a set's name apart from its material's and a word from export naming; and the brush ring that follows
  its alpha, with a stamp preview.
