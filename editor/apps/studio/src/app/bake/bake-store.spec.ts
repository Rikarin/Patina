import { TestBed } from '@angular/core/testing';
import { type JobState, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { BakeStore, jobSocketUrl } from './bake-store';

const bake = { high: ['/m/high.glb'], low: '/m/low.glb', output: '/out', options: { resolution: 2048 } };

function job(overrides: Partial<JobState> = {}): JobState {
  return {
    id: 'j1',
    key: 'k',
    state: 'running',
    progress: 0,
    attempts: 1,
    created: '2026-11-05T00:00:00Z',
    bake,
    cancelRequested: false,
    ...overrides
  } as JobState;
}

/** A Server: each route answers from the table, and a socket that a test can speak through. */
function speak(socket: GraphSocket | undefined, message: unknown): void {
  const hear = socket?.onmessage as ((event: MessageEvent) => void) | null | undefined;
  hear?.({ data: JSON.stringify(message) } as MessageEvent);
}

function setup(routes: Record<string, (request: Request) => Response>) {
  const sockets: GraphSocket[] = [];
  const urls: string[] = [];
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
        useValue: (url: string) => {
          urls.push(url);
          const socket = { onopen: null, onmessage: null, onclose: null, close: vi.fn() } as unknown as GraphSocket;
          sockets.push(socket);
          return socket;
        }
      }
    ]
  });
  return { store: TestBed.inject(BakeStore), sockets, urls };
}

const empty = { 'GET jobs': () => Response.json({ jobs: [], sequence: 7 }) };

async function settled(): Promise<void> {
  await new Promise(resolve => setTimeout(resolve));
}

describe('BakeStore', () => {
  it('follows the jobs socket from the sequence the Server gave it', async () => {
    const { urls } = setup(empty);
    await settled();

    expect(urls[0]).toContain('/ws/jobs?since=7');
    expect(urls[0]).toContain('access_token=secret');
  });

  it('shows a started bake and the events that move it on', async () => {
    const { store, sockets } = setup({
      ...empty,
      'POST jobs/bakes': () => Response.json(job(), { status: 202 })
    });
    await settled();
    await store.start(bake as never);

    expect(store.current()?.state).toBe('running');

    speak(sockets[0], { sequence: 8, job: job({ progress: 0.5 }) });

    expect(store.current()?.progress).toBe(0.5);
  });

  it('starts a job per texture set and follows the first still going, then the last', async () => {
    let made = 0;
    const { store, sockets } = setup({
      ...empty,
      'POST jobs/bakes': () => Response.json(job({ id: `j${++made}`, state: 'queued' }), { status: 202 })
    });
    await settled();

    const jobs = await store.startAll([bake, bake] as never);

    expect(jobs.map(started => started.id)).toEqual(['j1', 'j2']);
    expect(store.batch().map(started => started.id)).toEqual(['j1', 'j2']);
    expect(store.current()?.id).toBe('j1');
    expect(store.busy()).toBe(true);

    speak(sockets[0], { sequence: 8, job: job({ id: 'j1', state: 'done', progress: 1 }) });
    expect(store.current()?.id).toBe('j2');

    speak(sockets[0], { sequence: 9, job: job({ id: 'j2', state: 'done', progress: 1 }) });
    expect(store.current()?.id).toBe('j2');
    expect(store.busy()).toBe(false);
  });

  it('keeps what the events said of a job that ended before the answer that started it came back', async () => {
    let sockets: GraphSocket[] = [];
    const routes = {
      ...empty,
      'POST jobs/bakes': () => {
        speak(sockets[0], { sequence: 8, job: job({ state: 'done', progress: 1 }) });
        return Response.json(job({ state: 'queued' }), { status: 202 });
      }
    };
    const made = setup(routes);
    sockets = made.sockets;
    await settled();

    await made.store.start(bake as never);
    expect(made.store.current()?.state).toBe('done');

    made.store.track(job({ state: 'queued' }));
    expect(made.store.find('j1')?.state).toBe('done');
  });

  it('keeps following a job another client started without taking the panel over', async () => {
    const { store, sockets } = setup(empty);
    await settled();

    speak(sockets[0], { sequence: 8, job: job({ id: 'other' }) });

    expect(store.current()).toBeNull();
  });

  it('passes on the Server refusing a bake', async () => {
    const { store } = setup({
      ...empty,
      'POST jobs/bakes': () => Response.json({ detail: "'tga' is not a format." }, { status: 422 })
    });
    await settled();

    await expect(store.start(bake as never)).rejects.toThrow(/not a format/);
  });

  it('hides a map by dropping its picture', async () => {
    URL.createObjectURL = vi.fn(() => 'blob:map');
    URL.revokeObjectURL = vi.fn();
    const { store } = setup({
      ...empty,
      'GET jobs/j1/previews/normal': () => new Response(new Blob(['png'], { type: 'image/png' }))
    });
    await settled();

    await store.show(job({ state: 'done' }), 'normal');
    expect(store.shown()?.map).toBe('normal');

    store.hide();
    expect(store.shown()).toBeNull();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:map');
  });

  it('shows a map wider than it is tall at its own shape', async () => {
    URL.createObjectURL = vi.fn(() => 'blob:map');
    URL.revokeObjectURL = vi.fn();
    const { store } = setup({
      ...empty,
      'GET jobs/j1/previews/normal': () => new Response(new Blob(['png'], { type: 'image/png' }))
    });
    await settled();

    const maps = [{ map: 'normal', path: '/out/normal.png', resolution: 2048, scale: 0, rows: 1024 }];
    const result = { maps, covered: 1, missed: 0, warnings: [], cached: false, log: [] };
    await store.show(job({ state: 'done', result }), 'normal');

    expect(store.shown()).toMatchObject({ width: 2048, height: 1024 });
  });

  it('builds a socket address that matches its server', () => {
    expect(jobSocketUrl({ baseUrl: 'https://host:1', token: null }, 3)).toBe('wss://host:1/ws/jobs?since=3');
  });

  it('shows a finished bake on its low mesh by asking the Server, and says the viewport changed', async () => {
    let asked: unknown;
    const { store } = setup({
      ...empty,
      'PUT viewport/bake': request => {
        asked = request.clone();
        return Response.json({ mesh: '/m/low.glb' });
      }
    });

    await store.showOnMesh(job({ state: 'done' }));

    expect(await (asked as Request).json()).toEqual({ job: 'j1', map: null });
    expect(store.viewportChanged()).toBe(1);

    await store.showOnMesh(job({ state: 'done' }), 'ao');

    expect(await (asked as Request).json()).toEqual({ job: 'j1', map: 'ao' });
    expect(store.viewportChanged()).toBe(2);
  });
});
