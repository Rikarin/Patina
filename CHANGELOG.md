# Changelog

Notable changes to Patina. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Every command is in one table that the menus, the keys, Keyboard Shortcuts and the tooltips all read, so a key you change is the key the menu and the tooltip show. Keyboard Shortcuts lists every command by section, with the keys lists, trees and the graph canvas answer themselves and what the mouse does in the viewport. A key takes its modifiers (Cmd+X is Cmd+X, never X), a key another command already has where both work is refused with an offer to swap, and a command can have no key. A second preset moves the view with the right button, as sculpting apps do: right-drag orbits, Alt+right-drag pans, Ctrl+right-drag zooms. Your keys are kept in `keys.json` in Patina's settings folder.
- Keys typed into a text field or a dialog stay there: B in a name field types a B, and paint keys no longer change the tool behind a dialog.
- Menus follow what is open: Layer and Tools with a layer stack in front, Graph with a graph, Viewport where a document shows one, and Window and Help always. An item is off when it would do nothing, so Cut, Copy and Paste need layers or nodes selected, Delete is off with the viewport focused, and Save As, Reload Mesh and Project Settings need a document. File has Bake…, Quick Bake, Save All, Close Document (Cmd+W) and Close Project, and Edit has Duplicate. Undo and Redo say what they would undo, in the menu and on the Layers buttons.
- Window > Layout switches between named layouts of the document in front, on F1 to F3: 3D and 2D, 3D Only or 2D Only when texturing, and the same for a layered material and a graph.
- A History pane lists the undo steps of the document in front, with the ones undone greyed. Clicking one goes back or forward to just after it, as one step for every client, through `GET` and `POST /api/v1/stacks/{id}/history` and `/graphs/{id}/history`.
- Symmetry reaches past three planes through the origin. A stroke copies itself through a mirror plane or round an axis, two to 64 times, and the plane or axis goes through the model's centre or the mesh's origin, so a model that is not centred on its origin mirrors onto itself. A mirror's plane moves along its axis by a share of the model's size. Strokes in the 2D view are copied too, wherever their place on the surface lands. The viewport draws a mirror's plane across the model, or a radial symmetry's axis, where the copies will go.
- The brush has every setting the engine has: a falloff curve, stretch and its angle for an elliptical stamp, flow jitter, scatter across the stroke only, whether stamps build up where they overlap, and what its size is measured in: pixels of the screen, texels of the map or a share of the model. Size runs to 1024 on a slider that is as fine for a small brush as a large one. Reset to Default puts every setting back.
- Brush and tool presets. Ten ship with Patina (CC0): Soft Round, Hard Round, Chisel Marker, Chalk, Spray, Stipple, Wash, Even Grime, Ink Line and Soft Eraser. Save keeps the brush as a preset of the project, with its colour if asked; Update rewrites the one taken up; the star makes one a favourite, which lists first. A preset is a `.ptbrush` document of its project, so other projects offer it too and the library moves and deletes it.
- A project keeps its recent colours, a palette of colours kept on purpose, the brush and the symmetry in its project.yaml, so they come back after a restart and in another client.
- Anchor points have names. A layer's Anchor field names the stack as it stands just above that layer, and Properties lists every layer that reads it, where and in which channel. A mask reads an anchor point in one channel it names, a fill layer can read one (in each channel or in one), and the image inputs of graph fills, filters, mask generators and mask effects can be fed from one through the API. Renaming an anchor point keeps every reference to it, in one undo step, and a duplicate's anchor point takes a name of its own.
- A folder flattens into one canvas from its layer menu, and the whole stack from Flatten Stack: the result is the picture the folder made, its layers are kept out of sight, and Unflatten Folder brings them back. A flattened folder takes a brush. Undo takes a flatten back like any edit.
- Menus work from the keyboard. Shift+F10 or the Menu key opens a layer's or a library row's menu where it is, the arrows walk it and its submenus, Enter chooses and Escape closes it and gives focus back. The arrows walk the layer rows too, and open and close folders. Layers has an Add… menu for new layers, masks and effects, and the list of meshes a layer keeps to takes focus and closes on Escape.
- Moving or renaming a folder or project with open documents asks first, since it closes them and loses their undo, and Project Settings says which documents a new mesh or layout will close before Apply.
- Twenty-nine graph nodes. Colour: Clamp, Threshold, Quantise, Channel Mix, Image Ramp (a ramp read from another image), Replace Colour, Replace Colour Range, Colour Match, Dissolve, sRGB to Linear, Linear to sRGB, Premultiply, Unpremultiply, Lookup Table (a 3D colour table stored as a 2D strip), Palette and Select Input. Filters: Median, Edge-Preserving Blur, Painterly Smooth, Spin Blur and Anti-Alias, and a Glow compound under Utility. Space: Cartesian to Polar, Polar to Cartesian, Four-Corner Warp, Rotate by Map, Swirl, Wedge Symmetry and Trim to Content. Every one with one image in and one out is also a filter layer and a mask step, except Premultiply and Unpremultiply, which cannot change a layer stack's opaque pictures.
- Noise has a Gaussian basis, a Scale and an Offset for each axis (the offset in cells, so 0.5 is half a cell), a Min Octave that skips the coarsest octaves, and a Profile that makes each octave ridged or billowed. Worley sums its distances over octaves. Shape has seven more kinds: Polygon with a Sides port, Star, Ring, Capsule, Crescent, Pyramid and Hemisphere. At their defaults both draw what they drew before.
- Histogram nodes: Histogram (one bin per column, as densities or cumulative shares), Histogram Render (the histogram as bars), Histogram Equalise (each value becomes the share of the image at or below it) and Summed Area Table (each texel the sum of the rectangle up to it, for region means). A host shows any node's histogram by adding Histogram after it and reading the first row.
- Compounds: Clouds, Cell Walls, Fractal Cells, Streaks, Spots and Liquid under Noise, and Stripes, Weave, Hex Cells, Triangle Grid and Varied Brick (a brick bond with a tone per brick) under Patterns.
- Non-Uniform Blur reaches any radius. It stopped at 12 texels at the bake resolution, and a graph that asked for more at a large bake was refused.
- Export is a dialog of its own, from File > Export… (Cmd+Shift+E) as well as the title bar. It exports the document in front, its project or the whole library, and asks first for the library. It has texture set checkboxes with Check All, Uncheck All and Invert, an output folder with Reveal, a size with its filter, padding, dithering, and the preset's files with each one on or off and in a format of its own. It says when documents it reads have unsaved changes, which it would leave out since export reads saved files, and offers to save them. It shows a progress bar with what it is working on, and keeps its settings in the project's project.yaml, so another client or another machine starts the next export from them.
- Export writes TGA, JPEG, TIFF (8-bit, 16-bit and 32-bit float), lossless WebP and Radiance HDR, from a preset or from the dialog.
- Export pads: the islands of the mesh's layout grow into the background without end, or by a distance in pixels and then leave it transparent, one colour, or a smooth diffusion. The factory presets pad without end, so mips no longer bleed the background into charts. A preset can also set a size, a filter and 8-bit dithering.
- The Bake dialog has a section per map. Every map can be turned off, the normal and the height included, and each has its own settings: the normal map's space, the height map's range (measured, the ray distance, or a number), ambient occlusion's spread, falloff, ray distribution, minimum occluder distance, ignore backfaces, a part's own surface only, and a ground plane, thickness normalised to its thickest point, and the ID map's colour source (vertex colour, material, material colour, mesh part or connected shell) and colours (random, hue shift or grey). The rays have a front and a rear distance apart, in a percentage of the mesh's size or in model units, and can ignore backfaces. Parts match by name with the endings a studio uses.
- A project keeps its bake settings in its own file: closing the dialog, restarting Patina or opening the project in another client starts from the same settings. A bake makes maps for the texture sets ticked in the dialog, a job each, and a bake from a layered material bakes the project's mesh.
- A bake can read several high poly meshes, in order, and its result has a log: the meshes it read, how each part matched by name, and how long it took. The dialog says why Bake cannot start, and shows each map by name.
- Thickness is the depth of the surface behind each point. It was the share of rays that met anything, which reads white inside every closed shape.
- A single-material mesh's ID map shows its parts as shells again, where it was one flat colour.
- The viewport has display settings beside the picture: perspective or orthographic with a field of view, the views it snaps to, exposure in stops, the environment and the light turned apart, shadows, the background's opacity and blur, Vixen's ACES, AgX, Filmic and Reinhard curves or none, contrast, saturation, vignette, grain, lateral aberration, sharpening and a wireframe. A `.cube` look sits beside the environment. The view shows the model lit, as clay, unlit, one stored channel exactly as the file holds it, or a mesh map from the last bake, and V steps through them. The number pad snaps to the front, side and top as Blender's does. A project keeps the settings and opens as it was left, and any client can set them with `PUT /api/v1/viewport/display`.
- A lazy mouse: the brush trails the pointer by a distance set in the brush panel, and paints the same however fast the pointer reports. Shift-click draws a straight line on from where the last stroke ended, in the map or on the mesh, and Cmd or Ctrl snaps it to 5°.
- Layers duplicate (Cmd+D), copy, cut and paste, from the layer menu and from Edit. A copy is the layers and their paint as they stood, so a cut layer still pastes, and it pastes into another texture set or stack of the same size. A duplicated or pasted paint layer and its mask have pixels of their own, never the original's. Each is one undo step.
- A new paint layer's first stroke no longer shows paint a removed layer of the same name left in its file.
- An anchor mask is one picture whichever channel its layer writes. It read the channel being built, so under roughness it was the stack's roughness, and an anchor onto a layer that left a channel alone stopped the whole stack.
- Every filter Patina has can be a filter layer or a step on a mask, from the same list the node menu reads: 39 on a layer and 38 on a mask, where five of each were offered before. Height to Normal, Bevel, Gradient Map and the other filters that read one channel take the grey of what is beneath, or a mask's own value. Properties shows each filter's fields as the graph's Inspector does, ramp and curve editors included, and choosing another filter starts it from its own defaults.
- The material shelf shows each material as a picture, in a list or a grid of small or large tiles, and narrows by type (materials, masks, filters, brushes) and by where a material is from. Search finds a material by name, type, kind, project or path. The Worker draws each picture once into the preview cache, so a picture costs nothing the second time and a deleted cache only draws them again.
- The node palette and the Nodes list rank what was typed, best first and in the same order: Levels comes before Auto Levels, and Enter adds it. A new node wires itself to the selected node, or goes into the selected wire, and lands beside what it joins. Space adds at the pointer whenever the pointer is over the canvas.
- Layers and Project Settings say when the mesh has changed since the last bake, and Reload Mesh says what it will cost before it closes anything.
- Project Settings lists a project's meshes, pictures and bakes under Files, and the status bar says what an import brought in.
- A preset can read every baked map: curvature, thickness, ID, position, world normal and bent normal, not only normal, height and occlusion.
- Export works on a project made in the app: every texture set of a texturing project goes through the preset with its bake, and a material project exports its materials. Nothing has to be declared. Files of a project with several sets carry the set's name, and presets can place it with `{set}`.
- Preview and the report say so when a build would write nothing, and why, instead of Done with no files.
- A graph tiles across and down by default, with a switch for each in its settings, and every filter keeps a tiling picture tiling. A graph reading a mesh map holds at its edges instead, as a layer stack always does.
- A Pixel Processor has a Format setting. It keeps all four lanes unless it is told to keep grey.
- A colour field in a graph has a number for each lane, so a colour can be past 1 and hold 0.18 exactly.
- Project Settings holds the project's mesh, Auto Unwrap and Preserve Paint. A mesh with no UVs gets a layout in a new file beside it, when it is set or from the error's Unwrap Mesh button.
- A layer can be kept to named meshes, from the square on the right of its row. It combines with any mask the layer has.
- Effects inside layers: a layer can hold paint, fills and filters, each with its own blend, opacity and eye, over its content and under its mask.
- Each layer row has a small blend dropdown and opacity slider on its right, beside the mesh square.
- A Texture Sets pane sits over Layers, whose toolbar is icons with a channel dropdown. Layers show a shaded sphere, a mask icon and an orange mark when something has been added, with mask steps listed under the layer.
- The layer menu adds white, black and bitmap masks, inverts a mask, adds generators, Levels and Blur, and groups layers (Cmd+G). Properties has headed sections.
- Reload Mesh (Cmd+Shift+R) reads the mesh again from where it came from, and Project Settings is on a project's right-click menu.
- Folders take masks from the layer menu.
- Texture sets: each material of a mesh gets its own layers, maps, bake and export, and the whole model shows together while one is painted.
- Paint follows a new mesh or a repacked layout by where it sits on the surface, with a copy of the old paint kept in the project.
- Patina libraries have their own icon in Finder.
- The macOS app carries MoltenVK, so it needs no Vulkan SDK or Homebrew.
- A library reopens on the document that was last in front.
- Mask generators have settings: how far Metal Edge Wear reaches, how much Dust settles, and the rest of what each one exposes.
- The viewport works from the keyboard: arrows turn it, Shift and the arrows pan it, plus and minus zoom, and F frames the model.
- The viewport has shadows, and an environment to choose: Studio, Overcast, Sunset, or an `.hdr` panorama.
- Projects can be duplicated and deleted from the Library. A deleted project goes to the library's trash.
- The graph toolbar and the brush tools are icons, the brush colour keeps recent colours, and roughness, metalness and the like are sliders on a constant fill.
- A pen's pressure changes brush size by default.
- A Nodes pane lists every node by category: search it, double-click to add, or drag onto the canvas. A number in the Inspector becomes a parameter with one button.
- A graph shows on a sphere when no mesh is open, and its button now keeps the 3D view up with edits.

