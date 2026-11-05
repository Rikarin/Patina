import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import {
  type Connection,
  type PatinaClient,
  type StackCommandBatch,
  type StackEvent,
  type StackLayer,
  type StackPreview,
  type StackStamp,
  type StackState,
  unwrap
} from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';

const ReconnectMs = 1_000;

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

  /** The layers top first, as a stack reads, with the ones a brush can reach marked by their kind. */
  readonly layers = computed<readonly StackLayer[]>(() => [...(this.state()?.layers ?? [])].reverse());

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
      this.adopt(state);
      await this.follow();
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }

  /** Adds a layer to paint on, at the top. */
  async addLayer(): Promise<void> {
    await this.command({ commands: [{ op: 'addLayer', kind: 'paint', name: 'Paint' }] });
    await this.refresh();
  }

  async setEnabled(layer: StackLayer, enabled: boolean): Promise<void> {
    await this.command({ commands: [{ op: 'setLayer', layer: layer.id, enabled }] });
    await this.refresh();
  }

  async act(action: 'undo' | 'redo' | 'save'): Promise<void> {
    const id = this.state()?.id;
    if (!id) {
      return;
    }

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

  /** The brush came down on the paint layer chosen. */
  begin(stamp: StackStamp): void {
    this.strokes += 1;
    this.stroke = `drag-${Date.now()}-${this.strokes}`;
    this.move(stamp);
  }

  move(stamp: StackStamp): void {
    if (this.stroke) {
      this.pending.push(stamp);
      this.flush(true);
    }
  }

  /** The brush came up: the last part closes the undo step. */
  end(): void {
    if (this.stroke) {
      this.flush(false);
    }
  }

  private flush(open: boolean): void {
    if (this.sending) {
      if (!open) {
        void this.sending.then(() => this.flush(false));
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
    this.sending = this.send(stroke, layer, stamps, open).finally(() => {
      this.sending = null;
      if (this.pending.length > 0) {
        this.flush(open && this.stroke !== null);
      }
    });
    if (!open) {
      this.stroke = null;
    }
  }

  private async send(stroke: string, layer: string, stamps: StackStamp[], open: boolean): Promise<void> {
    await this.command({
      commands: [
        {
          op: 'stroke',
          stroke,
          layer,
          channel: this.channel(),
          brush: { radius: this.radius(), flow: 1, opacity: 1, spacing: 0.15, falloff: 0.5 },
          colour: colourOf(this.colour()),
          stamps,
          open,
          mask: false
        }
      ]
    });
  }

  private async command(body: StackCommandBatch): Promise<void> {
    const id = this.state()?.id;
    if (!id) {
      return;
    }

    try {
      const client = await this.api();
      unwrap(
        await client.POST('/api/v1/stacks/{id}/commands', {
          params: { path: { id } },
          body
        })
      );
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }

  private adopt(state: StackState): void {
    this.state.set(state);
    this.sequence = state.sequence;
    this.channel.update(channel =>
      state.channels.some(c => c.usage === channel) ? channel : (state.channels[0]?.usage ?? channel)
    );
    if (!state.layers.some(layer => layer.id === this.layer())) {
      this.layer.set([...state.layers].reverse().find(layer => layer.kind === 'paint')?.id ?? null);
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

  private async api(): Promise<PatinaClient> {
    this.client ??= this.createClient(await this.connection());
    return this.client;
  }
}

/** A colour swatch's value as the four numbers a stroke carries. */
export function colourOf(hex: string): number[] {
  const value = Number.parseInt(hex.slice(1), 16);
  return [((value >> 16) & 255) / 255, ((value >> 8) & 255) / 255, (value & 255) / 255, 1];
}
