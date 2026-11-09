# Factory content

`materials/` holds the starter graphs the shelf offers, and `brushes/` the starter brush and tool presets, each a `.ptbrush` keyed as
`BrushPreset` is. `compounds/` holds Patina's own compounds, published as node types beside Vixen's by `Patina.Content`: a
file at `compounds/Grunges/Speckle.vxtexgraph` is the node `Grunges/Speckle`. Each is a graph of Patina's nodes and no
picture, so it can be opened, read and regenerated in the app; `FactoryCompoundTests` holds every one to compiling, to
drawing something and to tiling. A file name may not contain a dot, which would read as a folder, and a path Vixen already
ships is left out rather than replacing Vixen's. Environments will join them.

**Everything in this folder is CC0** (public domain dedication), separately from the code's GPL-3.0-or-later. A texture
built from bundled materials can contain that content, and game studios must be able to ship those textures without doubt
(plan §11). Add nothing here that cannot be released under CC0.

`export-presets/` holds the factory export presets. Each is a text file; a library adds or overrides one by putting a file
with the same shape in `export-presets/`.
