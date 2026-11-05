# ADR 0005: Where node previews live

**Status:** accepted, 2026-11-05.

## Decision

A node's preview is a PNG in a content-addressed folder of the library, `.ptcache/previews/<first two hex>/<hash>.png`,
where the hash is the SHA-256 of the file's bytes. **The Worker writes it, the Server serves it**, at
`GET /api/v1/previews/{hash}`, and an event carries only the hash. A Server with no library uses a temp folder.

- **Not in the Worker's memory.** The Worker can die at any time, and a client must still reach the picture it was told
  about. A file outlives the Worker, and the Server can answer without one.
- **Not through the Server.** The socket carries hashes (plan §3.1). The file is the shared memory for this path: previews
  are small and rarely read twice, so a mapped slot like the viewport's would buy nothing.
- **In `.ptcache`.** It is rebuildable, so it follows the rule that deleting the cache loses nothing but time. After a
  delete, `GET` answers 404 and `POST /graphs/{id}/evaluate` writes the files again.
- **The hash is of the bytes, not Vixen's result key.** A client can check what it received, and two nodes with one picture
  share one file. The result key would be cheaper, needing no encode, but it names a recipe, and a recipe's picture may
  differ between GPUs while a library moves between machines. The Worker keeps a memory from key to hash, which is why an
  unchanged node costs nothing.
- **Written to a unique temporary name and renamed**, so a reader sees all of a file or none of it.
- **A budget of 256 MiB**, trimmed oldest first by the Server, every 64 evaluations. A drag leaves a preview per position,
  and none of them is worth keeping. Previews a client is showing are never trimmed.

## Size, and a real image

Previews are at most 256 pixels on a side, and they are reductions of the image the graph really computes, not a second
evaluation at a low resolution: resolution-dependent nodes would otherwise show something else. The evaluator reduces each
image with Vixen's `Resample` kernel, as ops appended to the plan in the Worker, because reading a 2K image back costs tens of
milliseconds before any encoding. No Vixen change was needed. Only the reductions are the plan's outputs, so 2K intermediates
are kept while they fit the result cache and are never pinned by previews.

## Full size

`POST /graphs/{id}/images` asks for one node's picture at the size the graph computes it. The Worker reads it from the plan
the last evaluation ran, usually out of the result cache, and writes a PNG into this same cache under the same rules; the
reply carries the hash, and `GET /previews/{hash}` serves the bytes. So there is one way to fetch a picture, pixels stay on
plain HTTP, and a file trimmed from the budget is written again by asking again. It holds up the next evaluation while it
encodes, and a Worker that has not evaluated the graph answers 409.

## Numbers

A slider, from the start of the REST call to the hash arriving in an event on the WebSocket: 2048×2048, 40 nodes (a noise,
a chain of 37 HSL and blur nodes, an Output, a side branch), every node previewed. Apple M1 Max, MoltenVK, Release build,
loopback. Three runs of 40 edits each; ranges are across runs.

| Slider on | Ops run | Median | p95 | Rate at the median |
|---|---|---|---|---|
| The head of the chain, the worst case | 82 | 137 to 139 ms | 152 to 198 ms | 7 Hz |
| The middle | 42 | 78 to 86 ms | 91 to 113 ms | 12 Hz |
| The tail | 2 | 8.9 to 9.4 ms | 11 to 13 ms | 100 Hz |

The first evaluation, cold Worker included, takes 0.8 to 0.9 s. No edit compiled. The Server adds 1 to 2 ms to the Worker's
time, and a drag faster than these rates skips positions rather than queueing them.

Where the worst case goes, measured before encoding went parallel: evaluation **30 ms**, reading back 41 previews **80 ms**,
encoding **180 ms**, now about 25 ms on ten cores. So the GPU is not the limit: the largest cost left is one synchronous
readback per preview, about 2 ms each, since `TextureBake.Read` opens a frame and waits for the device every time.

Reproduce with:

```
dotnet build Patina.slnx -c Release
PATINA_BENCH_OUT=/tmp/bench.txt tests/Patina.Server.Tests/bin/Release/net10.0/Patina.Server.Tests \
  -method Patina.Server.Tests.GraphProcessTests.A_slider_on_a_2K_40_node_graph_is_measured_end_to_end
```

## Costs and open questions

- **The worst case misses a strict reading of "interactive".** A batched read in `TextureBake`, one submission for all
  previews, should take about 70 ms off it. It needs a Vixen change, and it was left out because nothing fails without it.
- **The chain is cheap.** HSL and a radius-3 blur are not the heaviest kernels, so a graph of warps and flood fills will
  spend more in evaluation. It has not been measured.
- **A long evaluation holds up the viewport.** The IPC handler answers one request at a time, in order, so a frame waits for
  an evaluation in progress. An evaluation over 30 s counts as a lost Worker (ADR 0004).
- **A graph with a compound recompiles on a value edit.** `TextureGraphCompiler.Refresh` refuses any graph that inlined a
  sub-graph, by design. The result cache still limits what runs, but the "no recompile" rule holds only for plain graphs.
- **Not measured:** Windows, Linux, any GPU where readback is not unified memory, and what a full-size image costs.
