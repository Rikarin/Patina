# 05. Where the two audits stand

Patina has had two audits, each kept as a GitHub issue with a checklist: #68, from a working engine to a usable
application (at `4bccebc`), and #219, parity with the reference tools and beyond (at `f51a694`). Their bodies are the
audits' record and stay as written. This page is their status at wave 19's merge (`2baa7af3`, Vixen `e0e6a08e8`), so
the next wave starts from what is really left rather than from the checklists.

An issue counts as done here only when it is closed and a test holds what closed it. Wave 10's agents closed some issues
from reading the code; each row below names the test that now holds it.

| Audit | Issues | Closed | Open | Of the open, waiting on the owner |
|---|---:|---:|---:|---:|
| #68, usable application | 70 | 58 | 12 | 5 |
| #219, parity and beyond | 182 | 124 | 58 | 37 |

Since wave 8, landing a wave files its own follow-ups (#245 onwards) instead of adding to #219: 67 so far, 53 closed.
The open ones are listed at the end.

## #68: from a working engine to a usable application

Groups 1, 2, 4, 8, 9 and 10 are closed: a new user can make a library, a project holds its work, painted work reaches
an export, and the data-loss and hang defects are gone. The new-user journey the audit asked to pass before new engine
scope is `journey.spec.ts` ("a new user makes a library, groups projects in folders, and comes back to them").

### Closed in waves 10 to 19

| Issue | What | Held by |
|---|---|---|
| #4 | Pickers and library references for every typed path, from another computer too | `LibraryUploadApiTests`, `LibraryFileApiTests.A_picture_the_library_holds_comes_into_a_project_by_its_path`, Python `test_a_mesh_sent_from_another_computer` |
| #13 | A design review of the densest panes in the bezel skin | `docs/04-design-review.md`; its fixes by `pane-names.spec.ts` and `bars.spec.ts` |
| #14 | Product language in place of implementation language | `core/language.spec.ts`, `RefusalWordsTests.No_refusal_goes_out_without_a_code` |
| #19 | Resolution and bit depth per painted channel | Vixen `PaintTexelFormatTests.A_set_computes_at_the_depth_its_widest_channel_paints`, `StackDepthExportProcessTests.A_png_that_follows_its_maps_is_as_deep_as_its_channel_is_painted` |
| #23 | Eraser, picker, projection, fill, clone, smudge | `paint-tools.spec.ts` ("a smudge drags paint along, and a clone copies it from its source"), `paint-store.spec.ts` (face loop, #129) |
| #24 | A mask as sub-layers, with effects and generator parameters | `mask-stack.spec.ts` |
| #26 | The occlusion test, and symmetry past planes | `paint-tools.spec.ts` ("a dab on the strap paints the top under it only with the occlusion test off"), `StackSymmetryProcessTests.A_symmetric_stroke_on_the_mesh_paints_each_of_its_copies` |
| #31 | Shadows and richer shading | Closed into #142 and #143 (ADR 0031); `ViewportLayerShadingTests` |
| #38 | Node library search and quick add | `graph.spec.ts` ("finds a node by name, stars a node for the palette, and adds one from several"), `GraphPaletteApiTests.The_first_match_is_what_was_typed` |
| #40 | Two graphs side by side | `graph.spec.ts` ("shows a second graph beside the first, and the panes follow the one touched last (#40)") |
| #42 | Fractal noises, Text and Vector | Vixen `TextureNoiseOctaveDeviceTests` |
| #43 | Bake and Export as part of the project | `bake.spec.ts` ("the bake settings are a section per map and stay in the project"), `export-project.spec.ts` ("a graph its texturing project does not ship exports on its own from the Document scope"), `ProjectBakePlanApiTests.A_preview_of_a_project_takes_its_meshes_and_options_from_the_projects_file`, `BakePreviewProcessTests.A_preview_of_a_project_follows_its_file_when_another_client_changes_it` |
| #47 | A stack's own export in any format | `MapFileTests.A_float_map_keeps_its_range_in_a_float_file`, `layers.spec.ts` ("any layer exports on its own, and so does its mask as one map") |

#43 is still open on GitHub, but its last item, the bake preview's setup built in the editor, moved to the Server in
waves 17 and 18 (`afc75c75`, `b84b6ac7`) and is held by the two tests above. It can close.

### What is left

- **Waiting on the owner:** #20 (one material over several UDIM tiles: a canvas per tile, or a set per tile on import),
  #34 (may Patina ship CC0 HDRIs from Poly Haven, and the licence of Vixen's six layered materials), #37 (the name
  smart-materials and the trademark check on "Patina"), #44 (how to meet the bake speed target), #45 (should a bake of a
  low mesh with no UVs from the API or CLI unwrap it, rather than be refused as
  `MeshMapBakeRunTests.A_low_with_no_atlas_is_refused_with_a_reason` holds today).
