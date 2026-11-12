import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  type ViewportFollow,
  type ViewportInput,
  type ViewportIsolation,
  createPatinaClient
} from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import type { ViewportFrame } from './frame-transport';
import { IsolationStore } from './isolation-store';
import { DefaultCamera } from './orbit';
import { DefaultDisplay, FRAME_TRANSPORT_FACTORY, ViewportClient } from './viewport-client';

function setup() {
  const inputs: ViewportInput[] = [];
  const displays: unknown[] = [];
  const opens: (() => void)[] = [];
  /** What the Server draws alone, which one that restarts forgets. */
  const server: { isolation: ViewportIsolation | null; following: ViewportFollow | null; reads: number } = {
    isolation: null,
    following: null,
    reads: 0
  };
  const frames: ((frame: ViewportFrame) => void)[] = [];
  const fetcher = (async (input: Request) => {
    const path = new URL(input.url).pathname;
    if (input.method === 'PUT' && path.endsWith('/viewport/follow')) {
      const follow = (await input.json()) as ViewportFollow;
      server.following = follow.graph === null ? null : follow;
      return Response.json({ mesh: '/cache/preview/sphere.glb', camera: DefaultCamera, following: server.following });
    }

    if (input.method === 'PUT' && path.endsWith('/viewport/display')) {
      const display: unknown = await input.json();
      displays.push(display);
      // A stand-in shape gives way to the one the display chooses.
      const shape = (display as { shape?: string }).shape ?? 'sphere';
      return Response.json({ mesh: `/cache/preview/${shape}.glb`, camera: DefaultCamera, display });
    }

    server.reads++;
    return Response.json({
      mesh: '/a/crate.glb',
      camera: DefaultCamera,
      display: DefaultDisplay,
      isolation: server.isolation,
      following: server.following,
      node: server.following?.node ? { graph: server.following.graph, node: 4, port: 'Out' } : null
    });
  }) as unknown as typeof fetch;
  TestBed.configureTestingModule({
    providers: [
      ViewportClient,
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://127.0.0.1:1', token: 't' }) },
      { provide: API_CLIENT_FACTORY, useValue: (c: never) => createPatinaClient(c, fetcher) },
      {
        provide: FRAME_TRANSPORT_FACTORY,
        useValue: (_connection: unknown, onFrame: (frame: ViewportFrame) => void, onOpen: () => void) => {
          frames.push(onFrame);
          opens.push(onOpen);
          return { send: (input: ViewportInput) => inputs.push(input), close: () => undefined };
        }
      },
      { provide: BakeStore, useValue: { viewportChanged: signal(0) } },
      { provide: ProjectStore, useValue: { active: signal(null), current: signal(null) } }
    ]
  });
  return { client: TestBed.inject(ViewportClient), inputs, displays, opens, server, frames };
}

describe('ViewportClient', () => {
  it('tells a Server it reconnected to where the view looked and how it was shown (#226)', async () => {
    const { client, inputs, displays, opens } = setup();
    await vi.waitFor(() => expect(opens).toHaveLength(1));
    const open = opens[0] ?? (() => undefined);
    open();
    expect(inputs).toEqual([]);

    const camera = { ...DefaultCamera, yaw: 2, pitch: -0.3, distance: 1.5 };
    client.moveCamera(camera, false);
    client.display.set({ ...DefaultDisplay, exposure: 1.25 });
    inputs.length = 0;

    // The Server restarted, and the socket opened again on one that starts from its defaults.
    open();

    expect(inputs).toEqual([expect.objectContaining({ camera, interacting: false })]);
    await vi.waitFor(() => expect(displays).toEqual([expect.objectContaining({ exposure: 1.25 })]));
  });

  it('asks the Server to follow a graph, and reads what lies on the mesh again when a frame says it changed', async () => {
    const { client, server, frames } = setup();
    await vi.waitFor(() => expect(frames).toHaveLength(1));
    const frame = (laid: number) => {
      frames[0]?.({
        sequence: 1,
        width: 2,
        height: 2,
        input: 0,
        renderMs: 1,
        painted: 0,
        laid,
        pixels: new Uint8Array(16)
      });
    };

    await client.follow('g1', true);
    expect(client.following()).toEqual({ graph: 'g1', node: true });

    // The first frame only says where the count stands; the next that moves it is a change made elsewhere.
    frame(3);
    const reads = server.reads;
    frame(3);
    expect(server.reads).toBe(reads);
    frame(4);
    await vi.waitFor(() => expect(client.node()).toEqual({ graph: 'g1', node: 4, port: 'Out' }));

    await client.follow(null);
    expect(client.following()).toBeNull();
  });

  it('takes the mesh the Server answers a new shape with', async () => {
    const { client } = setup();
    client.setDisplay({ shape: 'cube' });
    await vi.waitFor(() => expect(client.mesh()).toBe('/cache/preview/cube.glb'));
  });

  it('hears what the Server draws alone when it connects, and that one which restarted draws everything', async () => {
    const { opens, server } = setup();
    server.isolation = { parts: [], materials: ['Wood'] };
    const isolation = TestBed.inject(IsolationStore);
    await vi.waitFor(() => expect(opens).toHaveLength(1));
    expect(isolation.isolation()).toEqual({ parts: [], materials: ['Wood'] });
    const open = opens[0] ?? (() => undefined);
    open();

    // The Server restarted: isolation lived in its memory only, so the controls go back to the whole model.
    server.isolation = null;
    open();

    await vi.waitFor(() => expect(isolation.isolation()).toEqual({ parts: [], materials: [] }));
  });
});
