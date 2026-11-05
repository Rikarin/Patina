import { DestroyRef, Injectable, InjectionToken, computed, effect, inject, signal, untracked } from '@angular/core';
import {
  ApiError,
  type Connection,
  type GraphCommand,
  type GraphEditResult,
  type GraphEvent,
  type GraphNodeType,
  type GraphState,
  type PatinaClient,
  unwrap
} from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { LibrarySelection } from '../library/library-selection';
import { DraftLayer } from './draft-layer';
import { EditQueue } from './edit-queue';
import { type GraphDocument, applyEvent, classify, documentOf } from './graph-model';
import { PreviewLoader } from './preview-loader';

export type GraphSessionState =
  | { kind: 'none' }
  | { kind: 'opening'; path: string }
  | { kind: 'failed'; path: string; reason: string }
  | { kind: 'open' }
  | { kind: 'ended'; path: string; name: string };

/** Whether events are arriving. Edits wait for `live`, since an edit nobody can see land is not an edit. */
export type GraphLink = 'live' | 'reconnecting';

/** Leaving a graph with unsaved edits: to open another, or just to close it. */
export type GraphLeaving = { kind: 'open'; path: string } | { kind: 'close' };

export interface GraphPoint {
  x: number;
  y: number;
}

/** What a graph's follower needs of a WebSocket. */
export type GraphSocket = Pick<WebSocket, 'onopen' | 'onmessage' | 'onclose' | 'close'>;

/** Opens a socket; tests replace it. Lives outside the OpenAPI document, like the socket itself. */
export const GRAPH_SOCKET = new InjectionToken<(url: string) => GraphSocket>('GRAPH_SOCKET', {
  factory: () => url => new WebSocket(url)
});

const FirstRetryMs = 250;
const LastRetryMs = 4_000;

type DraftValue = readonly number[] | string;

const valueKey = (node: number, port: string): string => `${String(node)}/${port}`;

function reasonOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/** The socket for a graph: the browser cannot set a header on one, so the token goes in the query. */
export function socketUrl(connection: Connection, id: string, since: number): string {
  const url = new URL(`/ws/graphs/${id}`, connection.baseUrl);
  url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
  url.searchParams.set('since', String(since));
  if (connection.token) {
    url.searchParams.set('access_token', connection.token);
  }

  return url.toString();
}

/**
 * The open graph, as a client of the Server. The Server owns the document, and everything here is a copy kept current
 * by its numbered events, this client's own edits included: an edit goes out as a command and comes back as a delta.
 * The only thing held ahead of the Server is what the user's hand is doing right now ({@link DraftLayer}).
 *
 * After a dropped socket it asks for the events since the last one it applied; a Server that has forgotten them, or a
 * gap, means reading the state again; a Server that restarted has lost the session, so the file is opened again.
 */
@Injectable({ providedIn: 'root' })
export class GraphStore {
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly openSocket = inject(GRAPH_SOCKET);
  private readonly mutableSession = signal<GraphSessionState>({ kind: 'none' });
  private readonly mutableDocument = signal<GraphDocument | null>(null);
  private readonly mutableTypes = signal<ReadonlyMap<string, GraphNodeType>>(new Map());
  private readonly mutableLink = signal<GraphLink>('live');
  private readonly mutableProblem = signal<string | null>(null);
  private readonly mutableNotice = signal<string | null>(null);
  private readonly mutableSelection = signal<ReadonlySet<number>>(new Set());
  private readonly mutableLeaving = signal<GraphLeaving | null>(null);
  private readonly mutableSaveConflict = signal(false);
  private readonly positionDrafts = new DraftLayer<GraphPoint>();
  private readonly valueDrafts = new DraftLayer<DraftValue>();
  private readonly edits = new EditQueue<GraphEditResult>();
  private previews = this.newLoader();
  private currentConnection: Connection | null = null;
  private client: PatinaClient | null = null;
  private socket: GraphSocket | null = null;
  private retry: ReturnType<typeof setTimeout> | null = null;
  private retryMs = FirstRetryMs;
  /** Goes up with each session, so that what an old one was waiting for lands nowhere. */
  private epoch = 0;
  private reloading = false;
  private held: GraphEvent[] = [];
  /** The live edit in progress, whose batches after the first fold into one undo step. */
  private gesture: string | null = null;
  /** The nodes of the drag in progress. */
  private readonly moving = new Map<number, GraphPoint>();
  private moveScheduled = false;
  private evaluateAsked = false;
  private stopped = false;

