# ADR 0006: `@xui/node-graph` at 500 nodes (spike S7)

**Status:** accepted, 2026-11-05.

## Decision

**The graph editor stays on `@xui/node-graph` as it is, with no culling and no fork.** With 500 nodes and a thumbnail on
each, inside the dock manager under bezel, panning holds 60 fps and zooming holds it but for one repeatable stall of about
200 ms. That stall is recorded below. It is not script, so culling in xUI, plan S7's fallback, may not touch it.

The canvas does nothing clever to get here. It sets one transform, and a pan or a zoom writes one signal. Patina's share is
to keep a delta from reaching the other 499 nodes: a view is reused while its node is unchanged, and each thumbnail is on a
signal of its own. Thumbnails have a fixed size, because a node that resizes makes every port on it measure again.

## Numbers

500 nodes, 480 wires, 8,609 DOM elements, each node showing a PNG of 256 × 256. Apple M1 Max, 60 Hz, device pixel ratio 2,
Release engine, Electron 45, window 1440 × 900, minimap on. Three runs of six seconds each; ranges are across runs.

One step of the gesture per display frame, through xUI's own pointer and wheel handlers. "Frames over 33 ms" and the worst
frame are from `requestAnimationFrame`, so they show the main thread. "Dropped" is Chromium's own `PipelineReporter`, which
is the compositor's view; a run has about 360 frames.

| Run | Pane | Zoom | fps | p99 ms | Worst ms | Frames over 33 ms | Dropped |
|---|---|---|---|---|---|---|---|
| Pan | Docked, 855 × 426 | Fit | 59.7 to 60 | 18.6 | 18.7 to 35.2 | 0 to 2 | 1 to 3 |
| Zoom | Docked | Fit | 57.7 to 58.2 | 18.7 | **200 to 232** | 1 | 12 to 16 |
| Pan | Docked | 95% | 60 | 18.6 | 18.7 | 0 | 0 |
| Zoom | Docked | 95% | 59.8 to 60 | 18.6 | 18.6 to 33.6 | 0 | 0 to 1 |
| Pan | Maximized, 1438 × 762 | Fit | 60 | 18.6 | 18.7 | 0 | 0 to 1 |
| Zoom | Maximized | Fit | 58.3 to 58.5 | 18.7 | **165 to 169** | 1 | 11 to 12 |
| Pan | Maximized | 47% | 59.8 to 60 | 18.7 | 18.7 to 33.3 | 0 | 0 to 1 |
| Pan, every thumbnail changing | Docked | 95% | 59.8 to 60 | 18.7 | 18.7 to 32.7 | 0 | 2 to 5 |
| Pan, every thumbnail changing | Docked | 10% | 59.5 to 60 | 18.6 | 18.7 to 34.4 | 0 to 1 | **30 to 45** |

No run showed a checkerboarded frame. Opening the graph: 0.6 to 0.9 s to all nodes in the DOM and 5.5 to 9.7 s to every
thumbnail, of which the first evaluation, 659 operations, is 2.7 to 5.9 s. The JS heap ends at 32 to 47 MiB.

Reproduce with:

```
dotnet build Patina.slnx -c Release
pnpm exec playwright test --config apps/desktop/e2e/bench.config.ts graph
```

## Costs and open questions

- **The stall.** Zooming with everything on screen stalls once per run, at the first zoom, for 165 to 232 ms, and the
  stall's long animation frame has no script, style or layout in it. So it is not Angular and not xUI's code, and the likely
  cause is Chromium rasterising the one large layer at a new scale. Nodes off screen are not the cost at full fit, where
  culling would remove nothing. It was not traced further.
- **"Every thumbnail changing" is not fast.** Every noise in the graph moved, so all 500 pictures were new, but one
  evaluation of this graph took 1.6 to 3.5 s, so the canvas took a burst of 500 fetches every few seconds rather than a
  stream. The fetches are capped at six at once. Far out, where the whole burst is on screen, 8% to 12% of compositor frames
  were dropped, even though the main thread kept its rate.
- **The gesture is synthetic.** Pointer capture needs a real pointer, so the bench stubs it and dispatches events from
  inside the page. A hand's input path, and wheel events off a real trackpad, are not in these numbers.
- **Dragging a node was not measured.** xUI works out every wire's route again whenever any port moves, so a drag at 500
  nodes costs more than a pan, and culling would not change that either. It wants a wire-at-a-time route in xUI if it hurts.
- **Not measured:** Windows, Linux, a 120 Hz display, a mid-range GPU, more than 500 nodes, and a graph that stays open while
  a thumbnail's object URL churns for hours.
