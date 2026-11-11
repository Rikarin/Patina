# Patina

A material authoring suite: C# backend, Angular and Electron editor, GPL-3.0-or-later. Read
`docs/01-implementation-plan.md` before larger work; §3 is the architecture and §7 the conventions. Cathode is the
reference for how this repository works, and its code can be copied freely (same owner, same licence).

## Rules that shape every change

- **One copy of the code, and it lives in Vixen (plan §4).** Patina ports nothing. All graph, paint and bake code stays in
  `external/Vixen` under Apache-2.0, with its own gates. Patina holds only what is specific to the product: Library,
  Server, Worker, CLI, export and the Angular app. A change to shared code is made in the `external/Vixen` checkout on a
  Vixen branch, pushed, and the pin moved to it. Never copy Vixen code here. Release tags need the pin on Vixen's main
  branch.
- **The Server owns all state; the Worker only executes.** The Worker can die at any time, so it holds nothing that cannot
  be rebuilt (plan §3.1).
- **Pixels never cross JSON or the socket.** They go through the content-addressed cache and shared memory; the socket
  carries hashes. Viewport frames reach clients as a long HTTP response, never WebSocket messages in a browser (ADR 0003).
- **Nothing in the API knows about Electron (plan §8).** The Server starts with `patina-server --library <path>` and needs
  nothing else. If the API would only suit the editor, it is wrong: Blender is the next client.
- **One evaluator, no CPU twin (plan §3.2).** Value changes never recompile a plan.
- **Files are the truth; SQLite is derived.** Deleting `.ptcache` loses nothing but time.
- **CoreCLR with ReadyToRun, never NativeAOT.** Do not set `IsAotCompatible` or `PublishAot`.
- **No Adobe formats or trademarks**: no `.sbs`, `.sbsar` or `.spp`, and none of their names in the product or in nodes.
- `content/` is CC0, separate from the code's licence.

## Engine

- .NET SDK 11.0.100, `net11.0`, `LangVersion=latest`, nullable, warnings as errors, central package management.
- Tests are xunit.v3 on Microsoft.Testing.Platform, with CsCheck for properties (trait `Category=Property`, so nightly
  finds them). One test project per library.
- `dotnet build Patina.slnx`, then `dotnet test --solution Patina.slnx`. Run from `engine/`.
- **Skala:** `dotnet skala verify engine` from the root must end in `OK`. Severities live only in `.editorconfig`, each
  with a reason that holds for Patina. The baseline starts empty and only shrinks; fix findings instead of absorbing them.
  `external/` is out of scope, since Vixen runs its own gates.
- **OpenAPI:** `engine/src/Patina.Server/openapi.json` is committed and a test fails on drift. Update it with
  `PATINA_UPDATE_OPENAPI=1 dotnet test --solution Patina.slnx`, then regenerate the client with
  `pnpm exec nx run api-client:generate` in `editor/`.
- The version lives in `engine/Directory.Build.props` and `editor/package.json`, and a test keeps them equal.

## Editor

- Angular 22 zoneless with signals, Tailwind 4, Nx 24, pnpm, Vitest, Playwright for Electron, Electron 45, Node 24.
- `pnpm exec prettier --check .`, `pnpm exec nx run-many -t lint typecheck test`, `pnpm exec nx build desktop`, and
  `pnpm exec playwright test --config apps/desktop/e2e/playwright.config.ts` after a Release engine build. CI fails on
  anything Prettier would change; `pnpm exec prettier --write .` fixes it. Tests that open a mesh use
  `content/fixtures/viewport-test.glb`, written by `patina-bench mesh`.
- **Use the xUI skin; don't rebuild it.** Import `theme.css` then `bezel.css`, set `data-skin="bezel"`, and use `pane`,
  `chrome-bar`, `well`, `readout` and friends for app chrome. `libs/studio-ui` holds only controls xUI lacks; a gap in a
  general control gets fixed in xUI. Install the xUI MCP server for agents.
- The renderer is sandboxed. It reaches Electron only through `apps/desktop/src/preload.ts`.

## CI

Actions are pinned by SHA. `.github/actions/setup-dotnet` replaces `actions/setup-dotnet` on purpose: it avoids the broken
aka.ms route. Linux CI gets GPU coverage from lavapipe; real-GPU goldens run nightly on self-hosted runners.

## Writing

Numbered docs and ADRs in `docs/`, a changelog entry for anything user-visible. A rule switched off says why it does not
fit Patina, not why it did not fit Cathode.
