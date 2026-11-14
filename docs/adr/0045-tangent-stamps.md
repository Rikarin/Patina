# ADR 0045: A stamp laid on the surface is weighed on the plane touching the surface under its middle

**Status:** accepted, 2026-11-14, for issue #123.

## Context

A stamp seen through a camera has always been the disc on the picture: every texel weighs what the brush weighs where
the texel projects. On a face that turns away from the camera that disc covers a long ellipse of the model, so a round
brush paints a streak on a slope and a stamped shape is foreshortened onto the model. Other painting tools offer a stamp
that lies on the surface instead. #123 asked for "tangent or planar stamp alignment"; ADR 0025 left it undecided.

## Decision

**A brush says what its stamps lie on: `camera` (the picture, as before) or `tangent`.** `StackBrush.Alignment`, Vixen's
`PaintBrush.Alignment` (`PaintStampAlignment`). A new brush is `camera`, so nothing painted before changes.

**Tangent is the plane that touches the surface where the stamp's middle meets it.** Each texel's point on the mesh is
laid on that plane, and the brush is weighed there in the picture's units at the stamp's depth. One plane, not a walk
along the surface: a geodesic stamp would cost a flood over the mesh per stamp and differ only where the surface bends
sharply under a single stamp.

- **Its right and up are the camera's, each laid on the plane.** So a face seen square on paints as it does under
  `camera`, and a fixed angle, a stroke-following rotation and a pen's turn mean what they mean on the screen. They are
  laid on the plane rather than crossed from its normal, since a mirrored copy's camera is left-handed on purpose and a
  cross product would turn its stamps over.
- **Only the weight is read on the plane.** A stencil, a projected picture and the occlusion test still read where each
  texel lands on the picture, so they stay fixed on the screen.
- **A texel reaches one radius off the plane either way**, where a stamp on the picture reaches as deep as the slope.
  That is how far a curve as tight as the brush falls away from the plane under it.
- **What a stamp scans is the picture of the box it can reach**, eight corners projected, and the whole picture when a
  corner is behind the eye. A plane seen nearly edge on reaches much further on the picture than its disc does seen
  square, and the square used for a stamp on the picture would cut it off.
- **A stroke in the 2D view takes no notice.** An atlas stamp is already measured along the surface by its seam walk.

## Consequences

- The Worker's wire is version 22, for the new field.
- On a strongly curved surface under a large stamp the stamp still lies on one plane, so its far side is thinner than a
  stamp wrapped round the surface would be.
- Additive height (#123) waits on #103 and is not decided here.
