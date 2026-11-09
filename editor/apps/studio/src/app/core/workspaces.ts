import type { XuiDockContentPane, XuiDockManagerLayout, XuiDockPane } from '@xui/dock-manager';

/** What the app is doing, which decides what is on screen (ADR 0012). */
export type WorkspaceName = 'start' | 'texturing' | 'layered-material' | 'image-material' | 'graph';

const Version = 8;

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
const history = pane('history', 'History');
/** What is on the stack over what the chosen layer is, as a painting tool has them, and its undo steps behind. */
const stack = down(1.4, layers, tabs(1.2, properties, inspector, history));
/** The same with the model's texture sets over it, which only a mesh has. */
const texturingStack = down(
  1.4,
  pane('sets', 'Texture Sets', 0.28),
  { ...layers, size: 1 },
  tabs(1, properties, inspector, history)
);
const shelf = pane('shelf', 'Materials');
/** Where a document's materials come from: the Library, and what it can be filled with. */
const sources = down(1, library, shelf);
const graphSources = tabs(1, library, pane('nodes', 'Nodes'));
const graphInspector = tabs(3, inspector, history);

/** A layout a workspace can be in, by the name the Window menu gives it. */
export interface NamedLayout {
  readonly name: string;
  readonly layout: XuiDockManagerLayout;
}

const Starting: NamedLayout = { name: 'Start', layout: root(library, pane('start', 'Start', 3), inspector) };

/**
 * Each workspace's layouts, the first being the one it opens in. Window > Layout and Cmd or Ctrl+1 to 3 choose among them, as a
 * painting tool switches between its 3D and 2D views.
 */
const Layouts: Readonly<Record<WorkspaceName, readonly NamedLayout[]>> = {
  // Nothing is open, so nothing is shown that would have nothing in it.
  start: [Starting],
  // The mesh is the work, and the map beside it is the same work flat.
  texturing: [
    {
      name: '3D and 2D',
      layout: root(
        sources,
        across(4, pane('viewport', 'Viewport', 3), tabs(2, pane('atlas', '2D View'), pane('texture', 'Maps'))),
        texturingStack
      )
    },
    { name: '3D Only', layout: root(sources, pane('viewport', 'Viewport', 4), texturingStack) },
    {
      name: '2D Only',
      layout: root(sources, across(4, pane('atlas', '2D View', 3), pane('texture', 'Maps', 2)), texturingStack)
    }
  ],
  // There is no mesh, so the map leads and a shape only previews it.
  // A baked map chosen in the Bake dialog shows in Maps, a tab beside the 2D View.
  'layered-material': [
    {
      name: '2D and Preview',
      layout: root(
        sources,
        across(4, tabs(3, pane('atlas', '2D View'), pane('texture', 'Maps')), pane('viewport', 'Preview', 2)),
        stack
      )
    },
    { name: '2D Only', layout: root(sources, tabs(4, pane('atlas', '2D View'), pane('texture', 'Maps')), stack) },
    { name: 'Preview Only', layout: root(sources, tabs(4, pane('viewport', 'Preview'), pane('texture', 'Maps')), stack) }
  ],
  'image-material': [
    {
      name: 'Maps',
      layout: root(library, pane('image', 'Maps', 1), pane('texture', 'Result', 3), tabs(1, inspector, history))
    }
  ],
  // The canvas is the work; pictures of it are at the side.
  graph: [
    {
      name: 'Graph and Previews',
      layout: root(
        graphSources,
        pane('graph', 'Graph', 4),
        down(1.6, tabs(2, pane('texture', '2D View'), pane('viewport', 'Preview')), graphInspector)
      )
    },
    { name: 'Graph Only', layout: root(graphSources, pane('graph', 'Graph', 5), down(1.6, graphInspector)) },
    {
      name: 'Large Previews',
      layout: root(
        graphSources,
        pane('graph', 'Graph', 2),
        down(3, across(1, pane('texture', '2D View'), pane('viewport', 'Preview')), graphInspector)
      )
    }
  ]
};

const key = (name: WorkspaceName, choice: number) => `patina.workspace.${Version}.${name}.${choice}`;
const choiceKey = (name: WorkspaceName) => `patina.workspace.${Version}.${name}.chosen`;

/** The names of a workspace's layouts, in the order Cmd or Ctrl+1 to 3 choose them. */
export function layoutNames(name: WorkspaceName): readonly string[] {
  return Layouts[name].map(held => held.name);
}

/** Which of its layouts a workspace was left in. */
export function chosenLayout(name: WorkspaceName): number {
  try {
    const chosen = Number(globalThis.localStorage.getItem(choiceKey(name)) ?? 0);
    return Number.isInteger(chosen) && chosen >= 0 && chosen < Layouts[name].length ? chosen : 0;
  } catch {
    return 0;
  }
}

/** Remembers which layout a workspace is in. */
export function chooseLayout(name: WorkspaceName, choice: number): void {
  globalThis.localStorage.setItem(choiceKey(name), String(choice));
}

function start(name: WorkspaceName, choice: number): XuiDockManagerLayout {
  return (Layouts[name][choice] ?? Layouts[name][0] ?? Starting).layout;
}

/** A layout as it was left, or as it starts. */
export function layoutOf(name: WorkspaceName, choice = chosenLayout(name)): XuiDockManagerLayout {
  try {
    const saved = globalThis.localStorage.getItem(key(name, choice));
    return saved ? (JSON.parse(saved) as XuiDockManagerLayout) : start(name, choice);
  } catch {
    return start(name, choice);
  }
}

export function keepLayout(name: WorkspaceName, layout: XuiDockManagerLayout, choice = chosenLayout(name)): void {
  globalThis.localStorage.setItem(key(name, choice), JSON.stringify(layout));
}

/** Forgets what the user did to a layout. */
export function resetLayout(name: WorkspaceName, choice = chosenLayout(name)): XuiDockManagerLayout {
  globalThis.localStorage.removeItem(key(name, choice));
  return start(name, choice);
}

/** Whether a layout has a pane anywhere in it. */
export function holds(layout: XuiDockManagerLayout, contentId: string): boolean {
  const find = (part: XuiDockPane): boolean =>
    part.type === 'contentPane'
      ? part.contentId === contentId
      : 'panes' in part && (part.panes as readonly XuiDockPane[]).some(find);
  return find(layout.rootPane);
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