### Fixed

- File > Import, or files dropped on the window, just after a project is made or opened bring the files into it, where they said to open a project while its tab was already showing.
- Ambient Occlusion and Height to AO tile on every GPU. On some drivers, Linux's software Vulkan among them, the first column and row read the far edge and left a seam where the picture repeats.
- Transform 2D turns a graph that is not square in texels, so a circle stays round. It turned in the unit square, which squashed a turned picture by the graph's aspect.
- The viewport shows the whole model in a pane of any shape: the opening view and F fit it to the pane's narrower side, where a tall pane used to cut it off. F keeps the angle the camera looks from.
- A panorama's sun keeps its light. Every texel used to be clamped at 64, which kept a small part of a real sun, and the panorama was read nearest, which could miss a small sun altogether.
- Painted height lights the surface. A texture set with a Height channel writes a normal from it, combined over its Normal channel when it has one, so a bump painted in height shows in the viewport and goes out with the export. Texture Sets has a Sharp or Smooth choice for how height becomes the normal; Smooth, the default, hides the steps of an 8-bit height.
- A mesh laid out past UDIM tile 1001 is no longer taken in silence. Layers says which tiles a texture set reaches as soon as it opens, and a bake refuses it by name instead of filling 1001 and leaving the rest empty. Patina still paints, bakes and exports one tile.
- Auto Unwrap keeps what it did not lay out: smooth normals, every material (each its own texture set with an atlas of its own), vertex colours and part names. A material that already has UVs keeps them. Project Settings sets its atlas size, margin, packing, island turns, texel density and what happens when islands do not fit, lays the mesh out again when they change, and shows what each set's layout came to: islands, stretch, how much of the atlas is used, and any turned-over triangles.
- Flood Fill goes round the wrap: on a graph that tiles, an island over the seam is one island, with its true size and a UV that runs across the seam. Its box can pass 1, by as much as it runs past the image.
- `project.yaml` no longer records the absolute path a mesh came from. A source inside the library is stored relative to it, one outside stays on this machine, and a fresh clone asks for the file once on Reload Mesh.
- Reload Mesh with an unchanged file does nothing and says so, and a changed file on the same UV layout keeps its paint as it is. Paint is only resampled, and copied to `.paint-backup`, when the layout really moved.
- Importing a file nothing can read, such as a `.dae`, `.usd` or `.txt`, is refused and says why, instead of being copied into the project as a picture.
- A layered document that cannot save says why, and one whose file changed on disk asks whether to overwrite it, as a graph does. Closing or quitting stays put when the save did not happen.
- Don't Save on a layered document throws the edits away. They used to stay open on the Server and come back on the next open, and autosaved copies of refused edits are no longer offered back.
- Discard in the recovery dialog works, and closing that dialog keeps the autosaved work and asks again next time. Work still open in a session is no longer offered back as if it had been lost.
- A failure shows once, in the bar above the status bar or in the dialog that caused it, and goes when the next action starts.
- Line and path strokes keep a pen's pressure, and a path lands where its points were placed: moving the view paints what has been placed so far. Mirror says when it does not apply, the picker's tooltip names its real key, and Esc cancels picking a colour.
- Smaller layer fixes: Add Generator no longer replaces a working mask, a graph fill chooses its graph in Properties, a filter from a graph can go back to a named one, the Layers channel select also changes what 2D View shows, the last mesh of a layer stays ticked, a brush on a channel the layer leaves alone says so instead of landing unseen, a new fill holds the defaults it shows, Delete Layers deletes layers with a mask in front, added layers are numbered, and colour selection masks have Softness. The stack's seed is in Properties.
- A filter shows the numbers it really runs with, as sliders and switches like the graph Inspector's, rather than 0 until set. Brush spacing goes past where stamps touch, so a brush can lay a spaced pattern.
- A new occlusion channel starts white, so the model no longer goes dark, and channels and blend modes that showed internal words have plain names.
- A drag on the mesh or the map turns the model or does nothing when no stroke can land, instead of raising a refusal, and the cursor shows which it will do.
- A layer that is half set up no longer stops the whole stack updating: an empty texture channel, graph, generator, bitmap, mesh map or anchor leaves that part out. Layers lists what the compile said, marks the layer at fault on its row, and shows Evaluating… as the graph does.
- A mask on a fill, filter or folder can be painted, not only one on a paint layer.
- Painting a white or grey mask changes only what the brush touches, instead of hiding the rest of the layer, and a new paint mask starts by showing the whole layer. The brush paints a mask in grey. A mask that is a picture refuses the brush and says why, rather than being replaced. Remove Mask takes all of the mask away, and Invert Mask turns off again.
- One rule says which bake a document reads: the one chosen for it, then the folder a bake of it goes to. Stacks and graphs reconnect on opening, a set with no bake no longer takes a neighbour's, and baking again into the same folder reaches what is open.
- Graph settings has a Bake picker, so a graph can read any bake in its project.
- Baking a texture set whose material has gone from the mesh is refused by name. It baked the whole model into the set's atlas.
- Normals from a graph fill are lit from the right side on the mesh, and export with the right green for every engine. A stack's normal was treated as Y+ though its fills are Y-.
- Every place that takes a picture reads the same formats, and file dialogs offer that list: PNG, JPEG, TGA, BMP, PSD, GIF, EXR and HDR. An `.exr` in a layer and a `.tga` skew map no longer fail after they are chosen.
- Layers and image materials keep a picture's depth: 16-bit PNGs, EXR and HDR were narrowed to 8 bits, or refused.
- The viewport's environment takes `.exr` panoramas as well as `.hdr`.
- A cage bake works on a mesh with several materials. It was refused, because the cage was read whole and the low mesh was not.
- The Export panel reports every way a build can end: failed steps with their reasons, files a preset left out, and what a stopped or failed build finished. Preview numbers and the Export Maps count clear when the question changes.
- Export Maps writes the open document's maps wherever the viewport is looking. It used to write every set of the model if the viewport wore them, and only one otherwise.
- `mips: True` in a preset is refused. It used to mean no mips.
- Fractional opacity exports as it is. All five presets wrote it through sRGB, so 0.5 came out as 0.735.
- `1-map` lanes in an sRGB file invert the value and not its encoded number, and a lane with no map goes through the file's encoding like any other.
- Changing a graph's size no longer changes its picture. Graph settings has a Size, up to 8192, and Width and Height move under Authored At, where they keep their old meaning. New Graph and New Project start a graph at the chosen size, `patina bake` takes `--size`, and a `texture-graph` output takes a `size` setting.
- Graph settings, New Graph, New Project and Bake offer the same sizes, 256 to 8192, from the Server.
- Number wires show in the graph, and node accents and frames show all their colours: a wired number has a port and says what drives it, in place of a control that would do nothing, and Disconnect removes the wire. In a compound, the Inspector's port button on a number gives it a port to wire to, so a Float input can be connected.
- Flood Fill finds whole islands of any width by default, at any size, and it and Distance work past 2048. If its Iterations run out on a very twisty mask, the graph says so.
- A default Levels no longer adds noise, which read as a surface tilted by up to 51 degrees once it reached Height to Normal.
- Each octave of a noise is its own field, rather than the one before it shrunk into the corner.
- Divide, Colour Dodge and Colour Burn go past 1 in a float graph, so a height of 4 over 2 is 2.
- Output nodes and nodes with no picture yet no longer draw an empty preview box.
- All eight grunges tile, along with the filters they are made of.
- Bake formats, export presets, job states and layer blend modes show plain names instead of internal ids.
- The first stroke after opening a 4K stack no longer waits seconds for the maps to reach the mesh.
- Save All on the quit dialog stays open and says why if a document could not be saved, instead of doing nothing or quitting without it.
- Quitting cannot be held up by a process that will not stop.
- Process tests skip only for a missing Vulkan device, and fail if Vulkan is required. The packaged app ignores development
  environment variables, its IPC answers only the studio, its fuses are set, and the frames file is private and swept after a kill.
