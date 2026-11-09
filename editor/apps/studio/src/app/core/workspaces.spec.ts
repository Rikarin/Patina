import { holds, keepLayout, layoutNames, layoutOf, type WorkspaceName } from './workspaces';

describe('workspaces', () => {
  let held: Map<string, string>;

  beforeEach(() => {
    held = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => held.get(key) ?? null,
      setItem: (key: string, value: string) => held.set(key, value),
      removeItem: (key: string) => held.delete(key)
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  const every = (name: WorkspaceName) => layoutNames(name).map((_, choice) => layoutOf(name, choice));

  it('has the Checks tab wherever a texture set is in front', () => {
    for (const name of ['texturing', 'layered-material'] as const) {
      for (const layout of every(name)) {
        expect(holds(layout, 'checks')).toBe(true);
      }
    }
  });

  it('has the Maps tab in every layout of a layered material, beside the Checks tab', () => {
    for (const layout of every('layered-material')) {
      expect(holds(layout, 'texture')).toBe(true);
      expect(holds(layout, 'checks')).toBe(true);
    }
  });

  it('keeps a layout under its version, and starts afresh from one a build without both tabs kept', () => {
    // A layout kept by a build that had the Checks tab and not Maps, or Maps and not Checks, was kept as version 8.
    held.set('patina.workspace.8.layered-material.0', JSON.stringify({ rootPane: { type: 'splitPane', panes: [] } }));
    expect(holds(layoutOf('layered-material', 0), 'texture')).toBe(true);
    expect(holds(layoutOf('layered-material', 0), 'checks')).toBe(true);

    keepLayout('layered-material', layoutOf('layered-material', 0), 0);
    expect(held.has('patina.workspace.9.layered-material.0')).toBe(true);
  });
});
