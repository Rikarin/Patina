import type { CollectionViewer } from '@angular/cdk/collections';
import { TestBed } from '@angular/core/testing';
import { type LibraryInfo, createPatinaClient } from '@patina/api-client';
import { Subject } from 'rxjs';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { LibrarySelection } from './library-selection';
import { LibraryStore, SearchDebounceMs } from './library-store';
import { counted, nextIndex } from './list-keys';

const libraryInfo: LibraryInfo = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Ember',
  path: '/games/Ember.ptlib',
  schemaVersion: 1,
  texelDensity: 1024,
  channels: ['baseColor'],
  defaultShader: 'pbr',
  exportTargets: [],
  projectCount: 2,
  assetCount: 1,
  indexing: false
};

const project = { id: 'p1', name: 'Crate', path: 'props/crate', tags: ['hero'], referenceCount: 3 };

/** A Server: each route answers from the table, and every request is written down. */
function server(routes: Record<string, (request: Request) => Response>) {
  const requests: Request[] = [];
  const answer = (input: RequestInfo | URL) => {
    const request = input as Request; // the client always sends one
    requests.push(request);
    const path = new URL(request.url).pathname.replace('/api/v1/', '');
    const key = `${request.method} ${path}`;
    const route = routes[key] ?? routes[path];
    return Promise.resolve(route ? route(request) : Response.json({ detail: `no route for ${key}` }, { status: 500 }));
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server', token: null }) },
      {
        provide: API_CLIENT_FACTORY,
        useValue: (connection: { baseUrl: string; token: string | null }) => createPatinaClient(connection, answer)
      }
    ]
  });
  return { requests, store: TestBed.inject(LibraryStore), selection: TestBed.inject(LibrarySelection) };
}

const empty = (): Response => Response.json({ items: [], total: 0, offset: 0, limit: 50 });

const viewer = (): CollectionViewer => ({ viewChange: new Subject() });

const page = (items: unknown[], total = items.length) => Response.json({ items, total, offset: 0, limit: 100 });