- A stack cannot name a paint file outside its own folder, and a Server with no token only answers to a loopback `Host` (ADR 0013).
- A second Server on a library is refused by an operating system lock before it touches anything, including on a shared drive.
- Files left by a crash are swept at start-up, `patina cache prune <library>` clears old objects and reports, and trimming previews
  no longer removes what a stack is showing.
- Library files reach the disk before their name does, an edited viewing table reloads, and a torn build log loses only its torn line.
- A fault in one job, stack or graph no longer stops the Server, and a bad setting in `project.yaml` fails its job instead of hanging
  the queue.
- A long stroke on a big stack no longer gets a healthy Worker dropped: health checks are answered while it works, and a Worker whose
  device stops moving ends itself so that a new one takes over. A new Worker brings open stacks back with no edit needed.
- Saving reports what it did: an edit made during a save, or more of a drag after one, leaves the stack unsaved, a stack that will not
  evaluate refuses to save, and one refused stroke no longer stops painting.
- A layer-stack build depends on the paint, mesh and bake its stack names, so painting and saving rebuilds it, and it reads the stack's
  bake.

### Changed

- Every slider has its number beside it, and a share reads as a percentage with %, an angle in degrees: layer opacity, mask value, colour tolerance and softness, and the mask grey. The brush angle steps a degree at a time, and the brush size says what it measures.
- Lists show names, never the engine's words: blend modes, mask and fill sources, generators, mesh maps, channels, a setting's choices, port kinds and anchor layers, and in the Export, Bake and viewport panes the padding modes, filters, maps and channel views. Export offers the Server's texture sizes. Delete Project and Delete Folder are red, and folder colours have names.
- Messages say what happened in plain words. A compiler problem shows its first sentence, with its code and the rest behind Details.
- A brush can paint several channels at once, one undo step, and a stack can add height, normal, emissive and the rest. There is an
  Eraser and a colour Picker, and painting has rebindable keys, with `[` and `]` for size and Esc to end a path.
