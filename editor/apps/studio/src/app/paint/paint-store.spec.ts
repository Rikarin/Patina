import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type StackLayer, type StackState, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { hexOf } from './layer-panel';
import { LiveValue } from './live-value';
import { PaintStore, colourOf, pressureOf, rowsOf, stackSocketUrl, toTexels } from './paint-store';
import { StrokeGesture } from './stroke-gesture';

const PickedView = {
  eye: [0, 0, 4],
  right: [1, 0, 0],
  up: [0, 1, 0],
  forward: [0, 0, -1],
  verticalFieldOfView: 0.7,
  width: 100,
  height: 100
};

function layer(id: string, kind: string, children: StackLayer[] = []): StackLayer {
  return {
    filterNode: '',
    id,
    name: id,
    kind,
    enabled: true,
    opacity: 1,
    blend: 'Copy',
    channels: [],
    painted: false,
    children,
    fill: 'Constant',
    values: {},
    textures: {},
    graph: '',
    projection: 'Uv',
    planarAxis: 'Y',
    filter: 'Levels',
    settings: {},
    mask: { source: 'None', value: 1, asset: '', anchor: '', generator: '', map: '', tolerance: 0.02, softness: 0 }
  };
}

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
    layers: [layer('base', 'fill'), layer('paint', 'paint')],
    previews: [],
    settled: 0,
    model: '',
    ...overrides
  };
  return state;
}

