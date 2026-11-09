# ADR 0018: A bin in the library, one thumbnail service, and preferences in plain files

**Status:** accepted, 2026-11-09, for issues #16, #145, #162 and #189.

## The bin

Deleting a project or a document moves it to `.trash/` in the library folder, beside `projects/`. Each entry is a folder
of its own: `bin.yaml` (what it was, its name, where it lived, when it went), `files/` (the project's folder, or the
document's file, the paint a stack keeps beside it and the note of the bake a graph was pointed at), and, for a document,
its line of `project.yaml` written as a one-document project, so it comes back with its id, role, mesh and tags.

- **Not in `.ptcache`.** Deleting the cache must lose nothing but time, and the bin holds the only copy of what it holds.
- **Not the system's trash.** A restore has to put a document back on its project's list, with its identity, which no
  system trash knows how to do, and it has to work the same on every platform and from every client (`GET /library/bin`,
  `POST /library/bin/restore`).
- **Not in Git.** A new library's `.gitignore` leaves `.trash/` out, so a deletion is a deletion in Git and the bin is this
  machine's. A clone has an empty bin.
- **Emptied only when asked.** Delete Forever on one entry, or Empty Bin, each asked first in the Bin dialog
  (`DELETE /library/bin/{id}`, `DELETE /library/bin`). Nothing empties it by age yet.
- A project whose folder was taken since comes back beside it with a number; a document needs its project back first,
  and its file names free.

Bakes stay where they are when a document goes, because the project's other documents read them.

## One thumbnail service

The shelf's picture service became the Library's: `POST /library/thumbnails` takes any source a list names (a document
below the library, a project's folder, or a material the shelf offers from Patina itself) and answers each with a hash in
the content-addressed preview cache, whose pixels are at `/previews/{hash}`, never in JSON. A project is drawn as the
document that best stands for it: a layered material, then an image material, then a material graph, then a texturing
document. Its picture is that document's, keyed the same, so it costs nothing more. The key of every picture is
everything that decides it (the document, the compounds and pictures it reads, the size), remembered in a small file in
the cache, so a restart draws nothing again and a deleted cache costs only the drawing. In the editor one client service,
`LibraryThumbnails`, asks in batches and holds the object URLs for every list: the Library's grid and list, and the shelf.

## Preferences

Two plain JSON files, each read by what acts on it:

- **The engine's**, `preferences.json` beside the discovery file in the per-user data folder: autosave's interval, idle
  wait and how many to keep. The Server acts on them whatever library it has open, and every client reads and changes them
  through `GET` and `PUT /api/v1/preferences`, so Blender and the editor share them. A setting given on the command line or
  in the environment (`Patina__AutosaveSeconds`, `Patina__PreferencesFile`) wins for that run, which is how tests stay off
  the person's file.
- **The editor's**, `preferences.json` beside `keys.json` in the desktop app's settings folder: the theme, how the
  Library and the shelf open, render statistics. Nothing about them is the API's business.

Settings a project keeps stay in `project.yaml`, and the library's in `library.yaml`.

## Autosave

Autosave keeps a document when its edits have been still for the idle wait (5 s), and at the latest every interval
(60 s) while they go on, as generations in `.ptcache/autosave/<document>/<time>/`. The last three whole ones are kept;
one cut short by a crash, with paint and no text, is passed over. Restore brings back the newest. After the Server
restarts, a document that had unsaved edits comes back from its file and offers the newest autosave, the same offer the
next open makes after a crash.