- The viewport paints as soon as a stack is on the mesh: Alt or right turns, middle or Shift pans, F frames, and the camera comes as
  close as a detail needs. It shows emissive and cut-out opacity, has exposure and light rotation, and statistics are off by default.
- The 2D view zooms and pans, and looking at a channel no longer changes what a stroke writes.
- Graphs copy, paste and duplicate, and the node search reads names and summaries.
- `.ptlib` opens the app, work is autosaved and offered back after a crash, Save As copies a document with its paint, Export Maps writes
  the open stack, files import from the File menu or by dropping them, and View has Diagnostics and Reset Layout.
- Four CC0 starter materials are on the shelf.
- There is no `shared/` folder any more: anything in any project can be used in any other. Pins, the impact preview and batch rebuilds
  work on project documents, the Library's second view lists Materials, and export presets live in `export-presets/`. A library that
  has `shared/` turns it into a Shared Assets project when it opens, and every reference still resolves.
- Layers can be selected together with Cmd and Shift, then moved or deleted as one, and Delete, Backspace and Cmd+Backspace delete
  what is selected. Cmd+Z, redo, Delete and Select All go through the Edit menu, so they work on a Mac, in stacks and graphs alike.
- Materials drag from the shelf into the stack and land where they are dropped. The shelf lists every material in the Library, with
  nothing to promote first, and a material from another project brings what it needs.
