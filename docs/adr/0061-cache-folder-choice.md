# ADR 0061: A person may keep a library's cache in a folder of their choosing

**Status:** proposed, 2026-11-16, for issue #189. Nothing is built until the owner accepts it.

Preferences can hold the cache to a budget (#189, wave 17) and keep autosaves outside the library (#162), but the cache
itself is always `.ptcache` inside the library. A library on a slow or shared drive, or one synced by a service that
uploads every file, would rather keep what is derived on a fast local disk. Moving the folder is a design change, not
a setting: `LibraryLayout.CacheFolder` is a constant that 24 places join to the library's root, and the folder holds
things of different natures that should not all move together.

## What `.ptcache` holds today

| Folder or file | Nature | Lost when deleted |
| --- | --- | --- |
| `index.sqlite` | derived from the files | a rescan |
| `objects/`, `objects/bakes/`, `results/`, `previews/`, `preview/` | content-addressed, derived | computing them again |
| `reports/` | what builds left | nothing a build reads |
| `jobs/` | requests not yet run | what was asked and not yet made |
| `paint/` | the live paint of open sessions | strokes since the last save |
| `autosave/` | recovery copies, unless Preferences chose a folder | the way back after a crash |
| `mesh-sources/` | where this machine found each mesh | the mesh's source, chosen once again |
| `server.lock` | one Server to a library | nothing, but two Servers could share it |

## Proposal

- **Only what is derived moves.** A chosen cache folder takes `index.sqlite`, `objects/`, `results/`, `previews/`,
  `preview/` and `reports/`: everything "Files are the truth; SQLite is derived" already says can be thrown away.
- **What a crash or a second Server needs stays in the library.** `server.lock` stays, because only a lock beside the
  files guards a library on a shared drive from two machines. `jobs/`, `paint/` and `mesh-sources/` stay, because each
  holds something no file of the library does. Autosaves keep their own folder choice (#162).
- **The folder is chosen per person, not per library.** A `cache.folder` in the Server's `preferences.json`, beside
  `cacheBudget`, a full path as the autosave folder is. Each library gets a folder of its own in it, named as
  `Recovery.Away` names an autosave folder (its name and a hash of its path), so two libraries never share one and a
  person can still find theirs. `library.yaml` never names it: a library cloned elsewhere must not point at this
  machine's disk.
- **A change takes effect when the library opens next.** The Server does not move a cache it is using; Preferences
  says the new folder applies after a restart, and the old one can be deleted by hand, which loses only time. Prune,
  the budget and the Library Settings' cache size read the chosen folder.
- **One answer for where the cache is.** `LibraryLayout` keeps the folder names, and a `CacheRoot` the Server makes
  once, from the library and the preference, answers every derived path; nothing joins `.ptcache` to a root by hand.
  The Worker is told the cache root with the library (a wire change), since it writes previews and results too.

## Questions for the owner

- Is the line between derived and kept the right one? Moving `paint/` too would put live paint on the fast disk, at the
  cost of a crash on a lost local disk losing strokes since the save.
- Should a library be able to say where its cache goes (for a build machine), or is this only the person's choice?