function setup() {
  const sockets: GraphSocket[] = [];
  const bodies: unknown[] = [];
  const picks: unknown[] = [];
  const posts: string[] = [];
  const fetcher = (async (input: Request) => {
    const path = new URL(input.url).pathname;
    posts.push(path);
    if (path.endsWith('/commands')) {
      bodies.push(await input.json());
      return Response.json({ revision: 2, sequence: 4, history: { canUndo: true, canRedo: false, dirty: true } });
    }

    if (path.endsWith('/viewport/pick')) {
      picks.push(await input.json());
      return Response.json({ found: true, view: PickedView });
    }

    if (path.endsWith('/stacks/vocabulary')) {
      return Response.json({ blends: [], filters: [], masks: [], generators: [], meshMaps: [], alphas: ['Round'] });
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
  return { store: TestBed.inject(PaintStore), bodies, picks, posts, sockets };
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
    expect(store.layers().map(row => row.layer.id)).toEqual(['paint', 'base']);
  });

  it('sends a drag as stamps and closes it with the last part', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.move({ x: 3, y: 4, pressure: 0.5 });
    store.end();
    await vi.waitFor(() => {
      const parts = bodies.flatMap(body => (body as { commands: { open: boolean; stamps: unknown[] }[] }).commands);
      expect(parts.at(-1)?.open).toBe(false);
      expect(parts.flatMap(part => part.stamps)).toEqual([
        { x: 1, y: 2, pressure: 1 },
        { x: 3, y: 4, pressure: 0.5 }
      ]);
    });
  });

  it('lists a group above what it holds, top first and indented', () => {
    const rows = rowsOf([layer('base', 'fill'), layer('group', 'group', [layer('a', 'paint'), layer('b', 'filter')])]);
    expect(rows.map(row => [row.layer.id, row.depth])).toEqual([
      ['group', 0],
      ['b', 1],
      ['a', 1],
      ['base', 0]
    ]);
  });

  it('takes pressure from a pen and not from a mouse', () => {
    expect(pressureOf({ pointerType: 'mouse', pressure: 0.5 } as PointerEvent)).toBe(1);
    expect(pressureOf({ pointerType: 'pen', pressure: 0.3 } as PointerEvent)).toBe(0.3);
  });

  it('asks the Worker where a stroke on the mesh is, and every part carries the camera it was given', async () => {
    const { store, bodies, picks } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.mirror.set('x');
    const aim = { width: 100, height: 100, camera: { yaw: 0, pitch: 0, distance: 2 } };
    store.begin({ x: 50, y: 25, pressure: 1 }, aim);
    store.move({ x: 60, y: 25, pressure: 1 });
    store.end();
    await vi.waitFor(() => {
      const parts = bodies.flatMap(
        body => (body as { commands: { open: boolean; view: unknown; mirror: string }[] }).commands
      );
      expect(parts.at(-1)?.open).toBe(false);
      expect(parts.every(part => part.mirror === 'x')).toBe(true);
      expect(parts.every(part => part.view !== null)).toBe(true);
    });
    expect(picks).toEqual([{ x: 0.5, y: 0.25, camera: aim.camera, width: 100, height: 100 }]);
  });

  it('holds an undo until the drag it follows has reached the Server', async () => {
    const { store, bodies, posts } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.move({ x: 3, y: 4, pressure: 1 });
    store.move({ x: 5, y: 6, pressure: 1 });
    store.end();
    await store.act('undo');
    const undone = posts.findIndex(path => path.endsWith('/undo'));
    expect(undone).toBeGreaterThan(posts.map(path => path.endsWith('/commands')).lastIndexOf(true));
    const parts = bodies.flatMap(body => (body as { commands: { open: boolean }[] }).commands);
    expect(parts.at(-1)?.open).toBe(false);
  });

  it('never mirrors a stroke on the atlas, since a mirror is on the mesh', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.mirror.set('x');
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.end();
    await vi.waitFor(() => {
      expect(bodies.flatMap(body => (body as { commands: { mirror: unknown }[] }).commands)[0]?.mirror).toBeNull();
    });
  });

  it('runs a line from press to release as one stroke with two points', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.tool.set('line');
    const gesture = new StrokeGesture(store, () => undefined);
    gesture.press({ x: 1, y: 1, pressure: 1 });
    gesture.move({ x: 5, y: 5, pressure: 1 });
    gesture.move({ x: 9, y: 9, pressure: 1 });
    expect(gesture.points()).toHaveLength(2);
    expect(bodies).toHaveLength(0);
    gesture.release();
    await vi.waitFor(() => {
      const [part] = bodies.flatMap(body => (body as { commands: { curve: string; stamps: unknown[] }[] }).commands);
      expect(part?.curve).toBe('line');
      expect(part?.stamps).toHaveLength(2);
    });
    expect(gesture.points()).toHaveLength(0);
  });

  it('collects a path until it is finished, and cancelling sends nothing', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.tool.set('path');
    const gesture = new StrokeGesture(store, () => undefined);
    for (const x of [1, 2, 3]) {
      gesture.press({ x, y: 1, pressure: 1 });
      gesture.release();
    }

    expect(gesture.points()).toHaveLength(3);
    gesture.cancel();
    gesture.press({ x: 1, y: 1, pressure: 1 });
    gesture.release();
    gesture.press({ x: 2, y: 2, pressure: 1 });
    gesture.release();
    gesture.finish();
    await vi.waitFor(() => {
      const parts = bodies.flatMap(body => (body as { commands: { curve: string; stamps: unknown[] }[] }).commands);
      expect(parts).toHaveLength(1);
      expect(parts[0]?.curve).toBe('path');
      expect(parts[0]?.stamps).toHaveLength(2);
    });
  });

  it('times a stamp to the first frame that shows its revision, and not an earlier one', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.begin({ x: 1, y: 2, pressure: 1 });
    await vi.waitFor(() => expect(bodies).toHaveLength(1));
    await new Promise(done => setTimeout(done));
    store.framePainted(1);
    expect(store.stampTimes()).toHaveLength(0);
    store.framePainted(2);
    expect(store.stampTimes()).toHaveLength(1);
    store.framePainted(2);
    expect(store.stampTimes()).toHaveLength(1);
  });

  it('keeps a field under the hand when an older echo arrives late', () => {
    const server = signal(0.2);
    const field = TestBed.runInInjectionContext(() => new LiveValue(() => server()));
    expect(field.shown()).toBe(0.2);
    field.edit(0.9);
    server.set(0.4);
    expect(field.shown()).toBe(0.9);
    field.release();
    expect(field.shown()).toBe(0.4);
  });

  it('shows four numbers on a swatch', () => {
    expect(hexOf([1, 0, 0.5, 1])).toBe('#ff0080');
    expect(hexOf(undefined)).toBe('#000000');
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
        evaluation: { previews: [{ usage: 'baseColor', hash: 'aa', width: 512, height: 512 }], diagnostics: [] }
      })
    } as MessageEvent);
    await vi.waitFor(() => expect(store.picture()).toBe('blob:map'));
    expect(store.state()?.history.canUndo).toBe(true);
  });
});
