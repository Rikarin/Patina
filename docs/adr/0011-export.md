# ADR 0011: Export and batch rebuild (P6)

**Status:** accepted, subject to review, 2026-11-05. **Exit (plan §6):** edit one shared material, and the library rebuilds only
what changed and writes engine-ready files. Met through the real Worker on a generated library, at the scale stated below. No file
has been opened in Unreal, Unity or Godot; that is the largest gap, and it is listed per engine.

## A preset is a file

Factory presets are in `content/export-presets` and are embedded. A library adds one, or replaces one of the same `id`, by putting
a file in `export-presets`; it needs no sidecar, because its `id` says what it is. A preset lists files, and each file lists
one to four lanes from a map (`roughness.r`), one minus a map (`1-roughness.r`) or a number. A map is one of a graph's Output
usages or a bake's map under its own name, so `curvature`, `thickness`, `id`, `position`, `world` and `bent` can be packed; a bake's
`ao` arrives as `occlusion`. A texture none of whose maps a project has is left out, and a lane whose map is
missing takes what a surface without it would be, with a warning in the report. Unknown fields, values the reader cannot read (`mips: True`) and lane counts a block cannot hold
are refused: the alternative is an uncompressed texture in a shipped game. An export's key holds the bytes of the preset's file.

`{project}`, `{preset}` and `{set}` are the whole naming language. A value cannot make a folder. In a project of several texture
sets every file has to say which it is, so a template with no `{set}` gets the set's name after `{project}`, or at its end when it
has neither. A project of one set keeps the name its preset gives, and `{set}` is then the project's name if the set has none.

## What a project builds

A project made in the app writes no outputs. `ProjectOutputs` works them out from its documents on every read, and writes nothing
back, because an output in a file would have to follow every change to the documents:

- Each texturing document is a texture set: one `layer-stack` output and, if it has a bake, one `baked-maps` output.
- A project with no texturing document is a material, and each of its layered materials, image materials and material graphs is a
  set in the same way. Mask, brush and filter graphs are not. Nor are the graphs in a texturing project, which are what its layers
  read and not what it ships.
- `outputs` in `project.yaml` replace all of this, for a project that wants something else, such as mesh LODs.

**An export never bakes.** It takes the bake that is there (`DocumentBakes`: the one chosen for the document, then the folder a bake
of it goes to by default), so a project that has not baked exports what its layers make, and a preset's normal file is reported
as skipped. A bake started by Export would run for minutes on settings nobody chose. `library check` counts declared outputs only:
a project nobody has exported is not unhealthy.

An output's key holds its set's name, through its settings, so two sets that swap their content do not share an export's key.
Outputs are merged per set when an export reads them, and an output with no set is every set's. A document's key covers what it can
read in its project: other documents, `pictures/`, compounds and every file of its bake, which is more than it does read. An edit
to one of those rebuilds a little too much, and never too little. What it reads in other projects is covered by the project's
`references`.

## Green, and why it is not a constant of a preset

Every map's rows run from `v = 0`, so a picture is never turned over: every engine below reads the top row of a file as the top of
the picture, and each converts a mesh's `v` to fit on import. Green is another matter, and the two sources in Patina disagree:

| Source | Green | Why |
|---|---|---|
| A bake | **up the image (Y+)** | `MikkTSpace.Generate` turns `v` over before it runs, so the bitangent runs up the picture (ADR 0008) |
| A graph's `Height to Normal` | **down the image (Y-)** | Vixen's frame takes the bitangent along `v`, which runs down. `Vixen.TextureGraph`'s README derives it, and a ramp test holds it |
| A layer stack | **down the image (Y-)** | What fills its normal channel is a graph, and the compositor leaves the numbers as they are |

So a normal map carries the way it runs, in its output's manifest, and `ChannelPacker` turns green over only when source and target
disagree. An output that composes a baked normal says `green: up` in its settings; nothing can work that out. The stored number is
the same one the viewport's shader reads, so this is a one-minus in unit space and not a sign flip of the byte. `Export Maps` writes
a stack's numbers as they are, so its normal is Y-.

The viewport's shader reads Y+ and takes a sign: `-1` for a graph's maps (`PUT /viewport/graph`) and for a stack on the mesh, and `+1`
for a bake. A stack's green was once declared Y+ and shown Y+, so the two agreed with each other and both disagreed with its fills.

## What each engine reads

"Documented" means the page says so. Where it does not, the row says what was relied on instead.

