# Architecture decision records

One numbered file per decision, `NNNN-short-title.md`, saying what was decided, why, and what it costs. The P0 spikes
(plan §6) each end in one. The go/no-go on Raven plus MoltenVK belongs here and has not been recorded yet.

- [0001: Library index](0001-library-index.md): spike S6, the index design and its measurements.
- [0002](0002-kernel-compile.md): kernel compile time and the SPIR-V cache (S2).
- [0003](0003-frame-transport.md): frames to the renderer (S3).
- [0004](0004-crash-isolation.md): crash isolation of the Worker (S5, viewport part).
- [0005](0005-preview-cache.md): where node previews live, and what a slider costs end to end.
- [0006](0006-node-graph-scale.md): `@xui/node-graph` at 500 nodes with live thumbnails (S7).
- [0007](0007-graph-authoring-references.md): how compounds, bitmaps, ramps and curves resolve, with the file as the truth.
- [0008](0008-baking.md): bake throughput (S9, failed), bakes as Worker jobs beside the viewport, and a managed EXR codec.
- [0009](0009-painting.md): stroke latency (S4), dirty regions in the plan, the journal that lets a Worker die mid-stroke (S5).
- [0010](0010-packaging.md): native libraries in a package (S8, failed for Vulkan on macOS), and Assimp with nothing installed.
- [0011](0011-export.md): presets as files, the green each source and engine uses, and a batch rebuild that is a plan from keys.
- [0012](0012-documents.md): accepted: a project holds typed documents, each with its own workspace, joined by a material contract.
- [0013](0013-paths.md): accepted: names in library files stay inside their folder, and API paths are the caller's.
- [0014](0014-graph-size.md): accepted: a graph's size is separate from what it is authored at, with one list of sides.
- [0015](0015-words.md): accepted: the API sends labels with its words and codes with its refusals, and each client words them.
- [0016](0016-commands.md): accepted: one command table that the menus, the keys, the shortcuts dialog and tooltips read.
- [0017](0017-filter-inputs.md): accepted: a filter's first image is its subject, every image names where it reads, and a node says when it is no filter.
- [0018](0018-library-care.md): accepted: a bin in the library, one thumbnail service for every list, and preferences in plain files.
- [0019](0019-export-presets.md): accepted: the export preset language exactly, presets in a project, the editor that writes them through the reader, material files beside the textures, and the compression preview.
- [0020](0020-texture-set-settings.md): accepted: a texture set's settings live in its stack, a resolution change rewrites no paint, and a set chooses its UV set.
- [0021](0021-decals-and-paths.md): accepted: a decal and a path are a fill's projection, kept as numbers and evaluated, never stamped.
- [0023](0023-bake-shading.md): accepted: a bake shades its texture set beneath its layers, and a set says when its bake no longer fits its mesh or its UV set.
- [0025](0025-stroke-control.md): accepted: a stroke carries the last one on from where its brush stood, a brush is pictured at a size of its own, and a material brush lays its maps as a fill does.
