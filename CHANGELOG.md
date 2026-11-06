# Changelog

Notable changes to Patina. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- The Library's projects are a tree of folders. Folders and subfolders can be made, renamed, coloured, moved and deleted, and
  projects can be dragged between them, one or several at a time. Deleting a folder moves what it holds up a level, so it never
  deletes a project. Folders are folders on disk, with a `folder.yaml` that holds the colour and keeps an empty one in Git. A search
  or a tag still lists across all of them.
- A project holds typed documents (ADR 0012): `project.yaml` is schema 2, and lists its meshes and its Texturing, Layered Material,
  Image Material and Material Graph documents. New Project takes a kind and opens ready to work. A project opens in the tree to
  show them, and the Paint pane has no path box.
- The app starts on a Start screen, with no empty panes. Each kind of document opens its own layout, kept per kind, and open
  documents are tabs that keep their edits and undo while another is in front. Bake and Export are dialogs.
- Materials reach layers from a shelf: a graph's maps fill a stack's channels by usage, live, and a Layered Material or one of the
  six shipped materials unfolds into a stack as layers. An Image Material makes a material from one picture, with height from its
  brightness and no neural step. A Layered Material is previewed on a sphere.
- The layer stack is a pane of its own, with Properties under it: rows with visibility, thumbnails, mask thumbnails, blend and opacity,
  folders that open and close, drag to reorder and to move in and out of folders, and a right-click menu. Clicking a mask chooses
  it for the brush. Thumbnails are a layer's colour or kind for now, not its rendered picture.
- Vertex colours reach masks: a glTF's `COLOR_0` bakes into an ID map as the colours you painted, each face keeping one colour, and a
  Colour Selection mask picks one from swatches of the map's colours or with an eyedropper on the mesh or the 2D view. A mesh with no
  vertex colours still gets its ID map from its materials.
- A fill's projection is set in Properties: UV, tri-planar with blending, or planar along an axis, with a scale.
- A stack whose masks read baked maps no longer stops with no bake: they read as grey, a notice offers to bake, and a project's bake
  reaches its stacks by itself.
- The app opens and creates libraries itself, from the File menu or the empty Library, remembers recent ones and reopens the last.
  `PATINA_LIBRARY` stays for scripts. `patina-server --create-library true` lays out a library that does not exist yet.
- The app has its own menus, and the window title says what is open. Reload and the developer tools are not in a packaged build.
- Paint now counts as unsaved work: Save shows it, the app asks before leaving a project or quitting, and closing a project closes
  its stack and clears the viewport.

- Reading a mesh on Linux no longer needs a system Assimp. The engine carries its own, 5.4.1 where Ubuntu's was 5.3, so a
  distribution without `libassimp5`, such as Ubuntu 26.04, can open meshes. It needs glibc 2.33 and `libstdc++` from GCC 11.
- ⚠ A baked tangent-space map is now in MikkTSpace, with green up the image, and its rows run from `v = 0`, so a map is the
  right way up on the mesh in every tool. Maps baked before this are in another basis and upside down; the bake key changed,
  so they bake again.
- `.glb` files written by `patina-bench` no longer turn `v` over.

### Added

- Export presets for Unreal, Unity URP, Unity HDRP, Godot and Vixen, as files. A library adds or replaces one with a file in
  `shared/export-presets`. Green follows the source and the target: a bake's runs up, a graph's `Height to Normal` runs down, and
  it turns over only when they disagree.
- Batch builds: `POST /api/v1/builds/preview` shows what would change from build keys with nothing run, `POST /api/v1/builds` runs it
  as a job that survives the Worker and the Server, and `/builds/{id}/report` lists what each project wrote. `patina build` is
  the same pipeline with no Server. A second build does nothing.
- `texture-graph`, `layer-stack`, `mesh-maps` and `mesh-lod` outputs in `project.yaml`, delivered to `exports/<preset>/<project>`.
- An Export pane: choose a preset, see the impact, run it, read the report.
- `PUT /api/v1/viewport/look` views frames through a `.cube` table. No export sees it.
- `patina-bench fan-out` and `patina-bench bake-library`.
- A generated Python client in `clients/python`, with a Blender-shaped conformance test that needs no Blender.
- Painting's foundations: layer stacks through the API (`/api/v1/stacks`, `/ws/stacks/{id}`), undoable batches on the same
  numbered events as graphs, strokes sent as lists of stamps, and a Paint pane with a layer list and a brush on the composited map.
  The composite is the plan's, repaired tile by tile: about 6 ms a stamp at 4K with 20 layers, 38 ms to a client. A Worker that dies
  in a stroke loses nothing. `POST /stacks/{id}/export` writes each map, and `GET /stacks/{id}/pixels` gives a digest of what the
  strokes left, which equals the export's.