| Target | Green | Tangent basis | Packing |
|---|---|---|---|
| Unreal | **Y-** (flip for our bakes), not stated by Epic in those words | MikkTSpace, by practice | Occlusion R, roughness G, metallic B: a common convention, not an Epic document |
| Unity URP | **Y+**: "Unity uses Y+ normal maps" | MikkTSpace is the importer's default | Metallic in RGB, smoothness in alpha, the default Smoothness Source; occlusion in its own file |
| Unity HDRP | **Y+**, as above | As above | Mask map: metallic R, occlusion G, detail mask B, smoothness A, linear. Documented |
| Godot 4 | **Y+**: "X+, Y+ and Z+", "OpenGL style". Documented | Mikktspace when it generates tangents, and it reads a file's own | Occlusion R, roughness G, metallic B: the three are documented, the order is not |
| Vixen | **Y-**, from its README and its ramp test | ⚠ **Not MikkTSpace:** the importer takes Assimp's tangents | Occlusion R, roughness G, metalness B, read from `TexturedOrmSurface` |

Sources: Unity's [normal map page](https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/built-in/shader-built-in-configure-properties/standard-shader-texture-maps/material-parameter-normal-map/map),
[URP Lit](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/built-in/shaders-in-universalrp/reference/lit-shader),
[HDRP mask map](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/Mask-Map-and-Detail-Map.html) and
[`ModelImporterTangents.CalculateMikk`](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/modelimportertangents/calculatemikk);
Godot's [StandardMaterial3D](https://docs.godotengine.org/en/stable/tutorials/3d/standard_material_3d.html),
[BaseMaterial3D](https://docs.godotengine.org/en/stable/classes/class_basematerial3d.html) and
[import configuration](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/import_configuration.html);
Epic's [naming conventions](https://dev.epicgames.com/documentation/unreal-engine/recommended-asset-naming-conventions-in-unreal-engine-projects),
[texture properties](https://dev.epicgames.com/documentation/en-us/unreal-engine/texture-properties?application_version=4.27) and
[Interchange import reference](https://dev.epicgames.com/documentation/en-us/unreal-engine/interchange-import-reference-in-unreal-engine).

**Uncertain, and marked so in the files:**

- **Unreal.** Epic documents a Flip Green Channel option and does not say "DirectX" on the pages read. Y- rests on long practice, and
  one old forum post disagrees. `T_` is Epic's; `_D`, `_N`, `_ORM`, `_E` and `_H` are community habit. PNGs need sRGB off on import
  for `_N` and `_ORM`, which the importer's normal detection handles for one and not the other. A preset cannot say so.
- **Unity.** The occlusion lane is read from green by long practice. The page does not say; a one-lane file is grey, so it cannot
  matter. Unity imports a PNG with its own `v`, and this assumes its importer agrees with glTF's v-down rows, as a glTF importer must.
- **Godot.** The ORM order is taken from glTF's. Its pages name the three and not their order.
- **Vixen.** A bake is in MikkTSpace, and Vixen's importer computes Assimp's per-vertex tangents, so a bake exported to Vixen is off
  by the difference of the two bases on a curved surface. Green is right. The Vixen engine has no MikkTSpace import path yet.

## Colour

Plan images have no transfer function. Vixen's material bake puts a graph's bytes straight into an sRGB texture and Patina's viewport
reads them so, whatever `TexturePixels` says of linear. So **base colour is sRGB numbers at any depth**, and an sRGB file holds them
unchanged, which is what the viewport showed. Everything else is linear, emissive included, because it goes past one, and the
viewport's emissive slot reads it so. A linear file of base colour decodes it. A float file holds linear numbers.

`PUT /viewport/look` views frames through a `.cube` table in the Worker, on the tonemapped, sRGB-encoded frame, after read-back. It
is a viewing aid and no export sees it. A table is taken to expect those numbers, which the format does not state. OCIO stays out.

## Block compression and files

All of it is in Vixen, one implementation: every BC7 and BC6H mode, wide mip chains, KTX2 and DDS. Results are at 1024² against
closed-form pictures, Balanced, against the single mode this replaced:

| Image | Old | Balanced | Target |
|---|---|---|---|
| Gradient, BC7 | 54.69 dB | 58.70 | 50 |
| Two-colour edges, BC7 | 52.39 | lossless | 90 |
| Three-colour edges, BC7 | 26.52 | 58.61 | 57 |
| Filtered noise, BC7 | 32.46 | 37.68 | 36.5 |
| Independent alpha, BC7 | 30.14 | 33.68 | 33 |
| Normal map, BC7 | 37.40 | 44.37 | 43.5 |
| HDR light edge, BC6H | 69.26 | 71.49 | 70 |
| HDR signed, BC6H | not possible | 64.47 | 63 |

Noise, the worst case, at Fast, Balanced and Best: 0.50, 2.71 and 6.29 s at 2K, and 1.91, 9.97 and 26.81 s at 4K, on ten cores. The
bit-level check is a real decoder: an Apple GPU agreed with Vixen on every block of every mode, which refuted Vixen's claim that Metal
has no BC formats. Vixen's `Core.Imaging` README has the corpus. `vixen` is the only factory preset that block-compresses. The rest
hand PNGs to importers that compress for themselves, so `dds` is available to a preset and no factory one uses it.

An object stores maps at the depth they arrived, as PNG for bytes and EXR for floats, through the public `TextureBake.ReadRaw`.
Mips run before blocks, at 16 bits or float, so a 16-bit height is averaged at 16 bits.

## How a build runs

Keys, which P3 made, do almost all of it. `BuildPlan` reads each project once, works out every output's key and each preset's export
key, and compares them with what exists. It writes nothing and runs nothing, so **a preview is the plan and a restart is planning
again**: what an attempt finished has its object, and is simply not in the next plan.

- **A build is one job**, not one per output. Ten thousand job files would each be a write on every change, and `GET /jobs` would hold
  them all. The cost is that `GET /jobs` shows one row.
- **The Server plans, delivers and reports. The Worker runs steps**, one at a time, through `build.start`, answering at once as a bake
  does, since a block compression can outlast any reply. GPU steps take a gate shared with graph and stack requests, because an
  evaluator is one thread's at a time.
- **Results:** an output's object is `objects/ab/<key>`, written last, beside its files in `<key>.d`, so `library check` needs no new
  rule. An export's object holds files under neutral names, and a project's name goes on when they are delivered, so two projects with
  one key share one encode. A folder holds `.patina-export.json`, which names its key and files; delivery removes what the last export
  made and this one does not, or a game would ship a stale file.
- **A journal bounds a crash loop per step.** `.ptcache/reports/<build>/journal.jsonl` records a step before it goes to a Worker. A
  step started three times and never ended is reported failed and passed over, where a bake's three deaths fail the job, because one
  Worker lost in ten thousand steps says nothing against the rest. It also carries the report across a restart, since the second plan
  no longer shows what the first attempt did.
- **Interactive work goes first.** A bake passes a queued build, and a running build lets bakes by between steps.
- **`patina build`** runs the same pipeline in-process, and opens a device only if a step needs one.

A `.part` folder is cleaned by whoever fails, and left by whoever is killed; nothing collects those yet, and nothing collects objects.

## Fan-out

`patina-bench fan-out <folder> 10000 500 512 unreal,vixen`, Release, an M1 Max with other builds running (load average 40 to 60). The
library is one shared graph read by every project, each with a noise scale of its own, and the edit is the graph's seed.

**The pipeline at 10,000 projects, with stand-in maps.** Nobody has run 10,000 real encodes: four 2×2 maps take the place of the GPU,
and everything else is real: keys, objects, journals, encodes of those maps, delivery and reports. The scale repeats every 5,000
projects, so half the outputs share a key, which is the sharing working.

| | Result |
|---|---|
| Preview over the shared graph | 889 ms: 10,000 projects, 10,000 to build, 20,000 exports |
| First build | 35.7 s, 5,000 ran and 5,000 reused, 40,000 files |
| Rebuild after one edit | 60.9 s, same counts |
| Second build | 3.1 s: nothing built, nothing written, all 10,000 fresh |

**Real pixels at 500 projects:** MoltenVK on the M1 Max, a seven-node graph at 512², both presets, so four PNGs and four KTX2 files
per project, 4,000 files. A rebuild after the edit took 186.7 s, which is **374 ms a project**, and a second build took 0.10 s. The
first build took 167.8 s. At that rate 10,000 projects would be about 62 minutes on one machine, which is arithmetic and was not run.
How that divides between GPU, compression and files was not measured.

The rise from 35.7 to 60.9 s is not understood; delivery over existing files and a larger journal are the suspects. The journal is
opened per line, which is the first thing to change past this scale.

## What was not done

- **Layer references (plan P6) are not built, and need a decision.** A paint layer's file is a path relative to its stack, which
  `LayerStackEvaluator` resolves, so a stack could already point at a shared file with `..`. That breaks "references are GUIDs", and
  the fix touches Vixen's stack format, the Server's stack sessions and the Worker. So this stopped at the finding.
- **Graph resolution per project.** A graph's size is its own setting; a project cannot ask for 2K of a 4K graph.
- **A bake does not mix with a stack.** A map a stack makes replaces the bake's whole: a stack's `occlusion` channel is not multiplied
  over the baked one, and its `normal` is not combined with the baked normal. Mixing belongs in the plan, as a slot under the stack,
  so that the mesh and the export are one picture (#86). The viewport shows a bake under a stack only through Show on Low Mesh.
- **Mesh LODs** still take an output written by hand: no default says how many levels a project wants.

## Not verified

No file has been loaded in Unreal, Unity or Godot, and none of the three is installed here. The KTX2 files of a real export passed
`ktx validate --warnings-as-errors`, and DDS was read by `sips` and FFmpeg for the formats they read. Windows and Linux are CI's to prove. All GPU numbers are one
Apple GPU under load. An engine's own compression of these PNGs is outside what this measures.
