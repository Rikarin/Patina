# ADR 0041: Sending a file from a client's own computer

**Status:** accepted, 2026-11-14.

## What is wrong

Every route that reads a person's file takes a path on the Server's machine: a project's mesh, a picture brought into a project, a
bake's high mesh and cage ([0013](0013-paths.md)). [0033](0033-picking-files.md) let a client of a Server on another computer pick
what the library already holds, but a file on that client's own computer still had no way in (#4). Blender on a laptop attached to a
Server on a workstation could not give it a mesh.

## Decision

1. **One route takes the bytes, and nothing else changes.** `POST /api/v1/library/uploads?name=<file name>` takes the file as the
   request body (`application/octet-stream`), streamed to disk, and answers with the file's full path on the Server's machine. Every
   route that takes a path there takes that one, so no route grows a second way of being given a file, and the Server copies a mesh
   or a picture into its project as it did one from its own disk.
2. **Kept in the library, not the cache.** Files go to `uploads/` at the library's top, each under the first 16 hex digits of its
   SHA-256 with its own name: `uploads/1f0c…/crate.glb`. A bake's high mesh and cage are read where they are, not copied, so a file in
   `.ptcache` would break a project's bake once the cache was deleted, and files are the truth. The digest keeps the name the person
   gave it, which a project's copy takes, keeps the same file sent twice once, and never lets two files of one name meet.
3. **Only what a project reads.** The name must be a file's name alone, not hidden, and a picture or a mesh Patina reads; anything
   else is refused with `upload-kind` before a byte is read. A file over 4 GiB is refused with `upload-too-large` and nothing of it is
   kept. A file is written beside the folder under a hidden partial name and moved into place once whole, so a broken connection
   leaves nothing behind.
4. **The editor offers it where it cannot open a dialog of the machine.** Outside the desktop app, Choose a Picture and Choose a
   Mesh have From This Computer…, which sends the chosen file and answers with it as From a File… does on the desktop.

## Consequences

- Blender and the Python client can send a mesh: send it, then create a project or set its mesh with the path the answer gives.
- `uploads/` grows with every distinct file sent. It is the person's to clear, as a downloads folder is; nothing refers to a file
  there once a project has copied it in, except a bake's high mesh or cage.
- The library's scan and its file lists leave `uploads/` out, so a file sent is chosen through the answer, not found again by
  browsing. A list of what was sent is left for when someone needs it.
