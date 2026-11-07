import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiError } from '@patina/api-client';
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

/** A fresh store for each test, since some Node versions have a real one that would carry from test to test. */
let storage = memoryStorage;

/** A ProjectStore over stores that only do what the tabs ask of them. */
function harness() {
  vi.stubGlobal('localStorage', storage());
  const paintState = signal<{ id: string; history: { dirty: boolean } } | null>(null);
  const graphDocument = signal<{ id: string; path: string; name: string } | null>(null);
  const graphDirty = signal(false);
  /** What the Server has open. */
  const sessions = new Set<string>();
  /** The autosave the Server holds for each document. */
  const autosaves = new Map<string, string>();
  const paint = {
    state: paintState,
    failure: signal<string | null>(null),
    useMeshMaps: vi.fn(() => Promise.resolve()),
    open: vi.fn((path: string) => {
      sessions.add(`session:${path}`);
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
      sessions.add(`session:${path}`);
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
    GET: vi.fn((route: string, options?: { params?: { query?: { path?: string } } }) =>
      Promise.resolve({
        data:
          route === '/api/v1/library/recovery'
            ? { saved: autosaves.get(options?.params?.query?.path ?? '') ?? null }
            : contents,
        response: new Response()
      })
    ),
    POST: vi.fn((route: string, options?: { body?: { path?: string; restore?: boolean } }) => {
      const path = options?.body?.path ?? '';
      if (route === '/api/v1/library/recovery') {
        if (sessions.has(`session:${path}`)) {
          return Promise.reject(new Error('Close the document first.'));
        }

        autosaves.delete(path);
      }

      return Promise.resolve({ data: { items: [] }, response: new Response() });
    }),
    DELETE: vi.fn(
      (route: string, options?: { params?: { path?: { id?: string }; query?: { discard?: boolean } } }) => {
        const id = options?.params?.path?.id ?? route;
        removed.push(id);
        sessions.delete(id);
        return Promise.resolve({ data: {}, response: new Response() });
      }
    )
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
  return { store: TestBed.inject(ProjectStore), paint, graph, paintState, graphDirty, removed, autosaves, client };
}

describe('ProjectStore', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    storage = memoryStorage;
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
    const held = memoryStorage();
    storage = () => held;
    held.setItem('patina.library.last.lib', JSON.stringify({ project: 'a', path: paths.graph }));
    const { store } = harness();

    await vi.waitFor(() => {
      TestBed.tick();
      expect(store.active()?.path).toBe(paths.graph);
    });
  });

  it('remembers the document in front, and nothing once all are closed', async () => {
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

  describe('work a crash left behind', () => {
    const saved = '2026-11-07T10:00:00Z';

    it('offers it when the document opens', async () => {
      const { store, autosaves } = harness();
      autosaves.set(paths.texturing, saved);

      await store.open('a');

      expect(store.recoverable()).toEqual({ path: paths.texturing, saved });
    });

    for (const [answer, restore] of [
      ['Restore', true],
      ['Discard', false]
    ] as const) {
      it(`${answer} closes the document for the Server, then opens it again`, async () => {
        const { store, autosaves, removed, paint, client } = harness();
        autosaves.set(paths.texturing, saved);
        await store.open('a');

        await store.resolveRecovery(restore);

        expect(store.failure()).toBeNull();
        expect(removed).toContain(`session:${paths.texturing}`);
        expect(client.POST).toHaveBeenCalledWith('/api/v1/library/recovery', {
          body: { path: paths.texturing, restore }
        });
        expect(autosaves.has(paths.texturing)).toBe(false);
        expect(store.recoverable()).toBeNull();
        expect(store.active()?.path).toBe(paths.texturing);
        expect(paint.open).toHaveBeenCalledTimes(2);
      });
    }

    it('keeps the autosave when the dialog is only dismissed, and offers it next time', async () => {
      const { store, autosaves, client } = harness();
      autosaves.set(paths.texturing, saved);
      await store.open('a');

      store.dismissRecovery();

      expect(store.recoverable()).toBeNull();
      expect(store.active()?.path).toBe(paths.texturing);
      expect(autosaves.has(paths.texturing)).toBe(true);
      expect(client.POST).not.toHaveBeenCalledWith('/api/v1/library/recovery', expect.anything());

      await store.open('a');
      expect(store.recoverable()).toEqual({ path: paths.texturing, saved });
    });

    it('leaves everything alone when the document will not close', async () => {
      const { store, autosaves, client } = harness();
      autosaves.set(paths.texturing, saved);
      await store.open('a');
      client.DELETE.mockRejectedValueOnce(new ApiError(409, 'This stack has unsaved edits.'));

      await store.resolveRecovery(false);

      expect(store.failure()).toBe('This stack has unsaved edits.');
      expect(autosaves.has(paths.texturing)).toBe(true);
      expect(store.tabs().map(tab => tab.path)).toEqual([paths.texturing]);
      expect(store.active()?.path).toBe(paths.texturing);
    });
  });

  it('says why settings did not apply in the dialog, and nowhere it could be read twice', async () => {
    const { store, client } = harness();
    await store.open('a');
    store.settingsOpen.set(true);
    TestBed.tick();
    (client as unknown as { PATCH: unknown }).PATCH = vi.fn(() => Promise.reject(new Error('No such mesh.')));

    expect(await store.applySettings({ mesh: '/nowhere.glb' })).toBe(false);

    expect(store.settingsFailure()).toBe('No such mesh.');
    expect(store.failure()).toBeNull();
  });

  it('says why settings did not apply from outside the dialog, after the document is back', async () => {
    const { store, client } = harness();
    await store.open('a');
    (client as unknown as { PATCH: unknown }).PATCH = vi.fn(() => Promise.reject(new Error('The Worker went away.')));

    expect(await store.applySettings({ autoUnwrap: true })).toBe(false);

    expect(store.failure()).toBe('The Worker went away.');
    expect(store.active()?.path).toBe(paths.texturing);
  });

  it('starts an action with nothing left over from the last one', async () => {
    const { store, paint } = harness();
    paint.failure.set('The stroke failed.');
    await store.importFiles(['/x.png']);
    expect(store.failure()).toBe('Open a project to bring files into it.');

    await store.open('a');

    expect(store.failure()).toBeNull();
    expect(paint.failure()).toBeNull();
  });

  it('leaves what is not below the folder alone', async () => {
    const { store } = harness();
    await store.open('a');

    expect(await store.release(['ab'])).toBe(true);
    expect(store.tabs()).toHaveLength(1);
  });
});
