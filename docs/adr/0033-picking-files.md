# ADR 0033: Picking a file from the library

**Status:** accepted, 2026-11-12.

## What is wrong

A layer's picture, a mask's bitmap, a filter's input and the viewport's stencil were each chosen in the desktop's file dialog, and
nowhere else (#4). A picture already in the library, in this project or another, was found again by walking the disk. Outside the
desktop app there was no way to choose one at all, and a client of a Server on another computer could not have shown that Server's
disk in a dialog of its own anyway: [0013](0013-paths.md) left a remote Server out of scope until there were library-relative
references and a browse route.

## Decision

1. **The Server lists what the library holds.** `GET /api/v1/library/files?kind=picture|mesh&project=` gives every picture or mesh
   the library's projects hold, by its path below the library, with the project each is in, the asked project's first. It leaves out
   what a bake wrote, hidden folders and a project folder inside another, and stops at a thousand with `more` set. It reads names
   only, never a file. Any client can offer it, and no client needs to see the Server's disk.
2. **A picture is brought in by its library path too.** `POST /api/v1/library/project/pictures` takes a path below the library as
   well as a full path on the Server's machine. One already in the project stays where it is; one of another project is copied in,
   as one from the machine is, so a project still travels with what it reads ([0013](0013-paths.md)). A path that climbs out of the
   library is refused.
3. **The editor asks once, in one dialog.** Every picture the app reads is chosen through `ProjectStore.pickPicture`, which opens
   Choose a Picture: the library's pictures by project, this one's first, found by name and drawn from `/library/picture`, and From a
   File… on the desktop. With no picture in the library, the desktop's file dialog opens at once, as there is nothing to pick from.

## Consequences

- A client on another computer picks a picture the library holds. It still cannot bring one in from its own disk: that needs an
  upload, which no route takes yet.
- Meshes followed in wave 11: New Project, Project Settings and the Bake dialog choose a mesh through `FileChooser`, which opens
  Choose a Mesh the same way. A bake and a project's mesh read a path on the Server's machine, so a mesh or a picture the library
  holds is given as the library's path joined to its own; the Server copies a project's mesh in as it did one from the disk. Only
  the bake's output folder is still typed outside the desktop app, as the list holds no folders.
- The list is read from the folders each time, as the project's own contents are; a library of many thousands of pictures would want
  it from the index instead.
