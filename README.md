# Patina

A material authoring suite: procedural graphs, painting on meshes and baking in one application with one evaluator,
built around a **Library** that holds one game's thousands of objects and rebuilds only what changed. It has a headless
backend with a versioned API, so the Electron editor, the `patina` CLI and a future Blender plugin are all clients of
the same Server. The design is in [docs/01-implementation-plan.md](docs/01-implementation-plan.md).

"Patina" is a working name, to be changed after a trademark check before the first release.

## Status

**Usable for trying out, not yet released.** There is no packaged download, and the macOS package still needs a Vulkan loader
([#48](https://github.com/Rikarin/Patina/issues/48)). The engine is strong: incremental GPU graphs, a Worker that can die at any
moment, baking, BC7 and BC6H encoding and keyed batch rebuilds. The app can now do the work from end to end, which the new-user
journey test proves: open a library, make a project from a mesh, bake, layer and paint, and save. Content, viewport and graph editing
are still thin, and the open list is the [audit tracker](https://github.com/Rikarin/Patina/issues/68).

## Running it

You need .NET SDK 11.0.100, Node 24, pnpm and a Vulkan device.

```bash
cd engine && dotnet build Patina.slnx -c Release
```

```bash
cd editor && pnpm install && pnpm exec nx build desktop && pnpm exec electron dist/apps/desktop/main.js
```

File > New Library starts from nothing. `patina-server --library <path> --create-library true` runs the Server alone for other clients.

## Layout

| Path | What |
|---|---|
| `engine/` | .NET 11: Server, Worker, CLI and libraries |
| `editor/` | Nx workspace: `apps/desktop` (Electron), `apps/studio` (Angular), `libs/` |
| `clients/python/` | The generated Python client and its conformance test |
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