- A graph can be used as a material, a mask, a brush or a filter, from its menu in the Library, and the Materials shelf puts it in the
  right place. A brush graph renders to a picture in the project's `brushes` folder.
- A mask runs a stack of effects: Levels, Invert, Auto Levels, Blur and Sharpen, each with its own numbers.
- Fill layers show what they really look like, drawn alone by the Worker. A Layered Material can be seen tiled.
- A graph fill shows the parameters its graph exposes in Properties, and each layer keeps its own values. Saving a graph updates every
  open stack that uses it.
- A fill can be Spherical or Cylindrical as well as UV, Tri-planar or Planar, and every projection has a scale, rotation and offset,
  so a texture on a UV fill can repeat and turn too. They live in the layer's `placement`, not its settings.
- Layer pictures, mask pictures and the Bake dialog's meshes are chosen in the system dialog, with no typed paths. A picture is
  copied into the project's `pictures` folder. New Project takes a high poly mesh.
- The Library's projects are a tree of folders. Folders and subfolders can be made, renamed, coloured, moved and deleted, and
  projects can be dragged between them, one or several at a time. Deleting a folder moves what it holds up a level, so it never
  deletes a project. Folders are folders on disk, with a `folder.yaml` that holds the colour and keeps an empty one in Git. A search
  or a tag still lists across all of them.
