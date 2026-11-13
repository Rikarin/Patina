# Factory content

`materials/` holds the starter graphs the shelf offers, and `brushes/` the starter brush and tool presets, each a `.ptbrush` keyed as
`BrushPreset` is. `compounds/` holds Patina's own compounds, published as node types beside Vixen's by `Patina.Content`: a
file at `compounds/Grunges/Speckle.vxtexgraph` is the node `Grunges/Speckle`. Each is a graph of Patina's nodes and no
picture, so it can be opened, read and regenerated in the app; `FactoryCompoundTests` holds every one to compiling with
nothing to say and every grunge to drawing something that tiles. `Generators/` reads the mesh's bakes and `Weathering/` and
`Stylised/` are layer filters, so they do not tile; `MeshCompoundTests` holds them to drawing over a stand-in bake, to their
seed and to changing what they are handed. Every field has a label and a sentence. A file name may not contain a dot, which would read as a folder, and a path Vixen already
ships is left out rather than replacing Vixen's. Environments will join them.

**Everything in this folder is CC0** (public domain dedication), separately from the code's GPL-3.0-or-later. A texture
built from bundled materials can contain that content, and game studios must be able to ship those textures without doubt
(plan §11). Add nothing here that cannot be released under CC0.

`samples/` holds the sample projects Start offers (#190), a folder each: one mesh, already laid out in UVs, and a texturing
document for each of its materials, named for the material, whose masks are compounds from `compounds/`. Making one
copies the folder into the library as a project of its own. `samples/Knot/knot.glb` is written by
`patina-bench mesh <file> --uvs`, the same knot and sphere as `fixtures/viewport-test.glb` with a layout.

`export-presets/` holds the factory export presets. Each is a text file; a library adds or overrides one by putting a file
with the same shape in `export-presets/`.
