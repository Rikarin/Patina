# ADR 0029: The Server follows a graph in the viewport, and a second graph has a store of its own

**Status:** accepted, 2026-11-12, for issues #244 and #40.

## The viewport follows a graph on the Server (#244)

Since wave 7 the viewport showed a graph's outputs, or one node alone, only while the editor asked again after each
evaluation and each change of selection: the editor decided what to show and sent `PUT /viewport/graph` each time. The
Server reported what it showed, and a client like Blender would have had to rebuild the following itself (plan §8).

Now the Server does it:

- **A graph session holds one selection.** `PUT /api/v1/graphs/{id}/selection` takes the chosen nodes and a pinned
  node, keeps those the document holds, and tells every client with a `selected` event; `GraphState.selection` carries
  it. It is no edit: the revision and the undo history do not move. A session has one selection, whichever client made
  it last. The editor reports what the user chooses and pins, never what it starts with, so opening a graph does not
  take away another client's choice; it does not adopt another client's choice on its canvas.
- **The viewport follows.** `PUT /api/v1/viewport/follow` with a graph keeps its Output nodes on the mesh, or with
  `node` the node its selection views (the pinned one, else the first chosen), shown again by the Server after each
  evaluation and each change of what the selection views. A node with no picture, or none chosen, shows the outputs.
  `ViewportState.following` says what is followed. The follower (`Viewport/GraphFollower.cs`) reads the session's
  events as any client would, and lays a picture only while the following it was made for stands, so a client that
  lays something else meanwhile keeps it.
- **What stops it.** Pictures laid by hand (`PUT /viewport/maps`), a picture asked for now (`PUT /viewport/graph`), a
  bake shown, a stack worn and a cleared viewport stop the following; a graph that closes is let go. Opening a mesh
  keeps it, and the graph is laid on the new mesh.
- **Clients learn by the frames.** The frame header's last four bytes (offset 28) count changes to what lies on the
  mesh. A client that sees the count move reads `GET /viewport` again, which is how the editor's label and view list
  learn that the Server showed another node. ADR 0003's transport is unchanged.

The editor's Show and Node buttons now ask for this and show what the Server says it follows; nothing in the editor
re-sends a graph. `PUT /viewport/graph` stays for a picture asked for once.

## Two graphs side by side (#40)

The editor had one `GraphStore`, a root service that every graph pane and the panes around them injected. A second graph
needs a second store, so:

- **A store per graph view.** The Second Graph pane (`graph/graph-beside.ts`, in the graph workspace's fourth layout,
  Two Graphs) provides a `GraphStore` of its own and shows the canvas inside it, which injects that store. It joins the
  session of one of the open graph tabs and never opens or closes one, so the tab stays what owns the session and a
  tab that closes takes it from the pane too. Going into a compound inside it keeps its own way back.
- **The graph touched last is in front.** `GraphViews.front` is the store of the canvas touched last (a pointer or focus
  in it). The panes that follow a graph, the Inspector, History, the 2D View and Nodes, sit under `FrontGraph`, a
  directive that gives them the front store, inside an `@for` over the front: they are made again with the other store
  when the other canvas is touched. That keeps every one of them as it was, injecting `GraphStore`, rather than teaching
  each of them about two graphs. Undo, Redo, Save and the viewport's Show follow the front too, and each canvas answers
  its keys only while it is in front.
- **Edits behind are kept.** A tab behind is told when its graph was edited in the second pane (`noteDirty`), so closing
  it or quitting asks about them. Copy and Paste share one clipboard, so nodes copied in one graph paste into the other.

## Rejected

- **One store holding several documents.** Every consumer would have had to say which document it meant; the store's
  1,600 lines are about one session, and that is what a second view needs.
- **Following in the editor with a Server event per evaluation.** It is what was there: any second client would have
  had to do it again.
- **Selection per client.** A viewport follows one selection, and a client that wants its own view can pin a node.
