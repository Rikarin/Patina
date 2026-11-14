import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiError } from '@patina/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { BakeStore } from '../bake/bake-store';
import { Commands } from '../core/commands';
import { Preferences } from '../core/preferences';
import { EngineRestarts, RestartedNotice } from '../core/session-return';
import { SystemStatus } from '../core/system-status';
import { GraphStore } from '../graph/graph-store';
import { BrushStore } from '../paint/brush-store';
import { PaintStore } from '../paint/paint-store';
import { LibraryStore } from './library-store';
import { ProjectCommands } from './project-commands';
import { inLibrary, ProjectStore, sameDocument } from './project-store';

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
  bakes: [],
  pictures: []
};

function memoryStorage(): Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> {
  const held = new Map<string, string>();
  return {
    getItem: key => held.get(key) ?? null,
    setItem: (key, value) => held.set(key, value),
    removeItem: key => held.delete(key)
  };
}

/** The desktop app's bridge as far as quitting needs it, with preferences that keep nothing. */
function quitting(quit: () => void) {
  return {
    quit,
    onQuitRequested: vi.fn(),
    setUnsaved: vi.fn(),
    readPreferences: () => Promise.resolve(null),
    writePreferences: () => Promise.resolve()
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
    reread: vi.fn(() => Promise.resolve()),
    open: vi.fn((path: string) => {
      sessions.add(`session:${path}`);
      paintState.set({ id: `session:${path}`, history: { dirty: false } });
      return Promise.resolve();
    }),
    detach: vi.fn(() => {
      paintState.set(null);
    }),
    act: vi.fn(() => Promise.resolve(true)),
    notice: signal<string | null>(null),
    sessionOpened: vi.fn((path: string) => Promise.resolve<string | null>(`session:${path}`))
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
    save: vi.fn(() => Promise.resolve(true)),
    restarted: vi.fn(),
    sessionOpened: vi.fn((path: string) => Promise.resolve<string | null>(`session:${path}`))
  };
  const removed: string[] = [];
  const client = {
    GET: vi.fn((route: string, options?: { params?: { query?: { path?: string } } }) =>
      Promise.resolve({
        data:
          route === '/api/v1/library/recovery'
            ? { saved: autosaves.get(options?.params?.query?.path ?? '') ?? null }
            : route === '/api/v1/stacks' || route === '/api/v1/graphs'
              ? [...sessions].map(id => ({ id, path: '', name: '', revision: 1, dirty: true }))
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
    DELETE: vi.fn((route: string, options?: { params?: { path?: { id?: string }; query?: { discard?: boolean } } }) => {
      const id = options?.params?.path?.id ?? route;
      removed.push(id);
      sessions.delete(id);
      return Promise.resolve({ data: {}, response: new Response() });
    })
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: PaintStore, useValue: paint },
      { provide: BrushStore, useValue: { adopt: () => Promise.resolve(), useFile: () => Promise.resolve() } },
      { provide: GraphStore, useValue: graph },
      { provide: BakeStore, useValue: { viewportChanged: signal(0), current: signal(null), batch: signal([]) } },
      { provide: SystemStatus, useValue: { state: signal({ kind: 'connecting' }), pictures: signal([]) } },
      {
        provide: LibraryStore,
        useValue: {
          api: () => Promise.resolve(client),
          refreshSoon: vi.fn(),
          state: signal({ kind: 'ready', info: { id: 'lib', path: '/game.ptlib' } })
        }
      }
    ]
  });
  return {
    store: TestBed.inject(ProjectStore),
    paint,
    graph,
    paintState,
    graphDirty,
    removed,
    autosaves,
    client,
    sessions
  };
}

