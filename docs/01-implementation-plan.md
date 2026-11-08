# Patina: implementation plan

> **Patina is a working name.** Rename it before the first release, after a trademark check.
> Written 2026-11-04 against Vixen `feb856973`, xUI `b2978c67`, Cathode `b062314` and Skala 4.4.0.
> Nothing in Vixen was built or run for this plan: the findings come from reading code. Phase 0 tests them.

Patina is a material authoring suite: procedural graphs (Substance Designer's job), painting on meshes (Substance
Painter's job) and baking, all in **one application** with **one evaluator**, as InstaMAT does. What sets it apart is
the **Library**: one per game, holding thousands of objects that share materials, with incremental batch rebuilds.

---

## 1. Findings that shape the plan

1. **Vixen already contains a working version of the product's core.** It has a GPU texture graph (49 nodes, 49 kernels,
   35 compounds), a layer stack that compiles to that graph, smart materials, a real 3D paint tool, nine baked map types,
   UV unwrapping and packing, and a CLI that bakes a graph with no UI. The plan is therefore a **split of Vixen plus four
   rewrites**, not a greenfield build. The code stays in Vixen and Patina consumes it (§4).
2. **The four rewrites are where the real work is.** Vixen doesn't have these, and each one changes a core design:
   - **Incremental evaluation.** Vixen re-evaluates the whole graph on any change, and a parameter change rebuilds the plan.
   - **Live full-stack painting.** The paint pane shows one layer, composited source-over only, so painted layers with any
     other blend mode, a mask or per-channel enables bake to something different from what was under the brush.
   - **Sparse tiled storage**, which 4K to 8K UDIM painting and 8K graphs need.
   - **A production baker**: cage and skew, supersampling, threads, match-by-name and 16/32-bit output.
3. **Vixen's GPU stack is clean and worth keeping, not forking.** Graphics, RenderGraph, Vulkan and Raven are separable.
   Raven already compiles node-generated shaders at runtime, in-process, in pure C#.
4. **Raven is the strategic lock-in.** All 49 kernels are Raven and have no CPU twin. Raven emits SPIR-V and Vulkan GLSL
   only, so the Vulkan path is the only proven one, and macOS means MoltenVK. There is no native Metal backend.
5. **Hardware ray tracing can't be a dependency.** Vixen's README says MoltenVK exposes neither `VK_KHR_ray_query` nor
   `VK_KHR_acceleration_structure`, and that the RT device test "has run nowhere". I could not confirm MoltenVK's current
   state from the web. The plan assumes no hardware RT on Mac, so the bake path has to work without it.
6. **No frame path to Electron exists.** Offscreen rendering and readback work today. Electron's `sharedTexture` module
   can take a platform texture handle and produce a `VideoFrame`, but it is experimental and main-process only, and Vixen
   has no external-memory support. It's an upgrade, not a foundation.
7. **The bezel skin is not released.** `styles/bezel.css` first appears after tag `v3.0.1`, no tag contains it, and the
   npm tarball for `@xui/core@3.0.1` holds only `theme.css`. 28 commits sit unreleased on xUI's main line, and the
   version there still reads 3.0.1. **Publishing xUI 3.1 is a prerequisite** (P0, task 1).
8. **`@xui/node-graph` and `@xui/dock-manager` are both published at 3.0.1.** They cover the graph canvas and the app
   layout. A grep of the node graph found no virtualisation or culling, so scale needs testing (S7).

**Don't trust Vixen's documents over its code.** `docs/plan/48` still says `MapBaker` bakes two maps; the code bakes nine.
The TextureGraph README says sub-graph overrides don't work; the code says they do. Doc 48 remains the best design record.

---

## 2. Scope

**Goals**
- One app covering graphs, layer stacks, painting, baking and export.
- A Library that stays fast at **10,000 projects** in one game.
- A headless backend with a versioned API. The Electron editor is one client, the Blender plugin is another, and the CLI
  and build farms are a third.
- Windows x64, macOS arm64 and Linux x64, matching Cathode's matrix.

**Non-goals for v1**
- **Reading or writing `.sbs`, `.sbsar` or `.spp`.** They're proprietary formats and a legal risk. Use no Adobe trademarks
  in the product or in node names.
- FX-Map recursion and function graphs. Splatter, Tile Sampler and Raven expressions replace them (doc 48 D7, D6).
- Real-time multi-user editing. Teams collaborate through Git.
- A runtime player like Substance's engine. Patina's output is files.
- The Blender plugin itself. The backend is built and tested for it (§8), and the plugin comes later.

---

## 3. Architecture

### 3.1 Processes