- A project holds typed documents (ADR 0012): `project.yaml` is schema 2, and lists its meshes and its Texturing, Layered Material,
  Image Material and Material Graph documents. New Project takes a kind and opens ready to work. A project opens in the tree to
  show them, and the Paint pane has no path box.
- The app starts on a Start screen, with no empty panes. Each kind of document opens its own layout, kept per kind, and open
  documents are tabs that keep their edits and undo while another is in front. Bake and Export are dialogs.
- Materials reach layers from a shelf: a graph's maps fill a stack's channels by usage, live, and a Layered Material or one of the
  six shipped materials unfolds into a stack as layers. An Image Material makes a material from one picture, with height from its
  brightness and no neural step. A Layered Material is previewed on a sphere.
- The layer stack is a pane of its own, with Properties under it: rows with visibility, thumbnails, mask thumbnails, blend and opacity,
  folders that open and close, drag to reorder and to move in and out of folders, and a right-click menu. Clicking a mask chooses
  it for the brush. Thumbnails are a layer's colour or kind for now, not its rendered picture.
- Vertex colours reach masks: a glTF's `COLOR_0` bakes into an ID map as the colours you painted, each face keeping one colour, and a
  Colour Selection mask picks one from swatches of the map's colours or with an eyedropper on the mesh or the 2D view. A mesh with no
  vertex colours still gets its ID map from its materials.
- A fill's projection is set in Properties: UV, tri-planar with blending, or planar along an axis, with a scale.
- A stack whose masks read baked maps no longer stops with no bake: they read as grey, a notice offers to bake, and a project's bake
  reaches its stacks by itself.
- The app opens and creates libraries itself, from the File menu or the empty Library, remembers recent ones and reopens the last.
  `PATINA_LIBRARY` stays for scripts. `patina-server --create-library true` lays out a library that does not exist yet.
- The app has its own menus, and the window title says what is open. Reload and the developer tools are not in a packaged build.
- Paint now counts as unsaved work: Save shows it, the app asks before leaving a project or quitting, and closing a project closes
  its stack and clears the viewport.

- Reading a mesh on Linux no longer needs a system Assimp. The engine carries its own, 5.4.1 where Ubuntu's was 5.3, so a
  distribution without `libassimp5`, such as Ubuntu 26.04, can open meshes. It needs glibc 2.33 and `libstdc++` from GCC 11.
- ⚠ A baked tangent-space map is now in MikkTSpace, with green up the image, and its rows run from `v = 0`, so a map is the
  right way up on the mesh in every tool. Maps baked before this are in another basis and upside down; the bake key changed,
  so they bake again.
- `.glb` files written by `patina-bench` no longer turn `v` over.

### Added

- Export presets for Unreal, Unity URP, Unity HDRP, Godot and Vixen, as files. A library adds or replaces one with a file in
  `shared/export-presets`. Green follows the source and the target: a bake's runs up, a graph's `Height to Normal` runs down, and
  it turns over only when they disagree.
- Batch builds: `POST /api/v1/builds/preview` shows what would change from build keys with nothing run, `POST /api/v1/builds` runs it
  as a job that survives the Worker and the Server, and `/builds/{id}/report` lists what each project wrote. `patina build` is
  the same pipeline with no Server. A second build does nothing.
