# ADR 0030: A material file comes in as graphs that read its pictures

**Status:** accepted, 2026-11-12. Decides the reading half of #157, whose writing half is in
[ADR 0019](0019-export-presets.md). Builds on [ADR 0012](0012-documents.md): what comes in is an ordinary graph document.

## Decision

`POST /api/v1/library/project/materials` (File > Import Materials…) takes a project and a file, and makes one material graph
per material the file describes. It has no format of its own and no evaluator of its own, as an image material has none:
opening the graph is how to go further, and it exports as any material graph of the project does.

Each material is first a list of maps, an `ImportedMaterial`: per output usage, the lanes of one picture times a factor,
one minus a lane, or a constant, and for a normal which way its green runs. The graph is made from that list alone:

| The file says | The graph has |
|---|---|
| a picture's colour | `Source/Bitmap` |
| one lane of it (glTF's packed occlusion, roughness, metallic, or Unity's smoothness in alpha) | `Colour/Channel Shuffle`, that lane in red, green and blue |
| a factor, or one minus the lane | `Colour/Channel Mix`, the factor on its diagonal, minus it and an offset for one minus |
| a base colour with a factor | the mix between `sRGB to Linear` and `Linear to sRGB`, since a factor is linear and a graph's base colour is sRGB numbers |
| an emissive picture | `sRGB to Linear`: every format stores it sRGB, and a graph's emissive is linear (ADR 0011) |
| a normal whose green runs up | a mix turning green over, since a graph's runs down (ADR 0011), and an export turns it back for a target that reads it up |
| a number | `Source/Uniform`, a base colour taken to sRGB |

Pictures are copied into the project's `pictures/<file name>/`, a picture already in the project is read where it is, and one
inside the file is written there. A graph reads them relative to itself, so the library can move.

## Who reads what

- **A model's materials** (glTF, FBX, OBJ, COLLADA) are read by the Worker, through Vixen's `ModelReader`, by the new message
  `material.read`: Assimp is a native parser that may crash, which is what the Worker is for. A picture inside the file is
  written by the Worker to the folder the Server names, so no pixel crosses the socket. A material no part wears is one Assimp
  adds itself, and is left out.
- **MaterialX** (`.mtlx`) and **USD's text form** (`.usda`, and `.usd` when it is text) are read by the Server, in
  `Patina.Export/Materials` beside the writers they invert. They are text, so reading them risks nothing a crash would cost.
  MaterialX: `standard_surface`, `open_pbr_surface`, `gltf_pbr` and `UsdPreviewSurface`, through `convert`, `extract`,
  `invert`, `normalmap` and a `multiply` by a value. USD: a `Material`'s `UsdPreviewSurface`, and a `UsdUVTexture`'s `scale`
  and `bias` say one minus a lane, a factor or a green that runs down.

Nothing is guessed. An input made by anything else, a node a graph would have to compute such as MaterialX's `noise2d` or a
USD shader that is not a texture, or a picture a graph cannot read, is left out and named
in the reply's `warnings`, and the rest of the material comes in.

## What is not done, and why

- **USD's binary forms** (`.usdc`, `.usdz`) need USD's own library, which Patina does not carry. They are refused with the
  sentence to save the layer as `.usda`.
- **glTF's emissive strength, alpha mode and texture transforms** are not in what Vixen's reader gives. An emissive picture
  comes in at a factor of one, and opacity not at all. The normal's scale is said, not applied.
- **Height** is read from USD's displacement only; no other format here names one.
- **USD and MaterialX output** are still unchecked in their own tools (#157); the reading here is checked against
  Patina's own writers and hand-written files, and the glTF path through a real Worker, a real GPU and an export.