describe('LibraryStore', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts out loading', () => {
    const { store } = server({});

    expect(store.state()).toEqual({ kind: 'loading' });
  });

  it('reports the library the Server has', async () => {
    const { store } = server({
      library: () => Response.json(libraryInfo),
      'library/projects/tags': () =>
        Response.json({ items: [{ tag: 'hero', count: 1 }], total: 1, offset: 0, limit: 500 }),
      'library/assets/tags': empty
    });

    await vi.waitFor(() => {
      expect(store.state().kind).toBe('ready');
    });
    await vi.waitFor(() => {
      expect(store.projects.tagOptions()).toEqual([{ tag: 'hero', count: 1 }]);
    });
  });

  it('tells no library from a failure', async () => {
    const { store } = server({ library: () => Response.json({ detail: 'none' }, { status: 404 }) });

    await vi.waitFor(() => {
      expect(store.state()).toEqual({ kind: 'none' });
    });
  });

  it('says why a Server cannot be reached', async () => {
    const { store } = server({ library: () => Response.json({ detail: 'on fire' }, { status: 500 }) });

    await vi.waitFor(() => {
      expect(store.state()).toEqual({ kind: 'error', reason: 'on fire' });
    });
  });

  it('asks for a page with the search and the tags, once the typing stops', async () => {
    const { requests, store } = server({
      library: () => Response.json(libraryInfo),
      'library/projects/tags': empty,
      'library/assets/tags': empty,
      'library/projects': () => page([project])
    });
    store.projects.list.connect(viewer());
    await vi.waitFor(() => {
      expect(store.projects.list.total()).toBe(1);
    });

    store.projects.setTags(['hero']);
    store.projects.setSearch('c');
    store.projects.setSearch('cr');
    store.projects.setSearch(' cra ');
    await new Promise(done => setTimeout(done, SearchDebounceMs * 2));

    const pages = requests.filter(r => new URL(r.url).pathname.endsWith('/library/projects'));
    const last = new URL(pages.at(-1)?.url ?? '').searchParams;
    expect(last.get('search')).toBe('cra');
    expect(last.getAll('tag')).toEqual(['hero']);
    expect(pages.filter(r => new URL(r.url).searchParams.get('search') === 'c')).toHaveLength(0);
    expect(store.projects.filtered).toBe(true);
  });

  it('asks again when an index finishes catching up', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    let info: LibraryInfo = { ...libraryInfo, indexing: true };
    let listed = 0;
    const { store } = server({
      library: () => Response.json(info),
      'library/projects/tags': empty,
      'library/assets/tags': empty,
      'library/projects': () => {
        listed++;
        return page([project]);
      }
    });
    store.projects.list.connect(viewer());
    await vi.advanceTimersByTimeAsync(10);
    const before = listed;
    const changes = store.changes();

    info = { ...libraryInfo, indexing: false };
    await vi.advanceTimersByTimeAsync(400);

    expect(listed).toBe(before + 1);
    expect(store.changes()).toBe(changes + 1);
  });

  it('selects a new project and looks again at once', async () => {
    const { requests, selection, store } = server({
      library: () => Response.json(libraryInfo),
      'library/projects/tags': empty,
      'library/assets/tags': empty,
      'POST library/projects': () => Response.json(project, { status: 201 })
    });
    await vi.waitFor(() => {
      expect(store.state().kind).toBe('ready');
    });
    const polled = requests.length;

    const created = await store.createProject({ name: 'Crate', folder: 'props', tags: ['hero'] });
    const posted: unknown = await requests
      .find(r => r.method === 'POST')
      ?.clone()
      .json();

    expect(created).toEqual(project);
    expect(posted).toEqual({ name: 'Crate', folder: 'props', tags: ['hero'] });
    expect(selection.item()).toEqual({ kind: 'project', project });
    await vi.waitFor(() => {
      expect(requests.length).toBeGreaterThan(polled + 1);
    });
  });

  it('passes on the reason a new project was refused', async () => {
    const { store } = server({
      library: () => Response.json(libraryInfo),
      'library/projects/tags': empty,
      'library/assets/tags': empty,
      'POST library/projects': () => Response.json({ detail: "'crate' exists already." }, { status: 409 })
    });

    await expect(store.createProject({ name: 'Crate' })).rejects.toThrow("'crate' exists already.");
  });

  it('carries an impact page and its counts', async () => {
    const asset = { id: 'a1', kind: 'graph', name: 'Rust', path: 'graphs/rust', tags: [], usageCount: 2 };
    const { store } = server({
      library: () => Response.json(libraryInfo),
      'library/projects/tags': empty,
      'library/assets/tags': empty,
      'library/assets/a1/impact': () =>
        Response.json({
          asset,
          projects: 412,
          pinned: 3,
          unreadableDocuments: 0,
          items: [{ id: 'p1', name: 'Crate', path: 'props/crate', pinned: true }],
          offset: 0,
          limit: 100
        })
    });

    const impact = store.impactOf(asset);
    impact.list.connect(viewer());

    await vi.waitFor(() => {
      expect(impact.impact()?.projects).toBe(412);
    });
    expect(impact.list.total()).toBe(412);
    expect(impact.list.at(0)?.pinned).toBe(true);
  });
});

describe('nextIndex', () => {
  it.each([
    ['ArrowDown', null, 0],
    ['ArrowDown', 3, 4],
    ['ArrowDown', 9, 9],
    ['ArrowUp', null, 0],
    ['ArrowUp', 0, 0],
    ['ArrowUp', 3, 2],
    ['PageDown', 2, 7],
    ['PageDown', 8, 9],
    ['PageUp', 2, 0],
    ['Home', 7, 0],
    ['End', 2, 9],
    ['a', 2, null]
  ])('%s from %s goes to %s', (key, index, next) => {
    expect(nextIndex(key, index, 10, 5)).toBe(next);
  });

  it('goes nowhere in an empty list', () => {
    expect(nextIndex('ArrowDown', null, 0, 5)).toBeNull();
  });
});

describe('counted', () => {
  it('speaks plainly', () => {
    expect(counted(1, 'project')).toBe('1 project');
    expect(counted(10_000, 'project')).toBe('10,000 projects');
  });
});
