import type { XuiDockContentPane, XuiDockManagerLayout, XuiDockPane } from '@xui/dock-manager';

/** What the app is doing, which decides what is on screen (ADR 0012). */
export type WorkspaceName = 'start' | 'texturing' | 'layered-material' | 'image-material' | 'graph';

const Version = 4;

type Part = XuiDockPane;

const fixed = { allowClose: false } as const;

const pane = (contentId: string, header: string, size = 1): XuiDockContentPane => ({
  ...fixed,
  type: 'contentPane',
  contentId,
  header,
  size
});

const across = (size: number, ...panes: Part[]): Part => ({
  type: 'splitPane',
  orientation: 'horizontal',
  size,
  panes
});

const down = (size: number, ...panes: Part[]): Part => ({ type: 'splitPane', orientation: 'vertical', size, panes });

const tabs = (size: number, ...panes: XuiDockContentPane[]): Part => ({ type: 'tabGroupPane', size, panes });

const root = (...panes: Part[]): XuiDockManagerLayout => ({
  rootPane: { type: 'splitPane', orientation: 'horizontal', panes }
});

const library = pane('library', 'Library');
const inspector = pane('inspector', 'Inspector');
const layers = pane('layers', 'Layers');
const properties = pane('paint', 'Properties');
/** What is on the stack over what the chosen layer is, as a painting tool has them. */
const stack = down(1.4, layers, tabs(1.2, properties, inspector));
const shelf = pane('shelf', 'Materials');
/** Where a document's materials come from: the Library, and what it can be filled with. */
const sources = down(1, library, shelf);

const Layouts: Readonly<Record<WorkspaceName, XuiDockManagerLayout>> = {
  // Nothing is open, so nothing is shown that would have nothing in it.
  start: root(library, pane('start', 'Start', 3), inspector),
  // The mesh is the work, and the map beside it is the same work flat.
  texturing: root(
    sources,
    across(4, pane('viewport', 'Viewport', 3), tabs(2, pane('atlas', '2D View'), pane('texture', 'Maps'))),
    stack
  ),
  // There is no mesh, so the map leads and a shape only previews it.
  'layered-material': root(sources, across(4, pane('atlas', '2D View', 3), pane('viewport', 'Preview', 2)), stack),
  'image-material': root(library, pane('image', 'Maps', 1), pane('texture', 'Result', 3), inspector),
  // The canvas is the work; pictures of it are at the side.
  graph: root(
    library,
    pane('graph', 'Graph', 4),
    down(1.6, tabs(2, pane('texture', '2D View'), pane('viewport', 'Preview')), { ...inspector, size: 3 })
  )
};

const key = (name: WorkspaceName) => `patina.workspace.${Version}.${name}`;

/** The layout a workspace was left in, or the one it starts with. */
export function layoutOf(name: WorkspaceName): XuiDockManagerLayout {
  try {
    const saved = globalThis.localStorage.getItem(key(name));
    return saved ? (JSON.parse(saved) as XuiDockManagerLayout) : Layouts[name];
  } catch {
    return Layouts[name];
  }
}

export function keepLayout(name: WorkspaceName, layout: XuiDockManagerLayout): void {
  globalThis.localStorage.setItem(key(name), JSON.stringify(layout));
}

/** Forgets what the user did to a workspace. */
export function resetLayout(name: WorkspaceName): XuiDockManagerLayout {
  globalThis.localStorage.removeItem(key(name));
  return Layouts[name];
}

/** The layout with a pane in front of its tab group. */
export function showing(layout: XuiDockManagerLayout, contentId: string): XuiDockManagerLayout {
  const reveal = <T extends XuiDockPane>(part: T): T => {
    if (part.type === 'tabGroupPane') {
      const index = part.panes.findIndex(held => held.contentId === contentId);
      return index < 0 ? part : { ...part, selectedIndex: index };
    }

    return part.type === 'splitPane' ? { ...part, panes: part.panes.map(reveal) } : part;
  };
  return { ...layout, rootPane: reveal(layout.rootPane) };
}
