# Factory content

`materials/` holds the starter graphs the shelf offers, and `brushes/` the starter brush and tool presets, each a `.ptbrush` keyed as
`BrushPreset` is. Compounds and environments will join them.

**Everything in this folder is CC0** (public domain dedication), separately from the code's GPL-3.0-or-later. A texture
built from bundled materials can contain that content, and game studios must be able to ship those textures without doubt
(plan §11). Add nothing here that cannot be released under CC0.

`export-presets/` holds the factory export presets. Each is a text file; a library adds or overrides one by putting a file
with the same shape in `export-presets/`.
