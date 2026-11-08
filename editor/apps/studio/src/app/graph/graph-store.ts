import { DestroyRef, Injectable, InjectionToken, computed, effect, inject, signal, untracked } from '@angular/core';
import {
  ApiError,
  type Connection,
  type GraphCommand,
  type GraphCompound,
  type GraphEditResult,
  type GraphEvent,
  type GraphEdgeState,
  type GraphInterfacePort,
  type GraphNodeType,
  type GraphPresetState,
  type GraphSettingDefinition,
  type GraphState,
  type PatinaClient,
  unwrap
} from '@patina/api-client';
import { desktop } from '../core/desktop';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { LibrarySelection } from '../library/library-selection';
import { DraftLayer } from './draft-layer';
import { EditQueue } from './edit-queue';
import { type GraphDocument, applyEvent, classify, documentOf, edgeKey } from './graph-model';
import { PreviewLoader } from './preview-loader';
import { messageOf } from '../core/messages';

export type GraphSessionState =
  | { kind: 'none' }
  | { kind: 'opening'; path: string }
  | { kind: 'failed'; path: string; reason: string }
  | { kind: 'open' }
  | { kind: 'ended'; path: string; name: string };

/** Whether events are arriving. Edits wait for `live`, since an edit nobody can see land is not an edit. */
export type GraphLink = 'live' | 'reconnecting';

/** Leaving a graph with unsaved edits: to open another, to close it, to go back up a level, or to quit. */
export type GraphLeaving =
  { kind: 'open'; path: string } | { kind: 'close' } | { kind: 'back'; index: number } | { kind: 'quit' };

/** A graph the user went through to reach the compound on screen. Its session stays open, edits and undo included. */
export interface GraphCrumb {
  id: string;
  path: string;
  name: string;
  dirty: boolean;
}

export interface GraphPoint {
  x: number;
  y: number;
}

/**
 * What a new node is wired to: an output it takes, an input it drives, or a wire it goes into the middle of. A port left
 * out is the first that fits, which the Server finds.
 */
export type NodeLink =
  | { kind: 'from'; node: number; port?: string }
  | { kind: 'to'; node: number; port: string }
  | { kind: 'wire'; node: number; port: string; toNode: number; toPort: string };

/** The kinds of wire a node must suit: one it can take on an input, and an input it can drive. */
export interface NodeWire {
  takes?: string | undefined;
  gives?: string | undefined;
}

/** What a graph's follower needs of a WebSocket. */
export type GraphSocket = Pick<WebSocket, 'onopen' | 'onmessage' | 'onclose' | 'close'>;

/** Opens a socket; tests replace it. Lives outside the OpenAPI document, like the socket itself. */
export const GRAPH_SOCKET = new InjectionToken<(url: string) => GraphSocket>('GRAPH_SOCKET', {
  factory: () => url => new WebSocket(url)
});

const PasteOffset = 40;
const FirstRetryMs = 250;
const LastRetryMs = 4_000;

type DraftValue = readonly number[] | string;

const valueKey = (node: number, port: string): string => `${String(node)}/${port}`;
const noteKey = (id: number): string => `note/${String(id)}`;
const parameterKey = (name: string): string => `parameter/${name}`;
const GroupPrefix = 'group-';
const ShippedPrefix = 'shipped:';
/** A reroute dot, which Vixen stores as a node of this type with one input and one output. */
export const RerouteType = 'Reroute';
export const RerouteIn = 'In';
export const RerouteOut = 'Out';

