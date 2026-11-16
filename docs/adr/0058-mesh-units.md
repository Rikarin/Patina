# ADR 0058: A mesh keeps its own units, and texel density is per metre

**Status:** accepted, 2026-11-16, for issues #146 and #307.

The library's texel density was documented in texels per metre, but the check and the density view measured texels
per unit of the model. A mesh in centimetres was judged a hundred times too coarse. #146 asked for units and a scale at
import, and wave 19 left it until someone learned what Assimp does with a centimetre FBX.

## What Assimp does

`MeshFactsTests.A_centimetre_fbx_keeps_its_numbers_and_says_its_unit` holds the finding. An ASCII FBX whose
`UnitScaleFactor` is 1 (centimetres), holding a triangle 100 units wide, reads 100 units wide everywhere Patina reads a
mesh: the facts, the viewport's geometry and a texture set's. Assimp reports the unit as `UnitScaleFactor` and applies
it only when asked through `aiProcess_GlobalScale` or `AI_CONFIG_FBX_CONVERT_TO_M`. Vixen's `ModelReader` asks for
neither. glTF is in metres by its specification, and a node's scale, as a DCC in centimetres writes one, is applied
with the rest of the node's transform. OBJ, PLY and STL say nothing.

## Decisions

- **Nothing is scaled at import.** The viewport frames the bounds, a brush's size is a share of the bounds' diagonal,
  and a bake's distances follow the mesh, so every one of them already means the same in centimetres as in metres.
  Scaling the numbers would change a bake's key and move nothing a person sees. `ModelReadOptions.Scale` stays 1.
- **A unit is known, not applied.** `MeshUnits.Stated` gives the file's unit (glTF a metre, a stating FBX its own,
  others none), and `MeshUnits.Of` gives the unit a project measures in: the project's `metresPerUnit` when it says
  one, else the file's, else a metre.
- **A project can say its mesh's unit.** `metresPerUnit` in `project.yaml`, written only when the project says one,
  from a micrometre to a kilometre. `PATCH /api/v1/library/project/settings` sets it, zero goes back to the file's, and
  anything else is refused with `settings-refused`. It serves an OBJ, PLY or STL in centimetres, and an FBX that states
  its unit wrongly. Project Settings offers it as Mesh units: As the file says, Metres, Centimetres, Millimetres,
  Inches or Feet.
- **Texel density is per metre everywhere.** The Worker measures a layout per unit, as it always has, and says the
  file's unit with it (`StackMeasured.MetresPerUnit`). The Server divides by the project's unit to judge it, so a check
  reads texels per metre. The density view's target goes to the Worker per metre with the project's unit
  (`ViewportDisplaySet.MetresPerUnit`), and the renderer turns it into texels per unit of the mesh it drew. The Worker
  wire is version 24.

## Left open

- Auto Unwrap's texel density is still texels per unit of the model, as `UnwrapOptions` says.
- The mesh facts shown in Project Settings give the file's unit, not the project's.
