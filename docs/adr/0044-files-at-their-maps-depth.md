# ADR 0044: A file can be as deep as the maps it reads, and a set computes at the depth its channels paint at

**Status:** accepted, 2026-11-14, for issues #141 and #19.

Wave 15 gave each channel of a texture set a depth (ADR 0020): 8-bit, 16-bit float or 32-bit float, so a painted height
keeps what eight bits round away. Two things still lost it on the way out. A preset names a file's depth with its
format (`png8`, `png16`, `exr16`, …), so a person who widened a channel had to find every preset file that reads it and
widen that too, and a preset shared between projects could not be right for both. And a set's graph computed at the
graph default of sixteen bits, so a channel painted at 32-bit float was rounded to a half in the composite whatever the
file was.

## Decisions

- **Three formats follow their maps.** `png`, `tiff` and `exr` sit beside the fixed formats in a preset and in the
  Export dialog. Each file of one is written as deep as the deepest map it reads that the set has: `png` as `png8`, or
  `png16` for linear numbers from a map deeper than eight bits; `tiff` as `tiff8`, `tiff16` or `tiff32`; `exr` as
  `exr16`, or `exr32` for a 32-bit map. The fixed formats are unchanged and still win when a preset wants one depth
  whatever was painted.
- **An sRGB file stays eight bits.** A 16-bit PNG or TIFF holds linear numbers here (ADR 0019), and sRGB numbers are
  made for eight bits, so a `png` or `tiff` file of `space: srgb` is always 8-bit. An `exr` file is linear only, as
  every float format is.
- **A map says how deep it is.** A build object's map carries `Depth`: a stack's map is as deep as its channel is
  painted, not as deep as the set computed it, so an 8-bit roughness stays an 8-bit file though the composite ran at
  sixteen; a graph's is the depth it computed at, and a bake's the depth of its format. A map an export works out
  (`glossiness`, `specular`, `diffuse`) is as deep as the deepest it comes from, and merged sets' maps as deep as the
  deepest set's. An object built before this has no depth and is read by its file: eight bits in a PNG, sixteen in an
  EXR. `OutputKeys.Generation` moves to 3 so those objects build once more instead.
- **The file says what it became.** An export's manifest names each file's settled format (`png16`, not `png`), which
  the material files beside the textures read. A stack's own export of one map follows its channel the same way.
- **A set with a float channel computes at 32 bits** (Vixen `LayerStackGraph`), so the paint the channel holds reaches
  every map whole. A 16-bit channel is a half already, which the default of sixteen holds exactly, so no other set
  costs more memory.
- **A budget counts the deepest.** Before an export the checks do not know what a following file will settle on, so
  its GPU memory is counted at the deepest it can be.

## Not decided here

- Whether the factory presets should follow their maps. They keep their fixed formats, so no export a person has
  already made changes.
- A height range of −1 to 1 and a set's display name (#141) are design choices left to the owner.

## Found while landing

A build named a preset only a project had: `POST /api/v1/builds` refused it as missing, though ADR 0019 says such a
preset exports the projects that have it and the build preview already planned it. The route now looks in every
project's presets before refusing.
