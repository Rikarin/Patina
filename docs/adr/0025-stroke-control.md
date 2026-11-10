# ADR 0025: A stroke carries the last one on from where its brush stood, and a brush is pictured at a size of its own

**Status:** accepted, 2026-11-10 (#123, #125, #126, #127).

## Context

#126 asked to "resume the previous stroke", and its status comment left it undecided: the reference documents no
behaviour for it. Two other questions came with the brush work of this wave. #123 wanted a stamp preview, which needs a
picture of a brush something has to draw. #125 wanted a material brush whose size does not follow the UV layout, and
#127 a material picker that takes every channel rather than one channel's colour at preview resolution.

What a person lifting a pen and putting it down again sees today is two strokes. Where they meet, the second lays a stamp
under the pointer at once, so a low-flow brush that builds up shows a dark knot at the join; and a lazy brush's string is
cut, so the second stroke jumps to the pointer rather than going on from where the paint stopped.

## Decision

**Carrying a stroke on is the pen lifted and put down again in the middle of one stroke.** A free stroke can say where
the brush already stands (`StackStroke.Resume`, two numbers in its stamps' units). Vixen's `PaintStroke` starts there
without a stamp: the first stamp comes a step along, so nothing is laid twice where the two strokes meet, and a lazy
brush keeps its string. A pen put down again has no pressure from before it was lifted, so the first stamps take the
first pressure rather than tapering from 1.

- **It is still an undo step of its own.** Joining the two into one step would make a lifted pen undo more than the
  person just did, and would make the Worker hold a finished stroke open, which a Worker that can die must not.
- **The client decides when, and the brush says whether.** `StackBrush.Resume` is the setting (Carry on the last
  stroke), off by default, since a stipple pressed twice in one place means two dabs. With it on, the app sends `Resume`
  when a press lands within the brush's size and its lazy string of where the last stroke's brush stood, under the same
  camera and on the same layer. Where the brush stood is what the app already works out for a Shift-click line, the same
  `Trailed` arithmetic Vixen uses. Another client decides the same way or never sends it.
- **The spacing runs on from where the brush stood, not from the last stamp.** The client cannot know the leftover
  distance the Worker carried, so the first stamp may come up to a step later than an unbroken stroke's would. At the
  default spacing that is a sixth of the radius, which no one sees; nothing is laid twice, which is the defect.
- **In the journal.** `Resume` travels with the stroke's first part and the journal keeps it, so a replay starts the
  stroke where it started.

**A brush's picture is a stroke of it, drawn by the Worker at a size of its own.** `POST /stacks/{id}/brush-preview`
asks the Worker to draw one stroke of the brush along a wave, its pressure rising and falling, white on nothing, into the
preview cache; the client is given its hash. Everything a brush has but its size is a share of its radius or none of it,
so drawn at one radius the stroke shows what the brush is at any, and a 400-texel brush does not draw as a white bar.
It is per stack because an alpha that is a picture is found relative to the stack, as a stroke finds it. It needs no
device and no stack loaded in the Worker, only that folder, so it draws with no GPU and while a stroke evaluates.

**A material brush lays its maps as a fill does.** Its source takes a fill's `projection`, `planarAxis` and `placement`,
and the one-layer stack it reads is that fill. So Triplanar lays a material in the model's space at one size wherever the
stroke goes, and a material painted is the same size as the material filled. ⚠ A noise in it is another noise than the
same graph filled: a noise's picture follows the id of the layer it is in (Vixen #875), and that layer is the brush's.

**A material picker reads numbers, not a picture.** `POST /stacks/{id}/sample` asks the Worker for one texel of every
channel of the composite at full size, as the plan stores it; the picker takes the painted channel's value as the colour
and the rest as Also paints. A mask still takes its grey from the picture on screen, since the composite has no mask.

## Consequences

- The Worker's wire gains `StackPaintPart.Resume`, `stack.sample` and `brush.preview`.
- A face angle (`StackBrush.Facing`, Vixen's `PaintOcclusion.Facing`) is measured per triangle, at its middle. On a mesh
  of large triangles the edge of what it keeps is the triangles' edges.
- Quick mask (#127), tangent and planar stamp alignment and additive height (#123) are not decided here.