```
┌───────────── Electron ─────────────┐     ┌──── Blender add-on (later) ────┐   ┌── patina CLI / build farm ──┐
│ main: sidecars, menus, dialogs     │     │ Python client, generated from  │   │ same API, or in-process     │
│ renderer: Angular 22 + xUI bezel   │     │ the same OpenAPI document      │   │ libraries with no server    │
└───────────────┬────────────────────┘     └───────────────┬────────────────┘   └──────────────┬──────────────┘
                │ REST + WebSocket (events, previews, viewport frames)                          │
┌───────────────▼──────────────────────────────────────────▼───────────────────────────────────▼──────────────┐
│ patina-server (ASP.NET Core): owns all domain state                                                         │
│ library index · projects · graphs and stacks · undo journal · job queue · dependency graph · file watcher   │
└───────────────┬─────────────────────────────────────────────────────────────────────────────────────────────┘
                │ Unix domain socket (commands, events) + content-addressed cache and shared memory (pixels)
┌───────────────▼─────────────────────────────────────────────────────────────────────────────────────────────┐
│ patina-worker: owns the GPU device and native parsers. Stateless: it executes plans and can be restarted    │
│ Vulkan (MoltenVK on macOS) · Raven kernels · viewport · paint stamps · bakes · Assimp · image codecs         │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

| Decision | Choice | Why |
|---|---|---|
| Two processes | **Yes**, the Cathode split with a different reason | Here the reason is **crash isolation**, not GC. A driver fault, device loss or a native crash on a bad FBX must not lose the session or disconnect Blender. S5 tests this |
| State ownership | The Server owns everything; the Worker only executes (Cathode ADR-0004) | The Worker can die at any time, so it must hold nothing that can't be rebuilt |
| Runtime | **CoreCLR with ReadyToRun, not NativeAOT** | Raven and ShaderCompiler are not AOT-proven, and the Worker needs runtime compilation. Nothing here needs AOT's latency. Worker start-up cost is covered by the kernel cache |
| Large data | Never through JSON or the socket | Pixels go through the content-addressed cache and shared memory. The socket carries hashes |
| API shape | REST + WS with a committed `openapi.json`, as in Cathode | Generates both the TypeScript and the Python client |
| Sync | Sequence-numbered **delta events**, as in Anode | A second client (Blender) needs to see edits made by the first |

### 3.2 One evaluator, two front ends

This comes from doc 48 D1 and D3, and it stays: **a stack compiles to a graph, and there is one GPU evaluator with no CPU
twin.** Vixen's history shows why: a parity test once checked a C# re-implementation instead of the shader that shipped.

```
Graph document ───────────────┐
                              ├─► Flatten sub-graphs ─► Compile ─► Plan ─► Evaluator ─► Result cache
Layer stack ─► stack compiler ┘                                     ▲
                                                 parameter values ──┘  (never recompiles the plan)
```

The evaluator rewrite adds:
- **Content-keyed node cache.** Key = hash(kernel version, parameters, input keys, resolution, format). Editing one node
  re-runs only its downstream nodes. Results live in a GPU LRU, then on disk.
- **Plan and values kept apart.** Moving a slider writes a uniform and doesn't recompile. Cathode's rule applies:
  structural changes recompile, value changes never do.
- **Dirty regions.** A paint stroke invalidates tiles, not images, so only the affected tiles recomposite.
- **Formats.** R32F and RGBA32F join the existing 8-bit and 16F set. **This overturns a Vixen policy**, because height
  and position need it. Non-power-of-two sizes come in the same work.
- **Tiled evaluation** for 8K, and a jump-flood rework of Flood Fill and Distance, which Vixen refuses above 2048.
- **An explicit CPU exception list.** Normal to Height stays CPU, as in Vixen. Anything else on the list needs a reason.

### 3.3 Solution layout

```
patina/
├─ engine/                         # .NET 10; Directory.Build.props and Packages.props as in Cathode
│  ├─ src/
│  │  ├─ Patina.Contracts/         # REST DTOs and file schemas, no references
│  │  ├─ Patina.Ipc/               # Server <-> Worker protocol
│  │  ├─ Patina.Library/           # library, projects, references, index, build keys. No GPU
│  │  ├─ Patina.Graphs/            # what a graph's references mean, for the Worker and `patina bake`
│  │  ├─ Patina.Content/           # the CC0 compounds under content/compounds, as shipped node types
│  │  ├─ Patina.Jobs/              # persistent job queue, batch rebuilds, impact analysis
│  │  ├─ Patina.Export/            # export presets, channel packing, naming templates
│  │  ├─ Patina.Viewport/          # small PBR forward renderer, environment, tonemap
│  │  ├─ Patina.Worker/            # executable
│  │  ├─ Patina.Server/            # executable
│  │  └─ Patina.Cli/               # `patina build`, `bake`, `export`, `library check`
│  ├─ tests/                       # one test project per library, plus Patina.Testing
│  └─ tools/                       # Patina.Bench, library generator for scale tests
├─ editor/                         # Nx workspace, the same tools as Cathode
│  ├─ apps/desktop/                # Electron main + preload
│  ├─ apps/studio/                 # Angular renderer
│  └─ libs/
│     ├─ api-client/               # generated from openapi.json
│     ├─ studio-ui/                # controls xUI doesn't have
│     └─ features/                 # library, graph, stack, paint, bake, export, viewport
├─ clients/python/                 # generated client + the "Blender-shaped" conformance test
├─ content/                        # factory materials, compounds, brushes, environments
├─ external/Vixen/                 # git submodule, pinned. All graph, paint and bake code lives here
├─ docs/  (adr/)
├─ skala.jsonc  dotnet-tools.json  .editorconfig  .skala/
└─ .github/ (workflows/, actions/setup-dotnet/)
```

---

## 4. Vixen: split, then consume

**Patina ports nothing.** There is one copy of the code, and it lives in Vixen. The headless parts of the editor projects
are split into libraries with no UI, and both Vixen's editor and Patina consume them. Vixen gets the incremental
evaluator, the live paint viewport and the better baker for free. Patina holds only what's specific to the product: the
Library, Server, Worker, CLI, export and the Angular app.

```
Vixen (Apache-2.0)                                                     Patina (GPL-3.0-or-later)
Core libraries, Graphics, Vulkan, Raven, Geometry ──┐
Vixen.NodeGraph · Vixen.TextureGraph · Vixen.Texturing ──┬─ submodule ─► Patina.Worker, Server, Cli
Vixen.Interchange                                   ─────┘
       └─► Vixen.Editor.* (panels only, consume the same libraries)
