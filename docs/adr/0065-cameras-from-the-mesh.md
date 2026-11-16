# ADR 0065: A mesh file's cameras, and how the orbit camera looks through one

**Status:** proposed, 2026-11-16, for issue #144 (and Vixen#1648).

A mesh file may hold cameras: a turntable shot, a product angle, the view a client signed off. Patina read none, since
Vixen's `ModelReader` did not, and the viewport has one camera of its own, an orbit camera that circles the model by a
yaw, a pitch, a distance in framings and a pan in bounding radii (`OrbitCamera`). A file's camera is a position, a
direction, an up and a lens, and nothing in it says what it circles. So mapping one onto the other is a choice.

## Decisions

- **The facts say the file's cameras as the file has them.** `MeshFacts.cameras` (`GET /api/v1/library/mesh`) lists
  each camera with its name, position, forward and up in the space the box round the model is in, its whole vertical
  angle, its aspect, and for an orthographic camera how tall a slab it sees. The Worker reads them with the rest of the
  facts (wire version 24). Nothing about the orbit camera is in the API, so Blender, or any client with a camera of
  its own, can use them as they are.
- **The orbit camera stands where the file's camera stands and looks where it looks.** Its yaw and pitch are the
  file camera's direction, and it circles the point on its line of sight nearest the model's centre, so the model stays
  what it turns round and the first drag orbits the model, not empty space. Its distance puts the eye back at the file
  camera's position for the pane's shape. A model behind the camera is circled as far ahead as the model is.
- **The file's vertical angle becomes the viewport's field of view,** within the viewport's range. An orthographic
  camera keeps the viewport's lens and is drawn orthographic, as tall a slab at the point it circles as the file says.
- **The pane's shape, not the file's, says how wide the picture is,** and **a file camera's roll is not kept,** since
  the orbit camera's up is the world's tipped towards it. A client that needs the file's framing exactly reads the facts.
- **The editor maps it, not the Server.** The mapping needs the pane's shape, which only the client knows, and the
  Server's camera state is already the orbit's; Display lists the file's cameras under Camera, and one click looks
  through one. The camera is then the orbit camera as any other, and nothing remembers which file camera it came from.

## Left open

- Keeping a file camera's roll, or its aspect as a frame over the picture, which needs a camera the orbit cannot be.
- A file camera's animation, depth of field and sensor size, which Assimp reads in part and Patina does not.
- Saving a viewport camera into the project, or exporting it into a mesh file.