  readonly session = this.mutableSession.asReadonly();
  readonly document = this.mutableDocument.asReadonly();
  readonly types = this.mutableTypes.asReadonly();
  readonly link = this.mutableLink.asReadonly();
  /** Why the last edit did not happen. */
  readonly problem = this.mutableProblem.asReadonly();
  /** Something the user should know about what happened to the session. */
  readonly notice = this.mutableNotice.asReadonly();
  readonly selection = this.mutableSelection.asReadonly();
  /** What is waiting while the user decides what to do with unsaved edits. */
  readonly leaving = this.mutableLeaving.asReadonly();
  /** A save refused because the file changed on disk. */
  readonly saveConflict = this.mutableSaveConflict.asReadonly();
  readonly positions = this.positionDrafts.values;

  /** The one node the Inspector and the texture view follow. */
  readonly selectedNode = computed(() => {
    const document = this.mutableDocument();
    const [first] = this.mutableSelection();
    return (first === undefined ? undefined : document?.nodes.get(first)) ?? null;
  });

  readonly editable = computed(() => this.mutableDocument() !== null && this.mutableLink() === 'live');

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.stopped = true;
      this.stopFollowing();
      this.previews.dispose();
    });

    // One selection on screen: the Inspector shows what was touched last, a library item or a node.
    const library = inject(LibrarySelection);
    let seen = library.item();
    effect(() => {
      const item = library.item();
      if (item !== seen) {
        seen = item;
        untracked(() => {
          this.select([]);
        });
      }
    });
  }

  /** A node's thumbnail. */
  thumbnail(node: number) {
    return this.previews.shown(node);
  }

  /** A library-relative path or an absolute one. With unsaved edits open, it asks first. */
  async open(path: string): Promise<void> {
    const document = this.mutableDocument();
    if (document && (document.path === path || document.path.endsWith(`/${path}`))) {
      return;
    }

    if (document?.history.dirty) {
      this.mutableLeaving.set({ kind: 'open', path });
      return;
    }

    if (document) {
      await this.closeSession(false);
    }

    await this.start(path);
  }

  /** Closes the session on the Server, which ends it for every client that joined. Unsaved edits ask first. */
  async close(): Promise<void> {
    if (this.mutableDocument()?.history.dirty) {
      this.mutableLeaving.set({ kind: 'close' });
      return;
    }

    await this.closeSession(false);
  }

  /** Answers {@link leaving}. */
  async resolveLeaving(choice: 'save' | 'discard' | 'cancel'): Promise<void> {
    const leaving = this.mutableLeaving();
    this.mutableLeaving.set(null);
    if (leaving === null || choice === 'cancel' || (choice === 'save' && !(await this.save()))) {
      return;
    }

    await this.closeSession(choice === 'discard');
    if (leaving.kind === 'open') {
      await this.start(leaving.path);
    }
  }

  /** Tries the file that failed to open, or that another client closed. */
  async reopen(): Promise<void> {
    const session = this.mutableSession();
    if (session.kind === 'failed' || session.kind === 'ended') {
      await this.start(session.path);
    }
  }

  select(nodes: Iterable<number>): void {
    const next = new Set(nodes);
    const held = this.mutableSelection();
    if (next.size !== held.size || [...next].some(node => !held.has(node))) {
      this.mutableSelection.set(next);
    }
  }

  dismissProblem(): void {
    this.mutableProblem.set(null);
  }

  dismissNotice(): void {
    this.mutableNotice.set(null);
  }

  /** The numbers an input shows: the hand's, then the document's, then the type's default. */
  valueOf(node: number, port: string, fallback: readonly number[]): readonly number[] {
    const draft = this.valueDrafts.values().get(valueKey(node, port));
    if (Array.isArray(draft)) {
      return draft as readonly number[];
    }

    return this.mutableDocument()?.nodes.get(node)?.values[port] ?? fallback;
  }

  /** The text a setting shows. */
  textOf(node: number, setting: string, fallback: string): string {
    const draft = this.valueDrafts.values().get(valueKey(node, setting));
    if (typeof draft === 'string') {
      return draft;
    }

    return this.mutableDocument()?.nodes.get(node)?.texts[setting] ?? fallback;
  }

  /** Adds a node, and wires it to the port a dropped wire came from. */
  async addNode(
    type: GraphNodeType,
    position: GraphPoint,
    from?: { node: number; port: string; incoming: boolean }
  ): Promise<void> {
    const reference = -1;
    const commands: GraphCommand[] = [{ op: 'addNode', type: type.path, x: position.x, y: position.y, ref: reference }];
    if (from) {
      const wanted = from.incoming ? 'Output' : 'Input';
      const port = type.ports.find(candidate => candidate.direction === wanted && candidate.kind === 'Image');
      if (port) {
        commands.push(
          from.incoming
            ? { op: 'connect', fromNode: reference, fromPort: port.name, toNode: from.node, toPort: from.port }
            : { op: 'connect', fromNode: from.node, fromPort: from.port, toNode: reference, toPort: port.name }
        );
      }
    }

    const result = await this.apply(commands, `Add ${type.title}`);
    const created = result?.created.find(item => item.ref === reference);
    if (created) {
      this.select([created.node]);
    }
  }

  remove(nodes: readonly number[], edges: readonly string[]): void {
    const document = this.mutableDocument();
    const commands: GraphCommand[] = [];
    for (const key of edges) {
      const edge = document?.edges.get(key);
      if (edge) {
        commands.push({ op: 'disconnect', toNode: edge.toNode, toPort: edge.toPort });
      }
    }

    if (nodes.length > 0) {
      commands.push({ op: 'removeNodes', nodes: [...nodes] });
    }

    if (commands.length > 0) {
      void this.apply(commands, null);
    }
  }

  connect(fromNode: number, fromPort: string, toNode: number, toPort: string): void {
    void this.apply([{ op: 'connect', fromNode, fromPort, toNode, toPort }], null);
  }

  disconnect(toNode: number, toPort: string): void {
    void this.apply([{ op: 'disconnect', toNode, toPort }], null);
  }

  /**
   * A node moved under the hand. A selection moves as several calls in one tick, so they go out as one batch, and every
   * batch of a drag lists the same nodes: the Server folds a batch into the last only if they match.
   */
  moveNode(node: number, position: GraphPoint): void {
    this.positionDrafts.set(String(node), position);
    this.moving.set(node, position);
    if (!this.moveScheduled) {
      this.moveScheduled = true;
      queueMicrotask(() => {
        this.moveScheduled = false;
        this.sendMoves();
      });
    }
  }

  /** The drag has ended. Later in the tick, since a keyboard nudge moves and ends in one. */
  endMove(): void {
    queueMicrotask(() => {
      this.moving.clear();
      this.endGesture();
    });
  }

  /** Sets the numbers on an input. Live: a slider sends this on every step. */
  setValue(node: number, port: string, value: readonly number[]): void {
    this.sendDraft(`value/${valueKey(node, port)}`, valueKey(node, port), value, {
      op: 'setValue',
      node,
      port,
      value: [...value]
    });
  }

  /** Sets a text setting, such as a blend mode. */
  setText(node: number, port: string, value: string): void {
    this.sendDraft(`text/${valueKey(node, port)}`, valueKey(node, port), value, { op: 'setText', node, port, value });
  }

  /** The hand has let go, so the next live edit is a new undo step. */
  endGesture(): void {
    this.gesture = null;
  }

  undo(): void {
    this.walk('undo');
  }

  redo(): void {
    this.walk('redo');
  }

  /** Writes the file. False if it did not happen. */
  async save(force = false): Promise<boolean> {
    const id = this.mutableDocument()?.id;
    if (id === undefined) {
      return false;
    }

    this.endGesture();
    this.mutableSaveConflict.set(false);
    try {
      await this.edits.submit(async () =>
        unwrap(
          await this.api().POST('/api/v1/graphs/{id}/save', {
            params: { path: { id }, query: { force } }
          })
        )
      );
      return true;
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        this.mutableSaveConflict.set(true);
      } else {
        this.mutableProblem.set(reasonOf(error));
      }

      return false;
    }
  }

  dismissSaveConflict(): void {
    this.mutableSaveConflict.set(false);
  }

  private newLoader(): PreviewLoader {
    return new PreviewLoader(
      {
        fetch: async hash =>
          unwrap(await this.api().GET('/api/v1/previews/{hash}', { params: { path: { hash } }, parseAs: 'blob' })),
        url: blob => URL.createObjectURL(blob),
        revoke: url => {
          URL.revokeObjectURL(url);
        }
      },
      () => {
        this.askToEvaluate();
      }
    );
  }

  /** Previews went missing, so the cache was deleted: an evaluation writes them again (ADR 0005). */
  private askToEvaluate(): void {
    const id = this.mutableDocument()?.id;
    if (id === undefined || this.evaluateAsked) {
      return;
    }

    this.evaluateAsked = true;
    void this.api()
      .POST('/api/v1/graphs/{id}/evaluate', { params: { path: { id } } })
      .catch(() => {
        this.evaluateAsked = false; // the reconnect will have another look
      });
  }

  private api(): PatinaClient {
    if (!this.client) {
      throw new Error('The Server has not been reached yet.');
    }

    return this.client;
  }

  /** The Server as it is now: a restarted one has a new port and token. */
  private async reconnect(): Promise<PatinaClient> {
    this.currentConnection = await this.connection();
    this.client = this.createClient(this.currentConnection);
    return this.client;
  }

  private sendMoves(): void {
    const moved = new Map(this.moving);
    const positions = [...moved].map(([node, { x, y }]) => ({ node, x, y }));
    this.sendLive(
      'move',
      { op: 'moveNodes', positions },
      (sequence, seen) => {
        for (const [node, position] of moved) {
          this.positionDrafts.settle(String(node), position, sequence, seen);
        }
      },
      () => {
        for (const node of moved.keys()) {
          this.positionDrafts.drop(String(node));
        }
      }
    );
  }

  private sendDraft(gesture: string, key: string, value: DraftValue, command: GraphCommand): void {
    this.valueDrafts.set(key, value);
    this.sendLive(
      gesture,
      command,
      (sequence, seen) => {
        this.valueDrafts.settle(key, value, sequence, seen);
      },
      () => {
        this.valueDrafts.drop(key);
      }
    );
  }

  /**
   * A live edit: the first batch of a gesture opens an undo step and the rest fold into it, and one waiting to go out is
   * replaced by the next ({@link EditQueue}). Its draft goes once the event announcing it has been applied.
   */
  private sendLive(
    gesture: string,
    command: GraphCommand,
    settle: (sequence: number, seen: number) => void,
    drop: () => void
  ): void {
    const id = this.mutableDocument()?.id;
    if (id === undefined) {
      return;
    }

    const merge = this.gesture === gesture;
    this.gesture = gesture;
    this.edits
      .submit(
        async folded =>
          unwrap(
            await this.api().POST('/api/v1/graphs/{id}/commands', {
              params: { path: { id } },
              body: { commands: [command], merge: folded }
            })
          ),
        { key: gesture, merge }
      )
      .then(
        result => {
          settle(result.sequence, this.mutableDocument()?.sequence ?? 0);
        },
        (error: unknown) => {
          this.refused(error);
          drop();
        }
      );
  }

  private async apply(commands: GraphCommand[], label: string | null): Promise<GraphEditResult | null> {
    const id = this.mutableDocument()?.id;
    if (id === undefined) {
      return null;
    }

    this.endGesture();
    try {
      return await this.edits.submit(async () =>
        unwrap(
          await this.api().POST('/api/v1/graphs/{id}/commands', {
            params: { path: { id } },
            body: { commands, merge: false, ...(label === null ? {} : { label }) }
          })
        )
      );
    } catch (error) {
      this.refused(error);
      return null;
    }
  }

  private walk(direction: 'undo' | 'redo'): void {
    const id = this.mutableDocument()?.id;
    if (id === undefined) {
      return;
    }

    this.endGesture();
    this.edits
      .submit(async () =>
        unwrap(
          await this.api().POST(direction === 'undo' ? '/api/v1/graphs/{id}/undo' : '/api/v1/graphs/{id}/redo', {
            params: { path: { id } }
          })
        )
      )
      .catch((error: unknown) => {
        this.refused(error);
      });
  }

  private refused(error: unknown): void {
    this.mutableProblem.set(reasonOf(error));
  }

  private async start(path: string): Promise<void> {
    const epoch = this.endSession();
    this.mutableSession.set({ kind: 'opening', path });
    try {
      const client = await this.reconnect();
      const state = unwrap(await client.POST('/api/v1/graphs', { body: { path } }));
      await this.adopt(state, epoch);
    } catch (error) {
      if (epoch === this.epoch) {
        this.mutableSession.set({ kind: 'failed', path, reason: reasonOf(error) });
      }
    }
  }

  private async adopt(state: GraphState, epoch: number): Promise<void> {
    const types = unwrap(
      await this.api().GET('/api/v1/graphs/{id}/node-types', { params: { path: { id: state.id } } })
    );
    if (epoch !== this.epoch) {
      return;
    }

    this.mutableTypes.set(new Map(types.map(type => [type.path, type])));
    this.accept(documentOf(state));
    this.mutableSession.set({ kind: 'open' });
    this.follow(epoch);
  }

  /** Takes a whole document, as on opening or after a resync. */
  private accept(document: GraphDocument): void {
    const before = this.mutableDocument();
    this.mutableDocument.set(document);
    for (const id of before?.nodes.keys() ?? []) {
      if (!document.nodes.has(id)) {
        this.previews.forget(id);
      }
    }

    for (const preview of document.previews.values()) {
      this.previews.want(preview);
    }

    this.keepSelectionReal(document);
    this.positionDrafts.reached(document.sequence);
    this.valueDrafts.reached(document.sequence);
  }

  private keepSelectionReal(document: GraphDocument): void {
    const held = this.mutableSelection();
    if ([...held].some(node => !document.nodes.has(node))) {
      this.mutableSelection.set(new Set([...held].filter(node => document.nodes.has(node))));
    }
  }

  private follow(epoch: number): void {
    const document = this.mutableDocument();
    const connection = this.currentConnection;
    if (!document || !connection || epoch !== this.epoch || this.stopped) {
      return;
    }

    const socket = this.openSocket(socketUrl(connection, document.id, document.sequence));
    this.socket = socket;
    socket.onopen = () => {
      if (socket === this.socket) {
        this.retryMs = FirstRetryMs;
        this.mutableLink.set('live');
      }
    };
    socket.onmessage = message => {
      if (socket === this.socket) {
        this.arrive(JSON.parse(String(message.data)) as GraphEvent);
      }
    };
    socket.onclose = () => {
      if (socket === this.socket) {
        this.socket = null;
        this.mutableLink.set('reconnecting');
        this.scheduleRetry(epoch);
      }
    };
  }

  private scheduleRetry(epoch: number): void {
    if (this.stopped || epoch !== this.epoch || this.retry !== null) {
      return;
    }

    this.retry = setTimeout(() => {
      this.retry = null;
      void this.resume(epoch);
    }, this.retryMs);
    this.retryMs = Math.min(this.retryMs * 2, LastRetryMs);
  }

  /**
   * Back after a dropped socket. Asking for the session first tells a Server that is down from one that restarted and
   * has no such session, which a failed WebSocket handshake cannot say.
   */
  private async resume(epoch: number): Promise<void> {
    const document = this.mutableDocument();
    if (!document) {
      return;
    }

    try {
      const client = await this.reconnect();
      unwrap(await client.GET('/api/v1/graphs/{id}', { params: { path: { id: document.id } } }));
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        await this.reopenLost(document, epoch);
        return;
      }

      this.scheduleRetry(epoch);
      return;
    }

    this.follow(epoch);
  }

  /** The Server restarted, and edits it had not saved went with it. The file is the truth (plan §3.1). */
  private async reopenLost(document: GraphDocument, epoch: number): Promise<void> {
    try {
      const state = unwrap(await this.api().POST('/api/v1/graphs', { body: { path: document.path } }));
      if (document.history.dirty) {
        this.mutableNotice.set('The Server restarted. Edits since the last save are gone.');
      }

      this.edits.clear(new Error('The session ended.'));
      await this.adopt(state, epoch);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        this.endSession();
        this.mutableSession.set({ kind: 'failed', path: document.path, reason: error.message });
      } else {
        this.scheduleRetry(epoch);
      }
    }
  }

  private arrive(event: GraphEvent): void {
    if (this.reloading) {
      this.held.push(event);
      return;
    }

    const document = this.mutableDocument();
    if (!document) {
      return;
    }

    switch (classify(document, event)) {
      case 'seen':
        return;
      case 'reload':
        void this.reload(document.id);
        return;
      case 'closed':
        this.ended(document);
        return;
      case 'apply':
        this.applyArrival(document, event);
    }
  }

  private applyArrival(document: GraphDocument, event: GraphEvent): void {
    const next = applyEvent(document, event);
    this.mutableDocument.set(next);
    for (const id of event.delta?.removedNodes ?? []) {
      this.previews.forget(id);
    }

    for (const preview of event.evaluation?.previews ?? []) {
      if (next.nodes.has(preview.node)) {
        this.previews.want(preview);
      }
    }

    if (event.kind === 'evaluated') {
      this.evaluateAsked = false;
      this.previews.retryLost();
    }

    this.keepSelectionReal(next);
    this.positionDrafts.reached(event.sequence);
    this.valueDrafts.reached(event.sequence);
  }

  /** Something was missed, so the whole state replaces what is held, and events that came meanwhile follow it. */
  private async reload(id: string): Promise<void> {
    this.reloading = true;
    const epoch = this.epoch;
    try {
      const state = unwrap(await this.api().GET('/api/v1/graphs/{id}', { params: { path: { id } } }));
      if (epoch === this.epoch) {
        this.accept(documentOf(state));
      }
    } catch {
      this.socket?.close(); // a failed read is a lost Server: the reconnect path takes it from here
    } finally {
      this.reloading = false;
    }

    for (const event of this.held.splice(0)) {
      this.arrive(event);
    }
  }

  private ended(document: GraphDocument): void {
    this.endSession();
    this.mutableSession.set({ kind: 'ended', path: document.path, name: document.name });
  }

  private async closeSession(discard: boolean): Promise<void> {
    const document = this.mutableDocument();
    if (!document) {
      this.endSession();
      this.mutableSession.set({ kind: 'none' });
      return;
    }

    // Off the socket first, or the session's last event would read as another client closing it.
    this.stopFollowing();
    try {
      await this.api().DELETE('/api/v1/graphs/{id}', {
        params: { path: { id: document.id }, query: { discard } }
      });
    } catch (error) {
      if (!(error instanceof ApiError && error.status === 404)) {
        this.mutableProblem.set(reasonOf(error));
        this.follow(this.epoch);
        return;
      }
    }

    this.endSession();
    this.mutableSession.set({ kind: 'none' });
  }

  /** Lets go of everything the session held. Returns the new epoch. */
  private endSession(): number {
    this.stopFollowing();
    this.edits.clear(new Error('The session ended.'));
    this.previews.dispose();
    this.previews = this.newLoader();
    this.positionDrafts.clear();
    this.valueDrafts.clear();
    this.gesture = null;
    this.moving.clear();
    this.held = [];
    this.reloading = false;
    this.evaluateAsked = false;
    this.mutableDocument.set(null);
    this.mutableSelection.set(new Set());
    this.mutableLink.set('live');
    this.mutableProblem.set(null);
    this.mutableSaveConflict.set(false);
    return ++this.epoch;
  }

  private stopFollowing(): void {
    if (this.retry !== null) {
      clearTimeout(this.retry);
      this.retry = null;
    }

    const socket = this.socket;
    this.socket = null;
    socket?.close();
  }
}
