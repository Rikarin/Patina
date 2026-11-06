import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import {
  type Connection,
  type PatinaClient,
  type StackBrush,
  type StackCommand,
  type StackCommandBatch,
  type StackEvent,
  type StackIdColour,
  type StackLayer,
  type StackMask,
  type StackPreview,
  type StackStamp,
  type StackState,
  type StackView,
  type StackVocabulary,
  type ViewportCameraState,
  unwrap
} from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';

const ReconnectMs = 1_000;

/** How many stamp-to-frame times are kept, which is what the readout and the bench read. */
const StampTimes = 2048;

/** What a stroke draws with, beyond its radius. */
export type BrushSettings = Omit<StackBrush, 'radius'>;

export type PaintTool = 'brush' | 'line' | 'path';
export type Mirror = 'none' | 'x' | 'y' | 'z';

/** Where a stroke on the mesh is aimed: a picture of this size through this camera. */
export interface Aim {
  width: number;
  height: number;
  camera: ViewportCameraState;
}

/** A layer with how deep in groups it is, top of the stack first. */
/** How the Worker says a stack read mesh maps with no bake, before their names. */
const NeedsBake = 'Needs a bake: ';

export interface LayerRow {
  layer: StackLayer;
  depth: number;
  /** The folder it is in, or null at the top of the stack. */
  parent: string | null;
}

/** The stack as a list, each group above what it holds. */
export function rowsOf(layers: readonly StackLayer[], depth = 0, parent: string | null = null): LayerRow[] {
  return [...layers]
    .reverse()
    .flatMap(layer => [{ layer, depth, parent }, ...rowsOf(layer.children, depth + 1, layer.id)]);
}

/** Where a client follows a stack from. */
export function stackSocketUrl(connection: Connection, id: string, since: number): string {
  const url = new URL(`/ws/stacks/${id}`, connection.baseUrl);
  url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
  url.searchParams.set('since', String(since));
  if (connection.token) {
    url.searchParams.set('access_token', connection.token);
  }

  return url.href;
}

/** A view position as a position in the stack's texels. */
export function toTexels(x: number, y: number, viewWidth: number, viewHeight: number, side: readonly [number, number]) {
  return { x: (x / viewWidth) * side[0], y: (y / viewHeight) * side[1] };
}

/**
 * One open stack, as a client of it. The Server owns the document and its history, so this is a copy kept current by
 * numbered events. A stroke goes out as lists of stamps: what the brush did, never what it left, batched per frame so
 * that a drag faster than the Server sends bigger parts. The picture is the map the Server composited, by hash.
 */
@Injectable({ providedIn: 'root' })
export class PaintStore {
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly sockets = inject(GRAPH_SOCKET);
  private client: PatinaClient | null = null;
  private socket: GraphSocket | null = null;
  private reconnect: ReturnType<typeof setTimeout> | null = null;
  private sequence = 0;
  private stopped = false;
  private stroke: string | null = null;
  private strokes = 0;
  private pending: StackStamp[] = [];
  private view: StackView | null = null;
  private aiming = false;
  private ending = false;
  private oldestStamp = 0;
  private lastShown = 0;
  private readonly sentAt = new Map<number, number>();
  private sending: Promise<void> | null = null;
  private shownUrl: string | null = null;
  private wanted: string | null = null;