```

**How a change flows:** make it in the `external/Vixen` checkout on a Vixen branch, push it, and point the pin at that
commit so work isn't blocked. A CI check on release tags requires the pin to be on Vixen's main branch. Move to packages
if Vixen ever publishes them (none are published today, and none exist for Raven).

LOC below comes from the audits; figures marked ~ are estimates from file sizes.

### Consume as it is

| Piece | LOC | Note |
|---|---|---|
| Graphics, Vulkan, RenderGraph, Shaders + Core, Collections, Mathematics, Serialization, Reflection, IO, Diagnostics, Platform.Native | ~44k | The minimal GPU closure |
| Raven, Core.Syntax + generator, ShaderCompiler | ~44k | Runtime kernel compilation |
| Geometry, Geometry.Uv, Geometry.Remeshing | 33.5k | Pure C#. Includes `MapBaker`, upgraded in place in P4 |
| Core.Imaging, Core.Threading, Core.Yaml | ~8k | Imaging's codecs and encoders are extended in place |
| Graphics.Null | 2k | Unit tests that check command streams, not pixels |

### Split into new Vixen libraries

Each new project gets a sibling `.Tests` project, as Vixen requires, and the existing tests move with their code.

| New project | Comes from | LOC | What stays behind |
|---|---|---|---|
| **`Vixen.NodeGraph`** + `.Generator` | `Editor.NodeGraph` (Model, Document, Compiler, SubGraphs, Registry, Commands, Layout…) | ~6.5k | View, inspector, search popup, theme (~3.4k) |
| **`Vixen.TextureGraph`** | `Editor.TextureGraph`: compiler, plans, evaluator, 49 nodes and kernels, 35 compounds | ~23k + 3k YAML | Little or nothing. Preview hooks may stay in the editor |
| **`Vixen.Texturing`** | `Editor.Texturing`: layer model, stack compiler, smart materials, strokes, footprint, projection, brushes, coverage | ~11k | Panels, views and `TexturingModule` (~8k), plus `PaintMeshRaster` until a GPU viewport replaces it |
| **`Vixen.Interchange`** | `Editor.Assets/Models` and decoders: `ModelReader`, `ModelWriter`, Stb, DDS, CubeLut | ~2.7k | The import pipeline, which stays engine-side |

**Small moves**, each a plain relocation:
- `MeshSimplifier` from `Rendering.VirtualGeometry` into `Vixen.Geometry`.
- `EnvironmentBaker`, `CubeImage` and related code out of `Vixen.Rendering/Lighting` into a headless library.
- Selected `.rvn` shading into a library file set that doesn't need `Vixen.Rendering`.

**Couplings to cut**

| Coupling | Fix |
|---|---|
| `NodeGraphModel` uses `Selection<T>`; `NodeGraphCommands` uses `IEditorCommand` | Small interfaces in `Vixen.NodeGraph`, implemented in `Editor.Core` |
| `TextureProjectImages` takes an `EditorProject` | An image-source interface. The editor supplies one and Patina supplies another |
| `TextureText` needs `Vixen.Ui.Text` | Confirm it's free of UI types, or move it behind an interface |
| Normal to Height reaches an internal `Geometry.Uv` solver (#752) | Make the solver public |
| Paint uses `TerrainBrush` from `Vixen.Terrain` | Move the brush primitive down into a shared library |
| `PaintProjection` and `LayerStackMesh` import `Vixen.Rendering` | Depend on `Vixen.Graphics` only |

**Things to watch**
- `Editor.NodeGraph` has **seven other consumers**: Ai, AnimationGraph, Gameplay.Quests, AssetEditors, ShaderGraph,
  VfxGraph and Texturing. Each moved type changes namespace for all of them, so do this first and in one step.
- Vixen's issue #720 proposes a narrower split and its README says the split was deferred because "no consumer would
  notice". Patina is that consumer. Rewrite #720 to the scope above rather than opening a parallel issue.
- **The gate that keeps the split honest:** a Vixen test fails if a headless library's output folder contains any
  `Vixen.Ui*` or `Vixen.Editor.*` assembly. #720 explains why compile-time checks alone don't catch this.
- Vixen's own gates apply to all of this work: docs pages, doc comments, whitespace and plan citations.

### Stays in Vixen, and Patina doesn't use

| Piece | Why |
|---|---|
| All `Vixen.Ui` views and the editor | Angular replaces them for Patina |
| `Vixen.Rendering`, `Rendering.PostFx`, `Engine.Renderer` | They pull in Engine, Ecs, Vfx and GI. Write a small forward pass on `Brdf` + `Ibl` instead |
| `ForwardPlus.rvn` | Tied to distance fields and virtual shadows |
| `Editor.Assets` (as a whole), `AssetCompiler`, `Core/Vixen.Assets` | Engine content pipeline, heavily entangled |
| WebGPU and OpenGL backends | Old wgpu pin, no ray tracing or bindless, and no goldens run on them |
| ShaderGraph, ShaderCompilerService | Not needed. In-process compilation is enough |

**Reference reading:** `docs/plan/48` (with its competitor inventory in Appendix A), plans 41 and 42,
`docs/guide/engine/map-baking.md`, and Vixen's `ci.yml` for running Vulkan on lavapipe.

---

## 5. The Library

The vocabulary: a **Library** is one game. A **Project** is one object or asset set inside it, holding typed documents
(ADR 0012). Anything in any project can be used in any other, so there is no separate place for shared assets.

### On disk

```
Ember.ptlib/                         # a folder, and normally a Git repository
├─ library.yaml                      # id, schema version, texel density, channels, default shader, export targets
├─ export-presets/                   # belong to the library, not to any project
├─ .revisions/                       # the bytes pinned references keep
├─ projects/
│  └─ props/crate_01/
│     ├─ project.yaml                # id, tags, documents, references to documents elsewhere
│     ├─ meshes/low.glb  high.glb    # or external paths, resolved through library.yaml
│     ├─ crate.vxlayers              # YAML, Vixen's layer stack format
│     ├─ paint/<layer-id>.vxpaint    # new version: one sparse tiled pack per layer (Git LFS)
│     └─ bakes.yaml                  # settings only; baked maps are cache
└─ .ptcache/                         # git-ignored, always rebuildable
   ├─ index.sqlite                   # search and dependency index
   ├─ objects/                       # content-addressed bakes, node results, exports
   ├─ thumbnails/  kernels/          # kernels = compiled SPIR-V