function reasonOf(error: unknown): string {
  return messageOf(error);
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
  private readonly mutableShownPorts = signal<ReadonlySet<string>>(new Set());
  private readonly mutableLeaving = signal<GraphLeaving | null>(null);
  private readonly mutableSaveConflict = signal(false);
  private readonly mutableCompounds = signal<ReadonlyMap<string, GraphCompound>>(new Map());
  private readonly mutableTrail = signal<readonly GraphCrumb[]>([]);
  private readonly mutableNote = signal<number | null>(null);
  private selectionDue: { nodes: readonly number[]; revision: number } | null = null;
  private readonly mutableGraphInspected = signal(false);
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
  /** A node the library asked for, which the canvas places in the middle of what it shows. */
  readonly adding = signal<GraphNodeType | null>(null);
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
  /** The compounds a node of this graph can be, by node type. */
  readonly compounds = this.mutableCompounds.asReadonly();
  /** The graphs around the one on screen, outermost first. */
  readonly trail = this.mutableTrail.asReadonly();
  /** The selected note, by its place in the document. */
  readonly selectedNote = this.mutableNote.asReadonly();
  /** Whether the Inspector shows the graph itself. */
  readonly graphInspected = this.mutableGraphInspected.asReadonly();

  /** The number inputs someone asked to see on the canvas, by `edgeKey`, since a port is where a wire would arrive. */
  readonly shownPorts = this.mutableShownPorts.asReadonly();

  /**
   * Whether anything in this graph can drive a number. Nothing in the catalogue outputs one, so today that is a
   * compound's own number input, seen from inside it.
   */
  readonly hasNumberSource = computed(() => {
    const types = this.types();
    return [...(this.mutableDocument()?.nodes.values() ?? [])].some(node =>
      types.get(node.type)?.ports.some(port => port.direction === 'Output' && port.kind !== 'Image')
    );
  });

  /** Whether anything open here differs from its file, on this level or one above it. */
  readonly unsaved = computed(
    () => (this.mutableDocument()?.history.dirty ?? false) || this.mutableTrail().some(crumb => crumb.dirty)
  );

  /** The one node the Inspector and the texture view follow. */
  readonly selectedNode = computed(() => {
    const document = this.mutableDocument();
    const [first] = this.mutableSelection();
    return (first === undefined ? undefined : document?.nodes.get(first)) ?? null;
  });

  readonly editable = computed(() => {
    const document = this.mutableDocument();
    return document !== null && !document.readOnly && this.mutableLink() === 'live';
  });

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
          this.mutableNote.set(null);
          this.mutableGraphInspected.set(false);
        });
      }
    });
  }

  /** Lets go of the graph, which stays open on the Server with its edits and history, to come back to. */
  detach(): void {
    this.endSession();
    this.mutableTrail.set([]);
    this.mutableDocument.set(null);
    this.mutableShownPorts.set(new Set());
    this.mutableSession.set({ kind: 'none' });
  }

  /** The wire arriving at an input, if it has one. */
  wireInto(node: number, port: string): GraphEdgeState | undefined {
    return this.mutableDocument()?.edges.get(edgeKey({ toNode: node, toPort: port }));
  }

  /**
   * Shows or hides a number input's port on the canvas. This is a view of the graph, like the selection, so it is not
   * saved: what needs keeping is the wire, and a wire keeps its port.
   */
  togglePort(node: number, port: string): void {
    const key = edgeKey({ toNode: node, toPort: port });
    const shown = new Set(this.mutableShownPorts());
    if (!shown.delete(key)) {
      shown.add(key);
    }

    this.mutableShownPorts.set(shown);
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

    if (document) {
      this.detach();
    }

    await this.start(path);
  }

  /** Writes a new graph into the library and opens it. The Server's refusal is what the caller shows. */
  async create(name: string, project: string | null, resolution: number | null = null): Promise<void> {
    const client = await this.reconnect();
    const { path } = unwrap(await client.POST('/api/v1/library/graphs', { body: { name, project, resolution } }));
    await this.open(path);
  }

  /** Closes the session on the Server, which ends it for every client that joined. Unsaved edits ask first. */
  /** Chooses the bake this graph's Mesh Map nodes read. The Server remembers it beside the graph's file. */
  async useMeshMaps(folder: string | null): Promise<void> {
    const id = this.mutableDocument()?.id;
    if (id) {
      const client = await this.reconnect();
      unwrap(await client.PUT('/api/v1/graphs/{id}/mesh-maps', { params: { path: { id } }, body: { folder } }));
    }
  }

  async close(): Promise<void> {
    if (this.unsaved()) {
      this.mutableLeaving.set({ kind: 'close' });
      return;
    }

    await this.closeAll(false);
  }

  /** Opens the graph behind a compound node, keeping this one open to come back to. */
  async enterCompound(type: string): Promise<void> {
    const compound = this.mutableCompounds().get(type);
    const document = this.mutableDocument();
    if (!compound || !document) {
      return;
    }

    const crumb = { id: document.id, path: document.path, name: document.name, dirty: document.history.dirty };
    this.mutableTrail.update(trail => [...trail, crumb]);
    await this.start(compound.path);
  }

  /** Saves the selected nodes as a compound in the folder this graph reads, as one undo. Says why it could not. */
  async saveSelectionAsCompound(name: string): Promise<string | null> {
    const document = this.mutableDocument();
    const nodes = [...this.mutableSelection()];
    if (!document || nodes.length === 0) {
      return null;
    }

    const held = nodes.flatMap(id => document.nodes.get(id) ?? []);
    const x = held.reduce((sum, node) => sum + node.x, 0) / held.length;
    const y = held.reduce((sum, node) => sum + node.y, 0) / held.length;
    try {
      const extraction = unwrap(
        await this.api().POST('/api/v1/graphs/{id}/compounds', {
          params: { path: { id: document.id } },
          body: { name, nodes, x, y }
        })
      );
      this.selectOnceHeld([extraction.node], extraction.edit.revision);
      return null;
    } catch (error) {
      return reasonOf(error);
    }
  }

  /** Copies the shipped compound on screen into the folder the graph it was entered from reads, and opens the copy. */
  async copyToLibrary(): Promise<void> {
    const document = this.mutableDocument();
    if (!document?.readOnly) {
      return;
    }

    try {
      const copy = unwrap(
        await this.api().POST('/api/v1/library/compounds', {
          body: {
            type: document.path.slice(ShippedPrefix.length),
            graph: this.mutableTrail().at(-1)?.id ?? null
          }
        })
      );
      await this.start(copy.path);
    } catch (error) {
      this.refused(error);
    }
  }

  /** Goes back to a graph on the trail. What is open below it closes, so unsaved edits there ask first. */
  async back(index: number): Promise<void> {
    if (
      (this.mutableDocument()?.history.dirty ?? false) ||
      this.mutableTrail()
        .slice(index + 1)
        .some(c => c.dirty)
    ) {
      this.mutableLeaving.set({ kind: 'back', index });
      return;
    }

    await this.goBack(index, false);
  }

  /** Answers {@link leaving}. */
  async resolveLeaving(choice: 'save' | 'discard' | 'cancel'): Promise<void> {
    const leaving = this.mutableLeaving();
    this.mutableLeaving.set(null);
    if (leaving === null || choice === 'cancel' || (choice === 'save' && !(await this.saveLeaving(leaving)))) {
      return;
    }

    const discard = choice === 'discard';
    switch (leaving.kind) {
      case 'back':
        await this.goBack(leaving.index, discard);
        break;
      case 'open':
        await this.closeAll(discard);
        await this.start(leaving.path);
        break;
      case 'close':
        await this.closeAll(discard);
        break;
      case 'quit':
        desktop()?.quit();
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

    if (next.size > 0) {
      this.mutableGraphInspected.set(false);
    }
  }

  /** Selects a note, or none. */
  selectNote(index: number | null): void {
    this.mutableNote.set(index);
    if (index !== null) {
      this.mutableGraphInspected.set(false);
    }
  }

  /** Shows the graph's own settings in the Inspector. */
  inspectGraph(): void {
    this.select([]);
    this.mutableNote.set(null);
    this.mutableGraphInspected.set(true);
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

  /** A parameter's default as it shows: the hand's, then the document's. */
  parameterDefault(parameter: GraphSettingDefinition): string {
    const draft = this.valueDrafts.values().get(parameterKey(parameter.name));
    return typeof draft === 'string' ? draft : parameter.default;
  }

  /** Sets a setting of the graph itself. */
  setSetting(key: string, value: string): void {
    void this.apply([{ op: 'setSetting', key, value }], `Set ${key}`);
  }

  addParameter(parameter: GraphSettingDefinition): void {
    void this.apply([{ op: 'addParameter', parameter }], null);
  }

  /** Changes a parameter in one step, which may rename it. */
  editParameter(name: string, parameter: GraphSettingDefinition): void {
    void this.apply([{ op: 'setParameter', name, parameter }], null);
  }

  /** Sets a parameter's default. Live: a slider sends this on every step. */
  setParameterDefault(parameter: GraphSettingDefinition, value: string): void {
    const key = parameterKey(parameter.name);
    this.sendDraft(key, key, value, {
      op: 'setParameter',
      name: parameter.name,
      parameter: { ...parameter, default: value }
    });
  }

  removeParameter(name: string): void {
    void this.apply([{ op: 'removeParameter', name }], null);
  }

  addPort(port: GraphInterfacePort): void {
    void this.apply([{ op: 'addPort', port }], null);
  }

  /** Changes a port in one step, which may rename it. The wires on its boundary node follow. */
  editPort(name: string, direction: string, port: GraphInterfacePort): void {
    void this.apply([{ op: 'setPort', name, direction, port }], null);
  }

  removePort(name: string, direction: string): void {
    void this.apply([{ op: 'removePort', name, direction }], null);
  }

  /** Draws a frame round the selected nodes. A node belongs to one frame, so they leave any they were in. */
  groupSelection(): void {
    const nodes = [...this.mutableSelection()];
    if (nodes.length > 0) {
      void this.apply([...this.leavingGroups(nodes), { op: 'addGroup', title: 'Group', nodes }], 'Group Nodes');
    }
  }

  renameGroup(id: number, title: string): void {
    void this.apply([{ op: 'setGroup', id, title }], null);
  }

  ungroup(id: number): void {
    void this.apply([{ op: 'removeGroup', id }], null);
  }

  /** Nodes came to rest, each inside a frame or outside them all. A node that changed frame changes group. */
  regroup(moved: readonly { node: number; group: string | undefined }[]): void {
    const groups = this.mutableDocument()?.groups ?? [];
    const members = new Map(groups.map(group => [group.id, new Set(group.nodes)]));
    let changed = false;
    for (const { node, group } of moved) {
      const target = group === undefined ? undefined : Number(group.slice(GroupPrefix.length));
      const held = [...members].find(([, set]) => set.has(node))?.[0];
      if (held !== target) {
        if (held !== undefined) {
          members.get(held)?.delete(node);
        }

        if (target !== undefined) {
          members.get(target)?.add(node);
        }

        changed = true;
      }
    }

    if (changed) {
      const commands = [...members].map(([id, nodes]): GraphCommand => ({ op: 'setGroup', id, nodes: [...nodes] }));
      void this.apply(commands, 'Move Between Groups');
    }
  }

  /** Adds a note, and selects it for typing into. */
  async addComment(position: GraphPoint): Promise<void> {
    const reply = await this.apply([{ op: 'addComment', text: 'Note', x: position.x, y: position.y, ref: -1 }], null);
    const note = reply?.created.find(item => item.ref === -1);
    if (note) {
      this.select([]);
      this.selectNote(note.node);
    }
  }

  setCommentText(id: number, text: string): void {
    void this.apply([{ op: 'setComment', id, text }], null);
  }

  /** A note moved under the hand. Live, and a drag is one undo. */
  moveComment(id: number, position: GraphPoint): void {
    const key = noteKey(id);
    this.positionDrafts.set(key, position);
    this.sendLive(
      key,
      { op: 'setComment', id, x: position.x, y: position.y },
      (sequence, seen) => {
        this.positionDrafts.settle(key, position, sequence, seen);
      },
      () => {
        this.positionDrafts.drop(key);
      }
    );
  }

  removeComment(id: number): void {
    this.selectNote(null);
    void this.apply([{ op: 'removeComment', id }], null);
  }

  /** Writes a node's picture at full size and fetches it. The Worker does the work and the file is the answer. */
  async fullImage(node: number, port: string): Promise<{ blob: Blob; width: number; height: number } | null> {
    const id = this.mutableDocument()?.id;
    if (id === undefined) {
      return null;
    }

    try {
      const image = unwrap(
        await this.api().POST('/api/v1/graphs/{id}/images', { params: { path: { id } }, body: { node, port } })
      );
      const blob = unwrap(
        await this.api().GET('/api/v1/previews/{hash}', { params: { path: { hash: image.hash } }, parseAs: 'blob' })
      );
      return { blob, width: image.width, height: image.height };
    } catch (error) {
      this.refused(error);
      return null;
    }
  }

  /** Adds a node, and wires it to the port a dropped wire came from. */
  async addNode(type: GraphNodeType, position: GraphPoint, link?: NodeLink): Promise<void> {
    const reference = -1;
    const commands: GraphCommand[] = [{ op: 'addNode', type: type.path, x: position.x, y: position.y, ref: reference }];
    // The Server picks the ports, since it is what knows which of them fit.
    if (link?.kind === 'from' || link?.kind === 'wire') {
      commands.push({ op: 'wire', fromNode: link.node, fromPort: link.port ?? null, toNode: reference });
    }

    if (link?.kind === 'to') {
      commands.push({ op: 'wire', fromNode: reference, toNode: link.node, toPort: link.port });
    }

    if (link?.kind === 'wire') {
      commands.push({ op: 'wire', fromNode: reference, toNode: link.toNode, toPort: link.toPort });
    }

    const result = await this.apply(commands, `Add ${type.title}`);
    const created = result?.created.find(item => item.ref === reference);
    if (created) {
      this.selectOnceHeld([created.node], result?.revision ?? 0);
    }
  }

  /** The nodes that match what was typed, best first. Every palette asks here, so that they agree on the order. */
  async findNodes(query: string, wire: NodeWire = {}): Promise<readonly GraphNodeType[]> {
    const id = this.mutableDocument()?.id;
    if (id === undefined) {
      return [];
    }

    const asked: { query: string; takes?: string; gives?: string } = { query };
    if (wire.takes) {
      asked.takes = wire.takes;
    }

    if (wire.gives) {
      asked.gives = wire.gives;
    }

    try {
      const found = unwrap(
        await this.api().GET('/api/v1/graphs/{id}/node-types/search', { params: { path: { id }, query: asked } })
      );
      const types = this.mutableTypes();
      return found.paths.flatMap(path => types.get(path) ?? []);
    } catch (failure) {
      this.mutableProblem.set(messageOf(failure));
      return [];
    }
  }

  /** What Copy took: nodes as they were, and the wires between them. It survives closing the graph. */
  private clipboard: Pick<GraphState, 'nodes' | 'edges'> | null = null;

  /** Remembers the selected nodes, and the wires that stay inside them. */
  copySelection(): void {
    const document = this.mutableDocument();
    const chosen = this.selection();
    if (document && chosen.size > 0) {
      this.clipboard = {
        nodes: [...document.nodes.values()].filter(node => chosen.has(node.id)),
        edges: [...document.edges.values()].filter(edge => chosen.has(edge.fromNode) && chosen.has(edge.toNode))
      };
    }
  }

  /** Puts a copy of what Copy took beside the original, with its numbers, settings and inner wires. */
  async paste(): Promise<void> {
    const held = this.clipboard;
    if (!held || !this.editable()) {
      return;
    }

    const refs = new Map(held.nodes.map((node, index) => [node.id, -1 - index]));
    const reference = (id: number): number => refs.get(id) ?? id;
    const commands: GraphCommand[] = held.nodes.flatMap((node): GraphCommand[] => [
      { op: 'addNode', type: node.type, x: node.x + PasteOffset, y: node.y + PasteOffset, ref: reference(node.id) },
      ...Object.entries(node.values).map(([port, value]): GraphCommand => ({
        op: 'setValue',
        node: reference(node.id),
        port,
        value
      })),
      ...Object.entries(node.texts).map(([port, value]): GraphCommand => ({
        op: 'setText',
        node: reference(node.id),
        port,
        value
      }))
    ]);
    for (const edge of held.edges) {
      commands.push({
        op: 'connect',
        fromNode: reference(edge.fromNode),
        fromPort: edge.fromPort,
        toNode: reference(edge.toNode),
        toPort: edge.toPort
      });
    }

    const result = await this.apply(commands, 'Paste');
    if (result) {
      this.selectOnceHeld(
        result.created.map(item => item.node),
        result.revision
      );
    }
  }

  /** Copy and Paste in one go, leaving what was copied before alone. */
  async duplicate(): Promise<void> {
    const before = this.clipboard;
    this.copySelection();
    await this.paste();
    this.clipboard = before;
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
      commands.push({ op: 'removeNodes', nodes: [...nodes], relink: false });
    }

    if (commands.length > 0) {
      void this.apply(commands, null);
    }
  }

  /** Removes nodes and keeps the chain joined: what fed each one is wired to what it fed. */
  removeRelinking(nodes: readonly number[]): void {
    if (nodes.length > 0) {
      void this.apply([{ op: 'removeNodes', nodes: [...nodes], relink: true }], null);
    }
  }

  /** Switches nodes off, or back on when every one of them is already off. */
  toggleBypass(nodes: readonly number[] = [...this.mutableSelection()]): void {
    const document = this.mutableDocument();
    const switchable = nodes.filter(id => {
      const type = document?.nodes.get(id)?.type ?? '';
      return type !== RerouteType && !type.startsWith('Sub-graph/');
    });
    if (switchable.length === 0) {
      return;
    }

    const bypassed = !switchable.every(id => document?.nodes.get(id)?.bypassed);
    void this.apply([{ op: 'setBypass', nodes: switchable, bypassed }], null);
  }

  /** Lays the selection out among itself, or the whole graph when nothing is selected. */
  arrange(nodes: readonly number[] = [...this.mutableSelection()]): void {
    void this.apply([{ op: 'arrange', nodes: nodes.length > 1 ? [...nodes] : [] }], null);
  }

  /** Moves nodes to places worked out by the canvas, such as lining them up, as one undo step. */
  place(positions: ReadonlyMap<number, GraphPoint>, label: string): void {
    if (positions.size > 0) {
      void this.apply(
        [{ op: 'moveNodes', positions: [...positions].map(([node, { x, y }]) => ({ node, x, y })) }],
        label
      );
    }
  }

  /** Bends a wire through a new reroute dot at a point. */
  async insertReroute(edge: GraphEdgeState, at: GraphPoint): Promise<void> {
    await this.apply(
      [
        { op: 'addNode', type: RerouteType, x: at.x, y: at.y, ref: -1 },
        { op: 'connect', fromNode: edge.fromNode, fromPort: edge.fromPort, toNode: -1, toPort: RerouteIn },
        { op: 'connect', fromNode: -1, fromPort: RerouteOut, toNode: edge.toNode, toPort: edge.toPort }
      ],
      'Add Reroute'
    );
  }

  /** Changes a frame's colour or what it says about its nodes. */
  describeGroup(id: number, change: { color?: string; description?: string }): void {
    void this.apply([{ op: 'setGroup', id, ...change }], null);
  }

  /** Saves every parameter's default as it is now, under a name. */
  savePreset(name: string): void {
    void this.apply([{ op: 'savePreset', name }], null);
  }

  /** Sets the parameters to a preset's values. */
  applyPreset(name: string): void {
    void this.apply([{ op: 'applyPreset', name }], null);
  }

  removePreset(name: string): void {
    void this.apply([{ op: 'removePreset', name }], null);
  }

  /** Sets a compound node's settings to one of its compound's presets, as one undo step. */
  applyNodePreset(node: number, preset: GraphPresetState): void {
    const settings = new Set(
      this.mutableTypes()
        .get(this.mutableDocument()?.nodes.get(node)?.type ?? '')
        ?.settings.map(setting => setting.name) ?? []
    );
    const commands: GraphCommand[] = Object.entries(preset.values)
      .filter(([name]) => settings.has(name))
      .map(([port, value]) => ({ op: 'setText', node, port, value }));
    if (commands.length > 0) {
      void this.apply(commands, `Preset ${preset.name}`);
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

  /**
   * Undo, Redo and Save, wherever the keys were pressed: true if this was one of them. A caller decides first whether
   * the press belongs to a text box.
   */
  shortcut(event: KeyboardEvent): boolean {
    if (!event.metaKey && !event.ctrlKey) {
      return false;
    }

    const key = event.key.toLowerCase();
    if (key === 'z') {
      event.preventDefault();
      if (event.shiftKey) {
        this.redo();
      } else {
        this.undo();
      }

      return true;
    }

    if (key === 's') {
      event.preventDefault();
      void this.save();
      return true;
    }

    if (key === 'd') {
      event.preventDefault();
      void this.duplicate();
      return true;
    }

    return false;
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
      throw new Error('Patina is still starting.');
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
    const catalogue = await this.catalogue(state.id);
    if (epoch !== this.epoch) {
      return;
    }

    this.showCatalogue(catalogue);
    this.accept(documentOf(state));
    this.mutableSession.set({ kind: 'open' });
    this.follow(epoch);
  }

  /** Every node type and compound this graph can use, which a saved compound can change. */
  private async catalogue(id: string): Promise<[GraphNodeType[], GraphCompound[]]> {
    const params = { params: { path: { id } } };
    const [types, compounds] = await Promise.all([
      this.api().GET('/api/v1/graphs/{id}/node-types', params),
      this.api().GET('/api/v1/graphs/{id}/compounds', params)
    ]);
    return [unwrap(types), unwrap(compounds)];
  }

  private showCatalogue([types, compounds]: [GraphNodeType[], GraphCompound[]]): void {
    this.mutableTypes.set(new Map(types.map(type => [type.path, type])));
    this.mutableCompounds.set(new Map(compounds.map(compound => [compound.type, compound])));
  }

  private async refreshCatalogue(id: string): Promise<void> {
    const epoch = this.epoch;
    const catalogue = await this.catalogue(id);
    if (epoch === this.epoch) {
      this.showCatalogue(catalogue);
    }
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

  /**
   * Selects what a reply says was made. The reply and its event race, and any event read before that one would find the
   * nodes missing from the document and take them back out of the selection, so this waits for the revision.
   */
  private selectOnceHeld(nodes: readonly number[], revision: number): void {
    if ((this.mutableDocument()?.revision ?? 0) >= revision) {
      this.select(nodes);
    } else {
      this.selectionDue = { nodes, revision };
    }
  }

  private keepSelectionReal(document: GraphDocument): void {
    if (this.selectionDue && document.revision >= this.selectionDue.revision) {
      const { nodes } = this.selectionDue;
      this.selectionDue = null;
      this.select(nodes);
    }

    const held = this.mutableSelection();
    if ([...held].some(node => !document.nodes.has(node))) {
      this.mutableSelection.set(new Set([...held].filter(node => document.nodes.has(node))));
    }

    const note = this.mutableNote();
    if (note !== null && !document.comments.some(comment => comment.id === note)) {
      this.mutableNote.set(null);
    }
  }

  /** The commands that take nodes out of the frames they are in. */
  private leavingGroups(nodes: readonly number[]): GraphCommand[] {
    const leaving = new Set(nodes);
    const commands: GraphCommand[] = [];
    for (const group of this.mutableDocument()?.groups ?? []) {
      if (group.nodes.some(node => leaving.has(node))) {
        commands.push({ op: 'setGroup', id: group.id, nodes: group.nodes.filter(node => !leaving.has(node)) });
      }
    }

    return commands;
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
        this.mutableNotice.set('Patina restarted its engine. Edits since the last save are gone.');
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

    if (event.kind === 'typesChanged' || event.delta?.interface) {
      this.refreshCatalogue(next.id).catch((error: unknown) => {
        this.refused(error);
      });
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

  /** Saves what leaving would lose: the graph on screen and whatever on the trail is going too. */
  private async saveLeaving(leaving: GraphLeaving): Promise<boolean> {
    const trail = this.mutableTrail();
    const going = leaving.kind === 'back' ? trail.slice(leaving.index + 1) : trail;
    if (this.mutableDocument()?.history.dirty && !(await this.save())) {
      return false;
    }

    try {
      for (const crumb of going.filter(item => item.dirty)) {
        unwrap(await this.api().POST('/api/v1/graphs/{id}/save', { params: { path: { id: crumb.id } } }));
      }

      this.mutableTrail.set(trail.map(crumb => (going.includes(crumb) ? { ...crumb, dirty: false } : crumb)));
      return true;
    } catch (error) {
      this.refused(error);
      return false;
    }
  }

  private async closeCrumb(crumb: GraphCrumb, discard: boolean): Promise<void> {
    try {
      await this.api().DELETE('/api/v1/graphs/{id}', { params: { path: { id: crumb.id }, query: { discard } } });
    } catch (error) {
      if (!(error instanceof ApiError && error.status === 404)) {
        this.refused(error);
      }
    }
  }

  /** Closes the graph on screen and every graph on the trail. */
  private async closeAll(discard: boolean): Promise<void> {
    await this.closeSession(discard);
    const trail = this.mutableTrail();
    this.mutableTrail.set([]);
    for (const crumb of trail) {
      await this.closeCrumb(crumb, discard);
    }
  }

  private async goBack(index: number, discard: boolean): Promise<void> {
    const trail = this.mutableTrail();
    const target = trail[index];
    if (!target) {
      return;
    }

    await this.closeSession(discard);
    for (const crumb of trail.slice(index + 1)) {
      await this.closeCrumb(crumb, discard);
    }

    this.mutableTrail.set(trail.slice(0, index));
    await this.start(target.path);
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
    this.mutableShownPorts.set(new Set());
    this.mutableNote.set(null);
    this.mutableGraphInspected.set(false);
    this.mutableCompounds.set(new Map());
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
