import { TestBed } from '@angular/core/testing';
import { type JobState, createPatinaClient } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { ExportStore } from './export-store';

const build = {
  id: 'b1',
  key: 'build',
  state: 'queued',
  progress: 0,
  attempts: 0,
  created: '2026-11-05T00:00:00Z',
  build: { presets: ['unreal'] },
  cancelRequested: false
} as JobState;

function setup(routes: Record<string, (request: Request) => Response>) {
  const sockets: GraphSocket[] = [];
  const answer = (input: RequestInfo | URL) => {
    const request = input as Request;
    const key = `${request.method} ${new URL(request.url).pathname.replace('/api/v1/', '')}`;
    const route = routes[key];
    return Promise.resolve(route ? route(request) : Response.json({ detail: `no route for ${key}` }, { status: 500 }));
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server', token: 'secret' }) },
      {
        provide: API_CLIENT_FACTORY,
        useValue: (connection: { baseUrl: string; token: string | null }) => createPatinaClient(connection, answer)
      },
      {
        provide: GRAPH_SOCKET,
        useValue: () => {
          const socket = { onopen: null, onmessage: null, onclose: null, close: vi.fn() } as unknown as GraphSocket;
          sockets.push(socket);
          return socket;
        }
      }
    ]
  });
  return { store: TestBed.inject(ExportStore), jobs: TestBed.inject(BakeStore), sockets };
}

const jobs = { 'GET jobs': () => Response.json({ jobs: [], sequence: 0 }) };

describe('ExportStore', () => {
  it('lists presets and what cannot be used', async () => {
    const { store } = setup({
      ...jobs,
      'GET export/presets': () =>
        Response.json({
          presets: [{ id: 'unreal', name: 'Unreal Engine', files: [] }],
          problems: ['broken.yaml: no id']
        })
    });

    await store.load();

    expect(store.presets().map(preset => preset.id)).toEqual(['unreal']);
    expect(store.problems()).toEqual(['broken.yaml: no id']);
  });

  it('writes a document’s maps as on screen, or in the format chosen', async () => {
    const bodies: unknown[] = [];
    const { store } = setup({
      ...jobs,
      'POST stacks/s1/export': request => {
        void request.json().then(body => bodies.push(body));
        return Response.json([{ usage: 'baseColor', width: 4, height: 4, digest: 'd', file: '/out/a.baseColor.tga' }]);
      }
    });

    await store.exportStack('s1', '/out');
    await store.exportStack('s1', '/out', 'tga');
    await new Promise(resolve => setTimeout(resolve));

    expect(bodies).toEqual([{ folder: '/out' }, { folder: '/out', format: 'tga' }]);
    expect(store.exported()).toEqual(['/out/a.baseColor.tga']);
  });

  it('asks for a preview with the preset it was given, and runs nothing', async () => {
    const sent: unknown[] = [];
    const { store } = setup({
      ...jobs,
      'POST builds/preview': request => {
        sent.push(request.url);
        return Response.json({ projects: 412, rebuilds: 412, pinned: 3, items: [] });
      }
    });

    await store.ask({ presets: ['unreal'] });

    expect(store.preview()?.projects).toBe(412);
    expect(sent).toHaveLength(1);
    expect(store.build()).toBeNull();
  });

  it('follows the build it started through the job events', async () => {
    const { store, jobs: following } = setup({
      ...jobs,
      'POST builds': () => Response.json(build, { status: 202 })
    });

    await store.start({ presets: ['unreal'] });
    expect(store.build()?.state).toBe('queued');

    following.track({ ...build, state: 'running', progress: 0.5 });
    expect(store.build()?.progress).toBe(0.5);
  });

  it('reads a report and its first page of projects', async () => {
    const { store } = setup({
      ...jobs,
      'GET builds/b1/report': () => Response.json({ projects: 2, built: 2, failed: 0, files: 8, folder: '/r' }),
      'GET builds/b1/report/projects': () =>
        Response.json({
          total: 60,
          items: [{ id: 'p', name: 'Crate', outputs: [], exports: [] }],
          offset: 0,
          limit: 50
        })
    });

    await store.read('b1');

    expect(store.report()?.files).toBe(8);
    expect(store.projects()[0]?.name).toBe('Crate');
    expect(store.projectCount()).toBe(60);
  });
});
