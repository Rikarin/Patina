# ADR 0039: The Server reads no mesh; it keeps what the Worker read of each file's texture sets

**Status:** accepted, 2026-11-14, for issue #285.

Plan §3.1 puts every native parser in the Worker: a bad FBX may crash Assimp, and that must take down the Worker, which
can die at any time, never the Server, which holds every session. Waves 13 and 14 broke that four ways. A texture set's
UDIM tile (`SetUdim`, for its state and a preset's file names), whether a bake still fits its set's faces
(`BakeFit.Geometry`), and a bake's key (the low mesh's faces and the pictures a colour bake reads) were all read with
Vixen's `ModelReader` inside the Server.

## Decisions

- **One read per file, of every set.** The Worker answers `mesh.sets` with each material, and the whole mesh, in each UV
  set it can be laid out in: its UDIM tile and the revision of its faces as a bake records them, and the base colour
  pictures the file names (`MeshFileReads.All`). The file is read twice at most, once for the first UV set as a bake reads
  it, so a revision is byte for byte the one a bake writes in its manifest, and once with every set. A test compares the
  one read with reading each set alone.
- **The Server keeps it until the file's bytes change** (`MeshSetCache`): a new length or time is hashed, and a file
  written again with the same bytes, as an exporter does, keeps what was read of it.
  Everything that asks goes through `IMeshSetReads` (Patina.Library), which the Worker and `patina` implement by reading
  the file and the Server by asking the cache. The library code that needs a tile or a face revision takes one as a
  parameter, so no path from the Server reaches the reader.
- **A question waits for the read, not for an evaluation.** A set's state is a property, and it used to read the mesh in
  place; it now waits up to two minutes for the Worker's read. So the Worker answers `mesh.sets` beside its queue
  (`IpcConnection.Aside`): it uses no device and keeps nothing, and behind an evaluation on a software GPU it would wait
  minutes. With no Worker connected nothing is known and nothing is kept, and a set says tile 1001 as for a mesh that
  will not read. A bake asked for then is keyed by the low mesh's bytes rather than its set's faces, so it can miss a
  cached bake of the same faces and bake again; it is never answered wrongly.
- **A file that took the Worker down is not asked about again** until it changes. Asked again, it would take the next
  Worker down too. It is kept as a file that does not read.
- **A guard test reads the IL.** `MeshReadGuardTests` walks every method the Server's assembly can reach through Patina's
  own assemblies, counting every overload of a name, every virtual method of a type that is made and every async body,
  and fails on a call to `Vixen.Interchange.ModelReader`, naming the reader and the Server methods it is reached from. It
  treats framework and Vixen code as leaves; only Patina reaches `ModelReader`, so that misses nothing today.

## Not yet

- Pictures still decode in the Server for previews and import (`MapPictures`). Plan §3.1 lists image codecs with the
  Worker too, and the same guard could name `Vixen.Interchange`'s decoders once they move.
- `GET /library/mesh` asks the Worker every time; it could answer from the same cache.
