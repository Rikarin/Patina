import type { XuiDockPane } from '@xui/dock-manager';
import { layoutNames, layoutOf, type WorkspaceName } from './workspaces';

/**
 * Each pane's name in each workspace, as docs/02-glossary.md gives it under Panes (#196). A pane id is the code's; a
 * person reads only the name, which may differ between workspaces where the pane shows a different thing.
 */
const Names: Readonly<Record<WorkspaceName, Readonly<Record<string, string>>>> = {
  start: { start: 'Start', library: 'Library', inspector: 'Inspector' },
  texturing: {
    library: 'Library',
    shelf: 'Materials',
    sets: 'Texture Sets',
    layers: 'Layers',
    paint: 'Properties',
    inspector: 'Inspector',
    history: 'History',
    checks: 'Checks',
    viewport: 'Viewport',
    atlas: '2D View',
    texture: 'Maps'
  },
  'layered-material': {
    library: 'Library',
    shelf: 'Materials',
    layers: 'Layers',
    paint: 'Properties',
    inspector: 'Inspector',
    history: 'History',
    checks: 'Checks',
    viewport: 'Preview',
    atlas: '2D View',
    texture: 'Maps'
  },
  'image-material': {
    library: 'Library',
    image: 'Maps',
    texture: 'Result',
    inspector: 'Inspector',
    history: 'History'
  },
  graph: {
    library: 'Library',
    nodes: 'Nodes',
    graph: 'Graph',
    beside: 'Second Graph',
    texture: '2D View',
    viewport: 'Preview',
    inspector: 'Inspector',
    history: 'History'
  }
};

/** Every pane of a layout, with the name its tab shows. */
function panesOf(part: XuiDockPane): { id: string; name: string }[] {
  if (part.type === 'contentPane') {
    return [{ id: part.contentId, name: part.header ?? '' }];
  }

  return 'panes' in part ? (part.panes as readonly XuiDockPane[]).flatMap(panesOf) : [];
}

describe('pane names (#196)', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', { getItem: () => null, setItem: () => undefined, removeItem: () => undefined });
  });

  afterEach(() => vi.unstubAllGlobals());

  it('names each pane as the glossary does for its workspace', () => {
    const found = (Object.keys(Names) as WorkspaceName[]).flatMap(workspace =>
      layoutNames(workspace).flatMap((layout, choice) =>
        panesOf(layoutOf(workspace, choice).rootPane)
          .filter(({ id, name }) => Names[workspace][id] !== name)
          .map(({ id, name }) => `${workspace} › ${layout}: ${id} is “${name}”`)
      )
    );

    expect(found).toEqual([]);
  });

  it('gives no two panes of one workspace the same name', () => {
    for (const names of Object.values(Names)) {
      const all = Object.values(names);
      expect(new Set(all).size).toBe(all.length);
    }
  });
});