```

| Rule | Why |
|---|---|
| **Files are the truth; SQLite is derived** | Deleting `.ptcache` loses nothing but time. Git pulls and external edits just work |
| Text documents are YAML with **stable ordering** and stable ids | Diffs and merges stay readable. Adding a node never reorders a file |
| References use **GUIDs, not paths** | Moving or renaming a thousand files breaks nothing |
| Paint goes into one pack per layer, not one file per tile | Git and filesystems cope badly with millions of small files |
| Schemas are versioned with migrations from day one | Cathode's rule: files saved from 1.0 on keep loading |
| Graph, stack and paint files **share Vixen's formats** | One codebase means no importer. A game on Vixen can use library content directly |

### Scale design

- **Nothing loads a whole library.** Opening one reads `library.yaml` and the index. A project loads on open.
- **The index** (SQLite with FTS5) serves search, tags, filters, thumbnails, usage counts and reverse dependencies. The
  browser is a virtualised grid backed by paged queries.
- **Reference modes.** Floating follows the document, and pinned stays at a specific revision. Floating is the default.
- **Impact preview.** Saving a material used across projects shows "affects 412 projects, 3 pinned" before anything rebuilds.
- **Build keys.** Each output has a key: a hash tree of its inputs and kernel versions. An output is stale only when its
  key changes, so a change to one material rebuilds exactly what depends on it. Build keys are also why this works
  across machines: **output is a file**, and a key match means the file is reused, not regenerated (doc 48 D4).
- **Job queue.** Persistent, prioritised and resumable, with interactive work always ahead of batch. A killed Worker
  loses only the job in progress.
- **Headless builds.** `patina build --library X --changed` runs the same pipeline on a farm with no UI.
- **Library health.** `patina library check` reports missing references, orphans, schema drift and stale outputs.

GPU output differs between vendors and drivers, and Vixen only asserts byte-identical results on the same machine. So
the build key identifies *what should be built*, and the tolerance for comparing two machines' output is a test setting.
Don't promise byte-identical output across GPUs.

---

## 6. Phases

Each phase ends with something usable and measured. Durations are rough judgement assuming **2 to 3 engineers**, Cathode's
basis; multiply by about 2.5 for one developer. All performance targets are proposals to confirm in P0.

```
P0 Spikes ─► PV Vixen split ─► P2 Graph editor ──────► P4 Baking ─► P5 Painting ─► P6 Batch and export ─► P8 Ship
 3–4 wk       4–6 wk            6–8 wk                  5–7 wk       12–16 wk       5–6 wk                 4–6 wk
          └─► P1 Foundation (∥ PV) 4–5 wk
                    └────────► P3 Library (∥ P2) 6–8 wk                     P7 Content depth (∥ from P4, ongoing)
                                                                            P9 Blender plugin (after P6, 4–6 wk)
