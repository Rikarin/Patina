# ADR 0014: A graph's size is separate from what it is authored at

**Status:** accepted, 2026-11-07, for issue #96.

## What was wrong

Vixen has always had two numbers. `baseWidth` and `baseHeight` are the unit a radius is counted in, and `BakeLevelOffset`
says how big this run makes the image. Patina set only the first, so Width and Height in Graph settings moved the unit. A
Blur of 8 at 1024 became half as wide at 2048, and so did every Warp and Slope Blur: a size change was a different material.

## The model

- **`baseWidth` and `baseHeight` are the authoring base.** They set the unit of every radius and the shape of the image.
  Changing them changes the picture, and Graph settings says so under *Authored at*. A new graph is always authored at
  1024, so a radius means one thing across a library.
- **`size` is the longer side a graph is evaluated at.** It is a graph setting, saved in the file with the others, and a
  graph with no `size` evaluates at its base. Changing it recompiles, because a plan is built for one size, and it doesn't
  change the picture.
- **One list of sides.** `TextureSizes` holds 256 to 8192, `GET /api/v1/system` reports it with the default of 2048, and
  Graph settings, New Graph, New Project and Bake all offer what it says. The three lists before it stopped at 4096, started
  at 512, or both.
- **One path.** `GraphSize.Apply` is how the Worker, a build and `patina bake` all set a compiler's level. A `texture-graph`
  output's `size` setting and `patina bake --size` override the graph's own for one run.

`size` only takes whole steps of the base. Vixen refuses anything else: a 1024 graph at 1536 has no level, and two levels
would make a radius wider in one axis than the other. A side that is no step of the base takes the nearest one, which only
happens with an odd base from a hand-written file, or a size that came from somewhere other than the list.

## Why it is in the file

Vixen's `TextureGraphSettings` keeps a bake's size out of the file on the ground that a saved one is somebody's preview
resolution saved by accident. That holds for Vixen, whose hosts pick a size per bake. In Patina a graph's size is what it
exports at unless a build says otherwise, the person chose it in Graph settings, and files are the truth. It stays out of
Vixen's own settings: the compiler doesn't read it, and `GraphSize` tells the compiler the answer.

## What this leaves

- A compound's own base is ignored when it is inlined, so a compound authored at another base has radii in its host's
  unit. New graphs all being 1024 narrows this, and it doesn't fix it.
- Sources stretch on a non-square image: a Disc on 2048x1024 is an ellipse.
- There is no size per node, no size per compound instance and no size relative to a parent.
- Export has no size per run in its pane. A build can already say one, through the output's `size` setting.
- A layer stack's base still means what a graph's used to. Stacks aren't part of this.