- **Needs hardware or people outside the repository:** #46 (each preset imported in its engine; the files themselves
  are checked by `EngineContractTests` and `ContainerHeaderTests`), #48 (a signed, notarised `.dmg` on a clean Mac).
- **Work:** #25 (stamp to frame: laying is still serial, and the target needs an idle-machine and a scanned-asset
  measure; `PaintMeshSurfaceTests` measures the merge), #41 (curves and ramps as exposed parameters, which is #181).
- **In Vixen:** Rikarin/Vixen#1528 (Editor.Texturing on Windows and Geometry.Remeshing on Ubuntu at `c4c5cbd76`),
  Rikarin/Vixen#1530 (stack contract gaps for material painting). Rikarin/Vixen#1529 is closed.

## #219: parity with the reference tools, and beyond

Group 1, the broken core path, is closed whole: masks paint on any layer, Export writes what the app made and says what
it wrote, and the export test's library comes from the app (`export-project.spec.ts`, "an empty library says so, and a
new project exports with nothing declared"). Groups 5 and 9 are closed too, and group 18 has two nodes left, both
waiting on an answer.

### Closed in waves 10 to 19

| Issue | What | Held by |
|---|---|---|
| #86 | A bake reaches the set's shading and its exports | `StackBakeShadingProcessTests.The_viewport_shades_a_set_with_its_bake`, `StackBakeShadingProcessTests.A_bake_lies_beneath_the_sets_normal_and_occlusion_in_what_it_exports` |
| #96 | A graph at another size is the same picture | `GraphProcessTests.The_same_graph_at_two_sizes_is_the_same_picture_resampled`, `OutputBuildersTests.A_side_is_the_nearest_whole_step_of_the_base` |
| #108 | Instance layers within and across texture sets | `StackInstanceProcessTests.Unsaved_strokes_on_a_source_show_in_its_instance_in_another_texture_set`, `find-isolate.spec.ts` |
| #110 | Richer selection masks | `StackMaskStackTests.A_mask_and_its_entries_read_the_lane_they_are_given`, Vixen `TextureAnalysisKernelTests` |
| #115 | Tone and colour filters | Vixen `FilterLibraryDeviceTests` |
| #116 | Edge and shape filters | `LayerAdjustmentTests`, `StackVocabularyTests` |
| #117 | Surface finish filters | Vixen `FilterLibraryDeviceTests` (Galvanised, Grainy, Ground, Perforated, Raw, Rough) |
| #118 | Weathering filters | `PanelFixtureTests` |
| #119 | Stylised filters | `FactoryCompoundTests.Every_knob_has_a_label_a_summary_and_a_range_its_default_is_in` |
| #121 | Generators that read the UV layout | `BakeMapKindsTests.The_bounds_and_density_maps_are_written_and_read_as_worked_out`, `MeshCompoundTests.Texel_density_is_blue_where_sparse_and_red_where_dense` |
| #124 | Brush, tool and material presets | `MaterialPresetApiTests.A_fill_kept_as_a_preset_fills_a_layer_of_another_project_at_its_values`, `paint-mesh.spec.ts` ("a preset takes up its brush…") |
| #125 | Paint with a material | `StackMaterialBrushProcessTests.A_layered_material_brush_lays_what_applying_the_material_lays`, `StackMaterialProjectionProcessTests` |
| #127 | Tool strip, smudge, clone and quick mask | `StackQuickMaskProcessTests`, `PaintKeepTests` |
| #128 | Projection tool and stencils | `StackToolProcessTests.A_material_brush_lays_what_its_graph_makes`, `paint-tools.spec.ts` ("a picture on the viewport projects through the brush, and as a stencil masks it") |
| #131 | Ribbon paths | Vixen `TextureSurfaceProjectionDeviceTests.A_ribbon_lies_over_itself_where_it_crosses` |
| #132 | Symmetry past three planes through the origin | `StackSymmetryProcessTests.A_symmetric_stroke_on_the_mesh_paints_each_of_its_copies`, `symmetry-guide.spec.ts` |
| #133 | Decals placed in 3D | `LayerSurfaceProjectionTests`, `surfaces.spec.ts` |
| #134 | Warp projection, and Warp to Geometry | `surface-frame.spec.ts`, `surfaces.spec.ts` |
| #137 | A display settings pane | `ViewportDisplayTests`, `ViewportEnvironmentTests.A_small_sun_in_a_panorama_keeps_its_light` |
| #147 | Per-map bake settings that stay in the project | `bake.spec.ts` ("the bake settings are a section per map and stay in the project"), `DocumentCodecTests.A_projects_bake_settings_survive_the_round_trip` |
| #151 | Rebake only what a mesh change touched | `BakeHighReadsTests.A_change_to_another_part_keeps_a_sets_key_and_a_change_to_its_own_does_not` |
| #153 | A real Export dialog | `export-project.spec.ts`, `export-pane.spec.ts`, `export-store.spec.ts` |
| #155 | Preset language and a preset editor | `export-presets.spec.ts` ("a duplicated preset is a file in the project, and a BC7 file shows what compression does") |
| #157 | glTF materials, USD and MaterialX in and out | `ModelMaterialsTests.A_gltfs_clear_coat_sheen_and_transmission_come_in_as_openpbrs_maps`, `OpenPbrLayerExportTests.The_specular_lobe_and_the_coats_colour_and_normal_reach_materialx_and_read_back`, `MaterialImportApiTests` |
| #159 | Budgets, texel density and validators | `ProjectChecksApiTests`, `checks.spec.ts` |
| #162 | Autosave and project care | `LibraryCareApiTests.Autosave_keeps_a_document_in_the_folder_chosen_for_it_and_offers_it_from_there`, `library-care.spec.ts` |
| #163 | A 3D mouse | `space-mouse.spec.ts` (not yet tried on a device) |
| #169 | Grunge and dirt content | `FactoryCompoundTests.Every_factory_compound_draws_a_picture_that_tiles` |
| #172 | Histogram and statistics | `GraphMeasureProcessTests.A_histogram_counts_the_full_size_picture_per_channel`, Vixen `TextureHistogramDeviceTests` |
| #175 | Transform nodes | Vixen `TextureRemapDeviceTests`, `TextureAspectDeviceTests` |
| #177 | Mesh-based generators and weathering | `MeshCompoundTests.A_generator_picks_its_part_of_the_stand_in_and_leaves_the_rest` |
| #178 | Scan and photo processing | `TexturedFillTests` (Photo/Fill Holes) |
| #179 | Noise driven by position | Vixen `TextureVolumeDeviceTests.An_unwired_position_is_the_bakes_own_position_map` |
| #180 | Graphs that make environments | `GraphPanoramaProcessTests` |
| #183 | Rank the node palette, add from a selection | `GraphPaletteApiTests.The_first_match_is_what_was_typed`, `NodeThumbnailProcessTests` |
| #190 | Help, docs and a first sample | `commands.spec.ts` ("Help has the guide, the keys, a problem report, and About…") |
| #192 | One place for errors, progress and jobs | `LogRingTests`, `activity-panel.spec.ts`, `library-care.spec.ts` |
| #196 | One word for each thing | `pane-names.spec.ts`, `language.spec.ts` |
| #197 | One way to show a number | `language.spec.ts`, `words.spec.ts` |
| #198 | Raw values and developer language | `RefusalWordsTests.No_refusal_goes_out_without_a_code`, `failures.spec.ts` |
| #200 | Toolbars agree | `bars.spec.ts` |
| #201 | A place for the document in Properties | `paint-tools.spec.ts` |
| #208 | Big stacks stay fast | `StackBudgetProcessTests.A_saved_stack_keeps_its_layers_for_a_new_worker` |
| #217 | Show what compression does, and what changed between builds | `CompressionPreviewTests.A_region_is_exactly_that_region_of_the_whole_file`, `BuildComparisonsTests` |
| #236 | Quitting with unsaved edits hung late in a long run | `ProcessTests.A_client_following_events_and_frames_does_not_keep_a_stopping_server` |
| #238 | A new UV set left the mesh maps in the old layout | `StackBakeShadingProcessTests.A_set_moved_to_another_uv_set_says_its_bake_is_stale_until_it_is_baked_again` |