```

### P0: feasibility spikes (3–4 weeks)

Prerequisite tasks:
1. **Publish xUI 3.1** with the bezel skin. Until it's out, the app can build against a local xUI.
2. Choose the name (§10), create the repo under GPL-3.0-or-later (§11), add the Vixen submodule.

> **Status (2026-11-06): no go/no-go has been recorded, and two spikes failed.** S8 (macOS Vulkan bundling) is open as
> [#48](https://github.com/Rikarin/Patina/issues/48), S9 (GPU traversal) is an open decision, and S3 fails its CPU line. Work went
> ahead regardless, so the decision on Raven plus MoltenVK is still owed.

Each spike is throwaway, with its result written up in `docs/adr/`.

| # | Spike | Pass | If it fails |
|---|---|---|---|
| S1 | **Consumption**: a new executable outside Vixen's solution, referencing `Editor.TextureGraph` through the submodule before any split, bakes a `.vxtexgraph` headless on macOS, Windows and lavapipe. The Vixen closure builds on our SDK | Same bytes as `vixen texture bake` on the same machine; Vixen's TextureGraph tests pass on all three | Fix upstream. A failure on Mac is the worst case: go straight to the Metal question in §9 |
| S2 | **Kernel compile time**: all 49 kernels cold and from a SPIR-V disk cache | Cold start tolerable with a progress UI; cached under 200 ms | Ship precompiled kernels; use uber-kernels for generated code |
| S3 | **Frames to Electron**: 1440p at 60 fps from Vulkan readback to a canvas, comparing WebSocket with shared memory via a small native addon, then Electron `sharedTexture` | Under 2 frames of added latency, under 10% of a core | Adaptive render scale while interacting; `sharedTexture` becomes a requirement |
| S4 | **Stroke latency**: 4K, 20 layers, tiled store, stamp through to the frame shown | Under 50 ms on an M-series Mac and a mid-range PC | Show an overlay preview during the stroke, and composite on pause |
| S5 | **Crash isolation**: kill the Worker in a bake and in a stroke; force device loss | Session intact, Worker back in under 3 s, job re-queued | Add a journal for unflushed strokes |
| S6 | **Library scale**: generate 10,000 projects. Measure index build, search, cold open, watcher load and fan-out over a popular material | Cold open under 2 s, search under 50 ms, full reindex in minutes | Change index design before any UI depends on it |
| S7 | **`@xui/node-graph` at scale**: 500 nodes with live thumbnails, inside `@xui/dock-manager`, under bezel | Pan and zoom at 60 fps | Add culling **in xUI**; don't fork or wrap it |
| S8 | **Packaging**: Vulkan loader, MoltenVK and ICD inside a signed `.dmg`; Worker finds them with no Homebrew or SDK | Launches on a clean Mac | Change the bundling layout in Vixen's `VulkanLoader` upstream |
| S9 | **Bake throughput**: threaded CPU AO, 4K, 256 rays, 2M triangles | Under 2 minutes on a current laptop | Bring forward GPU traversal in Raven compute |

**Exit:** ADRs written, and the go/no-go on Raven plus MoltenVK decided.

### PV: the Vixen split (4–6 weeks, all work in the Vixen repo)

> **Status (2026-11-04): done, apart from the rewrite of issue #720.** Vixen `ef63fe921` holds `Vixen.Authoring`,
> `Vixen.NodeGraph`, `Vixen.TextureGraph`, `Vixen.Interchange` and `Vixen.Texturing`, each with the closure gate, and
> `patina bake` evaluates a graph through them on MoltenVK. Differences from the plan below:
> - **`Vixen.Authoring` is new.** It holds `IAuthoringCommand`, `ICommandContext`, `IAuthoredDocument` and `Selection<T>`.
>   `Vixen.Ui.Reactive` is allowed in the closure, because it is signals and references only Core.
> - **The projects live under `Tools/`**, so they take Vixen's tooling profile rather than the engine one.
> - **`Vixen.Terrain` stays.** It draws nothing, so `Vixen.Texturing` references it directly.
> - **`MeshData`, `ModelData` and `MorphTargetData` moved to `Vixen.Geometry`**, which the plan did not foresee.
> - **Left behind for now:** `CubeLut` (still uses `Vixen.Rendering`), `LayerStackMesh` and `PaintAlphaSource` (need an
>   `EditorProject`), and the `.rvn` shading split. Most Texturing tests stay in `Vixen.Editor.Texturing.Tests`, because
>   their fixtures open a real document.
> - **S1 is met on one machine.** `patina bake` and `vixen texture bake` write the same bytes on an M1 Max with MoltenVK, at
>   the same Vixen commit: 11 maps of 11 from three graphs, at 16 and 32 bits, through compounds, an inline ramp and
>   curve, a bitmap, normals, occlusion and a distance field (ADR 0007). Windows and lavapipe have not been tried.

**Exit:** S1's executable runs on the split libraries, no UI or editor assembly is in its output, and Vixen's editor and
full test suite still pass.

Do these in order, one merge each, so Vixen stays green throughout:
1. Rewrite #720 to the scope in §4, and add the closure gate.
2. `Vixen.NodeGraph`, updating all seven other consumers.
3. `Vixen.TextureGraph`, with its 33.7k lines of tests.
4. `Vixen.Interchange`, `MeshSimplifier` and the environment code.
5. `Vixen.Texturing`, with its share of the 31.8k lines of tests. Cut the Terrain and Rendering ties first.

No behaviour changes in this phase: **tests move unchanged**, and any rewrite waits for P2. If a move needs a test edit
beyond namespaces, that's a sign the split is wrong.

### P1: foundation (4–5 weeks, parallel with PV)

> **Status (2026-11-06): done, with menus and saved layouts arriving only in the library work.** All three platforms pass in CI.
> Windows runners have no Vulkan, so its viewport tests skip. Left open: the 2 cores spent while orbiting (ADR 0003). The viewport now reads textures, so a glTF's PBR
> material is drawn whole.

**Exit:** the desktop app opens, starts both sidecars, and shows a glTF with a PBR material in a docked viewport, under
bezel, on all three platforms, in CI.

- Repo, CI, Skala and packaging (§7) on the first day, before any feature.
- Port Cathode's `Sidecar` supervision, `app://` scheme with CSP, preload bridge and settings store.
- Server, Worker, IPC, health checks and OpenAPI with the generated client.
- Consume the Vixen closure, using `Vixen.Interchange` once it lands.
- Viewport: small forward pass on `Brdf` + `Ibl`, orbit camera, tonemap, the transport S3 chose.
- App shell: `<html class="dark" data-skin="bezel">`, `@xui/dock-manager`, menus, omnibar, save and restore layouts.

### P2: graph editor (6–8 weeks)

> **Status (2026-11-05): the exit is met but for what P4 owes.** A user can build a tiling material from nodes with live
> thumbnails. Of the 84 node types and compounds a palette offers, 76 give a preview through the real API at 16 and 32
> bits, which a process test holds. The other eight read a mesh map, and baking one is P4: `Source/Mesh Map`, and the
> compounds Curvature Edge Wear, Dirt, Dust, Grunge Rough Dirty, Mask Editor, Metal Edge Wear and Position Gradient. The
> evaluator is in Vixen, and S7 holds 60 fps at 500 nodes apart from one zoom stall of about 200 ms (ADR 0005, 0006).
> Left open:
> - A slider at the head of a 2K, 40-node chain takes about 82 ms, now mostly evaluation and PNG encoding (ADR 0005).
> - Input ports declare a range where the kernel gives one; the rest are still guessed from defaults.
> - Previews and bakes stay 8-bit until a float export lands, though a graph can compute at 32.
> - The minimap is hidden on a canvas under 480 px, because a graph overlay takes a port's hit test in xUI.
>
> New graphs, settings, exposed parameters, groups and notes with identities, compounds authored, saved from a selection
> and copied from those that ship, bitmaps, the curve editor, gradient ramp, histogram, channel view, full-size images,
> compound refresh without a recompile and 32-bit formats are in (ADRs 0005 and 0007).

