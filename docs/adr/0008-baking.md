# ADR 0008: Baking (spike S9, and how a bake runs)

**Status:** S9 failed, 2026-11-05. The rest is accepted. **Pass criterion (plan §6):** threaded CPU occlusion, 4K, 256 rays, two
million triangles, under 2 minutes on a current laptop. **If it fails:** bring forward GPU traversal in Raven compute.

## S9: failed, and the choice it leaves

`patina-bench s9 <folder> 4096 256 0.5` runs the real path, file to file. The mesh is a bumpy torus knot of 2,000,000 triangles, and
the low is a smooth one with a full atlas: 16,777,216 covered texels, the worst an atlas can be, so 4.29 billion occlusion rays. An M1
Max has 8 performance and 2 efficiency cores.

| Measure | Result |
|---|---|
| Vixen's baker as it stood, one thread | 0.19 M rays/s, an extrapolated 6.1 hours |
| Threaded, SAH, four wide, `patina-bench s9`, on a loaded machine | 447 s wall, 2,920 CPU-seconds, 9.6 M rays/s, 3.2 GB resident |
| The best those CPU-seconds allow on 10 idle cores | 292 s |
| Target | 120 s |

The wall time was measured with other builds running (load average 46 to 482), so it overstates the cost; the CPU time is what to read,
and it fails by a factor of 2.4 however idle the machine is. Efficiency cores make the real floor higher.

Three numbers this ADR first gave were weaker than they read, and are corrected here:

- **The 4K figures were not from `patina-bench s9`.** They were 332 s wall and 2,761 CPU-seconds from a scratch harness, which is
  gone, on 16.6 million texels. The table is now the command anyone can run again. The command needed no change to reach 4K.
- **"A real atlas covers about 70%, which is about 193 s" is arithmetic**, 70% of the idle-machine floor, and nothing measured it.
  The same sum on the new CPU time gives 204 s.
- **"A reach of a tenth of the diagonal is about 1.5 times faster" was measured at 1024, under load, by wall time.** By CPU time at
  1024 and 256 rays, in two runs each, it is 1.25 times: 184 and 186 CPU-seconds at a reach of a half, 148 and 148 at a tenth. No 4K
  run has measured it.

**The CPU is not going to get there on its own.** Per ray, one-threaded, the tree cost 320 ns on 2,000 triangles and 680 ns on two
million, so more than half is cache misses, and arithmetic can only win back the rest. Removing the division, a branch-free push
and any-hit ordering each moved it by 10% or less, and the branch-free push lost on the large mesh. The other ways down are Embree,
which is a native library per platform, and GPU traversal, which the plan names.

**Not started: GPU traversal.** The plan says one implementation, and starting it is a decision about Raven compute, MoltenVK and
Worker scheduling that this phase should not make by momentum. Everything else in P4 stands either way: a GPU path would replace
`BakeTree` and the rays, and keep the cage, skew, parts, charts, files and jobs. Until then a 4K, 256-ray bake of a dense mesh takes
minutes, and 2K at 64 rays takes seconds, which is usable. Whether that is enough is the owner's call.

## How a bake runs beside everything else

The Worker answers IPC one request at a time, and a reply over 30 s counts as a lost Worker. Neither changes.

**`bake.start` answers at once.** The bake runs on a thread of its own, and tells the Server how it is going with `bake.progress` and
`bake.ended`, which are requests in the other direction. So the loop is never waiting on a bake, the viewport and graph evaluation
carry on through it, health probes are answered, and no reply can time out however long a bake takes. Two tests hold this, as an
order and not a timing: `A_graph_evaluates_while_a_bake_runs` looks at the job after the evaluation arrives, and
`The_worker_keeps_answering_through_a_bake` waits out two health probes mid-bake.

A second process per bake was the alternative, and would have isolated a crash in a bake from the viewport. It was turned down
because the Server starts no process (plan §8), and the Worker that holds a bake and the one a supervisor restarts should be the same
thing. The cost is that a bake that crashes the Worker takes the viewport with it, for the second that S5 measured.

A bake casts on all cores but two, which are left to the viewport, evaluation and the loop. A long bake still slows them by
competing for memory.

## Jobs

