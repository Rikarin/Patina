# ADR 0053: OpenPBR's specular lobe, a coat's colour and normal, and glTF's layers

**Status:** accepted, 2026-11-16, for issues #142, #157 and #305; amended the same day (wave 20).

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

## Amended, wave 20: the anisotropy's direction, subsurface, a thin film, and glTF's specular and anisotropy

- **Six more usages, as the eleven are**, each starting at OpenPBR's default: `anisotropyRotation`, `subsurface`,
  `subsurfaceColor` (OpenPBR's light grey, 0.8), `subsurfaceRadius` (1), `thinFilm` and `thinFilmThickness` (0.5).
  OpenPBR's names less the underscore, `subsurface_weight` and `thin_film_weight` losing their suffix as `coat_weight`
  did.
- **The anisotropy's direction is a turn, not a vector**, since a person paints a number: `anisotropyRotation` is a
  fraction of a whole turn counter-clockwise from the tangent towards the bitangent, which is glTF's sense and
  standard surface's `specular_rotation`. OpenPBR's `geometry_tangent` is a vector, so its MaterialX file works the
  vector out, (cos, sin, 0) in tangent space put into the world by `normalmap`; that input is written and not read
  back. `subsurfaceRadius` is in the scene's units and `thinFilmThickness` in micrometres, so a painted 0 to 1 is
  OpenPBR's soft range of each. `subsurfaceColor` is linear, as OpenPBR's other colours are.
- **Standard surface takes all but the thin film**: the radius as `subsurface_scale`, which multiplies its
  `subsurface_radius` colour of white. Its thin film is a thickness in nanometres with no weight, so it is said and
  left out. USD and glTF have none of them, and say so.
- **glTF writes the specular lobe and anisotropy** through `KHR_materials_specular` (weight in alpha, colour sRGB in
  red, green and blue, which the shipped preset packs in one file) and `KHR_materials_anisotropy`, whose picture holds
  the direction in red and green and the strength in blue. That picture is a derived map, `anisotropyVector`, worked out
  at export only for a set with an anisotropy, from it and its turn. Import reads both back; a direction picture is
  no one lane, so an imported anisotropy brings its strength and its factor's turn and says so. A whole white lobe and
  no anisotropy are glTF's defaults and are not written.

## Left

A glTF coat colour, which glTF has no input for; glTF's iridescence and diffuse transmission for a thin film and
subsurface, which Assimp does not read back; reading a picture's colour space from the file rather than assuming it by
usage; viewport shading of the specular lobe, anisotropy, subsurface and thin film; and baking the layers down into the
base for an engine without them.