**Exit:** a user can build a tiling material from nodes with live thumbnails, and every Vixen node and compound works.

- Rewrite the evaluator **in `Vixen.TextureGraph`**: result cache, separated values, new formats. Vixen's editor adopts it
  in the same change. Target: a slider on a 2K, 40-node graph updates at interactive rates.
- Change Vixen's policy on 32-bit float in doc 48, along with the code.
- Graph API: undoable command batches, previews over WS.
- Graph UI on `@xui/node-graph`: search palette, inspector, parameters, sub-graphs, comments, groups, 2D texture viewer.
- `studio-ui`: curve editor, gradient ramp, histogram, channel view.

### P3: Library (6–8 weeks, parallel with P2)

> **Status (2026-11-05): the exit is met for opening, searching and browsing a generated 10,000-project library.** It holds
> at 60 fps under scripted scrolling, and a project can be created through the Server. Left open: thumbnails, a grid, drag
> to apply, the file watcher and bulk import. Opening a library or a project from the app arrived later, in ADR 0012's work.

**Exit:** a generated 10,000-project library opens, searches and browses within the S6 targets.

- File formats and migrations, GUID references, SQLite index, watcher, scanning.
- Reference modes, reverse dependencies, impact preview, build keys.
- Library browser: virtualised grid, tree, tags, search, drag to apply, thumbnails rendered by the Worker.
- Project create and import; bulk import of a folder of meshes.
- Generated `.gitattributes` and `.gitignore`. Git-friendliness is a test, not a promise.

### P4: baking (5–7 weeks)

> **Status (2026-11-05): the path works from panel to maps, the exit is not met, and S9 failed.** An owner has to judge the
> pictures, on an artist's asset rather than the generated pair, and decide what S9's result means: threaded CPU occlusion takes
> 2,920 CPU-seconds for 4K at 256 rays over two million triangles, so at best 4.9 minutes against a 2-minute target
> ([ADR 0008](adr/0008-baking.md)). GPU traversal has not been started, because the plan keeps one implementation. Everything
> else below is in: `MapBaker` is threaded with a SAH tree, supersampling, a cage, skew and match-by-name; 16-bit PNG and EXR are
> in `Core.Imaging`, the EXR codec managed rather than a native library; a bake is a job that survives both processes; and
> the Bake panel and Mesh Map nodes are wired. The tangent basis is MikkTSpace, shared by the baker and the viewport, so a bake
> is judged on the low mesh in the viewport, from the panel or `PUT /api/v1/viewport/bake`, and in the maps as a game engine
> reads them. `patina bake` binds mesh maps, and cage and skew map go through the panel and `bake-maps`. Left open: nobody has
> judged a real asset.

**Exit:** a high-to-low bake of a real game asset is judged usable by an artist, on a mesh with hard edges.

- Upgrade `MapBaker` in place: threaded, SAH BVH, supersampling, cage mesh, skew, match-by-name, 16/32-bit output.
- **EXR and 16-bit PNG in `Core.Imaging`.** Vixen encodes all maps as 8-bit today, so this has to land alongside. EXR
  needs a native library, which joins Vixen's `build/native-dependencies.json`.
- Bake-as-a-job, cached by build key. Bake panel. Mesh-map nodes in graphs.
- Keep one implementation. If S9 pushes baking to GPU, the GPU path replaces the CPU one rather than joining it.

### P5: painting (12–16 weeks, the biggest risk)

> **Status (2026-11-05): painting works on a mesh and the exit is not met.** A stack names a model. A stroke is stamps through a camera on a
> picture, found on the surface by the Worker; only covered texels are painted, each island has a gutter, and a stroke crosses seams in the
> viewport and in the atlas. The stack's maps wear the mesh in the viewport as you paint, dirty rectangles only, and a test holds the texture each
> slot samples equal to the exported file ([ADR 0009](adr/0009-painting.md)). Vixen's panes moved onto `LayerStackEvaluator` in the same change,
> so #849 is gone and there is one implementation. The studio paints in the viewport with a brush, a line, a path, mirrors, alphas, jitter and
> pressure, and has fill, paint, filter and group layers, masks, anchors, generators over a bake, and all sixteen blend modes; the atlas pane paints
> as a UV pane. **What stops the exit:** stamp to frame in a real window is 67 to 100 ms at the median for a brush of 48 texels at 4K on 20 layers,
> and a brush of 96 texels falls behind the pointer, measured under load only, against S4's 50 ms; and no artist has textured a hero prop.
> Not started: UDIM, multiple texture sets, stencil, clone, smudge, an occlusion test under the brush, mask sub-layers and effects in the UI.

**Exit:** an artist textures a hero prop end to end, and **what they see under the brush is exactly what exports**.

- **Tile store first, in `Vixen.Texturing`:** sparse tiles, copy-on-write undo, `.vxpaint` packs, a GPU tile cache, and a
  migration from today's format. Vixen's paint pane moves to it in the same change.
- **Live full-stack viewport.** Fixing Vixen's documented mismatch is the phase's main goal, and Vixen's editor gets the
  fix too.
- Layer stack UI: fill, paint, filter and group layers, masks, anchors, generators, 16 blend modes.
- Paint tools: brush, alpha, symmetry, line and path strokes, UV-pane painting, seam handling.
- Then, in order: UDIM across the painter, multiple texture sets, stencil projection, clone, smudge.
- Particle brushes are stretch scope and may slip past v1.

