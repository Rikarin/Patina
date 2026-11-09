import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type StackLayer, type StackState, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { hexOf } from './layer-panel';
import { LiveValue } from './live-value';
import {
  NewBrush,
  PaintStore,
  colourOf,
  pressureOf,
  rowsOf,
  stackSocketUrl,
  stackSummary,
  toTexels,
  toggledEffect
} from './paint-store';
import { StrokeGesture, snapped, trailed } from './stroke-gesture';

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
    anchorPoint: '',
    flattened: false,
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
    mask: { source: 'None', value: 1, asset: '', generator: '', map: '', tolerance: 0.02, softness: 0, lane: 'Red' }
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
    seed: 0,
    heightNormal: 'Smooth',
    ...overrides
  };
  return state;
}

function setup() {
  const sockets: GraphSocket[] = [];
  const bodies: unknown[] = [];
  const picks: unknown[] = [];
  const posts: string[] = [];
  /** What the Server answers to an undo, a redo or a save, when a test wants it to refuse. */
  const steps: { reply: (() => Response) | null; queries: string[] } = { reply: null, queries: [] };
  /** What a test's Server answers before the usual, by method and path, such as one that has restarted. */
  const server: { reply: ((method: string, path: string) => Response | null) | null } = { reply: null };
  const fetcher = (async (input: Request) => {
    const { pathname: path, search } = new URL(input.url);
    posts.push(path);
    const answered = server.reply?.(input.method, path);
    if (answered) {
      return answered;
    }

    if (/\/(?:undo|redo|save)$/u.test(path)) {
      steps.queries.push(search);
      if (steps.reply) {
        return steps.reply();
      }
    }

    if (path.endsWith('/commands')) {
      bodies.push(await input.json());
      return Response.json({ revision: 2, sequence: 4, history: { canUndo: true, canRedo: false, dirty: true } });
    }

    if (path.endsWith('/viewport/pick')) {
      picks.push(await input.json());
      return Response.json({ found: true, view: PickedView });
    }

    if (path.endsWith('/clip')) {
      bodies.push(await input.json());
      return Response.json({ id: 'c1', layers: 1, stack: '/a/Crate.vxlayers' });
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
  return { store: TestBed.inject(PaintStore), bodies, picks, posts, sockets, steps, server };
}

const notOpen = () =>
  Response.json(
    { detail: "There is no open stack 's1'.", code: 'stack-not-open', values: { id: 's1' } },
    { status: 404 }
  );

/** What a socket does when its Server goes away. */
function drop(socket: GraphSocket | undefined): void {
  (socket?.onclose as unknown as (() => void) | null)?.();
}

const problem = (status: number, detail: string) => () => Response.json({ detail }, { status });

describe('PaintStore saving', () => {
  async function opened() {
    const test = setup();
    await test.store.open('/a/Crate.vxlayers', false);
    return test;
  }

  it('says why a save failed, where other failures go', async () => {
    const { store, steps } = await opened();
    steps.reply = problem(503, 'No Worker came.');

    expect(await store.act('save')).toBe(false);

    expect(store.failure()).toBe('No Worker came.');
    expect(store.saveConflict()).toBe(false);
  });

  it('asks about a file that changed instead of calling it a failure', async () => {
    const { store, steps } = await opened();
    steps.reply = problem(409, 'It changed on disk.');

    expect(await store.act('save')).toBe(false);

    expect(store.saveConflict()).toBe(true);
    expect(store.failure()).toBeNull();
  });

  it('overwrites only when asked, and the question goes', async () => {
    const { store, steps } = await opened();
    steps.reply = problem(409, 'It changed on disk.');
    await store.act('save');
    steps.reply = null;

    expect(await store.act('save', true)).toBe(true);

    expect(steps.queries).toEqual(['', '?force=true']);
    expect(store.saveConflict()).toBe(false);
  });

  it('takes nothing left to undo as an answer', async () => {
    const { store, steps } = await opened();
    steps.reply = problem(409, 'There is nothing to undo.');

    expect(await store.act('undo')).toBe(false);

    expect(store.failure()).toBeNull();
    expect(store.saveConflict()).toBe(false);
  });

  it('says why an undo failed', async () => {
    const { store, steps } = await opened();
    steps.reply = problem(500, 'It broke.');

    expect(await store.act('undo')).toBe(false);

    expect(store.failure()).toBe('It broke.');
  });

  it('starts over when something new starts', async () => {
    const { store, steps } = await opened();
    steps.reply = problem(503, 'No Worker came.');
    await store.act('save');
    steps.reply = null;

    await store.act('save');

    expect(store.failure()).toBeNull();
  });
});

describe('PaintStore copies', () => {
  it('copies the outermost of what is selected, and a cut removes them after', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.state.set(stack({ layers: [layer('base', 'fill'), layer('folder', 'group', [layer('inner', 'paint')])] }));
    store.layer.set('folder');
    store.marked.set(new Set(['inner']));

    await store.cutLayers();

    expect(bodies).toEqual([{ layers: ['folder'] }, { commands: [{ op: 'removeLayer', layer: 'folder' }] }]);
    expect(store.clip()?.id).toBe('c1');
  });

  it('pastes the last copy above the chosen layer, and duplicates what is selected', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.layer.set('paint');
    await store.copyLayers();

    await store.pasteLayers();
    await store.duplicateLayers();

    expect(bodies.slice(1)).toEqual([
      { commands: [{ op: 'pasteLayers', clip: 'c1', above: 'paint', asInstances: false }] },
      { commands: [{ op: 'duplicateLayers', layers: ['paint'] }] }
    ]);
  });

  it('pastes nothing before anything is copied', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);

    await store.pasteLayers();

    expect(bodies).toEqual([]);
  });
});

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

  it('paints a mask in grey, whatever the swatch says, and in one channel', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.colour.set('#0000ff');
    store.alsoPaints.set(new Map([['roughness', '#808080']]));
    store.masking.set(true);
    store.maskGrey.set(0.25);
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.end();
    await vi.waitFor(() => {
      const batches = bodies.map(body => (body as { commands: { colour: number[]; open: boolean }[] }).commands);
      expect(batches.at(-1)?.[0]?.open).toBe(false);
      expect(batches.every(batch => batch.length === 1)).toBe(true);
      expect(batches[0]?.[0]?.colour).toEqual([0.25, 0.25, 0.25, 1]);
    });
    expect(store.recentColours()).toEqual([]);
  });

  it('paints an entry of a mask stack by its place, and only a paint entry (#109)', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    const base = store.state()?.layers[0] as StackLayer;
    const stacked: StackLayer = {
      ...base,
      mask: {
        ...base.mask,
        source: 'Generator',
        generator: 'Generators/Dirt',
        layers: [{ source: 'Constant' }, { source: 'Paint' }] as never
      }
    };
    store.state.update(state => (state ? { ...state, layers: [stacked] } : state));

    store.chooseMaskLayer(stacked, 0);
    expect(store.canPaint()).toBe(false);
    store.chooseMaskLayer(stacked, 1);
    expect(store.canPaint()).toBe(true);
    store.chooseMask(stacked);
    expect(store.maskLayer()).toBeNull();
    expect(store.canPaint()).toBe(false);

    store.chooseMaskLayer(stacked, 1);
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.end();
    await vi.waitFor(() => {
      const first = (bodies[0] as { commands: { mask: boolean; maskLayer: number | null }[] }).commands[0];
      expect([first?.mask, first?.maskLayer]).toEqual([true, 1]);
    });
  });

  it('starts a brush on a flat mask as what would show on it', () => {
    const { store } = setup();
    const masked = (value: number): StackLayer => {
      const held = layer('a', 'fill');
      return { ...held, mask: { ...held.mask, source: 'Constant', value } };
    };
    store.chooseMask(masked(0));
    expect(store.maskGrey()).toBe(1);
    expect(store.masking()).toBe(true);
    store.chooseMask(masked(1));
    expect(store.maskGrey()).toBe(0);
  });

  it('offers a stroke only where the Server would take it', async () => {
    const { store } = setup();
    await store.open('/a/Crate.vxlayers', false);
    expect(store.layer()).toBe('paint');
    expect(store.canPaint()).toBe(true);
    expect(store.canPaintMesh()).toBe(false);

    store.layer.set('base');
    expect(store.canPaint()).toBe(false);
    store.masking.set(true);
    expect(store.canPaint()).toBe(true);

    const base = store.state()?.layers[0];
    const masked = (source: string) => ({ ...(base as StackLayer), mask: { ...(base as StackLayer).mask, source } });
    store.state.update(state => (state ? { ...state, model: 'crate.glb' } : state));
    expect(store.canPaintMesh()).toBe(true);
    for (const [source, paintable] of [
      ['Constant', true],
      ['Paint', true],
      ['Generator', false],
      ['Texture', false],
      ['Bake', false],
      ['Anchor', false]
    ] as const) {
      store.state.update(state => (state ? { ...state, layers: [masked(source)] } : state));
      expect(store.canPaint()).toBe(paintable);
    }
  });

  it('offers no brush on a channel the layer leaves alone, and a material brush skips it', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.state.update(state =>
      state
        ? { ...state, layers: state.layers.map(held => (held.id === 'paint' ? { ...held, channels: ['roughness'] } : held)) }
        : state
    );
    expect(store.canPaint()).toBe(false);

    store.channel.set('roughness');
    expect(store.canPaint()).toBe(true);
    store.alsoPaints.set(new Map([['baseColor', '#808080']]));
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.end();
    await vi.waitFor(() => {
      const batches = bodies.map(body => (body as { commands: { channel: string }[] }).commands);
      expect(batches[0]?.map(command => command.channel)).toEqual(['roughness']);
    });
  });

  it('says what the stack is doing, as a graph does', () => {
    const evaluation = {
      revision: 2,
      compiled: false,
      ran: 3,
      repaired: 0,
      reused: 0,
      milliseconds: 12.4,
      previews: [],
      diagnostics: [],
      error: null,
      problems: []
    };
    expect(stackSummary({ revision: 3, settled: 2, evaluation })).toBe('Evaluating…');
    expect(stackSummary({ revision: 2, settled: 2, evaluation })).toBe('3 ran, 12 ms');
    expect(stackSummary({ revision: 2, settled: 2, evaluation: { ...evaluation, ran: 0 } })).toBe('Up to date');
    const stopped = { ...evaluation, problems: [{ layer: 'a', severity: 'Error', message: 'No.' }] };
    expect(stackSummary({ revision: 2, settled: 2, evaluation: stopped })).toBe('Not evaluated');
  });

  it('opens the stack again from its file after the Server restarts, keeping the layer chosen (#226)', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const { store, sockets, server, posts } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.layer.set('paint');
    store.state.update(state => (state ? { ...state, history: { ...state.history, dirty: true } } : state));
    const reopened = stack({ id: 's2', sequence: 0 });
    server.reply = (method, path) =>
      path === '/api/v1/stacks/s1' ? notOpen() : method === 'POST' && path === '/api/v1/stacks' ? Response.json(reopened) : null;

    drop(sockets[0]);
    await vi.advanceTimersByTimeAsync(1_000);

    expect(posts).toContain('/api/v1/stacks/s1');
    expect(store.state()?.id).toBe('s2');
    expect(store.layer()).toBe('paint');
    expect(store.notice()).toBe('Patina restarted its engine. Edits since the last save are gone.');
    expect(sockets.at(-1)).not.toBe(sockets[0]);
    expect(sockets).toHaveLength(2);
    vi.useRealTimers();
  });

  it('keeps asking while the Server is down, and follows the same stack when it kept it', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const { store, sockets, server, posts } = setup();
    await store.open('/a/Crate.vxlayers', false);
    let down = true;
    server.reply = (_method, path) => {
      if (down && path === '/api/v1/stacks/s1') {
        throw new TypeError('fetch failed');
      }

      return null;
    };

    drop(sockets[0]);
    await vi.advanceTimersByTimeAsync(2_000);
    expect(posts.filter(path => path === '/api/v1/stacks/s1').length).toBeGreaterThan(1);
    expect(sockets).toHaveLength(1);

    down = false;
    await vi.advanceTimersByTimeAsync(5_000);
    expect(sockets).toHaveLength(2);
    expect(store.state()?.id).toBe('s1');
    expect(store.notice()).toBeNull();
    vi.useRealTimers();
  });

  it('hears what a compile said, and keeps the worst for each row', async () => {
    const { store, sockets } = setup();
    await store.open('/a/Crate.vxlayers', false);
    const hear = sockets[0]?.onmessage as unknown as (event: MessageEvent) => void;
    hear({
      data: JSON.stringify({
        sequence: 5,
        kind: 'evaluated',
        revision: 2,
        history: { canUndo: false, canRedo: false, dirty: true },
        evaluation: {
          revision: 2,
          previews: [],
          diagnostics: [],
          error: null,
          problems: [
            { layer: 'base', severity: 'Warning', message: 'Half set.' },
            { layer: 'base', severity: 'Error', message: 'A loop.' },
            { layer: '', severity: 'Error', message: 'Of the stack.' }
          ]
        }
      })
    } as MessageEvent);
    expect(store.problems()).toHaveLength(3);
    expect(store.faults().get('base')?.message).toBe('A loop.');
    expect(store.faults().size).toBe(1);
  });

  it('turns an effect on, and off again rather than twice', () => {
    const once = toggledEffect([{ node: 'Filters/Blur', enabled: true }], 'Colour/Invert');
    expect(once.map(effect => effect.node)).toEqual(['Filters/Blur', 'Colour/Invert']);
    expect(toggledEffect(once, 'Colour/Invert').map(effect => effect.node)).toEqual(['Filters/Blur']);
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

  it('shows a flattened folder as its canvas, with its layers kept out of sight', () => {
    const folder = { ...layer('group', 'group', [layer('a', 'paint')]), flattened: true, painted: true };
    expect(rowsOf([layer('base', 'fill'), folder]).map(row => row.layer.id)).toEqual(['group', 'base']);
  });

  it('offers a brush on a flattened folder, whose canvas is its picture', async () => {
    const { store } = setup();
    await store.open('/a/Crate.vxlayers', false);
    const folder = { ...layer('group', 'group', [layer('a', 'fill')]), flattened: true, painted: true };
    store.state.update(state => (state ? { ...state, layers: [folder] } : state));
    store.layer.set('group');
    expect(store.canPaint()).toBe(true);
    store.state.update(state => (state ? { ...state, layers: [{ ...folder, flattened: false }] } : state));
    expect(store.canPaint()).toBe(false);
  });

  it('flattens a folder, the whole stack, and back, each as one command', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    await store.flatten('group');
    await store.flatten();
    await store.unflatten('group');
    expect(bodies.slice(-3)).toEqual([
      { commands: [{ op: 'flattenLayer', layer: 'group' }] },
      { commands: [{ op: 'flattenLayer', layer: null }] },
      { commands: [{ op: 'unflattenLayer', layer: 'group' }] }
    ]);
  });

  it('takes pressure from a pen and not from a mouse', () => {
    expect(pressureOf({ pointerType: 'mouse', pressure: 0.5 } as PointerEvent)).toBe(1);
    expect(pressureOf({ pointerType: 'pen', pressure: 0.3 } as PointerEvent)).toBe(0.3);
  });

  it('asks the Worker where a stroke on the mesh is, and every part carries the camera it was given', async () => {
    const { store, bodies, picks } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.state.update(state => (state ? { ...state, model: 'crate.glb' } : state));
    store.symmetry.set({ kind: 'mirror', axis: 'x', centre: 'model', offset: 0, count: 2 });
    const aim = {
      width: 100,
      height: 100,
      camera: { yaw: 0, pitch: 0, distance: 2, panX: 0, panY: 0, panZ: 0, exposure: 1, lightRotation: 0 }
    };
    store.begin({ x: 50, y: 25, pressure: 1 }, aim);
    store.move({ x: 60, y: 25, pressure: 1 });
    store.end();
    await vi.waitFor(() => {
      const parts = bodies.flatMap(
        body => (body as { commands: { open: boolean; view: unknown; symmetry: { axis: string } | null }[] }).commands
      );
      expect(parts.at(-1)?.open).toBe(false);
      expect(parts.every(part => part.symmetry?.axis === 'x')).toBe(true);
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

  it('copies a stroke on the map too when there is a mesh, and never sends a symmetry with none', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.symmetry.set({ kind: 'radial', axis: 'y', centre: 'model', offset: 0, count: 6 });
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.end();
    store.state.update(state => (state ? { ...state, model: 'crate.glb' } : state));
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.end();
    await vi.waitFor(() => {
      const parts = bodies.flatMap(body => (body as { commands: { symmetry: { count: number } | null }[] }).commands);
      expect(parts[0]?.symmetry).toBeNull();
      expect(parts.length).toBeGreaterThan(1);
      expect(parts.slice(1).every(part => part.symmetry?.count === 6)).toBe(true);
    });
  });

  it('sends a size in the screen\'s units on the map as texels of the map as drawn, and one in texels as it is', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.radius.set(10);
    store.atlasScale.set(4);
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.end();
    store.brush.update(brush => ({ ...brush, sizeSpace: 'texture' }));
    store.begin({ x: 1, y: 2, pressure: 1 });
    store.end();
    await vi.waitFor(() => {
      const parts = bodies.flatMap(body => (body as { commands: { brush: { radius: number } }[] }).commands);
      expect([parts[0]?.brush.radius, parts.at(-1)?.brush.radius]).toEqual([40, 10]);
    });
  });

  it('puts every setting of the brush back to a new one, its size among them', () => {
    const { store } = setup();
    store.radius.set(300);
    store.brush.update(brush => ({ ...brush, aspect: 6, flowJitter: 0.4, accumulate: false }));
    store.resetBrush();
    expect(store.wholeBrush()).toEqual(NewBrush);
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

  it('draws a Shift-click line on from where the lazy brush stopped, and snaps it with Cmd', async () => {
    const { store, bodies } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.brush.update(brush => ({ ...brush, lazy: 10 }));
    const gesture = new StrokeGesture(store, () => undefined);
    expect(gesture.canLine()).toBe(false);

    gesture.press({ x: 0, y: 0, pressure: 1 });
    gesture.move({ x: 30, y: 0, pressure: 1 });
    gesture.release();
    expect(gesture.canLine()).toBe(true);
    gesture.press({ x: 50, y: 1, pressure: 1 }, { line: true, snap: true });

    await vi.waitFor(() => {
      const parts = bodies.flatMap(
        body => (body as { commands: { curve: string; stamps: { x: number; y: number }[] }[] }).commands
      );
      const line = parts.find(part => part.curve === 'line');
      expect(line?.stamps[0]).toMatchObject({ x: 20, y: 0 });
      expect(line?.stamps[1]?.y).toBeCloseTo(0, 5);
      expect(line?.stamps[1]?.x).toBeCloseTo(20 + Math.hypot(30, 1), 5);
    });
  });

  it('draws no line through a camera that has moved since the stroke', async () => {
    const { store } = setup();
    await store.open('/a/Crate.vxlayers', false);
    let width = 100;
    const gesture = new StrokeGesture(store, () => ({ width, height: 100, camera: {} as never }));
    gesture.press({ x: 0, y: 0, pressure: 1 });
    gesture.release();
    expect(gesture.canLine()).toBe(true);

    width = 120;

    expect(gesture.canLine()).toBe(false);
  });

  it('trails a pointer by a distance, and turns an end to the nearest snap', () => {
    expect(trailed({ x: 0, y: 0, pressure: 1 }, { x: 3, y: 4, pressure: 1 }, 5)).toMatchObject({ x: 0, y: 0 });
    expect(trailed({ x: 0, y: 0, pressure: 1 }, { x: 9, y: 12, pressure: 1 }, 5)).toMatchObject({ x: 6, y: 8 });
    const end = snapped({ x: 0, y: 0, pressure: 1 }, { x: 10, y: 0.4, pressure: 1 });
    expect(end.y).toBeCloseTo(0, 5);
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

  it('takes one point from a double click', () => {
    const { store } = setup();
    store.tool.set('path');
    const gesture = new StrokeGesture(store, () => undefined);
    for (const x of [1, 2, 2]) {
      gesture.press({ x, y: 1, pressure: 1 });
      gesture.release();
    }

    expect(gesture.points()).toHaveLength(2);
  });

  it('aims a path through the camera its first point was placed with', async () => {
    const { store, picks } = setup();
    await store.open('/a/Crate.vxlayers', false);
    store.tool.set('path');
    const camera = { yaw: 0, pitch: 0, distance: 2, panX: 0, panY: 0, panZ: 0, exposure: 1, lightRotation: 0 };
    let yaw = 0;
    const gesture = new StrokeGesture(store, () => ({
      width: 100,
      height: 100,
      camera: { ...camera, yaw: yaw++ }
    }));
    for (const x of [10, 20, 30]) {
      gesture.press({ x, y: 1, pressure: 1 });
      gesture.release();
    }

    gesture.finish();
    await vi.waitFor(() => expect(picks).toHaveLength(1));
    expect((picks[0] as { camera: { yaw: number } }).camera.yaw).toBe(0);
  });

  it('lets go of a mask waiting for its colour', () => {
    const { store } = setup();
    store.picking.set('paint');
    new StrokeGesture(store, () => undefined).cancel();
    expect(store.picking()).toBeNull();
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
