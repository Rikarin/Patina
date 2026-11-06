# ADR 0012: A project holds typed documents, and each opens its own workspace

**Status:** accepted, 2026-11-06, and being built in the order below on `patina/library-project`.

## What is wrong

Patina has the engines of a material suite but no idea of *what the user is working on*. The app opens one fixed layout of Viewport,
Texture, Graph, Inspector, Paint, Bake and Export, whatever the task. A project has no type. A graph and a stack are two singletons,
one of each, sharing one screen. So an artist making a tiling material sees a 3D viewport with nothing in it, and an artist painting
a prop gives a third of the window to an empty graph canvas.

## What InstaMAT does

**Confirmed** from a published review and tutorials (sources below):

- The welcome screen offers four things: **Asset Texturing**, **Material Layer Project**, **Materialize Image** and **Element Graph**.
- Choosing one lands in a **Package**: a folder structure holding projects (layer setups and node graphs), imported meshes and other
  assets, saved as one `.IMP` file. More projects are created inside it.
- Asset Texturing is typed Multi-Material, Multi-Material Unified or UDIM, and takes a mesh when created.
- A layer can be a **Multi-Channel Layer** (a value or a graph per channel), a **Multi-Channel Brush**, or an **Element Layer** (an
  Element Graph whose outputs map onto the project's channels). There are also **Element Brushes**.
- Brushes and masks built in a graph are written to the Package and can be painted with straight away.
- Baked maps live in the Package and are linked to the project automatically.
- A Materialize Image result can be used in both painting and the node graph, and the Element Graph has a Materialize Image node.
- Bundled materials are Element Graphs, so they can be opened and edited.
- Element Graphs also process meshes (UV unwrap, baking, optimisation), not only images.
- Abstract says layering projects can be dropped into Element Graphs for automation.

**Not confirmed.** Abstract's documentation could not be read, so these are inferences: that Material Layer and Asset Texturing share
one document format, how a Material Layer Project is consumed by another project, and the default layout of any type but Asset
Texturing.

## The boundaries

The four types are not four programs. Three produce a **material** and one consumes it, which is the whole of the design:

| | Element Graph | Material Layering | Materialize Image | Asset Texturing |
|---|---|---|---|---|
| **Question it answers** | How is this made? | What do these make together? | What is this photo made of? | What does this object look like? |
| **Works in** | Procedural nodes | Layers, in UV space | One source image | Layers and paint, on a mesh |
| **Needs a mesh** | No | No | No | **Yes** |
| **Needs bakes** | No | No | No | Usually |
| **Must tile** | Usually | Yes | Usually | No |
| **Parameters** | Authored and exposed | Exposed from layers | A fixed set | None outward |
| **Produces** | A material, mask, brush or filter | A material | A material | Textures for one asset |
| **Consumes** | Images, other graphs | Materials | An image | Materials, masks, brushes, bakes |
| **Can be used by others** | Yes | Yes | Yes | No: it is an end point |

So the dependency runs one way, and that is the rule to enforce:

```
Image ──► Materialize ─┐
Element Graph ─────────┼──► Material ──► Material Layering ──► Material
Material Layering ─────┘        │                                  │
                                └──────► Asset Texturing ◄─────────┘
                                                │
                                                ▼
                                             Export
```

## How Patina maps onto that

| InstaMAT | Patina today | Gap |
|---|---|---|
| Element Graph | `.vxtexgraph`, graph pane | Declares no role. Cannot process a mesh |
| Asset Texturing | `.vxlayers` with a mesh | One document, one texture set |
| Material Layering | `.vxlayers` with no mesh | The format allows it. Nothing outputs it as a material |
| Materialize Image | Nothing | All of it. Normal to Height and Auto Levels exist as nodes |
| Package | A project folder | Close, but it is a folder and has no contents list |
| User Library | `shared/` | Fine, and it scales further |
| Element Layer | `LayerFillSource` graph | Typed path only, with no parameters or channel mapping |
| Element Brush | Nothing | Alphas are four built-ins |
| Mesh Bake (in the Package) | Bake pane with typed paths | Results are not owned |

Two engine facts help a lot. `.vxlayers` already handles both layering types, and a layer can already reference a graph. So this is
mostly a change of model and shell, not a new engine, and **one evaluator stays one evaluator** (plan §3.2): a material's output is a
compiled plan whichever document made it.

## Decision

### 1. Terms

Keep Patina's **Project** as the thing grouped in the Library tree: one asset's folder, which plays the part of InstaMAT's Package. It
holds **documents**, and those are what have a type. Patina differs here on purpose, because a Library holds thousands of projects
and a batch rebuild works per project. A project with a mesh, two graphs and a stack is normal, so one type per project would not fit.

Names for the UI, none of them Adobe's: **Texturing**, **Layered Material**, **Image Material** and **Material Graph**.

### 2. Files

`project.yaml` lists documents instead of a single mesh and stack:

```yaml
schema: 2
id: 0190…
name: Crate
meshes:
  - id: 0190…
    file: crate_low.glb
    high: crate_high.glb
documents:
  - id: 0190…
    kind: texturing        # texturing | layered-material | image-material | graph
    file: crate.vxlayers
    mesh: 0190…
```

A document has a GUID, as an asset does, so a layer's reference survives a move. The kind is stored and also checked: a `texturing`
document must name a mesh, and a `layered-material` must not.

`image-material` is a `.vxtexgraph` written from a template, with one image input and a fixed list of exposed parameters, so it needs
no new format or evaluator. Its screen is simpler than a graph, and "Open as Graph" turns it into a normal one. That matches
InstaMAT's Materialize node inside the graph.

⚠ InstaMAT's neural de-lighting and upscaling have no equivalent. Without them this is height from luminance, normals, levels and
tiling. That is useful, but it is not the same feature, and it should be labelled honestly until a model is chosen.

### 3. One contract between documents

Add a **material** contract to the API, which every consumer uses and no consumer looks behind:

```
Material { id, name, kind, thumbnail, channels: [usage…], parameters: [exposed…] }
```

A graph declares a **role** (`material`, `mask`, `brush` or `filter`) so that the shelf can offer the right thing in the right place.
A role is checked against its outputs: a mask has one grey output. Fill layers, masks, brushes and filter layers hold a reference by
GUID plus parameter overrides and a channel mapping. This reuses `AssetReference`, so pins, the impact preview and batch rebuild keep
working, and a reference cycle is refused at save.

A material lives in the project until it is **promoted** to `shared/`, which is one action. That keeps the Library's strength:
editing one shared material rebuilds what uses it.

### 4. The shell

`app.ts` stops having one layout. Each kind has a **workspace**, a saved dock layout, and an open document is a tab along the top:

| Workspace | Centre | Left | Right |
|---|---|---|---|
| **Start** (nothing open) | Recent, New: the four kinds, Open Library | Library tree | |
| **Texturing** | Viewport beside the 2D view | Library and asset shelf | Layers over Properties |
| **Layered Material** | Tiled 2D view beside a preview shape | Library and asset shelf | Layers over Properties |
| **Image Material** | Source beside result | | Parameters |
| **Material Graph** | Node canvas | Node library | 2D and 3D previews over Inspector |

The Graph pane never opens in Texturing, and the Viewport is only a preview in Material Graph. **Bake** and **Export** stop being
panes: they are dialogs started from a Texturing document, which already knows its mesh and where its results go.

### 5. Refactoring

- **Studio.** The first draft made `GraphStore` and `PaintStore` per-document. That turned out to be unnecessary: the Server already
  holds every open session with its edits and history, so a tab is a path, and going to one joins its session again. The two stores
  show whichever document is in front, and `ProjectStore` owns the tabs. This follows "the Server owns all state", and it leaves two
  large stores alone. A tab behind keeps the unsaved flag it last reported.
- **Server.** `SessionTable` already holds many sessions per file, so little changes. `ViewportSession` is one global state, and it
  becomes "shows this document", with the active tab deciding, which avoids a Worker renderer per tab.
- **Contracts.** `GET /library/project` returns documents. `POST /library/project/documents` makes one of a kind. `GET /library/materials`
  serves the shelf. Blender uses all of these, so nothing depends on the shell (plan §8).
- **Worker.** Nothing changes for layering. Mesh processing in graphs is a separate decision, below.

## Order of work, and where it stands

1. **Document model:** built. `project.yaml` lists meshes and documents, and tree rows expand to show them. Documents and meshes use
   `AssetId`, so asset references can reach them.
2. **Shell:** built. Start screen, a layout per kind, tabs, Bake and Export as dialogs.
3. **Material contract:** built, smaller than drafted.
   - `GET /library/materials` lists shipped, project and shared materials, and a shelf applies them.
   - A graph is published as a compound in `Compounds/Materials`, with one output per usage, and Vixen's graph fill now takes the
     output named for its channel. Publishing again on opening a stack is what carries a saved edit across.
   - ⚠ A graph's role is inferred from having Outputs, so `mask`, `brush` and `filter` roles do not exist yet.
   - ⚠ A fill names a compound by path, not by GUID, and a layer's numbers reach a graph's parameters in Vixen but no UI sets them.
4. **Layered Material:** built. It unfolds into a stack through Vixen's smart material path, so it is a copy of its layers at that
   moment: editing it later does not change a stack that used it, and paint layers do not travel. Previewed on a generated sphere.
   ⚠ No tiling preview yet.
5. **Image Material:** built. A template graph and a screen of maps. Height from brightness; no neural step.
6. **Graph brushes and masks:** not built. It needs a way to render a graph to a picture for a brush alpha.

## Open questions

1. **Project naming.** This keeps Project as the container with typed documents, while InstaMAT calls the document the project. If you
   would rather copy that, Project becomes Package in the UI and nothing else changes.
2. **Graphs that process meshes.** InstaMAT's Element Graph unwraps and bakes in nodes, and Patina's graphs only process images.
   Adding it is a large change to the evaluator's boundary and is not needed for the four types. The recommendation is to defer it,
   with bakes staying a Texturing action.
3. **A single-file package.** InstaMAT saves one `.IMP`. Patina stays folders, because a library is a Git repository of readable files.
   A package export for sharing is the separate P7 work.
4. **Image Material without neural models.** See the warning above.

## Consequences

- The default view stops showing panes with nothing in them.
- Patina's plan said Graph and Paint share one app. They still do, joined by a contract rather than one screen.
- `paint.spec.ts`, `graph.spec.ts` and the other end-to-end specs change, and the new-user journey test grows a path per kind.
- Work already on `patina/library-project` keeps its library opening, folder tree and project opening. Only its `mesh` and `stack`
  fields are replaced, before they reach a user.

## Sources

- [InstaMAT Test: Painting, Procedurals and Pasta](https://digitalproduction.com/2026/01/28/instamat-test-painting-procedurals-and-pasta/), Digital Production
- [Basic use of Layering mode](https://cg-journal.com/en/instamat-layering-mode-basics/), CG Journal
- [InstaMAT home page](https://instamaterial.com/), Abstract
- [What is this: Regenerate Library Preview?](https://community.abstract3d.com/t/what-is-this-regenerate-library-preview/901), Abstract community
- [InstaMAT's Layering Fundamentals](https://community.abstract3d.com/t/instamats-layering-fundamentals-a-deep-dive-into-layering-concepts/762), Abstract community
- [Switching from Mari to InstaMAT](https://abstract3d.com/de/switching-from/mari-to-instamat/), Abstract
- [Layer Stack](https://docs.instamat.io/Products/InstaMAT_Studio/Layering/Layering_Interface/Layer_Stack) and [Layer References](https://docs.instamat.io/Products/InstaMAT_Studio/Layering/layer_references), InstaMAT documentation: titles only were readable