### P6: batch and export (5–6 weeks)

> **Status (2026-11-06): the engine half of the exit is met, and the exit as a whole is not.** No engine has opened a file, and
> the 10,000-project measurement used stand-in maps. Presets are files, BC7 and
> BC6H are complete in Vixen, a build is a plan from keys that survives its Worker and the Server, `patina build` and the Export pane
> run it, and LODs and `.cube` viewing are in. Measured at 10,000 projects with stand-in maps and 500 with real ones
> ([ADR 0011](adr/0011-export.md)). Not done: layer references, which need a decision about Vixen's stack format, and any
> check inside Unreal, Unity or Godot.

**Exit:** edit one material that many projects use, and the library rebuilds only what changed and writes engine-ready files.

- Export presets with channel packing and naming templates: Unreal, Unity, Godot, and Vixen.
- Full BC7 and BC6H modes in `Core.Imaging` (it has mode 6 and mode 11 only), KTX2, mip chains.
- Batch rebuild from impact preview, `patina build`, per-project and per-library reports.
- Layer references (InstaMAT's idea): one layer's content reused in many projects.
- LOD generation via `MeshSimplifier`. Colour: sRGB and linear plus `.cube` viewing; OCIO only if demand is real.

### P6.5: usable (before P7)

P0 to P6 proved technology, and every gate tested that, so each phase could pass while the app stayed unusable. This phase has one
exit, and new engine scope waits for it.

**Exit:** a person goes from a mesh to exported textures in the app, unaided, and `journey.spec.ts` does the same with no fixtures.
**From here on, a phase's exit is judged in the app, by a person.**

### P7: content depth (from P4, ongoing)

Doc 48 calls node count the competitors' moat and content "the honest gap". This is the long game.

- Text and SVG nodes, compound selector and colour knobs, more generators and grunges, all in `Vixen.TextureGraph`.
- A factory library of materials, smart materials and brushes, kept in `content/`.
- A compiled, parameterised graph package format, so graphs can be shared with no source.

### P8: ship (4–6 weeks)

Onboarding, sample library, docs, website, signing and notarising, auto-update, crash reporting that's opt-in, schema
freeze. Cathode's `release.yml` and provenance attestation apply unchanged.

### P9: Blender plugin (4–6 weeks, later)

See §8.

---

## 7. Engineering conventions

Inherited from Cathode unless noted.

| Area | Decision |
|---|---|
| .NET | SDK 10.0.401, `net10.0`, `LangVersion=latest`, nullable, warnings as errors, central package management |
| Tests | xunit.v3 on Microsoft.Testing.Platform, CsCheck for properties |
| Skala | `rikarin.skala.cli` 4.4.0 pinned in `dotnet-tools.json`. Copy `skala.jsonc` and its three gates (`local`, `pr`, `ci`). Severities live only in `.editorconfig`, each with a reason. **Start with an empty baseline.** Exclude `external/`, since Vixen runs its own gates |
| Workflows | `engine.yml`, `editor.yml`, `skala.yml`, `desktop.yml`, `release.yml`, `nightly.yml`. Actions pinned by SHA. Reuse `actions/setup-dotnet`, Cathode's composite that avoids the broken aka.ms route |
| Vixen in CI | Vixen's CI covers its libraries. Patina's CI checks out the submodule recursively, builds the closure with a cache, and runs integration tests. Nothing retests Vixen's suite. Release tags require the pin on Vixen's main branch |
| GPU in CI | Linux on **lavapipe**, as in Vixen's `ci.yml`. It gives pixel coverage on every PR. Real-GPU goldens run nightly on a **self-hosted Mac and PC**, since hosted runners can't be relied on for this |
| Frontend | Angular 22 zoneless with signals, Tailwind 4, Nx 24, pnpm, Vitest, Playwright for Electron, Storybook with axe, Electron 45, Node 24 |
| xUI | **Use the skin; don't rebuild it.** Import `theme.css` then `bezel.css`, and set `data-skin="bezel"`. Use `pane`, `chrome-bar`, `well`, `readout` and friends for app chrome. `studio-ui` holds only controls xUI lacks. A gap in a general control gets fixed in xUI. Install the xUI MCP server for agents |
| OpenAPI | Committed, with a drift check in CI, as in Cathode |
| Docs | Numbered docs and ADRs, a changelog, and `CLAUDE.md` from the start |

**Test strategy**

| Layer | What it catches |
|---|---|
| Closed-form kernel tests (in Vixen) | Wrong maths. A blur of a constant is that constant; an FFT of a known input has a known result |
| Conformance per node, generated from descriptors | Tiling seams, resolution independence, seed stability, format coverage |
| Golden images with perceptual tolerance | Visible changes. Per GPU family; updated deliberately via a workflow, as Cathode's `baselines.yml` does |
| "What you paint is what exports" | Viewport pixels equal exported pixels. The P5 exit criterion, automated |
| Evaluator: incremental equals full | Random edits, then compare with a clean evaluation. **The evaluator's most important test** |
| Library: generated 10k corpus, nightly | Performance regressions, with budgets that fail the build |
| Python conformance client | The API works for a client that isn't Electron (§8) |
| Crash soak | Worker killed at random under load, with no session loss |

---

## 8. Keeping the backend ready for Blender

The plugin comes later, so every decision below happens **now**. Otherwise the API will quietly become Electron-shaped.

- **No Electron in the contract.** Nothing in the API depends on Electron. The Server starts with `patina-server
  --library <path>` and needs nothing else.
- **Instance discovery.** One Server per library per machine, listed in a per-user discovery file with port and token.
  A client attaches to a running Server or starts one.
- **Several clients at once.** Editor and Blender share a session through delta events, and undo is server-side.
- **Mesh exchange by path.** A client gives the Server a file. A later fast path may use shared memory, with the same
  operation behind it.
- **Results are files.** Export returns paths. Blender loads them with its own image API.
- **Previews without a viewport.** Blender renders with its own engine, so maps must be available as images and don't
  depend on a Patina viewport.
- **Proof in CI.** `clients/python` is a generated client, and a test drives a Blender-shaped session with **no Blender
  installed**: attach, send a mesh, apply a material, bake, change a parameter, receive updated textures.

The plugin itself then needs panels in Blender, a sync operator for mesh changes, live-link texture refresh and a
material node setup. It should also ship the backend binaries per platform.

**Licensing:** Blender add-ons that import `bpy` are generally treated as GPL, which matches Patina's licence, so the
add-on can live in this repo and share code with it.

---

## 9. Risks

| Risk | Likelihood | Impact | Response |
|---|---|---|---|
| **Painting performance** with the full stack live | High | High | S4 first, tile store before tools, overlay preview as the fallback |
| **Raven is a single point of failure**: a research-labelled language with one output family | Medium | High | Golden tests and Vixen's own suite protect behaviour. A Metal path would need an MSL emitter, or SPIRV-Cross, which Vixen already wraps for GL |
| **MoltenVK** gaps or bugs on Mac | Medium | High | S1 and S8 up front. If they fail, Mac support needs native Metal, which should be costed before continuing |
| Evaluator cache bugs show stale pixels | Medium | High | The incremental-equals-full property test, on every PR |
| Content depth takes longer than code | High | Medium | P7 is ongoing, and Vixen's 35 compounds give a starting point |
| Baker quality on real assets, where tests can't judge | Medium | High | Artist assets in the test corpus, and artist sign-off as an exit criterion |
| Viewport transport can't hold 60 fps | Medium | Medium | Adaptive render scale. Brush cursor drawn in the renderer so it never lags |
| `sharedTexture` is experimental and may change | Medium | Low | It's an upgrade behind the transport interface, not a foundation |
| **Vixen's editor slows the rewrites**, since the evaluator and tile store must keep it working | High | Medium | Each rewrite migrates the editor in the same change. In P5, if a change is too big for that, a feature flag can keep the old path until the editor catches up |
| Vixen's gates and CI slow iteration on core code | Medium | Medium | Point the pin at a branch during development, and merge to Vixen's main in batches |
| The split takes longer than PV's estimate, especially `Vixen.Texturing` | Medium | Medium | P1 and P3 don't depend on it. S1 proves the whole path works before the split, so Patina can consume the unsplit projects meanwhile |
| Git LFS handles paint packs badly at game scale | Medium | Medium | Test in S6. Keep storage behind an interface, so object storage can come later |

---

## 10. Decisions for you

Work can start on the recommended defaults; each one is cheap to change now and expensive later.

| # | Decision | Recommendation | Why it matters |
|---|---|---|---|
| 1 | **Name** | Decide before release | Namespaces and file extensions carry it. `Patina` is a placeholder |
| 2 | ~~Licence~~ | **Decided: GPL-3.0-or-later** | See §11 |
| 3 | ~~Vixen consumption~~ | **Decided: split in Vixen, consume through the submodule, port nothing** | See §4 |
| 4 | **32-bit float** | Add it | Reverses a Vixen policy. Needed for height and position quality |
| 5 | **Library in Git** | Yes, with LFS for paint | Changes the file layout if you'd rather use Perforce, which is common in game studios |
| 6 | **First game engine target** | Your call | Sets the first export preset and the viewport's shading reference |
| 7 | **xUI 3.1 release** | Publish before P1 | Only you can do this |

---

## 11. Licensing

**Patina is GPL-3.0-or-later, as Cathode is.** This is my reading of the licences, not legal advice.

| Topic | Rule |
|---|---|
| Cathode code | Reusable directly: sidecar supervision, IPC, CI workflows and packaging |
| Vixen and xUI (Apache-2.0) | Can be combined into a GPLv3 project. Vixen's and xUI's `NOTICE` and licence texts ship with the app |
| **The core is Apache** | The graph, paint and bake libraries live in Vixen, so they stay Apache-2.0, and anyone can use them in a closed product. GPL covers only the Library, Server, Worker, export and app. **This is a deliberate trade for having one copy of the code** |
| Moving code between projects | No constraint: you hold the rights to Cathode, Vixen and Patina, so code can move in either direction and be relicensed as needed |
| Dependencies | Must be compatible with GPLv3. Check each one in `THIRD-PARTY-NOTICES.md`, as Cathode does. Assimp, MoltenVK and the EXR library are in the first inventory |
| **Factory content** | **License `content/` separately under something permissive such as CC0.** GPL generally doesn't reach a program's output, but a texture built from bundled materials can contain that content. Game studios must be able to ship those textures without doubt |
| Users' work | Libraries, projects and exported textures belong to their authors. State this plainly in the README and the app, because studios will ask |
| Scripts and plugins | The Blender add-on is GPL (§8). Clients that talk to the API over HTTP are separate programs, so a studio's own pipeline tools don't need to be GPL. Say so in the API docs |
| Contributors | Decide before accepting outside contributions. Without a CLA or copyright assignment, relicensing Patina code back to Apache later becomes impractical |

---

## Sources

- [Electron `sharedTexture` API](https://electronjs.org/docs/latest/api/shared-texture)
- [Vulkan ray tracing guide](https://docs.vulkan.org/guide/latest/extensions/ray_tracing.html)
