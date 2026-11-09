import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type ViewportInput, createPatinaClient } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { DefaultCamera } from './orbit';
import { DefaultDisplay, FRAME_TRANSPORT_FACTORY, ViewportClient } from './viewport-client';

function setup() {
  const inputs: ViewportInput[] = [];
  const displays: unknown[] = [];
  const opens: (() => void)[] = [];
  const fetcher = (async (input: Request) => {
    const path = new URL(input.url).pathname;
    if (input.method === 'PUT' && path.endsWith('/viewport/display')) {
      const display: unknown = await input.json();
      displays.push(display);
      return Response.json({ mesh: null, camera: DefaultCamera, display });
    }

    return Response.json({ mesh: '/a/crate.glb', camera: DefaultCamera, display: DefaultDisplay });
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
      { provide: BakeStore, useValue: { viewportChanged: signal(0) } },
      { provide: ProjectStore, useValue: { active: signal(null), current: signal(null) } }
    ]
  });
  return { client: TestBed.inject(ViewportClient), inputs, displays, opens };
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
});
