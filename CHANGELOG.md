# Changelog

Notable changes to Patina. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
  metallic-roughness factors. Texture maps are not read yet.
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
- A slider on a compound, or on an exposed parameter, no longer recompiles the graph.
- Graphs can compute in 32-bit float: `bitDepth: 32` in a graph's settings, for height and position. PNG previews and
  bakes are still eight bits.
- `GET /api/v1/graphs/{id}/node-types` gives an input port's `range`, with `minimum`, `maximum`, `softMinimum` and
  `softMaximum`, where the node defines them, and the Inspector's sliders use them.
