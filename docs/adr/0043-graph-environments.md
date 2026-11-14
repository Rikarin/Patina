# ADR 0043: A graph lights the viewport as a panorama drawn from its file

**Status:** accepted, 2026-11-14, for issue #180.

Wave 13 gave graphs the nodes that make an environment (Sky, Panorama Rotate, Panorama Light, Horizon, Exposure), and
the issue's next step was the one that makes them useful: let a graph be what lights the viewport. The viewport read
only a built-in room or an `.hdr` or `.exr` panorama, by path.

## Decisions

- **A graph is named where a panorama is.** `environment` in `PUT /api/v1/viewport/display` takes the path of a
  `.vxtexgraph` as well as a room or a panorama, and `.vxtexgraph` is one of the system's `environments`, so any client
  that offers a file for an environment offers graphs too. Nothing new in the API knows about an editor: the path is the
  whole request, and Blender can send it as well as Patina's app.
- **The file, not a session.** The graph is read from its file, so the display a project keeps lights the model again
  after a restart, whatever is open. A graph open with unsaved edits lights it as it was saved; a save draws it again
  (`PanoramaKeeper`, told by `SaveGraph`). Following a session's every evaluation, as the viewport follows a graph on
  the mesh, would light it with a picture no file holds.
- **Its Output, drawn deep.** The Worker computes the graph's base colour Output, else its first, at 1024 along the
  longer side through the graph's `size` (which changes the texel count and not the picture), and writes it as a
  half-float `.exr` into the preview cache, where its hash names it. A PNG would clip the sun to one, and a sun is
  most of a sky's light. The Server keeps which picture a graph's files make as it keeps thumbnails (the document, its
  compounds and the pictures it reads), so choosing it again draws nothing that is drawn already, and a deleted cache
  costs a drawing.
- **The Server resolves, the Worker reads.** The Server tells the Worker the drawn panorama beside the display
  (`ViewportDisplaySet.Environment`), and the Worker lights the model with that file in place of the path the display
  names. The display a client reads still names the graph. The wire is version 22.
- **A graph that draws nothing is refused** with `panorama-empty` (422), and no Worker within the wait with
  `renderer-missing` (503).

## Not yet

- A graph with several Outputs cannot say which lights the model; the base colour's is the rule.
- The preview cache's trim counts PNGs only, so drawn panoramas (about 4 MB each) are kept until the cache is deleted.
