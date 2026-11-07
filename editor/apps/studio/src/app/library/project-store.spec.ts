import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { BakeStore } from '../bake/bake-store';
import { SystemStatus } from '../core/system-status';
import { GraphStore } from '../graph/graph-store';
import { PaintStore } from '../paint/paint-store';
import { LibraryStore } from './library-store';
import { ProjectStore } from './project-store';

const paths = { texturing: 'projects/a/a.vxlayers', graph: 'projects/a/rust.vxtexgraph' };

const contents = {
  id: '1',
  name: 'A',
  path: 'a',
  meshes: [{ id: 'm', path: 'projects/a/low.glb', high: null }],
  documents: [
    { id: 'd', kind: 'texturing', name: 'A', path: paths.texturing, mesh: 'm' },
    { id: 'g', kind: 'graph', name: 'Rust', path: paths.graph, mesh: null }
  ],
  bakeFolder: 'projects/a/bakes',
  bakes: []
};

function memoryStorage(): Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> {
  const held = new Map<string, string>();
  return {
    getItem: key => held.get(key) ?? null,
    setItem: (key, value) => held.set(key, value),
    removeItem: key => held.delete(key)
  };
}

/** A ProjectStore over stores that only do what the tabs ask of them. */
function harness() {
  const paintState = signal<{ id: string; history: { dirty: boolean } } | null>(null);
  const graphDocument = signal<{ id: string; path: string; name: string } | null>(null);
  const graphDirty = signal(false);
  const paint = {
    state: paintState,
    useMeshMaps: vi.fn(() => Promise.resolve()),
    open: vi.fn((path: string) => {
      paintState.set({ id: `session:${path}`, history: { dirty: false } });
      return Promise.resolve();
    }),
    detach: vi.fn(() => {
      paintState.set(null);
    }),
    act: vi.fn(() => Promise.resolve())
  };
  const graph = {
    document: graphDocument,
    unsaved: graphDirty,
    session: signal({ kind: 'none' }),
    open: vi.fn((path: string) => {
      graphDocument.set({ id: `session:${path}`, path, name: 'Rust' });
      return Promise.resolve();
    }),
    detach: vi.fn(() => {
      graphDocument.set(null);
    }),
    save: vi.fn(() => Promise.resolve(true))
  };
  const removed: string[] = [];
  const client = {
    GET: vi.fn(() => Promise.resolve({ data: contents, response: new Response() })),
    POST: vi.fn(() => Promise.resolve({ data: { items: [] }, response: new Response() })),
    DELETE: vi.fn((route: string, options?: { params?: { path?: { id?: string } } }) => {
      removed.push(options?.params?.path?.id ?? route);
      return Promise.resolve({ data: {}, response: new Response() });
    })
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: PaintStore, useValue: paint },
      { provide: GraphStore, useValue: graph },
      { provide: BakeStore, useValue: { viewportChanged: signal(0), current: signal(null) } },
      { provide: SystemStatus, useValue: { state: signal({ kind: 'connecting' }) } },
      {
        provide: LibraryStore,
        useValue: {
          api: () => Promise.resolve(client),
          state: signal({ kind: 'ready', info: { id: 'lib', path: '/game.ptlib' } })
        }
      }
    ]
  });
  return { store: TestBed.inject(ProjectStore), paint, graph, paintState, graphDirty, removed };
}

