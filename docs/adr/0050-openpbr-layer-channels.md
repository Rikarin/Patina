# ADR 0050: OpenPBR's coat, sheen and transmission are channels named after OpenPBR

**Status:** accepted, 2026-11-16, for issue #142.

OpenPBR is the material model Patina moves to (#142). Wave 17 wrote an OpenPBR material file, but a texture set could
only hold the base: colour, roughness, metalness, normal, height, occlusion, emissive and opacity. The layers OpenPBR
puts over the base had nowhere to be painted.

## Decision

- **Six usages beside the standard ones**, in Vixen's `Output` node (Apache-2.0, `TextureUsages.OpenPbr`) and
  `ChannelAsset.OpenPbrUsages`: `coat`, `coatRoughness`, `sheen`, `sheenColor`, `sheenRoughness` and `transmission`.
  Each is OpenPBR's input less its layer's prefix and underscore, except the fuzz layer, which is called a sheen: every
  other model, standard surface, glTF and the tools people come from, calls it that, and a person adds a channel by
  what they know it as. The material file still names OpenPBR's `fuzz_weight`, `fuzz_color` and `fuzz_roughness`.
- **A weight is a channel of its own**, as OpenPBR has it, never folded into a colour. A sheen of 0.5 grey is a weight
  of 0.5 and a white colour, which reads back from any of the three material files without division.
- **Each starts at OpenPBR's default**, so adding one changes nothing until it is painted: no coat, sheen or
  transmission, a smooth coat (`coat_roughness` 0), a white sheen colour and a sheen roughness of 0.5. These are
  `ChannelAsset.Untouched` and `ExportMaps.Default`, and a Server test holds them equal.
- **Vixen's own material has no slot for them.** A Vixen material bake writes no file for one and says so by name, as
  it does a user channel (#141); `MaterialBakeRouteDeviceTests` counts them so a new usage cannot be dropped silently.
- **A material file wires a layer only when the set has its channel.** Its default leaves the base alone, so wiring it
  would only add a texture read. OpenPBR and standard surface take all six; UsdPreviewSurface has a clear coat and its
  roughness only; glTF's extensions are not written yet. Each says what it leaves out.
- **Import keeps a layer only when it does something.** A tool writes every input of a surface, so a layer input at
  its default, or a layer's roughness or colour with no weight, brings in no channel.
- **Sheen colour is linear**, as emissive is: no viewport shows it yet, and which space painted colours are in is
  #105's question, whose answer it follows.

## Left

Viewport shading of the layers (each is seen on the mesh as its map holds it, through the view list, until then);
OpenPBR's other inputs (specular weight and colour, anisotropy, subsurface, thin film, a coat's colour and its own
normal); glTF's `KHR_materials_clearcoat`, `_sheen` and `_transmission`, which need Vixen's glTF writer; and baking the
layers down into the base for an engine that has none.
