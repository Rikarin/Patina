# Patina

A material authoring suite: procedural graphs, painting on meshes and baking in one application with one evaluator,
built around a **Library** that holds one game's thousands of objects and rebuilds only what changed. It has a headless
backend with a versioned API, so the Electron editor, the `patina` CLI and a future Blender plugin are all clients of
the same Server. The design is in [docs/01-implementation-plan.md](docs/01-implementation-plan.md).

"Patina" is a working name, to be changed after a trademark check before the first release.

## Status

**Bootstrap only. There is nothing to use yet.** What exists:

- `engine/`: `patina-server` (`/api/v1/system`, `--library <path>`, a per-user discovery file), `patina-worker` (connects
  over a Unix domain socket and answers health) and `patina version`. `Patina.Library` has only identities, an interface
  and the discovery file. `Patina.Jobs`, `Patina.Export` and `Patina.Viewport` are empty assemblies with a marker type.
- `editor/`: the desktop app, which supervises both processes, and a studio shell with placeholder panes.
- CI workflows, which have not run yet.

There is no graph, painting, baking, GPU code or Vixen dependency yet. The Vixen submodule (`external/Vixen`) has not been
added, and xUI's bezel skin waits for xUI 3.1 (see `editor/apps/studio/src/styles.css`).

## Layout

| Path | What |
|---|---|
| `engine/` | .NET 10: Server, Worker, CLI and libraries |
| `editor/` | Nx workspace: `apps/desktop` (Electron), `apps/studio` (Angular), `libs/` |
| `clients/python/` | The future generated client and conformance test |
| `content/` | Factory content, CC0 |
| `docs/` | The plan and the ADRs |

## Building

```sh
cd engine && dotnet build Patina.slnx && dotnet test --solution Patina.slnx
cd editor && pnpm install && pnpm exec nx run-many -t lint typecheck test && pnpm exec nx build desktop
dotnet tool restore && dotnet skala verify engine
```

To run the app, build the engine with `-c Release`, then `pnpm exec nx serve desktop` in `editor/`.

## Licence

Patina is GPL-3.0-or-later ([LICENSE](LICENSE)); see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

**Your work is yours.** Libraries, projects and exported textures belong to their authors, and the GPL does not reach
them. Factory content in `content/` is CC0, so textures built from it can ship in anything. Programs that talk to the API
over HTTP are separate programs and need not be GPL.
