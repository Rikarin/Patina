# ADR 0019: The export preset language, its editor, and what compression does

**Status:** accepted, 2026-11-09. Extends [ADR 0011](0011-export.md), which decided that a preset is a file. This one says
exactly what the file may hold (#155), how the editor writes it, where it lives, the material files beside the textures
(#157), and the compression preview (#217).

## A preset is a file, in three places

A preset is a YAML file named `<id>.yaml`. There are three places, each winning over the one before by `id`:

1. **Factory**, in `content/export-presets`, embedded in Patina. Read-only: changing one means duplicating it.
2. **Library**, in `<library>/export-presets`.
3. **Project**, in `<library>/projects/<project>/export-presets`. Only that project's exports and checks see it.

A build resolves presets per project (`BuildPresets`): a request that names an id runs the project's own preset of that
id where it has one, and the library's or the factory's otherwise. An id nothing has is refused before anything runs; an
id only some projects have exports those and leaves the rest alone. A file that is not a preset is reported in the list's
`problems` and takes nothing else down with it.

Files are the truth. The editor sends a document, the Server writes the YAML, reads it back through the same reader as a
hand-written file, and keeps it only if that reads it (`ExportPresetWriter`): the editor can make nothing a person could
not write. A hand-written file's comments survive too where they stand above something the writer writes again
(`PresetComments`): the block at the top, a top-level field's, and a texture's, found again by its `file`; a comment
inside a texture or after a value is lost. Every field survives, and a property test
holds that every factory preset comes back from the editor as it was. Removing a preset deletes its file.

## The language

Every scalar is read as text and refused if it is not one of the spellings below: `True`, `yes` and `1` are not
booleans, because a misread `mips` or `compression` would ship an uncompressed texture. Unknown fields are refused at
the top level and in a texture.

### Top level

| Field | Required | Value | Default |
|---|---|---|---|
| `version` | yes | `1` | |
| `id` | yes | lower-case letters, digits and hyphens | |
| `name` | yes | text | |
| `engine` | no | text: what it is for | `name` |
| `normal` | yes | `y-up` or `y-down`: the green the target reads | |
| `size` | no | whole number 1 to 8192: the longest side of every file | the maps' own size |
| `filter` | no | `box`, `bilinear` or `nearest`: how a file changes size | `box` |
| `dither` | no | `true` or `false`: 8-bit files round up by chance, not to the nearest level | `false` |
| `padding` | no | see Padding | `none` |
| `materials` | no | a list of `gltf`, `usda`, `mtlx`, each once | none |
| `textures` | yes | a list of one or more textures, no two with the same `file` | |

### A texture

| Field | Required | Value | Default |
|---|---|---|---|
| `file` | yes | a naming template (see Names), with no extension | |
| `format` | yes | `png8`, `png16`, `tga`, `jpeg`, `tiff8`, `tiff16`, `tiff32`, `webp`, `exr16`, `exr32`, `hdr`, `ktx2`, `dds` | |
| `space` | yes | `srgb` or `linear`: what the file's numbers are | |
| `compression` | no | `none`, `half`, `float`, `bc1`, `bc3`, `bc4`, `bc5`, `bc6h`, `bc7`; only for `ktx2` and `dds` | `none` |
| `quality` | no | for `jpeg` 1 to 100; for everything else `fast`, `balanced` or `best` (how hard BC6H and BC7 search) | `90`, `balanced` |
| `mips` | no | `true` or `false`; only for `ktx2` and `dds` | `false` |
| `scale` | no | `1`, `0.5`, `0.25`, `0.125` or `0.0625`: its side as a share of every other file's | `1` |
| `dither` | no | `true` or `false`, for this file alone | the preset's |
| `padding` | no | see Padding, for this file alone | the preset's |
| `channels` | yes | one to four lanes, in file order | |

A file's shape is refused when it cannot be written as asked: BC4 takes one lane, BC5 two, BC1 and BC6H three, BC3 and BC7
three or four; PNG, TGA and WebP hold 1, 3 or 4 lanes and JPEG and HDR 1 or 3; BC6H, `half`, `float` and the float
formats hold linear numbers only, BC4 and BC5 have no sRGB form, and a 16-bit PNG or TIFF holds linear numbers here.

### Lanes

A lane is one of:

- `map.l`: lane `l` (`r`, `g`, `b` or `a`) of a map. `map` alone is `map.r`.
- `1-map.l`: one minus it, such as smoothness from `1-roughness.r`.
- a number, such as `0.5` or `1`.

The maps are a graph's Output usages and a bake's maps under their own names: `baseColor`, `normal`, `roughness`,
`metalness`, `occlusion`, `height`, `emissive`, `opacity`, `mask`, `curvature`, `thickness`, `id`, `position`, `world`,
`bent` and `coverage`. Three more are worked out when a file reads them (`DerivedMaps`), from a metal-roughness set:

| Map | What it is | From |
|---|---|---|
| `specular` | F0: 4 % for a dielectric, the base colour for a metal, as sRGB numbers | `baseColor`, `metalness` |
| `diffuse` | the base colour with the metal taken out, as sRGB numbers | `baseColor`, `metalness` |
| `glossiness` | one minus roughness | `roughness` |

Colour is mixed in linear numbers and stored as sRGB numbers, as base colour is (ADR 0011, Colour). A derived map exists
when a set has any map it is worked out from, a missing one taking a surface's default; a set's own map of that name wins.

A file none of whose maps a set has is left out and reported; a lane of a missing map takes what a surface without the
map would be. Only the colour lanes of `baseColor`, `specular` and `diffuse` start out sRGB, and only the colour lanes
of an `srgb` file end up so; alpha is always linear.

### Names

A template is text with names in braces, and forward slashes for folders. It must stay inside the export folder: no
leading slash, no backslash, no `.` or `..` part, no unknown name and no stray brace.

| Name | Value |
|---|---|
| `{project}` | the project's name |
| `{preset}` | the preset's id |
| `{set}` | the texture set's name, or the project's for a project of one unnamed set |
| `{mesh}` | the file name, without its extension, of the mesh the set paints, or `{project}` |
| `{material}` | the mesh material the set paints, or `{set}` |
| `{udim}` | `1001`: Patina paints one UV tile (`OneTile`) |
| `{tile}` | `u1_v1`, the same tile as Mari names it |
| `{space}` | `srgb` or `linear` |
| `{size}` | the file's longest side in texels |

A value is cleaned to letters, digits, `_` and `-`, so a name cannot make a folder. In a project of several sets a
template with no `{set}` gets `_{set}` after `{project}`, or at its end, so every file says which set it is.

### Padding

`none`, `infinite`, or a mapping of `mode` (`transparent`, `colour` or `diffusion`), `distance` in texels (0 to 8192,
default 16) and, for `colour`, up to four numbers. A material with no mesh has no coverage map and is not padded.

### Size, in order

A file is padded at the maps' size, then sized: the preset's `size`, or else the maps' own longest side, times the file's
`scale`. Enlarging is reported. The maps' size is the texture set's own width and height (ADR 0020), which need not be
square; every size here is a longest side, so a file keeps its set's shape. An export's size, the whole export's or one
set's under `perSet`, changes only what is written: the set is still authored, painted, baked and checked at its own
size and in its own UV set, and padding grows from that UV set's layout.

## An export's changes

An export changes a preset for one run (`ExportOptions`), and the changes go into the export's key:

- `size`, `filter`, `padding` and `dither` replace the preset's. A file's own `dither` and `padding` win over both,
  because they were set for that file.
- `files`: a file turned off, or written in another `format`, `compression`, `quality` or with or without `mips`. A
  plain file made a container keeps its compression if it had one and is otherwise uncompressed; a container made plain
  loses its compression and mips; a format that holds only linear numbers takes the file as linear.
- `sets`: the sets written.
- `perSet`: a set's own `size`, `filter`, `padding`, `dither` and `files`, over the rest.

A change a preset cannot take is refused before anything runs, with the reader's sentence. A project keeps what its
export was last set to in its `export` block (ADR 0011), now with each file's compression, quality and mips and each
set's own changes. `patina build --kept` starts from it, and `--size`, `--filter`, `--padding`, `--distance`,
`--dither`, `--no-dither`, `--set` and `--file <template>=off|<format>[:<compression>]` are the same changes on a
command line.

## Material files

`materials` writes, beside each set's files, one file per kind named like `{project}` (with `_{set}` in a project of
several sets). Each names the files the set's export wrote, by their delivered names, relative to itself. None looks
inside an image: the slots come from the preset's lanes (`MaterialSlots`). A colour slot is three lanes of one map in
order (`baseColor.r, .g, .b`); a grey slot is lane `r` of the map in any lane, inverted or not, and `glossiness` is
roughness inverted. Opacity counts only when the set had an opacity map, because its default is opaque and a material
told an image holds opacity is drawn transparent. A slot a format cannot carry is left out and the report says so.

| | glTF 2.0 (`gltf`) | USD (`usda`) | MaterialX 1.38 (`mtlx`) |
|---|---|---|---|
| Written by | Vixen's `ModelWriter.GltfMaterials`: materials, textures, images and a sampler, no mesh | `MaterialFiles.Usd`: a `Material` with a `UsdPreviewSurface` | `MaterialFiles.MaterialX`: a node graph, a `standard_surface` and a `surfacematerial` |
| Base colour | sRGB file only; opacity only from its alpha, then `alphaMode: BLEND` | `diffuseColor`; opacity from any lane | `base_color`; `opacity` from any lane |
| Roughness, metalness | only green and blue of one file, not inverted; otherwise factors of 0.5 and 0 | any lane; one minus by a texture of scale −1 and bias 1 | any lane through `extract`, one minus through `invert` |
| Normal | only a `y-up` preset | scale and bias turn a `y-down` normal over | a `y-down` normal is turned over before `normalmap` |
| Occlusion, height, emissive, specular | occlusion from red; emissive sRGB | all four; specular as `useSpecularWorkflow` when there is no metalness | emissive; no occlusion, height or specular, which are said |
| Image formats | PNG, JPEG, WebP through `EXT_texture_webp` (then required) | PNG, JPEG, TGA, HDR, EXR | PNG, JPEG, TGA, HDR, EXR |

The glTF writer is Vixen's, so there is one glTF writer; Vixen's `ModelReader` reads every slot back, the emissive one
included, and its tests read a written material through Assimp. The USD and MaterialX writers are Patina's: they describe
an export and nothing else needs them. ⚠ **Not verified in their own tools.** No USD or MaterialX library is installed
here: the `.usda` is held by tests to name every file and lane, and the `.mtlx` to be well-formed XML whose every input
names an output of its graph, but neither has been opened by `usdcat`, `usdview`, MaterialX's validator or a renderer.
glTF's has been read only by Assimp.

## What compression does

`POST /api/v1/export/compression` shows one file of an export as an engine reads it (#217). It reads the objects a build
left, so it needs the project's maps built under their current keys and is refused with `export-unbuilt` otherwise; the
dialog then offers Build Maps, which builds with no export. It packs, pads and sizes the file as the export does, makes
the file's mips from the whole picture, and then block-compresses only a region of the level asked for, on block lines,
and decodes it again (`CompressionPreview`). A block is made alone, so **the region is exactly that region of the whole
file**: a test holds it texel for texel against the encoded KTX2 at three levels and three formats. A 4K BC7 file is
shown in a moment. A JPEG or another plain file is written and read back, which shows its loss and an 8-bit file's steps
the same way; TIFF and WebP have no reader here and are refused.

The reply gives, per lane, the PSNR against a peak of one (or the brightest value, for float files), the largest and
the mean error in the file's own numbers, a gain for the difference picture (a power of two that brings the largest error
near white), and the GPU memory of the whole file compressed and not. That memory is `ExportMemory`, the one measure the
pre-export check's `texture-over-budget` uses (#159), so a budget and a preview never disagree. The three pictures are
PNGs in the cache under a hash of everything the preview was made from, served by
`GET /api/v1/export/compression/{id}/{source|written|difference}`; deleting `.ptcache` loses them and nothing else.

The preview runs in the Server, one at a time: it reads files a build wrote, runs no GPU, and the region bounds its cost.
The Worker stays the only place pixels are made from graphs and stacks.

**Reproducible output, stated plainly.** A map's seeds come from its file and a node's identity from the node, and an
export is keyed on the preset file's bytes, the export's changes and the maps' keys, so the same library makes the same
files on one machine. Block compression and the CPU writers are managed code and give the same bytes on any machine.
What comes off a GPU is not promised to be the same bytes across GPUs or drivers (plan §3.2), so neither is a file made
from it.

**What differs between two builds** (#217) is read from their reports, never from the files: by the time anyone asks,
the later build has written over the earlier one's. An export's report keeps each delivered file's SHA-256 beside its
path, in the journal as each export ends, so a restarted build keeps them too. `BuildComparisons` sets two reports side
by side, project by project: an output is the same when its build key is, and a file when its digest is; a file only
one build delivered is added or removed, and one from a report written before digests were kept is unknown. A build
records only the projects it touched, so a project one build left alone is listed as the other build left it. It is
`GET /api/v1/builds/{id}/compare?before=`, `patina build compare <earlier> <later>` over the report folders a build
prints, and in the Export dialog the files changed and the same since the build read before, in that session.

## Built-in presets

Beside ADR 0011's five: `gltf` (base colour with alpha, normal, ORM in glTF's order, emissive, and a `.gltf`), `blender`
(one image per Principled BSDF input, a 16-bit normal and a half-float height, and a `.usda`), `pbr` (one PNG per map,
nothing packed) and `spec-gloss` (diffuse with alpha, specular with glossiness in alpha, normal, occlusion, emissive). All
read green up. None has been opened in its target; that is #46.

## What is left

- **Mixed maps.** Mixed AO (baked times painted) and a normal combined from baked, painted and height need the bake and
  the stack mixed in one picture first (ADR 0011, #86); a preset cannot ask for them yet.
- **Mesh maps as their own preset.** Every bake map can be read, so a mesh-maps preset can be written, but none ships.
- **Basis, ASTC and ETC.** `compression` is BC1 to BC7, half and float; there is no encoder for the mobile formats.
- **TIFF and WebP are written and not read here.** Their round trip is Vixen's `ImageWriterTests`, through libtiff,
  ImageIO and libwebp, which skip where those are not installed.
- **Binary USD** (`.usdc`, `.usdz`) is not read; text USD, MaterialX and glTF materials are imported (ADR 0030).