describe('ProjectStore', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    storage = memoryStorage;
  });

  describe('a tab on its way to the front', () => {
    /** Opens a project with the viewport's DELETE held, which is the moment its tab shows and is not yet in front. */
    async function opening() {
      const test = harness();
      let release = (): void => undefined;
      test.client.DELETE.mockImplementationOnce(
        () =>
          new Promise(resolve => {
            release = () => resolve({ data: {}, response: new Response() });
          })
      );
      TestBed.inject(ProjectCommands).install();
      const commands = TestBed.inject(Commands);
      const opened = test.store.open('a');
      await vi.waitFor(() => expect(test.store.tabs()).toHaveLength(1));
      expect(test.store.current()).toBeNull(); // the viewport is still letting go of what was in front
      return { ...test, commands, opened, release: () => release() };
    }

    it('keeps the commands about it on, and they wait for it rather than act on nothing', async () => {
      const { store, paint, commands, opened, release } = await opening();
      expect(store.arriving()).toBe(paths.texturing);
      for (const id of ['projectSettings', 'save', 'saveAs', 'reloadMesh', 'closeDocument', 'closeProject']) {
        expect(commands.enabled(id), id).toBe(true);
      }

      commands.run('projectSettings', 'menu');
      commands.run('save', 'menu');
      await Promise.resolve();
      expect(store.settingsOpen()).toBe(false);
      expect(paint.act).not.toHaveBeenCalled();

      release();
      await opened;
      await vi.waitFor(() => expect(store.settingsOpen()).toBe(true));
      expect(paint.act).toHaveBeenCalledWith('save');
      expect(store.arriving()).toBeNull();
    });

    it('lets it arrive before closing it, so the session its open gets is the one closed (#232)', async () => {
      const { store, paint, sessions, removed, opened, release } = await opening();
      expect(paint.open).not.toHaveBeenCalled(); // its open has not even started

      const closing = store.closeTab(paths.texturing);
      release();
      await Promise.all([opened, closing]);

      expect(paint.open).toHaveBeenCalled();
      expect(removed).toContain(`session:${paths.texturing}`);
      expect(sessions.size).toBe(0);
      expect(store.tabs()).toHaveLength(0);
    });

    it('keeps a mesh opened while it comes forward, where letting go of the one before would wipe it (#237)', async () => {
      const { store, client, opened, release } = await opening();
      const order: string[] = [];
      client.DELETE.mockImplementation((route: string) => {
        order.push(`DELETE ${route}`);
        return Promise.resolve({ data: {}, response: new Response() });
      });
      const put = vi.fn((route: string, options: { body: { path: string } }) => {
        order.push(`PUT ${route}`);
        return Promise.resolve({ data: { mesh: options.body.path }, response: new Response() });
      });
      Object.assign(client, { PUT: put });

      const mesh = store.openMesh('/meshes/crate.glb');
      await new Promise(resolve => setTimeout(resolve, 0));
      expect(put).not.toHaveBeenCalled(); // the viewport is still letting go of what was in front

      release();
      await opened;
      expect(await mesh).toEqual({ mesh: '/meshes/crate.glb' });
      expect(order.at(-1)).toBe('PUT /api/v1/viewport/mesh');
      expect(store.current()).not.toBeNull();
    });

    it('turns them off with nothing open or on its way', () => {
      harness();
      TestBed.inject(ProjectCommands).install();
      const commands = TestBed.inject(Commands);
      for (const id of [
        'projectSettings',
        'save',
        'saveAs',
        'saveAll',
        'reloadMesh',
        'closeDocument',
        'closeProject'
      ]) {
        expect(commands.enabled(id), id).toBe(false);
      }
    });
  });

  it('closes every document of a project, asking once when one is unsaved', async () => {
    const { store, paintState, graphDirty } = harness();
    await store.openDocument('a', paths.graph);
    await store.openDocument('a', paths.texturing);
    expect(store.tabs()).toHaveLength(2);
    graphDirty.set(true);
    paintState.set({ id: `session:${paths.texturing}`, history: { dirty: true } });
    TestBed.tick();

    await store.closeProject();
    expect(store.closingProject()).toBe('a');
    expect(store.tabs()).toHaveLength(2);

    await store.resolveClosingProject('discard');
    expect(store.tabs()).toHaveLength(0);
    expect(store.active()).toBeNull();
  });

  it('stays open when Save All cannot save', async () => {
    const quit = vi.fn();
    vi.stubGlobal('patina', quitting(quit));
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

  it('stays open when a save only says it did not happen', async () => {
    const quit = vi.fn();
    vi.stubGlobal('patina', quitting(quit));
    const { store, paint, paintState } = harness();
    await store.open('a');
    paintState.set({ id: 's', history: { dirty: true } });
    TestBed.tick();
    paint.act.mockResolvedValueOnce(false);

    await store.resolveQuitting('save');

    expect(quit).not.toHaveBeenCalled();
    expect(store.failure()).toContain('stayed open');
  });

  it('quits once everything is saved, whether or not the tabs have heard yet', async () => {
    const quit = vi.fn();
    vi.stubGlobal('patina', quitting(quit));
    const { store, paintState } = harness();
    await store.open('a');
    paintState.set({ id: 's', history: { dirty: true } });
    TestBed.tick();

    await store.resolveQuitting('save');

    expect(quit).toHaveBeenCalled();
  });

  it('keeps the tab when Save in the close dialog does not save', async () => {
    const { store, paint, paintState, removed } = harness();
    await store.open('a');
    paintState.set({ id: 's', history: { dirty: true } });
    TestBed.tick();
    await store.closeTab(paths.texturing);
    paint.act.mockResolvedValueOnce(false);

    await store.resolveClosing('save');

    expect(store.tabs()).toHaveLength(1);
    expect(removed.filter(id => id.startsWith('session'))).toEqual([]);
  });

  it('copies nothing for Save As when the save did not happen', async () => {
    const { store, paint, client } = harness();
    await store.open('a');
    paint.act.mockResolvedValueOnce(false);

    await store.saveAs('Copy');

    expect(client.POST).not.toHaveBeenCalledWith('/api/v1/library/project/document/copy', expect.anything());
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

  it('keeps each document brought to the front as recent, in its library', async () => {
    const { store } = harness();
    const preferences = TestBed.inject(Preferences);
    TestBed.tick();

    await store.open('a');
    TestBed.tick();
    await store.openDocument('a', paths.graph);
    TestBed.tick();

    expect(preferences.recentDocuments()).toEqual([
      { library: 'lib', project: 'a', path: paths.graph, name: 'Rust' },
      { library: 'lib', project: 'a', path: paths.texturing, name: 'A' }
    ]);
  });

  describe('a recent document', () => {
    it('opens where it is', async () => {
      const { store } = harness();
      await store.openRecent({ library: 'lib', project: 'a', path: paths.graph, name: 'Rust' });

      expect(store.active()?.path).toBe(paths.graph);
    });

    it('is forgotten and said so when its project no longer has it, and nothing else opens', async () => {
      const { store } = harness();
      const preferences = TestBed.inject(Preferences);
      const gone = { library: 'lib', project: 'a', path: 'projects/a/gone.vxtexgraph', name: 'Gone' };
      preferences.usedDocument(gone);

      await store.openRecent(gone);

      expect(store.active()).toBeNull();
      expect(preferences.recentDocuments()).toEqual([]);
      expect(store.failure()).toBe('Gone is no longer in a, so Start no longer offers it.');
    });

    it('is forgotten when its project has gone, and kept when the engine cannot answer', async () => {
      const { store, client } = harness();
      const preferences = TestBed.inject(Preferences);
      const rust = { library: 'lib', project: 'a', path: paths.graph, name: 'Rust' };
      preferences.usedDocument(rust);

      client.GET.mockRejectedValueOnce(new Error('The engine is not running.'));
      await store.openRecent(rust);
      expect(preferences.recentDocuments()).toEqual([rust]);
      expect(store.failure()).toBe('The engine is not running.');

      client.GET.mockRejectedValueOnce(new ApiError(404, 'No such project.'));
      await store.openRecent(rust);
      expect(preferences.recentDocuments()).toEqual([]);
      expect(store.active()).toBeNull();
    });
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

  it("sends Don't Save to the Server for a stack, or it would stay open with the edits", async () => {
    const { store, paintState, client } = harness();
    await store.open('a');
    paintState.set({ id: `session:${paths.texturing}`, history: { dirty: true } });
    TestBed.tick();
    await store.closeTab(paths.texturing);

    await store.resolveClosing('discard');

    expect(client.DELETE).toHaveBeenCalledWith('/api/v1/stacks/{id}', {
      params: { path: { id: `session:${paths.texturing}` }, query: { discard: true } }
    });
    expect(store.tabs()).toHaveLength(0);
  });

  it("sends Don't Save to the Server for a graph", async () => {
    const { store, graphDirty, client } = harness();
    await store.openDocument('a', paths.graph);
    TestBed.tick();
    graphDirty.set(true);
    TestBed.tick();
    await store.closeTab(paths.graph);

    await store.resolveClosing('discard');

    expect(client.DELETE).toHaveBeenCalledWith('/api/v1/graphs/{id}', {
      params: { path: { id: `session:${paths.graph}` }, query: { discard: true } }
    });
  });

  for (const kind of ['graph', 'texturing'] as const) {
    it(`ends the session of a ${kind} tab closed while its document was still opening (#232)`, async () => {
      const test = harness();
      const store = kind === 'graph' ? test.graph : test.paint;
      let answer = (): void => undefined;
      const answered = new Promise<void>(done => {
        answer = done;
      });
      // Let go of before the Server answered, so the store shows nothing and the tab learns no session.
      store.open.mockImplementationOnce((path: string) =>
        answered.then(() => {
          test.sessions.add(`session:${path}`);
        })
      );
      store.sessionOpened.mockImplementation((path: string) => answered.then(() => `session:${path}`));

      const opening = test.store.openDocument('a', paths[kind]);
      await vi.waitFor(() => expect(store.open).toHaveBeenCalled());
      const closing = test.store.closeTab(paths[kind]);
      answer();
      await Promise.all([opening, closing]);

      expect(test.removed).toContain(`session:${paths[kind]}`);
      expect(test.sessions.has(`session:${paths[kind]}`)).toBe(false);
      expect(test.store.tabs()).toHaveLength(0);
    });
  }

  it('brings a file’s materials into the open project and says what was left out', async () => {
    const { store, client } = harness();
    await store.open('a');
    client.POST.mockImplementationOnce(
      () =>
        Promise.resolve({
          data: { documents: ['projects/a/Brick.vxtexgraph'], warnings: ['USD: opacity is left out.'] },
          response: new Response()
        }) as never
    );

    await store.importMaterials('/art/brick.usda');

    expect(client.POST).toHaveBeenCalledWith('/api/v1/library/project/materials', {
      body: { project: 'a', file: '/art/brick.usda' }
    });
    expect(store.notice()).toBe('Brought 1 material from brick.usda into A. USD: opacity is left out.');
    expect(store.failure()).toBeNull();
  });

  it('clears what went wrong before when a tab closes or a graph saves', async () => {
    const { store } = harness();
    await store.importFiles(['/x.png']);
    expect(store.failure()).not.toBeNull();
    await store.closeTab('nothing open');
    expect(store.failure()).toBeNull();

    await store.importFiles(['/x.png']);
    await store.saveActive();
    expect(store.failure()).toBeNull();
  });

  it('keeps the tab of a session that would not close, so its edits stay in reach', async () => {
    const { store, client } = harness();
    await store.open('a');
    client.DELETE.mockRejectedValueOnce(new ApiError(409, 'This stack has unsaved edits.'));

    await store.closeTab(paths.texturing);

    expect(store.failure()).toBe('This stack has unsaved edits.');
    expect(store.tabs().map(tab => tab.path)).toEqual([paths.texturing]);
    expect(store.active()?.path).toBe(paths.texturing);
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

  it('lets a tab behind forget the edits a Server that restarted lost with its session (#226)', async () => {
    const { store, paintState, sessions } = harness();
    await store.open('a');
    paintState.set({ id: `session:${paths.texturing}`, history: { dirty: true } });
    TestBed.tick();
    await store.openDocument('a', paths.graph);
    TestBed.tick();
    expect(store.unsavedTabs().map(tab => tab.path)).toEqual([paths.texturing]);

    // The graph in front found the Server restarted: it holds the graph's new session and not the stack's.
    sessions.delete(`session:${paths.texturing}`);
    TestBed.inject(EngineRestarts).count.update(count => count + 1);
    TestBed.tick();

    await vi.waitFor(() => expect(store.unsavedTabs()).toEqual([]));
    expect(store.tabs().find(tab => tab.path === paths.texturing)?.session).toBeNull();
  });

  it('offers what autosave kept to a tab behind whose edits a restart took, once it is in front (#162)', async () => {
    const { store, paint, paintState, sessions, autosaves } = harness();
    await store.open('a');
    paintState.set({ id: `session:${paths.texturing}`, history: { dirty: true } });
    TestBed.tick();
    await store.openDocument('a', paths.graph);
    TestBed.tick();

    // Autosave kept the stack's edits before the Server went, and the new one does not have its session.
    autosaves.set(paths.texturing, '2026-11-07T10:00:00Z');
    sessions.delete(`session:${paths.texturing}`);
    TestBed.inject(EngineRestarts).count.update(count => count + 1);
    TestBed.tick();
    await vi.waitFor(() => expect(store.unsavedTabs()).toEqual([]));
    // Clean, but not forgotten: nothing is offered while the graph is in front.
    expect(store.recoverable()).toBeNull();

    await store.activate(paths.texturing);

    expect(store.recoverable()).toEqual({ path: paths.texturing, saved: '2026-11-07T10:00:00Z' });
    expect(paint.notice()).toBe(RestartedNotice);

    // Said once: the next time it comes to the front it is a document like any other.
    store.dismissRecovery();
    paint.notice.set(null);
    await store.activate(paths.graph);
    await store.activate(paths.texturing);
    expect(store.recoverable()).toBeNull();
    expect(paint.notice()).toBeNull();
  });

  it("keeps the Server's word for a tab behind whose session it still has", async () => {
    const { store, paintState, client } = harness();
    await store.open('a');
    paintState.set({ id: `session:${paths.texturing}`, history: { dirty: true } });
    TestBed.tick();
    await store.openDocument('a', paths.graph);
    TestBed.tick();

    TestBed.inject(EngineRestarts).count.update(count => count + 1);
    TestBed.tick();

    await vi.waitFor(() => expect(client.GET).toHaveBeenCalledWith('/api/v1/stacks'));
    await Promise.resolve();
    expect(store.unsavedTabs().map(tab => tab.path)).toEqual([paths.texturing]);
    expect(store.tabs().find(tab => tab.path === paths.texturing)?.session).toBe(`session:${paths.texturing}`);
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

  it('names the open documents a move would close, so the tree can ask first', async () => {
    const { store } = harness();
    await store.open('a');

    expect(store.openInside(['a']).map(tab => tab.project)).toEqual(['a']);
    expect(store.openInside(['b'])).toEqual([]);
    expect(store.movesMesh({ mesh: '/meshes/new.glb' })).toBe(true);
    expect(store.movesMesh({})).toBe(false);
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

    it('closes for an answer without telling the Server to throw the autosave away', async () => {
      const { store, autosaves, client } = harness();
      autosaves.set(paths.texturing, saved);
      await store.open('a');

      await store.resolveRecovery(true);

      expect(client.DELETE).toHaveBeenCalledWith('/api/v1/stacks/{id}', {
        params: { path: { id: `session:${paths.texturing}` }, query: { discard: false } }
      });
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

  describe('snapshots (#209)', () => {
    /** The Server says every open document is saved, as the tabs do. */
    function serverClean({ client, sessions }: ReturnType<typeof harness>): void {
      const get = client.GET.getMockImplementation();
      if (!get) {
        throw new Error('The harness answers every GET.');
      }

      client.GET.mockImplementation((route, options) =>
        route === '/api/v1/stacks' || route === '/api/v1/graphs'
          ? Promise.resolve({
              data: [...sessions].map(id => ({ id, path: '', name: '', revision: 1, dirty: false })),
              response: new Response()
            })
          : get(route, options)
      );
    }

    it('restores into a closed document, saving its edits first, then opens it again', async () => {
      const { store, removed, paint, client } = harness();
      await store.open('a');
      store.noteDirty(paths.texturing, true);

      expect(await store.restoreSnapshot(paths.texturing, 'snap')).toBe(true);

      expect(paint.act).toHaveBeenCalledWith('save');
      expect(removed).toContain(`session:${paths.texturing}`);
      expect(client.POST).toHaveBeenCalledWith('/api/v1/library/snapshots/restore', {
        body: { path: paths.texturing, id: 'snap' }
      });
      expect(store.active()?.path).toBe(paths.texturing);
      expect(paint.open).toHaveBeenCalledTimes(2);
    });

    it('keeps the workspace while the document is closed for a restore, so its panes stay as they were (#270)', async () => {
      const { store, client } = harness();
      await store.open('a');
      const seen: string[] = [];
      const post = client.POST.getMockImplementation();
      if (!post) {
        throw new Error('The harness answers every POST.');
      }

      client.POST.mockImplementation((route, options) => {
        if (route === '/api/v1/library/snapshots/restore') {
          seen.push(store.workspace());
        }

        return post(route, options);
      });

      expect(await store.restoreSnapshot(paths.texturing, 'snap')).toBe(true);

      expect(seen).toEqual(['texturing']);
      expect(store.workspace()).toBe('texturing');
    });

    it('says why a snapshot was not deleted (#270)', async () => {
      const { store, client } = harness();
      await store.open('a');
      client.DELETE.mockRejectedValueOnce(new ApiError(404, 'There is no such snapshot.'));

      expect(await store.deleteSnapshot(paths.texturing, 'gone')).toBe(false);

      expect(store.failure()).toBe('There is no such snapshot.');
    });

    it('saves first what the Server holds unsaved though the tab says it is clean (#284)', async () => {
      const { store, paint, client } = harness();
      await store.open('a');
      expect(store.tabs()[0]?.dirty).toBe(false);

      expect(await store.restoreSnapshot(paths.texturing, 'snap')).toBe(true);

      expect(paint.act).toHaveBeenCalledWith('save');
      expect(client.POST).toHaveBeenCalledWith('/api/v1/library/snapshots/restore', {
        body: { path: paths.texturing, id: 'snap' }
      });
    });

    it('saves nothing first when the Server and the tab both say the document is clean', async () => {
      const test = harness();
      const { store, paint } = test;
      await store.open('a');
      serverClean(test);

      expect(await store.restoreSnapshot(paths.texturing, 'snap')).toBe(true);

      expect(paint.act).not.toHaveBeenCalledWith('save');
    });

    it('restores nothing when the edits could not be saved', async () => {
      const { store, paint, client } = harness();
      await store.open('a');
      store.noteDirty(paths.texturing, true);
      paint.act.mockResolvedValueOnce(false);

      expect(await store.restoreSnapshot(paths.texturing, 'snap')).toBe(false);

      expect(client.POST).not.toHaveBeenCalledWith('/api/v1/library/snapshots/restore', expect.anything());
      expect(store.active()?.path).toBe(paths.texturing);
    });

    it('takes a snapshot of a clean document without saving it', async () => {
      const test = harness();
      const { store, paint, client } = test;
      await store.open('a');
      serverClean(test);

      expect(await store.takeSnapshot(paths.texturing, 'Before rust')).toBe(true);

      expect(paint.act).not.toHaveBeenCalledWith('save');
      expect(client.POST).toHaveBeenCalledWith('/api/v1/library/snapshots', {
        body: { path: paths.texturing, name: 'Before rust' }
      });
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

  describe('bringing files in', () => {
    const withPicture = {
      ...contents,
      pictures: [{ kind: 'picture', name: 'rust.png', path: 'projects/a/pictures/rust.png' }]
    };
    const reply = () => Promise.resolve({ data: withPicture, response: new Response() });

    it('hands each file to the Server, which says what it is, and shows the project as it comes back', async () => {
      const { store, client } = harness();
      await store.open('a');
      client.POST.mockImplementation(reply as never);

      await store.importFiles(['/in/rust.png']);

      expect(client.POST).toHaveBeenCalledWith('/api/v1/library/project/files', {
        body: { project: 'a', file: '/in/rust.png' }
      });
      expect(store.current()?.pictures).toEqual(withPicture.pictures);
    });

    it('says it worked, and the next action clears that', async () => {
      const { store, client } = harness();
      await store.open('a');
      client.POST.mockImplementation(reply as never);

      await store.importFiles(['C:\\in\\rust.png']);
      expect(store.notice()).toBe('Imported rust.png into A');
      expect(store.failure()).toBeNull();

      await store.importFiles(['/in/a.png', '/in/b.png']);
      expect(store.notice()).toBe('Imported 2 files into A');

      await store.open('a');
      expect(store.notice()).toBeNull();
    });

    it('keeps going past a file the Server refuses, and says what happened to both', async () => {
      const { store, client } = harness();
      await store.open('a');
      client.POST.mockImplementationOnce(() => Promise.reject(new ApiError(400, "Nothing reads 'model.blend'.")));
      client.POST.mockImplementationOnce(reply as never);

      await store.importFiles(['/in/model.blend', '/in/rust.png']);

      expect(store.failure()).toBe("Nothing reads 'model.blend'.");
      expect(store.notice()).toBe('Imported rust.png into A');
      expect(store.current()?.pictures).toHaveLength(1);
    });

    it('brings files into a project whose tab shows but is not in front yet', async () => {
      const { store, client } = harness();
      let release = (): void => undefined;
      client.DELETE.mockImplementationOnce(
        () =>
          new Promise(resolve => {
            release = () => resolve({ data: {}, response: new Response() });
          })
      );
      client.POST.mockImplementation(reply as never);
      const opening = store.open('a');
      await vi.waitFor(() => expect(store.tabs()).toHaveLength(1));
      expect(store.current()).toBeNull(); // the viewport is still letting go of what was in front

      const importing = store.importFiles(['/in/rust.png']);
      release();
      await Promise.all([opening, importing]);

      expect(store.failure()).toBeNull();
      expect(store.notice()).toBe('Imported rust.png into A');
    });

    it('says nothing worked when nothing did', async () => {
      const { store, client } = harness();
      await store.open('a');
      client.POST.mockImplementationOnce(() => Promise.reject(new ApiError(400, "Nothing reads 'model.blend'.")));

      await store.importFiles(['/in/model.blend']);

      expect(store.failure()).toBe("Nothing reads 'model.blend'.");
      expect(store.notice()).toBeNull();
    });
  });

  describe('reloading the mesh', () => {
    const onSource = {
      ...contents,
      meshes: [{ id: 'm', path: 'projects/a/low.glb', high: null, source: '/art/low.glb' }]
    };

    /** A reload whose file changed or not, and which kept its layout or moved something (#89). */
    function reloadable(changed: boolean, kept = false) {
      const test = harness();
      test.client.GET.mockImplementation(((route: string) =>
        Promise.resolve({
          data: route === '/api/v1/library/project/mesh-change' ? { changed } : onSource,
          response: new Response()
        })) as never);
      const patch = vi.fn((_route: string, options: { body: { sameLayout?: boolean } }) =>
        options.body.sameLayout && !kept
          ? Promise.reject(new ApiError(409, 'moved', [], 'mesh-layout-changed', { reason: 'moved' }))
          : Promise.resolve({ data: onSource, response: new Response() })
      );
      (test.client as unknown as { PATCH: unknown }).PATCH = patch;
      /** The reloads that close documents: every one but those asked to keep the layout. */
      const closing = () => patch.mock.calls.filter(([, options]) => !options.body.sameLayout);
      return { ...test, patch, closing };
    }

    it('leaves everything open when the file has not changed, and says so', async () => {
      const { store, patch, removed } = reloadable(false);
      await store.open('a');

      await store.reloadMesh();

      expect(store.reloading()).toBeNull();
      expect(store.notice()).toBe('The mesh has not changed.');
      expect(patch).not.toHaveBeenCalled();
      expect(removed.filter(id => id.startsWith('session'))).toEqual([]);
      expect(store.tabs()).toHaveLength(1);
    });

    it('asks first when something moved, and closes nothing until told', async () => {
      const { store, closing, removed } = reloadable(true);
      await store.open('a');

      await store.reloadMesh();

      expect(store.reloading()).toBe('/art/low.glb');
      expect(closing()).toEqual([]);
      expect(removed.filter(id => id.startsWith('session'))).toEqual([]);
      expect(store.failure()).toBeNull();

      await store.resolveReloading(false);
      expect(store.reloading()).toBeNull();
      expect(closing()).toEqual([]);
    });

    it('reloads on the answer', async () => {
      const { store, closing } = reloadable(true);
      await store.open('a');
      await store.reloadMesh();

      await store.resolveReloading(true);

      expect(closing()).toHaveLength(1);
      expect(store.active()?.path).toBe(paths.texturing);
    });

    it('reloads a mesh on which nothing moved without asking, keeping its documents open (#89)', async () => {
      const { store, patch, closing, removed } = reloadable(true, true);
      await store.open('a');

      await store.reloadMesh();

      expect(store.reloading()).toBeNull();
      expect(patch).toHaveBeenCalledTimes(1);
      expect(closing()).toEqual([]);
      expect(removed.filter(id => id.startsWith('session'))).toEqual([]);
      expect(store.tabs()).toHaveLength(1);
      expect(store.notice()).toBe('Reloaded the mesh. Its layout is the same, so paint and undo stay as they were.');
    });

    it('reassigns the project a tab on its way to the front belongs to, once it has arrived (#89, #237)', async () => {
      const { store, client, patch } = reloadable(true);
      let release = (): void => undefined;
      client.DELETE.mockImplementationOnce(
        () =>
          new Promise(resolve => {
            release = () => resolve({ data: {}, response: new Response() });
          })
      );
      const opened = store.open('a');
      await vi.waitFor(() => expect(store.tabs()).toHaveLength(1));

      const applied = store.applySettings({ mesh: '/art/low.glb', reassign: { Old: 'New' } });
      await new Promise(resolve => setTimeout(resolve, 0));
      expect(patch).not.toHaveBeenCalled(); // the viewport is still letting go of what was in front

      release();
      await opened;
      expect(await applied).toBe(true);
      const [route, options] = patch.mock.calls[0] as unknown as [
        string,
        { body: { project: string; reassign: Record<string, string> } }
      ];
      expect(route).toBe('/api/v1/library/project/settings');
      expect(options.body.project).toBe('a');
      expect(options.body.reassign).toEqual({ Old: 'New' });
      expect(store.active()?.path).toBe(paths.texturing);
    });

    it('says when the bake layers read is out of date', async () => {
      const { store, client } = harness();
      const stale = { ...contents, bakes: [{ kind: 'bake', name: 'a', path: 'p', stale: true }] };
      client.GET.mockImplementation((() => Promise.resolve({ data: stale, response: new Response() })) as never);

      await store.open('a');

      expect(store.bakeOutOfDate()).toBe(true);
    });

    it('reads the bake of the document in front, not the last folder the project lists', async () => {
      const { store, client } = harness();
      const own = 'projects/a/bakes/maps/A';
      const stale = {
        ...contents,
        documents: [{ ...contents.documents[0], bake: own }, contents.documents[1]],
        bakes: [
          { kind: 'bake', name: 'maps/A', path: own, stale: false },
          { kind: 'bake', name: 'maps/B', path: 'projects/a/bakes/maps/B', stale: true }
        ]
      };
      client.GET.mockImplementation((() => Promise.resolve({ data: stale, response: new Response() })) as never);

      await store.open('a');

      expect(store.document()?.path).toBe(paths.texturing);
      expect(store.bakeOutOfDate()).toBe(false);
    });
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

describe('a path the Server gives', () => {
  it('names the same document on Windows as on Linux', () => {
    const tab = 'projects/moss/moss.vxlayers';
    const windows = String.raw`C:\Libraries\Ember.ptlib\projects\moss`;
    expect(inLibrary(String.raw`${windows}\moss.vxlayers`)).toBe(tab);
    expect(inLibrary('/home/a/Ember.ptlib/projects/moss/moss.vxlayers')).toBe(tab);
    expect(sameDocument(tab, String.raw`${windows}\moss.vxlayers`)).toBe(true);
    expect(sameDocument(tab, String.raw`${windows}\other.vxlayers`)).toBe(false);
  });
});
