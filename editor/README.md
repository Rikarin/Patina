# Editor

An Nx workspace: `apps/studio` is the Angular renderer, `apps/desktop` the Electron main process and preload, and
`libs/api-client` the client generated from the Server's committed OpenAPI document.

```sh
pnpm install
pnpm exec nx run-many -t lint typecheck test
pnpm exec nx build desktop
pnpm exec nx serve desktop   # needs a Release engine build; PATINA_LIBRARY opens a library
pnpm dist                    # installers for this OS, in dist/release
```

In a plain browser (`pnpm exec nx serve studio`) the studio talks to a development Server on `http://127.0.0.1:5190`,
which is what `patina-server` listens on when started with no library.

**Bezel:** `@xui/core` 3.0.1 has no `styles/bezel.css`. Uncomment the one marked line in `apps/studio/src/styles.css` once
xUI 3.1 is out; `data-skin="bezel"` and the `chrome-bar` and `readout` classes are already in place.