- `texture-graph`, `layer-stack`, `mesh-maps` and `mesh-lod` outputs in `project.yaml`, delivered to `exports/<preset>/<project>`.
- An Export pane: choose a preset, see the impact, run it, read the report.
- `PUT /api/v1/viewport/look` views frames through a `.cube` table. No export sees it.
- `patina-bench fan-out` and `patina-bench bake-library`.
- A generated Python client in `clients/python`, with a Blender-shaped conformance test that needs no Blender.
- Painting's foundations: layer stacks through the API (`/api/v1/stacks`, `/ws/stacks/{id}`), undoable batches on the same
  numbered events as graphs, strokes sent as lists of stamps, and a Paint pane with a layer list and a brush on the composited map.
  The composite is the plan's, repaired tile by tile: about 6 ms a stamp at 4K with 20 layers, 38 ms to a client when idle (under load ADR 0009 measures 67 ms at p50, and 10.9 s for a
  24 px brush). A Worker that dies
  in a stroke loses nothing. `POST /stacks/{id}/export` writes each map, and `GET /stacks/{id}/pixels` gives a digest of what the
  strokes left, which equals the export's.
- Painting on a mesh. A stack binds a model (`setModel`) and its maps wear it in the viewport as you paint (`PUT /viewport/stack`), through
  dirty rectangles, as the bytes an export writes: `GET /stacks/{id}/pixels?sampled=true` reads what each material slot samples, and a test
  holds it equal to the exported file. Strokes carry a camera (`StackView`) and `POST /viewport/pick` finds a position on the mesh through the
  Worker. Stamps paint only what the mesh covers, dilate into a gutter, and carry on over seams in the viewport and in the atlas.
- The Viewport pane paints: a Paint button, Alt to orbit, a brush outline, and a stamp-to-frame readout.
- The Paint pane has the whole stack: fill, paint, filter and group layers, masks by constant, texture, anchor, generator, bake or paint,
  all sixteen blend modes, per-layer channels, and a bake for generators to read (`PUT /stacks/{id}/mesh-maps`). `GET /stacks/vocabulary` lists
  the words these take, so a client keeps no list of its own.
- Brushes: alphas, rotation, jitter, pressure to size and flow, line and path strokes, and mirroring on the mesh across its X, Y or Z plane.
- `patina-bench stamps` and `s4-stack`, and `paint.bench.ts`, which measures stamp to frame in a real window.
- `patina-bench s4`.
- `patina-worker --check-natives` says which Vulkan device and which Assimp this copy of the engine finds, and reads a mesh.
  Packaging runs it on what it just built.
- The repository: licence, conventions, Skala gates with an empty baseline, and CI workflows.
- `patina-server`: `GET /api/v1/system`, a committed `openapi.json` with a drift test, `--library <path>`, a bearer token
  and a per-user discovery file that keeps one Server per library.
- `patina-worker`: connects to the Server over a Unix domain socket, says hello and answers health.
- `patina version`.
- The Library's engine half: `library.yaml`, `project.yaml` and asset sidecars as canonical YAML with schema versions and
  migrations, GUID references that float or pin to a content hash, a rebuildable SQLite index with search, usage counts
  and reverse dependencies, build keys, and a health check.
- `patina library new` and `patina library check`.
- Read-only library endpoints: `GET /api/v1/library`, `/library/projects`, `/library/assets` and
  `/library/assets/{id}/impact`.
- `POST /api/v1/library/projects`, which writes a new project's files and lets the index catch up, and
  `GET /api/v1/library/projects/tags` and `/library/assets/tags`.
- The Library browser: projects and shared assets in a virtualised list that pages through the Server, with search and a
  tag filter. Selecting a shared asset shows its impact in the Inspector, selecting a project shows its details, and
  New… creates a project.
- The desktop app, which supervises both processes, and a studio shell with Library, Viewport and Inspector panes.
- A docked viewport: open a glTF, FBX or OBJ, orbit and zoom it, lit by a built-in studio environment, with the file's
  metallic-roughness factors.
- The viewport draws textures: base colour, normal, roughness, metallic and occlusion from a glTF's materials, embedded or
  beside it, and from files. Base colour is read as sRGB and the rest as linear; a 16-bit PNG or an EXR keeps its depth.
- `PUT /api/v1/viewport/maps` lays pictures over a mesh by path, `PUT /api/v1/viewport/bake` shows a finished bake on its
  low mesh, and `PUT /api/v1/viewport/graph` lays a graph's Output nodes over the open mesh. `GET /api/v1/viewport`
  says what is laid on.
- The Bake panel shows a finished bake on the low mesh, and takes a skew map. The viewport shows the open graph's outputs.
- `patina bake --mesh-maps <folder>`, and `patina bake` follows the bake chosen beside a graph.
- `patina-bench render --bake=<folder> --yaw=<r> --pitch=<r>`, which replaces `bake-preview`.
- `GET /api/v1/viewport`, `PUT /api/v1/viewport/mesh`, `PUT /api/v1/viewport/camera`, frames from `/api/v1/viewport/frames`,
  and `/ws/viewport` for input.
- A killed Worker no longer costs the view: the Server keeps it, and the next Worker carries on within two seconds.
- `patina-worker --shader-cache <folder>`, and a disk cache of compiled shaders.
- Texture graphs over the API: open a graph file, read its nodes, edges and parameters, list node types for a palette, and
  save it back (`/api/v1/graphs`). A graph that opens is shared by every client that opens it.
