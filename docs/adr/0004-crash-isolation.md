# ADR 0004: Crash isolation of the Worker (spike S5, viewport part)

**Status:** accepted for the viewport, 2026-11-05. **Pass criterion (plan §6):** session intact, Worker back in under 3 s, job
re-queued.

## Decision

Keep the split. The Server holds the viewport's whole state: mesh path, camera, size. The Worker holds only what can be
rebuilt from that: a device, a parsed mesh, a mapped file. When a Worker connects, the Server tells it the mesh and camera
again, then runs the frame that was in flight.

## Result

The Worker was killed with `SIGKILL` in the real desktop app, under its real supervision.

| Measure | Result |
|---|---|
| Kill to the next frame on screen | 1,187 to 1,191 ms in four runs, 1,745 ms in a fifth |
| Of which the supervisor's restart backoff | 500 ms |
| Server | Stayed up; clients stayed connected |
| Mesh, camera | Kept; input sent during the gap is shown by the first new frame |
| Canvas | Keeps the last frame through the gap |

It passes on an M1 Max with warm shader caches. `Patina.Server.Tests` holds the same behaviour without Electron, with the camera
moving when the kill lands (`A_killed_worker_loses_nothing_and_its_successor_carries_on`), and `viewport.spec.ts` holds it
in the app. A Worker whose reply never comes costs a 30 s IPC timeout before the Server treats it as lost.

## Not covered

S5 as written also covers a kill during a bake and during a stroke, and a forced device loss. None exist here: bakes do not run
in the Worker yet, and device loss was not simulated. A first-ever launch on a Mac pays ADR 0002's Metal compile, so recovery
then may exceed 3 s, though after one good start the system cache has the shaders. The supervisor's backoff also grows with
repeated crashes, up to 10 s, by design.