The checklist in #219's body lags GitHub: #86, #96, #115 to #119, #121, #124, #125, #127, #128, #131 to #134, #137,
#147, #151, #153, #155, #157, #159, #162, #163, #169, #172, #175, #177 to #180, #183, #190, #192, #196 to #198, #200,
#201, #208, #217, #236 and #238 are closed but unticked.

### What is left

**Decisions the owner holds** (label `question`): #103 (per-channel blend and opacity), #105 (the colour space layers
blend in), #140 (particle brushes), #143 (a path tracer), #152 (whether baking stays a dialog), #160 (OCIO), #161
(parity that would touch Adobe formats), #164 (per-pixel programming in a graph), #165 (a whole material through a
graph), #166 (paths and splines in graphs), #187 (node names), #204 (strokes bound to the surface), #215
(image-to-material and generative work), #233 (whether the bake preview follows the viewport's isolation; it does
today, `BakePreviewShapesTests.An_isolation_draws_the_reach_of_the_parts_shown_alone`).

**Open issues waiting on an answer in their last comment:** #109 (what "a fill layer as a mask" and "a mask with height
combination" mean), #122 (a picker in each slot, and which drop wins on an ID region), #129 (what a partial loop is),
#130 (what a path point snaps to), #135 (what a project template carries), #141 (the owner's two choices on set
settings), #148 (what edge protection is), #156 (whether to take the native `astcenc` for ASTC and ETC2), #168 (block
solid), #176 (a normal matrix), #182 (what pins are), #184 (the UV overlay on a graph's 2D view), #185 (a named graph
type and outputs), #186 (what declaring a build from the CLI adds), #189 (language, the cache folder, simplified
thumbnails), #203 (what a grid view means beyond the Library's), #205 (ADR 0056: may a set's layers as a node read
paint from the layer document's folder), #206 (masters, overrides and variants need a design), #209 (ADR 0051), #211
(which layer a stroke lands on across sets, and one undo across sets), #212 (where an atlas's maps live), #213 (ADR
0012 Q2), #216 (Perforce or Git, plan §10 decision 5).