  readonly state = signal<StackState | null>(null);
  readonly failure = signal<string | null>(null);
  readonly layer = signal<string | null>(null);
  readonly channel = signal('baseColor');
  readonly radius = signal(24);
  readonly colour = signal('#d9401a');
  readonly picture = signal<string | null>(null);
  readonly tool = signal<PaintTool>('brush');
  readonly mirror = signal<Mirror>('none');
  /** Whether strokes go to the chosen layer's mask rather than its channels. */
  readonly masking = signal(false);
  readonly brush = signal<BrushSettings>({
    flow: 1,
    opacity: 1,
    spacing: 0.15,
    falloff: 0.5,
    alpha: 'Round',
    angle: 0,
    rotation: 'fixed',
    positionJitter: 0,
    angleJitter: 0,
    sizeJitter: 0,
    pressureSize: 0,
    pressureFlow: 0
  });
  readonly vocabulary = signal<StackVocabulary | null>(null);
  /** The colours of the id map this stack reads, once asked for, or null with no such map. */
  readonly idColours = signal<readonly StackIdColour[] | null>(null);
  /** The layer whose mask is taking its colour from the next click on the mesh or the map. */
  readonly picking = signal<string | null>(null);
  /** The mesh maps layers want and no bake has given, which read as grey until one does. */
  readonly unbaked = signal<string | null>(null);
  /** Whether the viewport is wearing this stack, so a stroke can be made on the mesh. */
  readonly worn = signal(false);
  /** From a stamp leaving the pointer to the first frame painted with it, newest last, in milliseconds. */
  readonly stampTimes = signal<readonly number[]>([]);

  /** The layers top first, as a stack reads, with the ones a brush can reach marked by their kind. */
  readonly layers = computed<readonly LayerRow[]>(() => rowsOf(this.state()?.layers ?? []));
  readonly chosen = computed(() => this.layers().find(row => row.layer.id === this.layer())?.layer ?? null);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.stopped = true;
      this.socket?.close();
      if (this.reconnect !== null) {
        clearTimeout(this.reconnect);
      }

