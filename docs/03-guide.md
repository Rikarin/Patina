# Guide

What Patina is for and how its parts fit, for someone opening it for the first time. Help > Documentation opens this
page. The words follow the [glossary](02-glossary.md); the design behind them is the [plan](01-implementation-plan.md).

## A library, and what it holds

A **library** is a folder that holds one game's work: its projects, the materials they share, and a cache Patina keeps
beside them. Start with File > New Library… or File > Open Library…, and the window remembers the libraries you had
open.

A **project** is one object or one asset set. File > New Project… asks what it starts with:

| Start with           | For                                                                                                                    |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| **Texturing**        | Painting and layering materials on a mesh. It asks for the mesh, and for a detailed mesh to bake from if you have one. |
| **Layered Material** | Stacking materials into a new one, with no mesh.                                                                       |
| **Image Material**   | A material made from one photo.                                                                                        |
| **Graph**            | A material, a mask or a brush built from nodes.                                                                        |

To look around first, Start's **Samples** copy a finished project into the library: **Knot** is a gold knot around a
red sphere, each a texture set with a few layers and masks to take apart. It is yours once copied, like any project.

Everything is a file in the library's folder, so it can sit in version control, and nothing is lost if the cache is
deleted but the time to rebuild it. **Your work is yours:** libraries, projects and the textures you export belong to
you, and Patina's licence does not reach them.

File > Library Settings… shows what new projects start from and looks after the library: **Check Library** lists what
is wrong or out of date, **Prune** takes out of the cache what nothing has used for a while, and **Clean** removes the
copies of paint a project keeps each time a new UV layout moves it.

## Painting and layers

A texturing project opens on its **viewport**, its **2D view** and its **layers**. Pick a tool from the strip down the
side of the viewport: the brush (B), the eraser (E), fill, clone, the color picker and the others. Its settings and the
layer's are in **Properties**.

- **Paint layers** hold strokes. **Fill layers** cover the layer with a color, a bitmap, a graph or a material.
  **Filter layers** change what is beneath them. **Folders** group layers.
- A layer can have a **mask**: painted, a bitmap, a generator that reads the mesh maps, or a color selection from an ID
  map. A mask's own effects run in order and can be reordered.
- Drag a material from the **Materials** shelf onto the stack to add it as a layer, or Alt-click one to paint with it.
- Every edit can be undone, and the **History** pane lists what each step did.
- **Snapshots**, under the history, keep the document as it is saved under a name you give, its paint with it, and
  outlive closing it. Restore puts it back, after keeping how it stood as a snapshot of its own.

## Baking

File > Bake… makes the **mesh maps** that generators and masks read: ambient occlusion, curvature, normals and the
rest. Quick Bake bakes again with the project's settings. A mask that needs a map shows grey until the map is baked,
and the Layers pane says so with a Bake button.

## Graphs

A graph is nodes and wires. Space opens Add Node… where you can search by name; a node's settings are in the
**Inspector**. A graph can be published as a node of its own, a **compound**, and used by other graphs and as a
layer's fill or filter.

## Export

File > Export… writes the texture sets as files, by an **export preset**: one for each engine Patina knows (Unreal,
Unity, Godot, Blender, glTF and others), or one of your own. **Checks** lists what an export would get wrong before you
make it.

## Keys and help

- Help > Keyboard Shortcuts… lists every command with its keys, and lets you change them. A button's tooltip names its
  key, where it has one.
- Help > Diagnostics… shows what the engine behind the app is doing, and restarts it.
- Help > Report a Problem… opens a new issue, where a problem or an idea is welcome.
- Help > About Patina says which version you have and the licences it and its parts come under.
