import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type ViewportInput, type ViewportIsolation, createPatinaClient } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { IsolationStore } from './isolation-store';
import { DefaultCamera } from './orbit';
import { DefaultDisplay, FRAME_TRANSPORT_FACTORY, ViewportClient } from './viewport-client';

function setup() {
  const inputs: ViewportInput[] = [];
  const displays: unknown[] = [];
  const opens: (() => void)[] = [];
  /** What the Server draws alone, which one that restarts forgets. */
  const server: { isolation: ViewportIsolation | null } = { isolation: null };
  const shown: { graph: string; node: number | null }[] = [];
  const fetcher = (async (input: Request) => {
    const path = new URL(input.url).pathname;
    if (input.method === 'PUT' && path.endsWith('/viewport/display')) {
      const display: unknown = await input.json();
      displays.push(display);
      // A stand-in shape gives way to the one the display chooses.
      const shape = (display as { shape?: string }).shape ?? 'sphere';
      return Response.json({ mesh: `/cache/preview/${shape}.glb`, camera: DefaultCamera, display });
    }

    return Response.json({
      mesh: '/a/crate.glb',
      camera: DefaultCamera,
      display: DefaultDisplay,
      isolation: server.isolation
    });
  }) as unknown as typeof fetch;
  TestBed.configureTestingModule({
    providers: [
      ViewportClient,
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://127.0.0.1:1', token: 't' }) },
      { provide: API_CLIENT_FACTORY, useValue: (c: never) => createPatinaClient(c, fetcher) },
      {
        provide: FRAME_TRANSPORT_FACTORY,
        useValue: (_connection: unknown, _onFrame: unknown, onOpen: () => void) => {
          opens.push(onOpen);
          return { send: (input: ViewportInput) => inputs.push(input), close: () => undefined };
        }
      },
      {
        provide: BakeStore,
        useValue: {
          viewportChanged: signal(0),
          showGraphOnMesh: (graph: string, node: number | null) => {
            shown.push({ graph, node });
            return Promise.resolve({
              mesh: '/cache/preview/sphere.glb',
              node: node === null ? null : { graph, node, port: 'Out' }
            });
          }
        }
      },
      { provide: ProjectStore, useValue: { active: signal(null), current: signal(null) } }
    ]
  });
  return { client: TestBed.inject(ViewportClient), inputs, displays, opens, server, shown };
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

  it('shows one node of a graph alone, says so, and lets it go for the material', async () => {
    const { client, shown } = setup();

    await client.showGraph('g1', 4);
    expect(shown).toEqual([{ graph: 'g1', node: 4 }]);
    expect(client.node()).toEqual({ graph: 'g1', node: 4, port: 'Out' });

    await client.showGraph('g1');
    expect(shown.at(-1)).toEqual({ graph: 'g1', node: null });
    expect(client.node()).toBeNull();
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
