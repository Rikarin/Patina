# Architecture decision records

One numbered file per decision, `NNNN-short-title.md`, saying what was decided, why, and what it costs. The P0 spikes
(plan §6) each end in one, and the go/no-go on Raven plus MoltenVK is decided here.

- [0001: Library index](0001-library-index.md): spike S6, the index design and its measurements.
- [0002](0002-kernel-compile.md): kernel compile time and the SPIR-V cache (S2).
- [0003](0003-frame-transport.md): frames to the renderer (S3).
- [0004](0004-crash-isolation.md): crash isolation of the Worker (S5, viewport part).
- [0005](0005-preview-cache.md): where node previews live, and what a slider costs end to end.
- [0006](0006-node-graph-scale.md): `@xui/node-graph` at 500 nodes with live thumbnails (S7).
- [0007](0007-graph-authoring-references.md): how compounds, bitmaps, ramps and curves resolve, with the file as the truth.
- [0008](0008-baking.md): bake throughput (S9, failed), bakes as Worker jobs beside the viewport, and a managed EXR codec.
