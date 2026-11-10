# 02 · Glossary

One word for each thing (#196). The app, refusals, the changelog and these docs use the word in the second column. The
third lists words that meant the same thing and are not used for it in the product. Code may keep older identifiers
(`group`, `colour`, `stack`) where a file format or an API carries them; what a person reads follows this table.

The Server's labels for stored values (`Terms.cs`) and the editor's refusal words (`core/messages.ts`) follow it too.

## Documents and what they hold

| Thing | Word | Not |
|---|---|---|
| A file a person opens in a tab | **document** | file (in the UI), stack, session |
| A document painted on a mesh | **Texturing** | |
| A document of layers with no mesh | **Layered Material** | stack, layer stack |
| A document of nodes | **Graph** | Material Graph, node graph |
| A graph used as one node of another | **compound** | sub-graph, subgraph |
| A graph's Output and what a layer writes: base color, roughness, height… | **channel** | material channel, map (for this) |
| A picture written for a channel, a bake or an export | **map** | texture (in the UI), output |
| A map baked from a mesh: ambient occlusion, curvature, normal… | **mesh map** | bake (for the map), baked map, bake output |
| The run that makes mesh maps | **bake** | |
| A graph or document a layer can be filled with, on the shelf and in the Library | **material** | shared asset, asset, starter (for the thing) |
| Where a material came from: shipped with Patina | **Starter** | factory, built-in |
| A graph published with what it reads, for any project or library | **package** | archive, bundle, export |
| A part of a model with its own maps | **texture set** | material slot |
| A document's width and height in texels | **size** | resolution (in Properties) |

## Layers and masks

| Thing | Word | Not |
|---|---|---|
| A layer that holds other layers | **folder** | group (in Layers) |
| A box around nodes on a graph's canvas | **frame** | group (in a graph) |
| A mask from one number | **Uniform** | Constant, White, Black |
| A mask from a picture | **Bitmap** | Texture |
| A mask from a mesh map | **Mesh Map** | Bake, Bake mask |
| A mask from the colors of an ID map | **Color Selection** | Colour Selection |
| The spelling of colour in the product | **Color** | Colour |

Prose in docs and code comments may spell *colour* the British way; a label, a button, a tooltip or a refusal says
**Color**, as Base Color and every blend mode already did.

## Actions

| Action | Word | Not |
|---|---|---|
| Making something go, to the bin or out of the document | **Delete** | Remove (for this), × alone |
| Taking something out of a list it belongs to, which stays where it is: a tag, a palette color, a filter of a mask | **Remove** | Take off, Delete |
| Breaking a folder up, keeping its layers | **Ungroup** | |
| Leaving changes unsaved on close | **Don't Save** | Discard, Leave |
| A document with edits no file holds | **unsaved changes** | changes that are not saved, edits that are not in its file |

A dialog about unsaved changes says *“{Name} has unsaved changes.”* and offers **Cancel**, **Don't Save** and **Save**
(**Save All** for several).

## Panes

| Pane | Name | Shows |
|---|---|---|
| `viewport` with a mesh | **Viewport** | the model, painted |
| `viewport` with no mesh | **Preview** | the material on a stand-in shape |
| `atlas` | **2D View** | the map being painted, flat |
| `texture` in a texturing or layered material | **Maps** | a mesh map or a channel's map chosen elsewhere |
| `texture` in a graph | **2D View** | the chosen node's picture |
| `texture` in an image material | **Result** | what the image material makes |
| `paint` | **Properties** | the document, the brush and the chosen layer |
| the status bar's panel | **Activity** | the engine's jobs and everything the app said |

A graph's 2D View and a painted document's are the same kind of view, a flat picture of the work, in workspaces that
never show both.

## Words for the engine

| Thing | Word | Not |
|---|---|---|
| The Server, to a person | **the engine** | Server, backend |
| The Worker, to a person | **the GPU engine** | Worker, renderer |
| A piece of work the engine runs: a bake or an export | **job** | task, build (in the UI) |

## Capitals and ellipses

Buttons, menu items, dialog titles and pane names are in Title Case: **New Project…**, **Save as Compound…**, **Add
Channel…**. Section labels and sentences are in sentence case. An ellipsis means the command asks for more before it
acts.
