# ADR 0003: Frames to the renderer (spike S3)

**Status:** accepted for P1, with one decision left to the owner (below), 2026-11-05. **Pass criterion (plan §6):** 1440p at
60 fps from Vulkan readback to a canvas, under 2 frames of added latency, under 10% of a core.

## Decision

Frames reach the renderer as **one long HTTP response**, `GET /api/v1/viewport/frames`, read with a BYOB `fetch` reader.
Input goes the other way over `/ws/viewport?frames=false`. Between the Worker and the Server, pixels stay in shared memory
and the socket carries a slot's offset (plan §3.1).

The plan's first candidate, WebSocket binary, fails by a factor of six. Shared memory through a native addon is the best
path by CPU, but it needs an unsandboxed renderer, so it is **not** shipped. That trade is the owner's call.

S3 passes on frame rate and latency, and **fails its CPU line**: orbiting at this size costs about two cores. At rest
everything is at 0 to 1%, because a frame is only rendered when something changed.

## Numbers

Apple M1 Max, 60 Hz, MoltenVK 1.4.2, Electron 45, Release build. The fixture mesh has about 9,000 vertices. A camera per
display frame. "Input to paint" runs from sending an input to the paint call for the first frame that includes it; the
display adds its own scan-out on top. CPU is percent of one core, from `ps` time over the run.

| Path | Size | fps | Throughput | Input to paint, median / p95 | CPU |
|---|---|---|---|---|---|
| WebSocket, WebGL | 2560×1440 | 10.0 | 147 MB/s | 160 / 233 ms | renderer 80, Server 22, Worker 30 |
| WebSocket, canvas 2D | 2560×1440 | 12.8 | 189 MB/s | 163 / 241 ms | renderer 100, Server 14, GPU 16 |
| WebSocket, WebGL | 1920×1080 | 21.8 | 181 MB/s | 136 / 192 ms | renderer 98, Server 15 |
| WebSocket, WebGL | 1280×720 | 50.3 | 186 MB/s | 78 / 104 ms | renderer 97, Server 15 |
| Stream, plain reader, WebGL | 2560×1440 | 56.2 | 828 MB/s | 20 / 22 ms | renderer 85, Server 58, GPU 11, Worker 9 |
| Stream, plain reader, canvas 2D | 2560×1440 | 56.2 | 828 MB/s | 18 / 19 ms | renderer 82, GPU 60, Server 57 |
| Stream, plain reader, WebGL | 1280×720 | 59.5 | 219 MB/s | 17 / 19 ms | renderer 31, Server 22 |
| **The pane: stream, BYOB, WebGL** | **2502×1724** | **60** | **1.03 GB/s** | **18 to 21 ms** | **Server 81, renderer 80, GPU 24, Worker 15** |
| Shared memory, addon, WebGL | 2560×1440 | 59.8 | 883 MB/s | 20 / 21 ms | Worker 16, GPU 14, renderer 9 |
| Shared memory, addon, WebGL | 1280×720 | 59.8 | 221 MB/s | 17 / 22 ms | Worker 10, GPU 6, renderer 6 |

Reference points: the renderer alone does 1440p in 2.17 ms including readback (451 fps, `patina-bench render`). The bench
rows above upload a texture but draw with no program; the pane row is the real product, drawing, at 4.3 megapixels against
1440p's 3.7.

Run `pnpm exec playwright test --config apps/desktop/e2e/bench.config.ts` for everything except shared memory, whose harness
is `apps/desktop/e2e/spike-shm/`.

### With textures

The renderer alone, `patina-bench render <mesh> <out.png> 2502 1724`, 240 frames of a spinning camera. Before and after were
alternated in one sitting, on a machine with other builds running (load average 125 to 225), so read the medians, and read them
against each other.

| Scene | Median | fps |
|---|---|---|
| The fixture, before textures | 2.48 to 2.54 ms | 344 to 383 |
| The fixture, with the textured forward pass | 2.52 to 2.59 ms | 280 to 384 |
| The hard-edged housing's low mesh, with a 2048 normal and occlusion map | 2.68 to 4.44 ms, rising with the load | 138 to 308 |