- Undoable command batches with server-side undo and redo, and numbered delta events over `/ws/graphs/{id}` and
  `/api/v1/graphs/{id}/events`, so a second client sees the first client's edits.
- Graph evaluation in the Worker: a slider edit does not recompile, only what depends on it runs, and a killed Worker loses
  nothing. Each node's preview is a PNG at `/api/v1/previews/{hash}`, and events carry the hash.
- The graph editor: select a graph in the Library's Shared Assets, choose Open, and edit it in the Graph pane. Search for a
  node and add it, wire, move, delete, undo, redo and save, with a thumbnail on every node that follows each edit and a second
  client's edits arriving without a reload. The Inspector sets a selected node's numbers, colours, text and choices, and
  the Texture pane shows its picture. The pane says when no graph is open, when the Worker or the Server has gone, what the
  compiler reported, and what is unsaved.
- New graphs: `POST /api/v1/library/graphs` writes an empty graph into `shared/graphs` or a project, the scan indexes it,
  and New in the Graph pane creates and opens one.
- Graph settings in the Inspector: width, height and seed.
- Exposed parameters: add, edit and remove them, with live defaults, and drive a number from one with an expression.
- Groups and notes: commands, state, deltas, frames and cards on the canvas. Dropping a node on a frame moves it there.
- Compounds: double click one or choose Open compound to go inside, a breadcrumb back, and saving one updates the graphs
  holding it. Those that ship with Patina open to be read.
- Compound authoring: `addPort`, `setPort` and `removePort` commands, an Interface list in the graph settings, Compound in
  the Graph pane to save a selection into `Compounds` as one undo (`POST /api/v1/graphs/{id}/compounds`), and Copy to edit
  on a shipped compound (`POST /api/v1/library/compounds`).
- A bit depth control in the graph settings, and a `bitDepth` setting a command can reach.
- Bitmaps evaluate: PNG, JPEG, BMP, TGA, PSD and GIF, by a path relative to the graph or the library.
- A curve editor and a gradient ramp for the Curve and Ramp settings, held inline in the graph's file.
- The Texture pane has a histogram, counted from the reduction and labelled so, a channel view, and Full size, from
  `POST /api/v1/graphs/{id}/images`.
- Quitting with unsaved edits asks first, and multi-selection has an Inspector state of its own.
- High-to-low baking: `POST /api/v1/jobs/bakes` takes paths to a high and a low mesh and returns paths to the maps: normal,
  height, ambient occlusion, bent normal, curvature, thickness, position, world normal and ID, as 8- or 16-bit PNG or 16- or
  32-bit EXR. Hard edges do not seam, a cage decides what rays reach, skew and a skew map straighten detail near hard edges,
  and parts match by name. The baker uses every core but two and gives the same bytes on any number of them.
- A persistent job queue: a bake survives a killed Worker and a killed Server and carries on, and stops being retried
  after three Workers die on it. Follow jobs over `/ws/jobs` or `GET /api/v1/jobs/events`; `POST /api/v1/jobs/{id}/cancel`
  stops one between rows of the atlas. The same bake twice comes from the cache, even with its meshes renamed.
- A long bake holds up neither the viewport nor graph evaluation.
- `patina bake-maps`, the same bake in-process for a build farm, which can share a cache folder with others.
- The Bake panel: choose meshes, cage, size, format and maps, watch progress, and show results in the Texture pane.
- Source/Mesh Map nodes read a finished bake once a graph is pointed at it, with Use in the Bake panel or
  `PUT /api/v1/graphs/{id}/mesh-maps`. The choice is kept in a `.bake` file beside the graph.
- `patina-bench bake-pair`, a generated hard-edged high and low pair; `patina-bench s9`, and `patina-bench bake-preview`,
  which shades a low mesh with its maps on the CPU.

### Changed

- The app has its new icon, on the window, the installers and the studio's favicon.
- A graph's compounds now come from a `Compounds` folder beside it, not from every graph in its folder.
- Groups and notes have an `id`, in the file and the API, and commands name them by it instead of `index`, so two clients
  can no longer hit the wrong one.
- `patina bake` accepts everything the Worker evaluates: bitmaps, inline ramps and curves, and a compound on its own, with
  `--library <path>` where the graph is outside its library. The bytes match the Worker's, and `vixen texture bake`'s.
- A compound with an image input shows its nodes when opened on its own, with a noise standing in for what would feed it.
- The minimap leaves a small canvas and steps aside while a wire is being dragged, because it took the drop on a port under
  it.
- A graph that does not compile makes `patina bake` say so rather than crash.
- Undo, Redo and Save work from the Inspector.
- The evaluation readout says Evaluating…, Up to date or Not evaluated, and no longer shows an old count after an edit.
- A slider on a compound, or on an exposed parameter, no longer recompiles the graph.
- Graphs can compute in 32-bit float: `bitDepth: 32` in a graph's settings, for height and position. PNG previews and
  bakes are still eight bits.
- `GET /api/v1/graphs/{id}/node-types` gives an input port's `range`, with `minimum`, `maximum`, `softMinimum` and
  `softMaximum`, where the node defines them, and the Inspector's sliders use them.