The Server owns them. Each job is one JSON file under `.ptcache/jobs`, replaced atomically. Deleting that folder loses what was asked
for and not yet made, which a client can ask again. A running job is only ever a job that can be run again, so:

- A Worker's connection closing puts its job back in the queue. S5's bake half is `A_killed_worker_loses_no_job_and_its_successor_runs_it`.
- A Server starting finds a running job and does the same: `A_killed_server_comes_back_to_its_queue`.
- Three dead Workers fail a job, across restarts too, so that a job that crashes the machine cannot loop.
- A report carries its attempt, so a Worker that lost a job cannot touch the next attempt.

Results are cached by `BakeKeys`: inputs count by content and by role, and paths do not count. A bake is made in a `.part` folder and
moved into place, so a folder in the cache is whole. The Worker writes only there; the Server copies the maps to the requested
output and leaves a `<name>.bake.json` beside them with the scale that undoes a signed map's encoding.

## Hard edges

A hard edge is two normals on one point, and rays along each corner's normal see different surfaces. Rays now follow the normal
averaged over the point, while the tangent frame keeps the corner's own. Vixen's `HardEdgeBakeTests` shows the gap closing with
resolution and the old rays not closing it. The price is that detail beside a hard edge shears, which skew and a skew map trade back.
Passing a test does not meet the exit; only an artist can, and they judge it in the viewport: choose the bake and the low mesh shows
it (`PUT /api/v1/viewport/bake`). `patina-bench render <low> <out.png> <w> <h> --bake=<folder>` draws the same frame with no Server.

## The basis

The basis is MikkTSpace, in Vixen's `MikkTSpace`, a managed port shared by the baker and the viewport. The baker writes the exact
inverse of the renderer's frame, so what is drawn is what was measured to within 4.8e-7 of a sine on a curved low mesh, against
9.7e-4 for a projection onto an orthonormal frame. It matches the original compiled, bit for bit, over 2,000 awkward meshes and
1.78 million corners. Every file that holds a map is v-down: row zero of a picture is `v = 0`, and green points up the image, as glTF
reads it. Bakes before this were upside down against their own atlas, which `bake-preview` hid by reading `v` the other way; the bake
key names the basis so the cache cannot hand one back.

The renderer does not use a file's own `TANGENT` attribute. The baker never reads one either, so the two agree, but a map authored
elsewhere against tangents that were not MikkTSpace will not match.

## EXR

The plan expected a native library. **A managed writer was compared first, and won.**

| | OpenEXR through a native library | `ExrCodec` in `Core.Imaging` |
|---|---|---|
| Licence | BSD-3-Clause, compatible with GPL-3.0-or-later | Vixen's Apache-2.0 |
| Packaging | OpenEXR, Imath, libdeflate and OpenJPH, built and signed for three platforms, in `native-dependencies.json`, in every workflow | Nothing |
| Reach | Everything the format has | Scanline, ZIP, half and float, one to four channels |
| Size | Tens of thousands of lines | About 300 lines over `System.IO.Compression` |
| Risk | ABI, packaging and a native crash in the Worker | A format misread, which tests against FFmpeg's decoder and macOS's image reader rule out |

A bake writes flat images, so that subset is all it needs. A reader refuses the rest. `THIRD-PARTY-NOTICES.md` and
`build/native-dependencies.json` do not change, and nothing new installs on any platform in CI or the app. Choosing PIZ or a tiled
file later would reopen this.

## Shape (#147)

A bake is as wide as `BakeOptions.Resolution` and as tall as `BakeOptions.Rows`, which is null for a square. The atlas still
spans the unit square of UV, stretched to that many rows, so the layout needs no change and a mask is still read by UV. The
height is a bake option rather than read from each texture set's size, because the bake settings are the project's and apply to
every set (#147's wave 3); a project whose sets are all 2:1 sets it once. A manifest's map says its `rows` only when it is not
square, and the key reads `rows` only when it is set, so every square bake keeps the key it had.

The position map's range (#148) is `BakeOptions.Position.Range`: `box`, as before, `cube`, `sphere` or `none`. It is part of the
options, so it joins every bake's key, and a bake made before it is baked once more rather than taken from the cache.