      if (this.shownUrl) {
        URL.revokeObjectURL(this.shownUrl);
      }
    });
  }

  /** Opens a stack, and writes a new one first when asked. Throws nothing: the pane shows the failure. */
  async open(path: string, create: boolean): Promise<void> {
    this.failure.set(null);
    try {
      const client = await this.api();
      const state = unwrap(
        create
          ? await client.POST('/api/v1/stacks/new', { body: { path, width: 2048, height: 2048 } })
          : await client.POST('/api/v1/stacks', { body: { path } })
      );
      this.worn.set(false);
      this.adopt(state);
      await this.follow();
      this.vocabulary.set(unwrap(await client.GET('/api/v1/stacks/vocabulary')));
      if (state.model) {
        await this.wear();
      }
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }

  /** Reads what the id map holds. No id map is an answer, not a failure: the mask says what to do about it. */
  async loadIdColours(): Promise<void> {
    const id = this.state()?.id;
    if (!id) {
      return;
    }

    const client = await this.api();
    const reply = await client.GET('/api/v1/stacks/{id}/id-colours', { params: { path: { id }, query: {} } });
    this.idColours.set(reply.data?.colours ?? null);
  }

  /** Takes the colour at a place on the id map for the mask waiting for one. */
  async pickColourAt(u: number, v: number): Promise<void> {
    const id = this.state()?.id;
    const layer = this.layers().find(row => row.layer.id === this.picking())?.layer;
    this.picking.set(null);
    if (!id || !layer) {
      return;
    }

    try {
      const client = await this.api();
      const params = { path: { id }, query: { u, v } };
      const colour = unwrap(await client.GET('/api/v1/stacks/{id}/id-colours', { params })).at;
      if (colour) {
        await this.setMask(layer, { ...layer.mask, colour: [...colour] });
      }
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }

  /** Takes the colour under a place on the mesh in the viewport. */
  async pickColourOnMesh(stamp: StackStamp, aim: Aim): Promise<void> {
    const client = await this.api();
    const { width, height, camera } = aim;
    const hit = unwrap(
      await client.POST('/api/v1/viewport/pick', {
        body: { x: stamp.x / width, y: stamp.y / height, camera, width, height }
      })
    );
    if (hit.found) {
      await this.pickColourAt(hit.u, hit.v);
    } else {
      this.picking.set(null);
    }
  }

  /** Lets go of the stack, which stays open on the Server with its edits and history, to come back to. */
  detach(): void {
    this.state.set(null);
    this.worn.set(false);
    this.picture.set(null);
    this.layer.set(null);
    this.idColours.set(null);
    this.picking.set(null);
    this.unbaked.set(null);
    const socket = this.socket;
    this.socket = null;
    socket?.close();
  }

  /** Adds a layer at the top of the stack, or of the chosen group, and chooses it. */
  async addLayer(kind = 'paint', name = 'Paint', filter?: string): Promise<void> {
    const chosen = this.chosen();
    const parent = chosen?.kind === 'group' ? chosen.id : null;
    const before = new Set(this.layers().map(row => row.layer.id));
    await this.command({ commands: [{ op: 'addLayer', kind, name, parent, filter: filter ?? null }] });
    await this.refresh();
    const added = this.layers().find(row => !before.has(row.layer.id));
    if (added) {
      this.layer.set(added.layer.id);
    }
  }

  async setEnabled(layer: StackLayer, enabled: boolean): Promise<void> {
    await this.edit({ op: 'setLayer', layer: layer.id, enabled });
  }

  /** Adds a layer filled from a material graph, whose maps reach the channels of the same usage. */
  async addGraphLayer(name: string, graph: string): Promise<void> {
    await this.addLayer('fill', name);
    const layer = this.layer();
    if (layer) {
      await this.edit({ op: 'setLayer', layer, fill: 'Graph', graph });
    }
  }

  /** One edit of the document, whose result the stack's events and this read bring back. */
  async edit(...commands: StackCommand[]): Promise<void> {
    await this.command({ commands });
    await this.refresh();
  }

  /** Binds a mesh and puts the stack on it in the viewport. */
  async bindMesh(path: string): Promise<void> {
    await this.edit({ op: 'setModel', model: path });
    await this.wear();
  }

  /** Puts the stack on its mesh in the viewport, which shows every stroke from then on. */
  async wear(): Promise<void> {
    const stack = this.state()?.id;
    if (!stack) {
      return;
    }

    try {
      const client = await this.api();
      unwrap(await client.PUT('/api/v1/viewport/stack', { body: { stack } }));
      this.worn.set(true);
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }

  /** The mesh the viewport shows, for binding what is already on screen. */
  async viewportMesh(): Promise<string | null> {
    const client = await this.api();
    return unwrap(await client.GET('/api/v1/viewport')).mesh ?? null;
  }

  /** Chooses the bake generators read. */
  async useMeshMaps(folder: string | null): Promise<void> {
    const id = this.state()?.id;
    if (!id) {
      return;
    }

    try {
      const client = await this.api();
      unwrap(await client.PUT('/api/v1/stacks/{id}/mesh-maps', { params: { path: { id } }, body: { folder } }));
      await this.refresh();
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }

  setMask(layer: StackLayer, mask: StackMask): Promise<void> {
    return this.edit({ op: 'setLayer', layer: layer.id, mask });
  }

  /** A frame reached the screen showing this revision of the stack. */
  framePainted(revision: number): void {
    this.lastShown = Math.max(this.lastShown, revision);
    const now = performance.now();
    const times: number[] = [];
    for (const [sent, at] of this.sentAt) {
      if (sent <= revision) {
        times.push(now - at);
        this.sentAt.delete(sent);
      }
    }

    if (times.length > 0) {
      this.stampTimes.update(held => [...held, ...times].slice(-StampTimes));
    }
  }

  async act(action: 'undo' | 'redo' | 'save'): Promise<void> {
    const id = this.state()?.id;
    if (!id) {
      return;
    }

    // An undo that overtook the end of a drag would take back the part before it and leave the rest standing.
    await this.drained();
    const client = await this.api();
    const path = { params: { path: { id } } };
    unwrap(
      await (action === 'undo'
        ? client.POST('/api/v1/stacks/{id}/undo', path)
        : action === 'redo'
          ? client.POST('/api/v1/stacks/{id}/redo', path)
          : client.POST('/api/v1/stacks/{id}/save', { ...path, params: { ...path.params, query: {} } }))
    );
    await this.refresh();
  }

  /** Waits until every stamp the pointer made has reached the Server. */
  private async drained(): Promise<void> {
    while (this.sending || this.aiming || this.ending || this.pending.length > 0) {
      await (this.sending ?? new Promise(done => setTimeout(done)));
    }
  }

  /** The brush came down on the layer chosen: on the atlas, or on the mesh when it is aimed. */
  begin(stamp: StackStamp, aim?: Aim): void {
    this.strokes += 1;
    this.stroke = `drag-${Date.now()}-${this.strokes}`;
    this.view = null;
    if (aim) {
      void this.aimAt(stamp, aim);
    }

    this.move(stamp);
  }

  move(stamp: StackStamp): void {
    if (this.stroke) {
      if (this.pending.length === 0) {
        this.oldestStamp = performance.now();
      }

      this.pending.push(stamp);
      this.flush(true);
    }
  }

  /** The brush came up: the last part closes the undo step. */
  end(): void {
    if (this.stroke) {
      this.ending = true;
      this.flush(false);
    }
  }

  /** A line or a path, all at once: the Worker runs the stroke through these points. */
  async paintCurve(points: readonly StackStamp[], curve: 'line' | 'path', aim?: Aim): Promise<void> {
    const layer = this.layer();
    const [first] = points;
    if (!layer || !first || points.length < 2) {
      return;
    }

    this.strokes += 1;
    const stroke = `${curve}-${Date.now()}-${this.strokes}`;
    const view = aim ? await this.pick(first, aim) : null;
    await this.send(stroke, layer, [...points], false, view, curve, performance.now());
  }

  /** The camera comes from the Server, and the stroke's first part waits for it: a stamp means nothing without it. */
  private async aimAt(stamp: StackStamp, aim: Aim): Promise<void> {
    this.aiming = true;
    try {
      this.view = await this.pick(stamp, aim);
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
      this.stroke = null;
      this.ending = false;
      this.pending = [];
    } finally {
      this.aiming = false;
    }

    if (this.stroke) {
      this.flush(true);
    }
  }

  private async pick(stamp: StackStamp, aim: Aim): Promise<StackView> {
    const client = await this.api();
    const { width, height, camera } = aim;
    return unwrap(
      await client.POST('/api/v1/viewport/pick', {
        body: { x: stamp.x / width, y: stamp.y / height, camera, width, height }
      })
    ).view;
  }

  private flush(open: boolean): void {
    if (this.sending || this.aiming) {
      if (!open) {
        void (this.sending ?? Promise.resolve()).then(() => setTimeout(() => this.flush(false)));
      }

      return;
    }

    const stroke = this.stroke;
    const layer = this.layer();
    if (!stroke || !layer || (open && this.pending.length === 0)) {
      return;
    }

    const stamps = this.pending;
    this.pending = [];
    this.sending = this.send(stroke, layer, stamps, open, this.view, 'free', this.oldestStamp).finally(() => {
      this.sending = null;
      if (this.pending.length > 0) {
        this.flush(open && this.stroke !== null);
      }
    });
    if (!open) {
      this.stroke = null;
      this.ending = false;
    }
  }

  private async send(
    stroke: string,
    layer: string,
    stamps: StackStamp[],
    open: boolean,
    view: StackView | null,
    curve: 'free' | 'line' | 'path',
    since: number
  ): Promise<void> {
    const mirror = this.mirror();
    const revision = await this.command({
      commands: [
        {
          op: 'stroke',
          stroke,
          layer,
          channel: this.channel(),
          brush: { ...this.brush(), radius: this.radius() },
          colour: colourOf(this.colour()),
          stamps,
          open,
          mask: this.masking(),
          view,
          curve,
          mirror: view && mirror !== 'none' ? mirror : null
        }
      ]
    });
    if (revision !== null && revision > this.lastShown) {
      this.sentAt.set(revision, since);
    }
  }

  /** Sends a batch, and answers the revision it made, or null when it was refused. */
  private async command(body: StackCommandBatch): Promise<number | null> {
    const id = this.state()?.id;
    if (!id) {
      return null;
    }

    try {
      const client = await this.api();
      return unwrap(
        await client.POST('/api/v1/stacks/{id}/commands', {
          params: { path: { id } },
          body
        })
      ).revision;
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
      return null;
    }
  }

  private adopt(state: StackState): void {
    this.state.set(state);
    this.sequence = state.sequence;
    this.channel.update(channel =>
      state.channels.some(c => c.usage === channel) ? channel : (state.channels[0]?.usage ?? channel)
    );
    const rows = rowsOf(state.layers);
    if (!rows.some(row => row.layer.id === this.layer())) {
      this.layer.set(rows.find(row => row.layer.kind === 'paint')?.layer.id ?? null);
    }

    this.show(state.previews);
  }

  /** Shows a channel, whose picture may already be here. */
  showChannel(channel: string): void {
    this.channel.set(channel);
    this.show(this.state()?.previews ?? []);
  }

  private show(previews: readonly StackPreview[]): void {
    const preview = previews.find(p => p.usage === this.channel());
    if (preview) {
      void this.load(preview.hash);
    }
  }

  private async load(hash: string): Promise<void> {
    this.wanted = hash;
    const client = await this.api();
    const data = unwrap(await client.GET('/api/v1/previews/{hash}', { params: { path: { hash } }, parseAs: 'blob' }));
    if (this.wanted === hash) {
      const url = URL.createObjectURL(data);
      if (this.shownUrl) {
        URL.revokeObjectURL(this.shownUrl);
      }

      this.shownUrl = url;
      this.picture.set(url);
    }
  }

  private async refresh(): Promise<void> {
    const id = this.state()?.id;
    if (id) {
      const client = await this.api();
      this.adopt(unwrap(await client.GET('/api/v1/stacks/{id}', { params: { path: { id } } })));
    }
  }

  private async follow(): Promise<void> {
    const id = this.state()?.id;
    if (!id || this.stopped) {
      return;
    }

    this.socket?.close();
    const socket = this.sockets(stackSocketUrl(await this.connection(), id, this.sequence));
    this.socket = socket;
    socket.onmessage = message => {
      this.hear(JSON.parse(String(message.data)) as StackEvent);
    };
    socket.onclose = () => {
      if (!this.stopped && this.socket === socket) {
        this.reconnect = setTimeout(() => void this.follow(), ReconnectMs);
      }
    };
  }

  private hear(event: StackEvent): void {
    this.sequence = event.sequence;
    this.state.update(state => (state ? { ...state, history: event.history, revision: event.revision } : state));
    if (event.evaluation) {
      this.heardNotes(event.evaluation.diagnostics);
    }

    const previews = event.evaluation?.previews ?? [];
    if (previews.length > 0) {
      this.state.update(state =>
        state
          ? {
              ...state,
              previews: [...state.previews.filter(p => !previews.some(n => n.usage === p.usage)), ...previews]
            }
          : state
      );
      this.show(previews);
    }

    if (event.delta) {
      const layers = event.delta.layers;
      this.state.update(state => (state ? { ...state, layers: [...layers] } : state));
    }
  }

  private heardNotes(notes: readonly string[]): void {
    const note = notes.find(held => held.startsWith(NeedsBake));
    this.unbaked.set(note ? note.slice(NeedsBake.length) : null);
  }

  private async api(): Promise<PatinaClient> {
    this.client ??= this.createClient(await this.connection());
    return this.client;
  }
}

/** How hard a pointer is pressing: a mouse says 0.5 for a button held, and means nothing by it. */
export function pressureOf(event: PointerEvent): number {
  return event.pointerType === 'pen' ? event.pressure : 1;
}

/** A colour swatch's value as the four numbers a stroke carries. */
export function colourOf(hex: string): number[] {
  const value = Number.parseInt(hex.slice(1), 16);
  return [((value >> 16) & 255) / 255, ((value >> 8) & 255) / 255, (value & 255) / 255, 1];
}
