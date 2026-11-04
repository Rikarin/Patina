# 0001: Library index (spike S6)

**Status:** accepted, subject to review. **Result:** all three S6 targets pass at 10,000 projects, with room to spare, on
a warm file cache. A cold disk was not measured (see "Not measured").

## Decision

Keep the design of plan §5: files are the truth, and one SQLite database with FTS5 in `.ptcache/index.sqlite` is derived
from them. No change to the index design was needed.

- **A full build is a rescan of an empty index.** One code path, and "rescan equals full rebuild" is a property test.
- **Each file's rows derive from that file alone.** A changed file replaces its own rows without looking at any other,
  and a project found under a new path is simply a delete and an insert.
- **Unchanged files cost one stat.** Size and time first, then the hash, then a parse only if the hash moved. A stamp is
  trusted only if the file was already 2 s old when it was read (Git's racy-clean problem): a test edits a file within
  the same timestamp and at the same size right after a scan, and it is seen.
- **One write transaction per scan, in write-ahead-log mode.** An interrupted scan leaves the last good index, and readers
  carry on during a big rescan.
- **The index has no migrations.** Another version, or a file that is not a database, is thrown away and rebuilt.
- **Opening does not scan.** It reads `library.yaml` and connects. The Server opens at once, rescans in the background
  and reports `indexing` in `GET /api/v1/library`.
- **A scan does not enter a project.** A folder with a `project.yaml` is a leaf, so meshes and paint cost nothing and
  projects cannot nest.

Decisions the plan left open, for review:

| Decision | Why | Cost |
|---|---|---|
| Asset identity lives in a sidecar, `<file>.asset.yaml`, for every kind of asset | `.vxtexgraph` has no GUID and a binary alpha cannot have one | A move must take the sidecar along; the check reports a file without one |
| A revision is `sha256:<hex>` of the asset's file | Keys travel between machines, so a collision would ship the wrong texture | Slower than xxHash; irrelevant at these sizes |
| Pinned bytes are kept in `shared/.revisions/`, committed, not in `.ptcache` | A pin must survive a deleted cache | Library size grows; nothing collects unused revisions yet |
| **A pin freezes the one file it names; what that file reads still floats** | It is the only rule where keys tell the truth, since an old graph still resolves its own references | "Pinned" is not a deep freeze. A change to a graph reaches a project pinned to a material that floats on that graph, and impact reports that project as rebuilding. The sidecar's current dependency list applies to an old revision |
| Impact counts: `Projects` is everything that reaches the asset, `Pinned` those it cannot change, the rest rebuild | "affects 412 projects, 3 pinned" in plan §5 reads either way; this one adds up | The figures count at project level: the 5,799 here is an upper bound, because a project's outputs may not use that reference (3,988 projects really went stale) |
| Output declarations live in `project.yaml` under `outputs:`; each names the files and references it reads | A change to anything else must not stale it, and the tool's version comes from a `ToolCatalog`, not the file | `bakes.yaml` keeps settings only and is not read yet. The two will need reconciling in P4 |
| An output is stale when no object exists at its key in `.ptcache/objects` | The plan's rule, and it needs no state of its own | A deleted cache reads as "everything stale", which is true |
| Until the Worker reports kernel versions, every tool's version is the engine release | Stales too much on upgrade, never too little | A new release rebuilds everything |
| `check` reads every `project.yaml` to get at outputs; the index does not hold them | The index stays small, and `check` is the one whole-library operation there is, in batches | About 1.3 s at 10,000 projects |
| `ILibrary` is synchronous and paged, and `ListProjectsAsync` is gone | It had no callers or implementation, returning every id contradicts "nothing loads a whole library", and Microsoft.Data.Sqlite has no real async | A remote `ILibrary` would need another interface |
| SQLitePCLRaw is pinned to 2.1.14 by hand | `Microsoft.Data.Sqlite` 10.0.10 pulls 2.1.11, flagged by GHSA-2m69-gcr7-jv3q, and NuGet audit fails the build | One extra line to drop when the package catches up |

## Measurements

`patina-bench s6`, Release build, framework-dependent with no ReadyToRun, on an Apple M1 Max (10 cores, 64 GB) with an
internal SSD at 93% full, on .NET 10.0.14. The library is generated with seed 42: 10,000 projects, 2,000 shared assets, a
Zipf curve of references, 2% of references pinned, 54,003 files, 55 MiB. Reference counts and tags are meant to look like
a game's. Payloads are 2 KiB placeholders.

| Measurement, 10,000 projects | Result | Target |
|---|---|---|
| Full index build, three runs | 2.02 to 2.35 s, median 2.27 s (14,000 files tracked) | minutes: **pass** |
| Cold open, new process to `library.yaml`, counts, first page and first search, seven runs | 122 to 153 ms, median 126 ms | under 2 s: **pass** |
| Search returning a count and 100 rows, 12 queries × 20 | median 1.6 ms, p95 6.5 ms, worst 7.2 ms | under 50 ms: **pass** |
| Worst query: one letter, `c`, 6,463 hits | median 6.5 ms | |
| Listing by tag (two tags) | median 6.7 ms | |
| First, middle and last page, no search | 0.26, 0.39 and 0.82 ms | |
| Rescan, nothing changed | 72 ms | |
| Rescan, 1 file edited | 66 ms | |
| Rescan, 100 edited | 93 ms | |
| Rescan, 1,000 edited | 330 ms | |
| Rescan, 1,000 touched with the same bytes | 85 ms | |
| Search during a rescan of 5,000 files, 85 searches | median 1.7 ms, worst 5.4 ms | |
| Impact over the popular material: 5,915 projects, 116 pinned | median 6.8 ms | |
| `check`, 30,006 outputs, input hashes cached | 1.35 s | |
| `check`, first run, hashing 20,000 input files | 2.3 s | |
| Fan-out: edit the popular material, then `check` | 11,987 of 30,006 outputs stale, in 3,988 projects | |
| Index size | 15.9 MiB | |
| Peak resident memory, whole S6 run | 200 MiB | |

**Headroom.** One run at 100,000 projects (540,003 files, 561 MiB, seed 42), same machine: full build 29 s, cold open
143 ms, index 161 MiB. Search median 10 ms, but the one-letter query reaches 58 ms, over the 50 ms target at ten times the
scope. Listing by two tags takes 82 ms, a rescan with nothing changed 0.9 s, impact over 50,865 projects 65 ms, and `check`
31 s with hashes cached. Most things grow about tenfold; the full build grew 13 times and `check` 23 times, which nobody has
profiled. The first thing to fix past 10,000 is the broad search, by capping the relevance sort; nothing needs it now.

## Not measured

- **A cold disk.** Every number above ran with the operating system's file cache warm: the bench had just written or read
  the same files, and dropping the cache needs root. Cold open reads few files so it should suffer least, but the rescan's
  stat walk and a first full build may be far slower, above all on a spinning disk or a network share. This is the
  biggest gap; repeat it after a reboot.
- **Windows, Linux and other filesystems.** One Mac, APFS. NTFS and Defender will change the walk and the build.
- **Watcher load**, which S6 in the plan lists. There is no watcher yet: it is P3 work, and a rescan is the only way the
  index learns of a change.
- **A real Server under load.** The numbers come from the library layer, one client. The REST overhead is unmeasured.
- **Real payloads.** A full build hashes shared assets and a first `check` hashes output inputs; here those are tiny. Real
  meshes and paint packs make a first `check` as slow as reading them. A rescan does not read project payloads.
- **ReadyToRun.** Cold open is plain JIT, so a published build should be faster, not slower.
- **Long documents.** `project.yaml` holds about 3 to 12 references and 2 to 4 outputs each. Thousands of references per
  project were not tried.
- **Several processes writing at once.** A CLI `check` beside a running Server waits on SQLite's lock, 30 s at most, and
  then fails. It was not exercised.

## Known limits

- A file changed with its size and time restored (`cp -p` over a file of the same size) is not seen by a rescan. Deleting
  `.ptcache` fixes it, which is the general answer.
- Deleting `.ptcache` under a running Server leaves that Server writing to the deleted file until it restarts. Nothing is
  lost: the files are the truth.
- External mesh paths through `library.yaml` (plan §5) are not implemented; output inputs must be inside the project.
- Raising `IndexDatabase.Version` is a manual step when a document gains a migration, because a rescan skips unchanged
  files and would otherwise keep rows read by the old rule.

## Reproduce

```sh
cd engine
dotnet build tools/Patina.Bench -c Release
tools/Patina.Bench/bin/Release/net10.0/patina-bench s6 /path/to/empty-folder 10000 42
```
