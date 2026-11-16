# ADR 0054: A project's normal format says how its normal maps come in

**Status:** accepted, 2026-11-16, for issue #144.

Project settings had no normal format: Patina's bakes run a tangent-space normal's green up the picture, an export
preset says which way its engine reads it (`normal: y-up | y-down`), and Bring In took a DirectX map only when it was
told, map by map, that its green runs down (#300). A team whose maps all run their green down said so every time.

## Decisions

- **One word per project, the preset's words.** `normalFormat` in `project.yaml`, `y-up` (OpenGL) or `y-down`
  (DirectX), written only when it is `y-down`, so every project made before reads as it did. `ProjectContents` says it,
  `PATCH /api/v1/library/project/settings` sets it, and anything else is refused with `settings-refused`. The words are
  the export preset's, so a client offers one list for both.
- **It says how a map comes in, not how Patina keeps one.** Every map a project holds, baked or brought in, keeps its
  green up, as glTF does. A normal or bent normal in tangent space brought in without `greenDown` is read as the
  project says, so in a `y-down` project it is turned over as it comes in; `greenDown` on the map still outranks it,
  both ways. A map with no green is never turned. Maps already brought in stay as they came in.
- **Export stays with its preset.** What an engine reads is the preset's to say, as it was. A project's format does
  not turn an export's normal over, so a DirectX project exporting for an OpenGL engine gets what that engine reads.

## Left open

- Tangent space per vertex or per fragment, and any basis other than MikkTSpace, waits on the owner (#144).
- ~~A normal map brought in as a layer's picture, or read by Import Materials (`greenUp` there), does not read the
  project's format yet.~~ Closed in wave 20 (#310), below.

## Amendments

- _2026-11-16 (#310):_ **A layer's picture.** A texture fill names its pictures and reads them at each compile, so
  how its normal and coat normal pictures run is kept on the layer, as `normalGreenUp` in the stack file (Vixen's
  `LayerAsset.NormalGreenUp`) and `normalFormat` (`y-up` or `y-down`) in the API. A stack keeps its normals green
  down (ADR 0011), so `y-up` turns them and `y-down` reads them as they are. A `setLayer` that lays a normal or coat
  normal picture and does not say is given the project's format, as a map brought in is; a layer laid before has
  no flag and reads as it did, `y-down`, so no project changes under anyone. A stack in no project is left as sent.
- _2026-11-16 (#310):_ **Import Materials.** glTF, MaterialX and USD say their normals run green up, and their
  readers say where a file turns one; FBX, OBJ and Collada say nothing, so their normals are read as the project
  says.
