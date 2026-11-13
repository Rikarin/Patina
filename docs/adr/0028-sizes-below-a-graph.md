# ADR 0028: Sizes below a graph, and an export's own size

**Status:** proposed, 2026-11-12, for issue #96. A compound's own base and an export's size are built (2026-11-13); the rest is a
design only.

## What this answers

ADR 0014 separated a graph's size from what it is authored at, and left five things. This records how each should work,
so that the work can be done in pieces without one piece deciding the others by accident.

1. An export's own size, chosen in the Export dialog, resamples the finished maps instead of computing the graph at it.
2. A compound's own base is ignored when it is inlined.
3. Sources stretch on a non-square image.
4. There is no size per node, per compound instance, or relative to a parent.
5. A layer stack's base still means what a graph's used to.

## The model

One rule holds every part together: **a radius is counted in its own graph's base, and a size only ever says how many
texels that base is computed at.** Everything below follows from keeping that rule at every level of nesting.

### An export's size computes the graph at it

An export run that names a size computes every `texture-graph` output at that size, the way a preset output's `size` and
`patina bake --size` already do, and resamples nothing. The build plans the run's outputs with that `size` setting, so
they are keyed apart from the project's own and cached once per size, and the export step finds maps already the size it
writes. Resampling stays for what has no graph behind it: a bake's maps, and a picture. The warning that a map was
enlarged stays for those, and goes away for graphs.

A layer stack is painted at its set's size, and painted texels cannot be computed at another: an export at another size
resamples a stack's maps as it does now, and says so when it enlarges them.

*Built (2026-11-13).* The size is the run's own, from `ExportOptions.Size` and a set's `PerSet` size over it, which is
what the Export dialog and `patina build --size` send. A preset's own `size` still resamples, because one build exports
through several presets and their graphs are built once for all of them. Sets merged into one set of files take the
export's size alone. A graph whose base is not square, or a side that is no whole step of its base, still meets a
resample for the last step, as a graph at its own size does.

### A compound has its own base

A compound inlined into a graph keeps the base it was authored at: a radius inside it is counted in the compound's
base, so that a Blur of 8 in a compound authored at 512 is as wide, measured against the compound's picture, inside a
host at 1024 as it was on its own. That is 16 of the host's texels.

*Built, and not as first designed.* The first design gave the inlined nodes a level of their own. A level changes how
many texels a picture has, not what a radius is counted in, so it would have computed the compound at half the size
and still read its radius in the host's unit. What Vixen does instead is scale every length a node dispatches (its
`TexelsAtBase` parameters) by the host's base over the compound's, as the node is compiled, which holds on a refresh
too. Any two bases work, so nothing is refused, and the compound computes at the host's size. A compound that declares
no base takes the base of what holds it, and a nested compound counts in the innermost base declared. Shipped
compounds are all authored at 1024, so none of them changes inside a graph authored at 1024.

### A size per node is a level offset

A node may say it is computed at a level relative to its graph: one step finer, two steps coarser. That is the only
per-node size there is, because a level is the only size a radius survives: an absolute pixel size on a node would mean
something else at every graph size. A coarser node feeding a finer one is resampled up where they meet, by the
compiler, never silently. The graph's own size still moves every node together.

A compound instance takes the same offset, applied to every node inside it on top of its own base.

### Relative to a parent is the same thing

"Relative to parent" is a level offset of zero, which is what every node already is. "Absolute" sizes are not offered,
for the reason above. A node whose input is a bitmap may still say *as the picture*, which reads the picture's own size
and is the one place an absolute size enters; it is resampled to the node's level where it meets the graph.

### Non-square images keep shapes round

A source draws in the image's unit square stretched to the image, as Designer does, unless it says otherwise. That
stays the default, because a non-square texture is almost always mapped onto a non-square face and the stretch is what
the face undoes. A source that draws a shape gains *Keep aspect*, which draws in a square of the shorter side's unit and
tiles that across the longer one, so a Disc on 2048×1024 is round. It is a setting per source, not a graph setting,
because a graph may want both.

### Stacks

A stack's set has a size and nothing else: its paint is texels at that size (ADR 0020). A filter or generator in a stack
is a graph and follows the rules above, computed at the set's size with radii in its own base.

## What it costs

- The compiler gains a level per node instead of one per plan. Plans are already built per size; a node with an offset is
  one more key in the plan's cache, and nothing reads a node's level but the compiler and the evaluator's resample.
- Every node that measures in texels already does so through the level, so no node changes, only what sets the level.
- A build caches a graph once per export size it is asked for.

## Order of work

1. The export's size computing graphs at it (Patina's build plan; no Vixen change). Built.
2. A compound's own base (Vixen's compiler). Built.
3. Keep aspect on the shape sources (Vixen's nodes).
4. Level offsets per node and per instance (Vixen's compiler, then the Inspector).