- Painting on a mesh. A stack binds a model (`setModel`) and its maps wear it in the viewport as you paint (`PUT /viewport/stack`), through
  dirty rectangles, as the bytes an export writes: `GET /stacks/{id}/pixels?sampled=true` reads what each material slot samples, and a test
  holds it equal to the exported file. Strokes carry a camera (`StackView`) and `POST /viewport/pick` finds a position on the mesh through the
  Worker. Stamps paint only what the mesh covers, dilate into a gutter, and carry on over seams in the viewport and in the atlas.
- The Viewport pane paints: a Paint button, Alt to orbit, a brush outline, and a stamp-to-frame readout.
- The Paint pane has the whole stack: fill, paint, filter and group layers, masks by constant, texture, anchor, generator, bake or paint,
  all sixteen blend modes, per-layer channels, and a bake for generators to read (`PUT /stacks/{id}/mesh-maps`). `GET /stacks/vocabulary` lists
  the words these take, so a client keeps no list of its own.
- Brushes: alphas, rotation, jitter, pressure to size and flow, line and path strokes, and mirroring on the mesh across its X, Y or Z plane.
- `patina-bench stamps` and `s4-stack`, and `paint.bench.ts`, which measures stamp to frame in a real window.
- `patina-bench s4`.
- `patina-worker --check-natives` says which Vulkan device and which Assimp this copy of the engine finds, and reads a mesh.
  Packaging runs it on what it just built.
- The repository: licence, conventions, Skala gates with an empty baseline, and CI workflows.
- `patina-server`: `GET /api/v1/system`, a committed `openapi.json` with a drift test, `--library <path>`, a bearer token
  and a per-user discovery file that keeps one Server per library.
- `patina-worker`: connects to the Server over a Unix domain socket, says hello and answers health.
- `patina version`.
- The Library's engine half: `library.yaml`, `project.yaml` and asset sidecars as canonical YAML with schema versions and
  migrations, GUID references that float or pin to a content hash, a rebuildable SQLite index with search, usage counts
  and reverse dependencies, build keys, and a health check.
- `patina library new` and `patina library check`.
- Read-only library endpoints: `GET /api/v1/library`, `/library/projects`, `/library/assets` and
  `/library/assets/{id}/impact`.
- `POST /api/v1/library/projects`, which writes a new project's files and lets the index catch up, and
  `GET /api/v1/library/projects/tags` and `/library/assets/tags`.
- The Library browser: projects and shared assets in a virtualised list that pages through the Server, with search and a
  tag filter. Selecting a shared asset shows its impact in the Inspector, selecting a project shows its details, and
  New… creates a project.
- The desktop app, which supervises both processes, and a studio shell with Library, Viewport and Inspector panes.
- A docked viewport: open a glTF, FBX or OBJ, orbit and zoom it, lit by a built-in studio environment, with the file's
  metallic-roughness factors.
- The viewport draws textures: base colour, normal, roughness, metallic and occlusion from a glTF's materials, embedded or
  beside it, and from files. Base colour is read as sRGB and the rest as linear; a 16-bit PNG or an EXR keeps its depth.
- `PUT /api/v1/viewport/maps` lays pictures over a mesh by path, `PUT /api/v1/viewport/bake` shows a finished bake on its
  low mesh, and `PUT /api/v1/viewport/graph` lays a graph's Output nodes over the open mesh. `GET /api/v1/viewport`
  says what is laid on.
- The Bake panel shows a finished bake on the low mesh, and takes a skew map. The viewport shows the open graph's outputs.
- `patina bake --mesh-maps <folder>`, and `patina bake` follows the bake chosen beside a graph.
- `patina-bench render --bake=<folder> --yaw=<r> --pitch=<r>`, which replaces `bake-preview`.
- `GET /api/v1/viewport`, `PUT /api/v1/viewport/mesh`, `PUT /api/v1/viewport/camera`, frames from `/api/v1/viewport/frames`,
  and `/ws/viewport` for input.
- A killed Worker no longer costs the view: the Server keeps it, and the next Worker carries on within two seconds.
- `patina-worker --shader-cache <folder>`, and a disk cache of compiled shaders.
- Texture graphs over the API: open a graph file, read its nodes, edges and parameters, list node types for a palette, and
  save it back (`/api/v1/graphs`). A graph that opens is shared by every client that opens it.
- Undoable command batches with server-side undo and redo, and numbered delta events over `/ws/graphs/{id}` and
  `/api/v1/graphs/{id}/events`, so a second client sees the first client's edits.
- Graph evaluation in the Worker: a slider edit does not recompile, only what depends on it runs, and a killed Worker loses
  nothing. Each node's preview is a PNG at `/api/v1/previews/{hash}`, and events carry the hash.