describe('ProjectStore', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('stays open when Save All cannot save', async () => {
    const quit = vi.fn();
    vi.stubGlobal('patina', { quit, onQuitRequested: vi.fn(), setUnsaved: vi.fn() });
    const { store, graph, graphDirty } = harness();
    await store.openDocument('a', paths.graph);
    TestBed.tick();
    graphDirty.set(true);
    TestBed.tick();
    graph.save.mockRejectedValueOnce(new Error('The disk is full.'));

    await store.resolveQuitting('save');

    expect(quit).not.toHaveBeenCalled();
    expect(store.failure()).toBe('The disk is full.');
  });

  it('reopens on what was last in front', async () => {
    vi.stubGlobal('localStorage', memoryStorage());
    globalThis.localStorage.setItem('patina.library.last.lib', JSON.stringify({ project: 'a', path: paths.graph }));
    const { store } = harness();

    await vi.waitFor(() => {
      TestBed.tick();
      expect(store.active()?.path).toBe(paths.graph);
    });
  });

  it('remembers the document in front, and nothing once all are closed', async () => {
    vi.stubGlobal('localStorage', memoryStorage());
    const { store } = harness();
    TestBed.tick();

    await store.open('a');
    TestBed.tick();
    expect(globalThis.localStorage.getItem('patina.library.last.lib')).toContain(paths.texturing);

    await store.closeTab(paths.texturing);
    TestBed.tick();
    expect(globalThis.localStorage.getItem('patina.library.last.lib')).toBeNull();
  });

  it('opens a project at its texturing, in that workspace', async () => {
    const { store } = harness();

    await store.open('a');

    expect(store.active()?.path).toBe(paths.texturing);
    expect(store.workspace()).toBe('texturing');
    expect(store.meshName()).toBe('low.glb');
  });

  it('starts with nothing open', () => {
    expect(harness().store.workspace()).toBe('start');
  });

  it('keeps a document open behind another, and comes back without a second tab', async () => {
    const { store, paint } = harness();
    await store.openDocument('a', paths.texturing);
    await store.openDocument('a', paths.graph);

    expect(store.workspace()).toBe('graph');
    expect(store.tabs()).toHaveLength(2);

    await store.activate(paths.texturing);

    expect(store.tabs()).toHaveLength(2);
    expect(paint.open).toHaveBeenLastCalledWith(paths.texturing, false);
  });

  it('asks before closing a tab with unsaved edits', async () => {
    const { store, paintState, removed } = harness();
    await store.open('a');
    paintState.set({ id: 's', history: { dirty: true } });
    TestBed.tick();

    await store.closeTab(paths.texturing);

    expect(store.closing()).toBe(paths.texturing);
    expect(store.tabs()).toHaveLength(1);
    await store.resolveClosing('cancel');
    expect(store.tabs()).toHaveLength(1);
    expect(removed.filter(id => id.startsWith('session'))).toEqual([]);
  });

  it('remembers unsaved edits of a tab that is behind', async () => {
    const { store, paintState } = harness();
    await store.open('a');
    paintState.set({ id: 's', history: { dirty: true } });
    TestBed.tick();

    await store.openDocument('a', paths.graph);
    TestBed.tick();

    expect(store.unsavedTabs().map(tab => tab.path)).toEqual([paths.texturing]);
  });

  it('counts a graph with unsaved edits when it is in front', async () => {
    const { store, graphDirty } = harness();
    await store.openDocument('a', paths.graph);
    TestBed.tick();

    graphDirty.set(true);
    TestBed.tick();

    expect(store.unsavedTabs().map(tab => tab.path)).toEqual([paths.graph]);
  });

  it('refuses to release a project with unsaved edits, and closes one without', async () => {
    const { store, paintState } = harness();
    await store.open('a');
    paintState.set({ id: 's', history: { dirty: true } });
    TestBed.tick();

    expect(await store.release(['a'])).toBe(false);
    expect(store.tabs()).toHaveLength(1);
    expect(store.failure()).toContain('unsaved');

    paintState.set({ id: 's', history: { dirty: false } });
    TestBed.tick();

    expect(await store.release(['a'])).toBe(true);
    expect(store.tabs()).toHaveLength(0);
  });

  it('leaves what is not below the folder alone', async () => {
    const { store } = harness();
    await store.open('a');

    expect(await store.release(['ab'])).toBe(true);
    expect(store.tabs()).toHaveLength(1);
  });
});