The fixture has no texture coordinates, so that row is what the wider vertex and five samples of a one-pixel texture cost a mesh with no
maps: about 0.05 ms, or 2%. A frame was 2.2 ms of a 16.7 ms budget and stays so, and this does not move the 2 cores spent orbiting,
which are the two copies of the frame named below. Tangents are only made when a normal map asks: laying a bake over a mesh of two
million triangles took 3.1 s including its two PNGs, inside the Worker's 30 s reply limit.

## What the numbers mean

- **The WebSocket limit is Chromium's receive path, not the engine.** About 190 MB/s at every size, with the renderer at
  100% of a core and the Server idling at 15%. The number does not move with resolution, painter or anything on our side.
- **A response body is about 4.5 times faster than WebSocket messages** in the same browser from the same Server, on the same
  loopback socket. A BYOB reader then closes the last 4 fps by not allocating a chunk per read.
- **The latency budget is met**: 19 ms is 1.2 frames, inside the plan's 2. The readout counts from send to paint.
- **Zero copy is refused.** `napi_create_external_arraybuffer` answers status 22 in Electron, whose V8 sandbox will not wrap
  memory it did not allocate, so the addon has to copy. That copy is cheap: 0.4 ms for 14.7 MB.
- **Most of the stream's CPU is two avoidable copies of the frame**: Kestrel into a socket and Chromium out of one. The
  shared-memory row shows what removing them saves: about 40% of a core in place of about 200%.

## The decision left to the owner

Shared memory would save about 1.6 cores while orbiting. It costs:

- **The renderer sandbox.** `CLAUDE.md` says the renderer is sandboxed. A sandboxed preload cannot load a native addon, and the
  harness ran with `sandbox: false`, `nodeIntegration: true`. A hardened version could use an unsandboxed preload behind
  `contextIsolation`, which was not measured, and `contextBridge` may add a copy.
- **A native module per platform**, in a stack that otherwise has none in Electron.
- **A second frame path**: same-machine clients would learn a slot's name from the Server, and remote ones keep the stream.

I would not trade the sandbox for CPU on a machine that holds 60 fps either way. If the CPU matters, try Electron's
`sharedTexture` first: it keeps the sandbox and avoids the copies. It is experimental, main-process only, and Vixen has no
external-memory support yet (plan §1, finding 6), so it is later work. It fits behind `FrameTransport` in the renderer and
`IFrameSubscriber` in the Server. A cheaper step is moving the reader and canvas into a Web Worker, so the 80% of a core
stops competing with Angular on the main thread. That is not done.

## How it is built

- `IFrameSubscriber` is the engine's seam: `FrameOutbox` holds one frame beyond the one being sent, so a slow client skips
  frames. WebSocket binary stays as a second implementation, for non-browser clients and because the tests and this bench
  use it. The session knows neither.
- `FrameTransport` is the client's seam; `StreamFrameTransport` is its one implementation, and the pane knows nothing else.
- **Shared memory between the processes:** four 4K slots in a temp file, sparse until touched. Slots are reference counted, so
  a client mid-send keeps its frame. A file-backed mapping gets written back by the OS; I judged that harmless, at a few
  frames per flush interval, but did not measure it.
- **Adaptive render scale:** `RenderScale` steps down by 15% to at most 50% while the user's hand is on the view and a frame
  costs more than 16.7 ms, and back up with headroom. The next frame after release is full size. It acted once in the pane
  run, during start-up (1806×1244), then recovered to full size and held there.
- The first frame after a Worker start is slower, and the WebSocket and REST channels are not ordered with each other. A
  client that needs both applied in order must wait for the state, as the tests do.

## Not measured

Textured frames on an idle machine, or through the whole pane. Windows, Linux and any non-Apple GPU, where readback is not unified memory and will cost far more than 2 ms. A heavy mesh.
Two clients at once. `sharedTexture`. A 120 Hz display.