**Blocked on something else:** #139 (a shipped Text or Vector compound can't expose its text or file, filed on
Vixen), #101 (value outputs wait on #164), #123 (additive height waits on #103), #195 (closable
tabs, option groups and overflow wait on xUI), #210 (waits on #211 and #20), #214 (needs a reader for FBX edge creases),
#146 (needs a real centimetre FBX to learn whether Assimp applies the unit scale), #158 (a new client, needs a design),
#218 (ADR 0055 is proposed).

**Work that can start now:**

- #136: mark on the mesh the layer Find finds. The Server owns the mark; it needs a Worker message and an API field.
- #142: glTF's `KHR_materials_specular` and `_anisotropy`, the anisotropy direction, subsurface, thin film, a graph's or
  bake's layer maps on the mesh, and baking down for engines without them.
- #120: a control for the mask's base projection (also in #311). A texture input on Mask Editor needs a choice first.
- #144: the project's normal format reaching layer pictures and Import Materials (filed); cameras from the mesh wait on
  Vixen's `ModelReader`.
- #148: height from the low mesh, ID from a file or a polygroup, and a min/max height.
- #150: paint skew and cage corrections (a paint target that writes a grey picture; large).
- #181: curves and ramps as exposed parameters, and an Expose for an X/Y pair (#311).
- #207: what changed since a tag.

**In other repositories:** Rikarin/Vixen#1531 (the tiling roll call's vertical seam reads zero for five patterns),
Rikarin/Vixen#1535 (Text and Vector images in `TextureProjectImages`), Rikarin/xui#162, #163 and #164 (menus under a
still pointer, menus taller than the window, a dark well in the light theme).

### Follow-ups filed since wave 8

Open: #281 (UDIM), #282 (quick mask across sets and for fills), #284 (tooling slips), #292 (wave 15, the app), #296
(wave 16, bake and export), #297 (wave 16, library, uploads and log), #298 (wave 16, paint, layers, sets and graphs),
#300 and #301 (wave 17), #307 (texel density per mesh unit, not per metre), #308 (Import's colour space), #309 (a Worker
slow or dying mid-answer still gets 500 or 503 on some routes), #310 and #311 (wave 19).