- The graph editor: select a graph in the Library's Shared Assets, choose Open, and edit it in the Graph pane. Search for a
  node and add it, wire, move, delete, undo, redo and save, with a thumbnail on every node that follows each edit and a second
  client's edits arriving without a reload. The Inspector sets a selected node's numbers, colours, text and choices, and
  the Texture pane shows its picture. The pane says when no graph is open, when the Worker or the Server has gone, what the
  compiler reported, and what is unsaved.
- New graphs: `POST /api/v1/library/graphs` writes an empty graph into `shared/graphs` or a project, the scan indexes it,
  and New in the Graph pane creates and opens one.
- Graph settings in the Inspector: width, height and seed.
- Exposed parameters: add, edit and remove them, with live defaults, and drive a number from one with an expression.
- Groups and notes: commands, state, deltas, frames and cards on the canvas. Dropping a node on a frame moves it there.
- Compounds: double click one or choose Open compound to go inside, a breadcrumb back, and saving one updates the graphs
  holding it. Those that ship with Patina open to be read.
- Compound authoring: `addPort`, `setPort` and `removePort` commands, an Interface list in the graph settings, Compound in
  the Graph pane to save a selection into `Compounds` as one undo (`POST /api/v1/graphs/{id}/compounds`), and Copy to edit
  on a shipped compound (`POST /api/v1/library/compounds`).
- A bit depth control in the graph settings, and a `bitDepth` setting a command can reach.
- Bitmaps evaluate: PNG, JPEG, BMP, TGA, PSD and GIF, by a path relative to the graph or the library.
- A curve editor and a gradient ramp for the Curve and Ramp settings, held inline in the graph's file.
- The Texture pane has a histogram, counted from the reduction and labelled so, a channel view, and Full size, from
  `POST /api/v1/graphs/{id}/images`.
- Quitting with unsaved edits asks first, and multi-selection has an Inspector state of its own.
- High-to-low baking: `POST /api/v1/jobs/bakes` takes paths to a high and a low mesh and returns paths to the maps: normal,
  height, ambient occlusion, bent normal, curvature, thickness, position, world normal and ID, as 8- or 16-bit PNG or 16- or
  32-bit EXR. Hard edges do not seam, a cage decides what rays reach, skew and a skew map straighten detail near hard edges,
  and parts match by name. The baker uses every core but two and gives the same bytes on any number of them.
- A persistent job queue: a bake survives a killed Worker and a killed Server and carries on, and stops being retried
  after three Workers die on it. Follow jobs over `/ws/jobs` or `GET /api/v1/jobs/events`; `POST /api/v1/jobs/{id}/cancel`
  stops one between rows of the atlas. The same bake twice comes from the cache, even with its meshes renamed.
- A long bake holds up neither the viewport nor graph evaluation.
- `patina bake-maps`, the same bake in-process for a build farm, which can share a cache folder with others.
- The Bake panel: choose meshes, cage, size, format and maps, watch progress, and show results in the Texture pane.
- Source/Mesh Map nodes read a finished bake once a graph is pointed at it, with Use in the Bake panel or
  `PUT /api/v1/graphs/{id}/mesh-maps`. The choice is kept in a `.bake` file beside the graph.
- `patina-bench bake-pair`, a generated hard-edged high and low pair; `patina-bench s9`, and `patina-bench bake-preview`,
  which shades a low mesh with its maps on the CPU.

### Changed

- The app has its new icon, on the window, the installers and the studio's favicon.
- A graph's compounds now come from a `Compounds` folder beside it, not from every graph in its folder.
- Groups and notes have an `id`, in the file and the API, and commands name them by it instead of `index`, so two clients
  can no longer hit the wrong one.
- `patina bake` accepts everything the Worker evaluates: bitmaps, inline ramps and curves, and a compound on its own, with
  `--library <path>` where the graph is outside its library. The bytes match the Worker's, and `vixen texture bake`'s.
- A compound with an image input shows its nodes when opened on its own, with a noise standing in for what would feed it.
- The minimap leaves a small canvas and steps aside while a wire is being dragged, because it took the drop on a port under
  it.
- A graph that does not compile makes `patina bake` say so rather than crash.
- Undo, Redo and Save work from the Inspector.
- The evaluation readout says Evaluating…, Up to date or Not evaluated, and no longer shows an old count after an edit.
- A slider on a compound, or on an exposed parameter, no longer recompiles the graph.
- Graphs can compute in 32-bit float: `bitDepth: 32` in a graph's settings, for height and position. PNG previews and
  bakes are still eight bits.
- `GET /api/v1/graphs/{id}/node-types` gives an input port's `range`, with `minimum`, `maximum`, `softMinimum` and
  `softMaximum`, where the node defines them, and the Inspector's sliders use them.
