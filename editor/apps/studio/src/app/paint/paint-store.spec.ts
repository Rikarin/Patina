import { TestBed } from '@angular/core/testing';
import { type StackState, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { PaintStore, colourOf, stackSocketUrl, toTexels } from './paint-store';

function stack(overrides: Partial<StackState> = {}): StackState {
  const state: StackState = {
    id: 's1',
    path: '/a/Crate.vxlayers',
    name: 'Crate',
    set: 'Body',
    width: 2048,
    height: 2048,
    revision: 1,
    sequence: 3,
    history: { canUndo: false, canRedo: false, dirty: false },
    channels: [{ usage: 'baseColor', default: [0, 0, 0, 1] }],
    layers: [
      { id: 'base', name: 'Base', kind: 'fill', enabled: true, opacity: 1, blend: 'Copy', channels: [], painted: false, children: [] },
      { id: 'paint', name: 'Paint', kind: 'paint', enabled: true, opacity: 1, blend: 'Copy', channels: [], painted: false, children: [] }
    ],
    previews: [],
    settled: 0,
    ...overrides
  };
  return state;
}

function setup() {
  const sockets: GraphSocket[] = [];
  const bodies: unknown[] = [];
  const fetcher = (async (input: Request) => {
    const path = new URL(input.url).pathname;
    if (path.endsWith('/commands')) {
      bodies.push(await input.json());
      return Response.json({ revision: 2, sequence: 4, history: { canUndo: true, canRedo: false, dirty: true } });
    }

    if (path.startsWith('/api/v1/previews/')) {
      return new Response(new Blob(['png']));
    }

    return Response.json(stack());
  }) as unknown as typeof fetch;
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://127.0.0.1:1', token: 't' }) },
      { provide: API_CLIENT_FACTORY, useValue: (c: never) => createPatinaClient(c, fetcher) },
      {
        provide: GRAPH_SOCKET,
        useValue: () => {
          const socket = { close: () => undefined } as unknown as GraphSocket;
          sockets.push(socket);
          return socket;
        }
      }
    ]
  });
  return { store: TestBed.inject(PaintStore), bodies, sockets };
}

describe('PaintStore', () => {
  it('turns a view position into texels', () => {
    expect(toTexels(50, 25, 100, 100, [2048, 1024])).toEqual({ x: 1024, y: 256 });
  });

  it('reads a swatch as four numbers', () => {
    expect(colourOf('#ff0080')).toEqual([1, 0, 128 / 255, 1]);
  });

  it('follows a stack from the number it read, with its token', () => {
    const url = stackSocketUrl({ baseUrl: 'http://127.0.0.1:9', token: 't' }, 's1', 7);
    expect(url).toBe('ws://127.0.0.1:9/ws/stacks/s1?since=7&access_token=t');
  });

  it('chooses the top paint layer when a stack opens', async () => {
    const { store } = setup();
    await store.open('/a/Crate.vxlayers', false);
    expect(store.layer()).toBe('paint');
    expect(store.layers().map(layer => layer.id)).toEqual(['paint', 'base']);
  });

  it('sends a drag as stamps and closes it with the last part', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.begin({ x: 1, y: 2 });
    store.move({ x: 3, y: 4 });
    store.end();
    await vi.waitFor(() => {
      const parts = bodies.flatMap(body => (body as { commands: { open: boolean; stamps: unknown[] }[] }).commands);
      expect(parts.at(-1)?.open).toBe(false);
      expect(parts.flatMap(part => part.stamps)).toEqual([
        { x: 1, y: 2 },
        { x: 3, y: 4 }
      ]);
    });
  });

  it('shows a map an event names', async () => {
    const { store, sockets } = setup();
    globalThis.URL.createObjectURL = () => 'blob:map';
    globalThis.URL.revokeObjectURL = () => undefined;
    await store.open('/a/Crate.vxlayers', false);
    const hear = sockets[0]?.onmessage as unknown as (event: MessageEvent) => void;
    hear({
      data: JSON.stringify({
        sequence: 5,
        kind: 'evaluated',
        revision: 2,
        history: { canUndo: true, canRedo: false, dirty: true },
        evaluation: { previews: [{ usage: 'baseColor', hash: 'aa', width: 512, height: 512 }] }
      })
    } as MessageEvent);
    await vi.waitFor(() => expect(store.picture()).toBe('blob:map'));
    expect(store.state()?.history.canUndo).toBe(true);
  });
});
