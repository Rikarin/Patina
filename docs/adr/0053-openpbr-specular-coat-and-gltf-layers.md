# ADR 0053: OpenPBR's specular lobe, a coat's colour and normal, and glTF's layers

**Status:** accepted, 2026-11-16, for issues #142, #157 and #305.

ADR 0050 gave a texture set OpenPBR's coat, sheen and transmission and left three things: OpenPBR's other inputs, glTF's
extensions for the layers, and a glTF material's layers on import. This takes the inputs that are a map a person paints
and leaves those that need more than a map.

## Decision

- **Five more usages, as ADR 0050's six are** (Vixen's `Output` node, `ChannelAsset.OpenPbrUsages` and
  `ExportMaps.OpenPbr`): `specularWeight`, `specularColor`, `anisotropy`, `coatColor` and `coatNormal`. Each starts at
  OpenPBR's default, so adding one changes nothing until it is painted: a whole specular lobe, white, with no
  anisotropy, a clear coat and a coat normal as flat as the base's.
- **Named as OpenPBR names the input, with one exception each way.** `specular_weight` would be `specular` by ADR 0050's
  rule, but `specular` is already the specular-glossiness export's specular colour, so the weight keeps its suffix.
  `specular_roughness_anisotropy` is `anisotropy`, what every tool and glTF call it. `geometry_coat_normal` is
  `coatNormal`.
- **A coat normal is a tangent-space normal like the set's**: its green runs as the set's does, an export turns it for a
  target as it turns the normal, MaterialX reads it through `normalmap`, and import turns it as it turns a normal.
- **glTF writes the coat, sheen and transmission** through `KHR_materials_clearcoat`, `_sheen` and `_transmission`, by
  Vixen's writer (`ModelWriterLayers`). Each extension fixes its lanes (coat red, coat roughness green, sheen roughness
  alpha, transmission red), so a slot packed otherwise is left out and said, as the core slots are. None is required:
  a reader without one still draws the base.
- **glTF's sheen colour is OpenPBR's colour times its weight**, since glTF's sheen has no weight. It is a derived map,
  `sheenTint`, worked out at export only for a set with a sheen weight; the shipped glTF preset writes it with the
  sheen's roughness in alpha, and files for the coat, its normal and transmission, each only for a set that has them.
- **Import reads glTF's layers back.** A clear coat or transmission whose factor is 0 brings in nothing; a sheen colour
  that is not black brings in a sheen weight of 1 and that colour, from a glTF and from MaterialX's `gltf_pbr` alike,
  so a glTF written here comes back as the maps it was written from. A coat's colour or normal without a coat weight
  brings in nothing, as its roughness does.
- **OpenPBR's colours are linear, as emissive is**, and a picture of one is taken from sRGB on import as an emissive
  picture is, which is what glTF's sheen colour holds. Which space painted colours are in is still #105's question.

## Left

glTF's `KHR_materials_specular` and `KHR_materials_anisotropy` (the second needs a direction this has no map for), and a
glTF coat colour, which glTF has no input for; the anisotropy's direction (`geometry_tangent`), subsurface and thin film;
reading a picture's colour space from the file rather than assuming it by usage; viewport shading of all of these.
