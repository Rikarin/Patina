import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import {
  ApiError,
  type Connection,
  type HistoryList,
  type PatinaClient,
  type StackBrush,
  type StackClip,
  type StackCommand,
  type StackCommandBatch,
  type StackEvent,
  type StackIdColour,
  type StackLayer,
  type StackMask,
  type StackMaskEffect,
  type StackPicture,
  type StackPreview,
  type StackProblem,
  type StackStamp,
  type StackState,
  type StackStrokeSource,
  type StackSymmetry,
  type StackView,
  type StackVocabulary,
  type ViewportCameraState,
  type ViewportPick,
  unwrap
} from '@patina/api-client';
import { learnTerms } from '../core/labels';
import { messageOf } from '../core/messages';
import { RecoveryOffers } from '../core/recovery-offers';
import { EngineRestarts, RestartedNotice, SessionReturn } from '../core/session-return';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { extendedPath, placedDecal } from './surface-frame';

/** The most recent colours a project keeps, which is the Server's limit. */
const RecentColours = 16;

/** The most colours a palette holds, which is the Server's limit. */
const PaletteColours = 64;

/** A part of one layer that a copy can be for (#106). */
export type LayerPart = 'Mask' | 'Content';

/** Whether a layer's mask applies: a mask is on unless it was switched off (#109). */
export function maskOn(layer: StackLayer): boolean {
  return layer.mask.enabled;
}

/** A new brush until the Server's vocabulary says otherwise: the same as `StackBrushes.Default`. */

export const NewBrush: StackBrush = {
  radius: 24,
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
  pressureSize: 1,
  pressureFlow: 0,
  lazy: 0,
  curve: 'smooth',
  aspect: 1,
  aspectAngle: 0,
  flowJitter: 0,
  scatterAxis: 'both',
  accumulate: true,
  sizeSpace: 'screen',
  occlusion: true,
  occlusionTolerance: 2
};

const ThumbnailQuiet = 400;

/** What a clone stroke with nowhere to read from says. */
export const CloneFirst = 'Set where the clone reads from first: Cmd- or Ctrl-click the model or the map.';

/** What a clone stroke that starts off the model says: its step to the source is measured from where it starts. */
export const CloneOnModel = 'Start a clone stroke on the model, so it knows how far it is from where it reads.';

/** How many stamp-to-frame times are kept, which is what the readout and the bench read. */
const StampTimes = 2048;

/** What a stroke draws with, beyond its radius. */
export type BrushSettings = Omit<StackBrush, 'radius'>;

export type PaintTool = 'brush' | 'eraser' | 'smudge' | 'clone' | 'projection' | 'fill' | 'line' | 'path' | 'picker';

/** How much of the mesh a click of the fill tool takes: the Server's words (`StackBrushes.Fills`). */
export type FillMode = 'triangle' | 'polygon' | 'part' | 'shell';

/**
 * Where a clone reads from: a place on the map, 0 to 1 across and down. Relative keeps the step from it to where the
 * first stroke after it started, so later strokes read beside themselves; absolute reads from it at every stroke's start.
 */
export interface CloneSource {
  u: number;
  v: number;
}

/** A picture on the viewport, fixed on the screen: what the projection tool paints and a stencil masks with. */
export type ScreenPicture = StackPicture;

/** A material from the shelf that a brush paints every channel of, by the graph its maps come from. */
export interface BrushMaterial {
  name: string;
  graph: string;
}

/** Where a stroke on the mesh is aimed: a picture of this size through this camera. */
export interface Aim {
  width: number;
  height: number;
  camera: ViewportCameraState;
}

/** A layer with how deep in groups it is, top of the stack first. */
/** How the Worker says a stack read mesh maps with no bake, before their names. */
const NeedsBake = 'Needs a bake: ';

/** The masks a brush can paint: a picture has to stay itself, and these are one number or a canvas already. */
const FlatMasks: ReadonlySet<string> = new Set(['None', 'Constant', 'Paint']);

export interface LayerRow {
  layer: StackLayer;
  depth: number;
  /** The folder or layer it is in, or null at the top of the stack. */
  parent: string | null;
  /** Whether it is an effect inside a layer rather than a layer of the stack. */
  effect: boolean;
}

/** The stack as a list, each group above what it holds. */
export function rowsOf(
  layers: readonly StackLayer[],
  depth = 0,
  parent: string | null = null,
  effect = false
): LayerRow[] {
  // A flattened folder is its canvas, and its layers are kept out of sight as the way back (#107).
  return [...layers]
    .reverse()
    .flatMap(layer => [
      { layer, depth, parent, effect },
      ...(layer.flattened ? [] : rowsOf(layer.children, depth + 1, layer.id, layer.kind !== 'group'))
    ]);
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
  /**
   * Brings the stack back after its socket drops, by the session it dropped in: the same session if the Server kept
   * it, or the file opened again if the Server restarted (#226), as a graph does.
   */
  private readonly recovery = inject(RecoveryOffers);
  private readonly comeback = new SessionReturn<string>(
    {
      probe: async id => {
        if (this.state()?.id === id && !this.stopped) {
          const client = await this.api();
          unwrap(await client.GET('/api/v1/stacks/{id}', { params: { path: { id } } }));
        }
      },
      follow: id => (this.state()?.id === id ? this.follow() : undefined),
      reopen: id => this.reopenLost(id),
      lost: (id, error) => {
        if (this.state()?.id === id) {
          this.failure.set(messageOf(error));
        }
      }
    },
    inject(EngineRestarts)
  );
  private sequence = 0;
  private stopped = false;
  private stroke: string | null = null;
  private strokes = 0;
  private pending: StackStamp[] = [];
  private view: StackView | null = null;
  /** Where the stroke under way started on the map, 0 to 1, which a clone measures its step from. */
  private startedAt: CloneSource | null = null;
  /** The clone's step for the stroke under way, and the one a relative clone keeps, in texels. */
  private cloneStep: [number, number] | null = null;
  private keptStep: [number, number] | null = null;
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
  /** Something the person should know that went right enough, such as edits lost when the engine restarted. */
  readonly notice = signal<string | null>(null);

  /** Told as something new starts, for whoever keeps a failure of its own. */
  onAction: () => void = () => undefined;

  /** The file changed behind this session, so a save waits on whether to replace it. */
  readonly saveConflict = signal(false);

  readonly layer = signal<string | null>(null);

  /** What is selected with the layer: more than one, with Cmd or Shift held. Properties follows {@link layer}. */
  readonly marked = signal<ReadonlySet<string>>(new Set());
  readonly channel = signal('baseColor');
  readonly radius = signal(24);

  /** The channel on screen when it is not the one being painted, so looking never changes what a stroke writes. */
  readonly viewed = signal<string | null>(null);

  /** What else a stroke paints beside {@link channel}: each channel, and the colour it gets. */
  readonly alsoPaints = signal<ReadonlyMap<string, string>>(new Map());
  readonly colour = signal('#d9401a');
  /** The colours strokes last used, newest first, offered beside the picker. The project keeps them. */
  readonly recentColours = signal<string[]>([]);
  /** Colours kept on purpose, in the order they were added. The project keeps them. */
  readonly palette = signal<string[]>([]);
  readonly picture = signal<string | null>(null);
  readonly tool = signal<PaintTool>('brush');
  /** How strokes copy themselves on the mesh, or null for not at all. Needs a mesh, in either view. */
  readonly symmetry = signal<StackSymmetry | null>(null);
  /** How much of the mesh a click of the fill tool takes. */
  readonly fillMode = signal<FillMode>('polygon');
  /** Where the clone tool reads from, or null until one is set. */
  readonly cloneSource = signal<CloneSource | null>(null);
  /** Whether the clone keeps its step from the first stroke after the source was set, or starts at the source each time. */
  readonly cloneRelative = signal(true);
  /** Whether the next click on the mesh or the map sets where the clone reads from. */
  readonly settingCloneSource = signal(false);
  /** The material the brush paints every channel of, from the shelf, or null for its colours. */
  readonly material = signal<BrushMaterial | null>(null);
  /** The picture on the viewport that the projection tool paints and a stencil masks with, or null. */
  readonly screenPicture = signal<ScreenPicture | null>(null);
  /** Whether the picture on the viewport masks every brush, by its brightness. */
  readonly stencil = signal(false);
  /**
   * Texels of the map per pixel of the 2D view as it is drawn, so that a size in the screen's units is the same under
   * the pointer at any zoom. The 2D view sets it as a stroke starts.
   */
  readonly atlasScale = signal(1);
  /** Whether strokes go to the chosen layer's mask rather than its channels. */
  readonly masking = signal(false);
  /**
   * While {@link masking}, the entry of the mask's own stack a stroke paints, counted from the bottom, or null for the
   * mask's base (#109).
   */
  readonly maskLayer = signal<number | null>(null);
  /** What a stroke on a mask lays down, from nothing shown to everything: a mask is one number, not a colour. */
  readonly maskGrey = signal(0);
  readonly brush = signal<BrushSettings>(settingsOf(NewBrush));
  readonly vocabulary = signal<StackVocabulary | null>(null);
  /** The layers last copied, from this stack or another. The Server holds them, pixels and all. */
  readonly clip = signal<StackClip | null>(null);
  /** What the last copy is for: whole layers, or one layer's mask or content to paste into others (#106). */
  readonly clipPart = signal<LayerPart | null>(null);
  /** The colours of the id map this stack reads, once asked for, or null with no such map. */
  readonly idColours = signal<readonly StackIdColour[] | null>(null);
  /** The layer whose mask is taking its colour from the next click on the mesh or the map. */
  readonly picking = signal<string | null>(null);
  /** Whether the next click on the mesh or the map chooses the layer on top there (#136). */
  readonly finding = signal(false);
  /**
   * What a click on the mesh does to the chosen fill (#130, #133): places its decal there, or adds a point to the end of
   * its path, until it is set back to null.
   */
  readonly placing = signal<'decal' | 'path' | null>(null);
  /** Whether the viewport shows the chosen decal's warp grid, whose points drag the picture (#134). */
  readonly warping = signal(false);
  /** The mesh maps layers want and no bake has given, which read as grey until one does. */
  readonly unbaked = signal<string | null>(null);
  /** Whether the viewport is wearing this stack, so a stroke can be made on the mesh. */
  readonly worn = signal(false);
  /** From a stamp leaving the pointer to the first frame painted with it, newest last, in milliseconds. */
  readonly stampTimes = signal<readonly number[]>([]);
  /** Edits of the stack sent and not yet answered: a mask entry, an instance, a filter's input, a stroke. */
  private readonly unanswered = signal(0);
  /** The newest revision an answer named, by stack, which the history the events carry has yet to reach. */
  private readonly answered = signal<{ id: string; revision: number } | null>(null);
  /**
   * Whether an edit is on its way, or answered with a history not yet heard: what Undo and Redo would do is known once
   * it is back, so a key for either waits (#223), as a graph's does.
   */
  readonly settling = computed(() => {
    const state = this.state();
    const answered = this.answered();
    return this.unanswered() > 0 || (state !== null && answered?.id === state.id && state.revision < answered.revision);
  });

  /** The layers top first, as a stack reads, with the ones a brush can reach marked by their kind. */
  readonly layers = computed<readonly LayerRow[]>(() => rowsOf(this.state()?.layers ?? []));
  /** Every selected layer that still exists, the current one among them. */
  readonly selection = computed<ReadonlySet<string>>(() => {
    const here = new Set(this.layers().map(row => row.layer.id));
    const chosen = new Set([...this.marked()].filter(id => here.has(id)));
    const layer = this.layer();
    return layer !== null && here.has(layer) ? chosen.add(layer) : chosen;
  });

  readonly chosen = computed(() => this.layers().find(row => row.layer.id === this.layer())?.layer ?? null);

  /** Whether a stroke would land on what is chosen, which is the Server's rule so that a drag is never refused. */
  readonly canPaint = computed(() => {
    const layer = this.chosen();
    if (!layer) {
      return false;
    }

    const entry = this.maskLayer();
    if (this.masking() && entry !== null) {
      return layer.mask.layers?.[entry]?.source === 'Paint';
    }

    return this.masking()
      ? FlatMasks.has(layer.mask.source)
      : (layer.kind === 'paint' || layer.flattened) && writesChannel(layer, this.channel());
  });

  /** Whether one would land on the mesh, which also takes a mesh: a layered material's sphere is only a preview. */
  readonly canPaintMesh = computed(() => this.canPaint() && !!this.state()?.model.trim());

  /** What the stack's compile had to say, and why an evaluation failed. */
  readonly problems = computed<readonly StackProblem[]>(() => {
    const evaluation = this.state()?.evaluation;
    return [
      ...(evaluation?.error ? [{ layer: '', severity: 'Error', message: evaluation.error }] : []),
      ...(evaluation?.problems ?? [])
    ];
  });

  /** The worst thing said about each layer, for its row. */
  readonly faults = computed<ReadonlyMap<string, StackProblem>>(() => {
    const worst = new Map<string, StackProblem>();
    for (const problem of this.problems()) {
      if (problem.layer && (problem.severity === 'Error' || !worst.has(problem.layer))) {
        worst.set(problem.layer, problem);
      }
    }

    return worst;
  });

  /** What the toolbar says about the last evaluation. */
  readonly evaluated = computed(() => {
    const state = this.state();
    return state ? stackSummary(state) : '';
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.stopped = true;
      this.socket?.close();
      this.comeback.cancel();

      if (this.shownUrl) {
        URL.revokeObjectURL(this.shownUrl);
      }
    });
  }

  /** Opens a stack, and writes a new one first when asked. Throws nothing: the pane shows the failure. */
  async open(path: string, create: boolean): Promise<void> {
    this.failure.set(null);
    this.comeback.cancel();
    const ticket = ++this.opens;
    const answer = this.api().then(async client =>
      unwrap(
        create
          ? await client.POST('/api/v1/stacks/new', { body: { path, width: 2048, height: 2048 } })
          : await client.POST('/api/v1/stacks', { body: { path } })
      )
    );
    this.opened.set(
      path,
      answer.then(
        state => state.id,
        () => null
      )
    );
    try {
      const state = await answer;
      if (ticket !== this.opens) {
        return; // let go of before it opened: the tab that asked has gone, or another came to the front
      }

      this.worn.set(false);
      this.adopt(state);
      await this.follow();
      await this.loadVocabulary();
      if (state.model) {
        await this.wear();
      }
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /**
   * Reads the words a stack's fields take, once: they are the same for every stack, and the labels they carry are what
   * every list and readout shows, so the app asks as soon as the Server answers rather than when a stack opens.
   */
  async loadVocabulary(): Promise<void> {
    if (this.vocabulary() !== null || this.vocabularyAsked) {
      return;
    }

    this.vocabularyAsked = true;
    try {
      const client = await this.api();
      const vocabulary = unwrap(await client.GET('/api/v1/stacks/vocabulary'));
      learnTerms(vocabulary);
      this.vocabulary.set(vocabulary);
    } finally {
      this.vocabularyAsked = false;
    }
  }

  private vocabularyAsked = false;
  /** Goes up with each open and each letting go, so that an open answered after either lands nowhere. */
  private opens = 0;
  /** The session each open got from the Server, by the path it opened, the open on its way included. */
  private readonly opened = new Map<string, Promise<string | null>>();

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
      this.failure.set(messageOf(failure));
    }
  }

  /** Takes the brush's colour from the map on screen, and goes back to the brush. */
  async pickBrushColour(u: number, v: number): Promise<void> {
    const url = this.picture();
    if (!url) {
      return;
    }

    const image = new Image();
    image.src = url;
    await image.decode();
    const canvas = globalThis.document.createElement('canvas');
    [canvas.width, canvas.height] = [1, 1];
    const context = canvas.getContext('2d');
    context?.drawImage(image, u * image.width, v * image.height, 1, 1, 0, 0, 1, 1);
    const [r = 0, g = 0, b = 0] = context?.getImageData(0, 0, 1, 1).data ?? [];
    if (this.masking()) {
      this.maskGrey.set(r / 255);
    } else {
      this.colour.set(`#${[r, g, b].map(value => value.toString(16).padStart(2, '0')).join('')}`);
    }

    this.tool.set('brush');
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
    if (hit.found && this.tool() === 'picker') {
      await this.pickBrushColour(hit.u, hit.v);
    } else if (hit.found) {
      await this.pickColourAt(hit.u, hit.v);
    } else {
      this.picking.set(null);
    }
  }

  /**
   * Chooses the layer on top at a place on the map, 0 to 1 across and down, which the Server finds from what each
   * layer covers there. Nothing found chooses nothing new.
   */
  async findLayerAt(u: number, v: number): Promise<void> {
    this.finding.set(false);
    const id = this.state()?.id;
    if (!id) {
      return;
    }

    try {
      const client = await this.api();
      const found = unwrap(
        await client.POST('/api/v1/stacks/{id}/find', { params: { path: { id } }, body: { u, v } })
      ).layer;
      if (found) {
        this.marked.set(new Set());
        this.layer.set(found);
        this.masking.set(false);
      }
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /** Chooses the layer on top under a place on the mesh in the viewport. */
  async findLayerOnMesh(stamp: StackStamp, aim: Aim): Promise<void> {
    const client = await this.api();
    const { width, height, camera } = aim;
    const hit = unwrap(
      await client.POST('/api/v1/viewport/pick', {
        body: { x: stamp.x / width, y: stamp.y / height, camera, width, height }
      })
    );
    if (hit.found) {
      await this.findLayerAt(hit.u, hit.v);
    } else {
      this.finding.set(false);
    }
  }

  /**
   * A click on the mesh while {@link placing}: the chosen fill's decal goes where it landed, facing out of the face it
   * met, or its path gains a point there. Each is one edit to the stack, which undoes like any other (#130, #133).
   */
  async placeOnMesh(stamp: StackStamp, aim: Aim): Promise<void> {
    const layer = this.chosen();
    const placing = this.placing();
    if (!layer || !placing) {
      return;
    }

    try {
      const hit = await this.picked(stamp, aim);
      if (!hit.found) {
        return;
      }

      if (placing === 'decal') {
        this.placing.set(null);
        await this.edit({ op: 'setLayer', layer: layer.id, projection: 'Decal', decal: placedDecal(hit, layer.decal) });
      } else {
        await this.edit({ op: 'setLayer', layer: layer.id, projection: 'Path', path: extendedPath(hit, layer.path) });
      }
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /** What is under a place on the viewport, for a handle dragged over the mesh. */
  pickOnMesh(stamp: StackStamp, aim: Aim): Promise<ViewportPick> {
    return this.picked(stamp, aim);
  }

  /**
   * Saves the chosen layers, or one layer's mask, to the project's shelf as a smart material or smart mask (#135), named
   * after the layer. What could not come is said in the notice.
   */
  async saveToShelf(layers: readonly string[], mask: boolean, name: string, project: string): Promise<void> {
    const id = this.state()?.id;
    if (!id || layers.length === 0) {
      return;
    }

    this.startAction();
    try {
      const client = await this.api();
      const saved = unwrap(
        await client.POST('/api/v1/stacks/{id}/save-material', {
          params: { path: { id } },
          body: { project, name, layers: [...layers], mask }
        })
      );
      this.notice.set(`Saved ${name} to the shelf as a smart ${saved.role}. ${saved.status}`);
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /**
   * The session an open of this path got from the Server, once the open on its way has its answer; null if none was
   * asked for or the Server made none. A tab closed while its stack is still opening closes this, or the Server keeps a
   * session that nothing shows, and the document can then be neither moved nor deleted (#232).
   */
  sessionOpened(path: string): Promise<string | null> {
    return this.opened.get(path) ?? Promise.resolve(null);
  }

  /** Lets go of the stack, which stays open on the Server with its edits and history, to come back to. */
  detach(): void {
    this.opens++;
    this.comeback.cancel();
    this.notice.set(null);
    this.state.set(null);
    this.worn.set(false);
    this.picture.set(null);
    clearTimeout(this.thumbnailWait);
    this.thumbnails.set(new Map());
    this.layer.set(null);
    this.idColours.set(null);
    this.picking.set(null);
    this.unbaked.set(null);
    const socket = this.socket;
    this.socket = null;
    socket?.close();
  }

  /** Puts a layer's mask in front of the brush, which starts as what shows on a flat mask: white on black, or black. */
  chooseMask(layer: StackLayer): void {
    this.layer.set(layer.id);
    this.masking.set(true);
    this.maskLayer.set(null);
    if (layer.mask.source === 'Constant') {
      this.maskGrey.set(layer.mask.value < 0.5 ? 1 : 0);
    }
  }

  /** Adds a layer at the top of the stack, or of the chosen group, and chooses it. */
  /** Adds an effect inside a layer, over its content and under its mask, and chooses it. */
  async addEffect(layer: StackLayer, kind: string, name: string, filterNode?: string): Promise<void> {
    const before = new Set(this.layers().map(row => row.layer.id));
    await this.command({
      commands: [{ op: 'addLayer', kind, name, parent: layer.id, filterNode: filterNode ?? null }]
    });
    await this.refresh();
    const added = this.layers().find(row => !before.has(row.layer.id));
    if (added) {
      this.layer.set(added.layer.id);
      this.masking.set(false);
    }
  }

  /** The named meshes in this texture set, which a layer can be kept to. */
  async meshParts(): Promise<readonly string[]> {
    const id = this.state()?.id;
    if (!id) {
      return [];
    }

    const client = await this.api();
    return unwrap(await client.GET('/api/v1/stacks/{id}/mesh-parts', { params: { path: { id } } })).parts;
  }

  /** Keeps a layer to these named meshes, or to the whole model when there are none. */
  async keepToMeshes(layer: StackLayer, parts: readonly string[]): Promise<void> {
    const id = this.state()?.id;
    if (!id) {
      return;
    }

    try {
      const client = await this.api();
      const asset =
        parts.length === 0
          ? ''
          : (unwrap(
              await client.POST('/api/v1/stacks/{id}/mesh-mask', {
                params: { path: { id } },
                body: { parts: [...parts] }
              })
            ).asset ?? '');
      await this.edit({ op: 'setLayer', layer: layer.id, meshMask: { parts: [...parts], asset } });
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

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

  /** The selected layers that are not inside another selected one, top first: what a copy or a cut takes. */
  private outermost(): string[] {
    const selected = this.selection();
    return this.layers()
      .filter(row => selected.has(row.layer.id) && (row.parent === null || !selected.has(row.parent)))
      .map(row => row.layer.id);
  }

  /** Copies the selected layers with their pixels as they stand, for a paste here or in another stack. */
  async copyLayers(): Promise<boolean> {
    const id = this.state()?.id;
    const layers = this.outermost();
    if (!id || layers.length === 0) {
      return false;
    }

    this.startAction();
    try {
      // A copy holds what the strokes left, so the last of a drag has to have arrived.
      await this.drained();
      const client = await this.api();
      this.clip.set(
        unwrap(await client.POST('/api/v1/stacks/{id}/clip', { params: { path: { id } }, body: { layers } }))
      );
      this.clipPart.set(null);
      return true;
    } catch (failure) {
      this.failure.set(messageOf(failure));
      return false;
    }
  }

  /** Copies the selected layers and takes them away, which undoes like a delete. */
  async cutLayers(): Promise<void> {
    const layers = this.outermost();
    if (await this.copyLayers()) {
      await this.edit(...layers.map(layer => ({ op: 'removeLayer' as const, layer })));
      this.marked.set(new Set());
    }
  }

  /** Pastes the last copy above the chosen layer, or at the top, and chooses what came in. */
  async pasteLayers(): Promise<void> {
    const clip = this.clip();
    const part = this.clipPart();
    if (!clip) {
      return;
    }

    // A copy of a mask or a content goes into the chosen layers, which is what it was copied for.
    if (part !== null) {
      await this.pasteInto(part);
      return;
    }

    await this.added({
      op: 'pasteLayers',
      clip: clip.id,
      above: this.layer(),
      asInstances: false,
      into: null,
      part: 'Mask'
    });
  }

  /** Pastes the last copy as instances of what was copied, which follow every edit to it (#108). */
  async pasteAsInstances(): Promise<void> {
    const clip = this.clip();
    if (!clip) {
      return;
    }

    await this.added({
      op: 'pasteLayers',
      clip: clip.id,
      above: this.layer(),
      asInstances: true,
      into: null,
      part: 'Mask'
    });
  }

  // Parts of a layer (#106, #109): its mask switched, its mask or content copied into others, its blend shared.

  /** Switches a layer's mask off or on, keeping everything in it. */
  switchMask(layer: StackLayer): Promise<void> {
    return this.setMask(layer, { ...layer.mask, enabled: !maskOn(layer) });
  }

  /** Copies one layer's mask or content, which a paste then puts into the chosen layers. */
  async copyPart(layer: StackLayer, part: LayerPart): Promise<void> {
    this.layer.set(layer.id);
    this.marked.set(new Set([layer.id]));
    if (await this.copyLayers()) {
      this.clipPart.set(part);
    }
  }

  /** Puts the last copy's first layer's mask or content into each chosen layer, as one undo step. */
  async pasteInto(part: LayerPart): Promise<void> {
    const clip = this.clip();
    const into = this.outermost();
    if (!clip || into.length === 0) {
      return;
    }

    await this.edit(
      ...into.map(layer => ({
        op: 'pasteLayers' as const,
        clip: clip.id,
        above: null,
        asInstances: false,
        into: layer,
        part
      }))
    );
  }

  /** Gives every chosen layer this layer's blend mode, as one undo step. */
  async blendEverywhere(layer: StackLayer): Promise<void> {
    const others = this.outermost().filter(id => id !== layer.id);
    if (others.length > 0) {
      await this.edit(...others.map(id => ({ op: 'setLayer' as const, layer: id, blend: layer.blend })));
    }
  }

  /** An instance of a layer just above it, chosen as it arrives. */
  async addInstance(layer: string, set = ''): Promise<void> {
    await this.added({ op: 'addInstance', source: { layer, set }, above: set ? null : layer });
  }

  /** The layer to choose once the texture set it is in opens, for Go to Source across sets. */
  readonly arriving = signal<string | null>(null);

  /** Flattens a folder into a canvas, keeping its layers as the way back, or the whole stack when none is named. */
  async flatten(layer?: string): Promise<void> {
    await this.edit({ op: 'flattenLayer', layer: layer ?? null });
    this.masking.set(false);
  }

  /** Takes a flattened folder back to its layers. */
  async unflatten(layer: string): Promise<void> {
    await this.edit({ op: 'unflattenLayer', layer });
  }

  /** Duplicates the selected layers, each just above itself, with pixels of its own, and chooses the copies. */
  async duplicateLayers(): Promise<void> {
    const layers = this.outermost();
    if (layers.length > 0) {
      await this.added({ op: 'duplicateLayers', layers });
    }
  }

  /** One edit that makes layers, which are then what is selected. */
  private async added(command: StackCommand): Promise<void> {
    const before = new Set(this.layers().map(row => row.layer.id));
    await this.edit(command);
    const made = this.layers().filter(
      row => !before.has(row.layer.id) && (row.parent === null || before.has(row.parent))
    );
    if (made.length > 0) {
      this.layer.set(made[0]?.layer.id ?? null);
      this.marked.set(new Set(made.map(row => row.layer.id)));
      this.masking.set(false);
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
    this.startAction();
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
      this.failure.set(messageOf(failure));
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
      this.failure.set(messageOf(failure));
    }
  }

  setMask(layer: StackLayer, mask: StackMask): Promise<void> {
    return this.edit({ op: 'setLayer', layer: layer.id, mask });
  }

  /** Puts an entry of a layer's mask stack in front of the brush and of Properties. */
  chooseMaskLayer(layer: StackLayer, index: number): void {
    this.layer.set(layer.id);
    this.masking.set(true);
    this.maskLayer.set(index);
  }

  /** Adds an entry at the top of a layer's mask stack and chooses it. A layer with no mask gets a white one under it. */
  async addMaskLayer(layer: StackLayer, source: string): Promise<void> {
    const index = layer.mask.layers?.length ?? 0;
    await this.edit({ op: 'addMaskLayer', layer: layer.id, source, index: null, generator: null });
    const added = this.layers().find(row => row.layer.id === layer.id)?.layer;
    if (added && (added.mask.layers?.length ?? 0) > index) {
      this.chooseMaskLayer(added, index);
    }
  }

  async removeMaskLayer(layer: StackLayer, index: number): Promise<void> {
    if (this.maskLayer() !== null && this.layer() === layer.id) {
      this.maskLayer.set(null);
    }

    await this.edit({ op: 'removeMaskLayer', layer: layer.id, index });
  }

  async moveMaskLayer(layer: StackLayer, index: number, delta: number): Promise<void> {
    await this.edit({ op: 'moveMaskLayer', layer: layer.id, index, delta });
    if (this.layer() === layer.id && this.maskLayer() === index) {
      this.maskLayer.set(index + delta);
    }
  }

  setMaskLayer(
    layer: StackLayer,
    index: number,
    change: Omit<Extract<StackCommand, { op: 'setMaskLayer' }>, 'op' | 'layer' | 'index'>
  ): Promise<void> {
    return this.edit({ op: 'setMaskLayer', layer: layer.id, index, ...change });
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

  /** Undoes, redoes or saves, and throws nothing. False if it did not happen, and the failure says why. */
  async act(action: 'undo' | 'redo' | 'save', force = false): Promise<boolean> {
    const id = this.state()?.id;
    if (!id) {
      return false;
    }

    this.startAction();
    this.saveConflict.set(false);
    try {
      // An undo that overtook the end of a drag would take back the part before it and leave the rest standing.
      await this.drained();
      const client = await this.api();
      const path = { params: { path: { id } } };
      unwrap(
        await (action === 'undo'
          ? client.POST('/api/v1/stacks/{id}/undo', path)
          : action === 'redo'
            ? client.POST('/api/v1/stacks/{id}/redo', path)
            : client.POST('/api/v1/stacks/{id}/save', {
                ...path,
                params: { ...path.params, query: force ? { force } : {} }
              }))
      );
      await this.refresh();
      return true;
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 409) {
        // For a step, a 409 is only that there is nothing to step to, and the keys can always ask.
        this.saveConflict.set(action === 'save');
      } else {
        this.failure.set(messageOf(failure));
      }

      return false;
    }
  }

  /** The undo history as a list, or null with no stack open. */
  async historySteps(): Promise<HistoryList | null> {
    const id = this.state()?.id;
    if (!id) {
      return null;
    }

    const client = await this.api();
    return unwrap(await client.GET('/api/v1/stacks/{id}/history', { params: { path: { id } } }));
  }

  /** Undoes or redoes until a step is the newest that stands, or undoes everything for null. */
  async jumpTo(step: number | null): Promise<void> {
    const id = this.state()?.id;
    if (!id) {
      return;
    }

    this.startAction();
    try {
      await this.drained(); // a jump that overtook the end of a drag would leave the rest of it standing
      const client = await this.api();
      unwrap(await client.POST('/api/v1/stacks/{id}/history', { params: { path: { id } }, body: { step } }));
      await this.refresh();
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /** What went wrong before has nothing to do with what is starting now. */
  private startAction(): void {
    this.failure.set(null);
    this.onAction();
  }

  /** Waits until every stamp the pointer made has reached the Server. */
  private async drained(): Promise<void> {
    while (this.sending || this.aiming || this.ending || this.pending.length > 0) {
      await (this.sending ?? new Promise(done => setTimeout(done)));
    }
  }

  /** The brush came down on the layer chosen: on the atlas, or on the mesh when it is aimed. */
  begin(stamp: StackStamp, aim?: Aim): void {
    this.startAction();
    this.strokes += 1;
    this.stroke = `drag-${Date.now()}-${this.strokes}`;
    this.view = null;
    this.cloneStep = null;
    this.startedAt = aim ? null : this.onMap(stamp);
    if (this.tool() === 'brush' && !this.masking()) {
      this.remember(this.colour());
    }

    if (aim) {
      void this.aimAt(stamp, aim);
    }

    this.move(stamp);
  }

  /** A position in the 2D view's texels as a place on the map, 0 to 1. */
  private onMap(stamp: StackStamp): CloneSource | null {
    const state = this.state();
    return state ? { u: stamp.x / state.width, v: stamp.y / state.height } : null;
  }

  /**
   * Sets where the clone reads from: a place on the mesh in the viewport, or in the 2D view's texels. A relative clone
   * measures its step again from the next stroke.
   */
  async setCloneSource(stamp: StackStamp, aim?: Aim): Promise<void> {
    this.settingCloneSource.set(false);
    try {
      const place = aim ? await this.hitAt(stamp, aim) : this.onMap(stamp);
      if (place) {
        this.cloneSource.set(place);
        this.keptStep = null;
      }
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /**
   * Fills the piece of the mesh under a click, as the fill tool's mode says, as one undo step. In the viewport the
   * click is picked as the viewport draws (#136): where an isolation leaves nothing under it, nothing is filled.
   */
  async fillAt(stamp: StackStamp, aim?: Aim): Promise<void> {
    const layer = this.layer();
    if (!layer || !this.state()?.model.trim()) {
      return;
    }

    this.startAction();
    try {
      const hit = aim ? await this.picked(stamp, aim) : null;
      if (hit && !hit.found) {
        return;
      }

      this.strokes += 1;
      const stroke = `fill-${Date.now()}-${this.strokes}`;
      await this.send(stroke, layer, [stamp], false, hit?.view ?? null, 'fill', performance.now());
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /** Where on the map a position on the viewport lands, or null off the mesh. */
  private async hitAt(stamp: StackStamp, aim: Aim): Promise<CloneSource | null> {
    const client = await this.api();
    const { width, height, camera } = aim;
    const hit = unwrap(
      await client.POST('/api/v1/viewport/pick', {
        body: { x: stamp.x / width, y: stamp.y / height, camera, width, height }
      })
    );
    return hit.found ? { u: hit.u, v: hit.v } : null;
  }

  private remember(colour: string): void {
    this.recentColours.update(colours =>
      [colour, ...colours.filter(other => other !== colour)].slice(0, RecentColours)
    );
  }

  /** Keeps a colour in the palette, once. */
  keepColour(colour: string): void {
    this.palette.update(colours =>
      colours.includes(colour) ? colours : [...colours, colour].slice(0, PaletteColours)
    );
  }

  /** Takes a colour out of the palette. */
  dropColour(colour: string): void {
    this.palette.update(colours => colours.filter(other => other !== colour));
  }

  /** The brush as a stroke carries it, its size among it. */
  readonly wholeBrush = computed<StackBrush>(() => ({ ...this.brush(), radius: this.radius() }));

  /** Takes up a whole brush, its size among it: a preset's, a project's, or a new one. */
  useBrush(brush: StackBrush): void {
    this.radius.set(brush.radius);
    this.brush.set(settingsOf(brush));
  }

  /** Puts the brush back to the one the Server says a new brush is. The symmetry is not the brush, and stays. */
  resetBrush(): void {
    this.useBrush(this.vocabulary()?.brush.default ?? NewBrush);
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
      const hit = await this.picked(stamp, aim);
      this.view = hit.view;
      this.startedAt = hit.found ? { u: hit.u, v: hit.v } : null;
    } catch (failure) {
      this.failure.set(messageOf(failure));
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

  /** What is at the middle of a picture through a camera: the view a stroke would carry, and the model's box. */
  async look(aim: Aim): Promise<ViewportPick> {
    const client = await this.api();
    const { width, height, camera } = aim;
    return unwrap(await client.POST('/api/v1/viewport/pick', { body: { x: 0.5, y: 0.5, camera, width, height } }));
  }

  private async pick(stamp: StackStamp, aim: Aim): Promise<StackView> {
    return (await this.picked(stamp, aim)).view;
  }

  private async picked(stamp: StackStamp, aim: Aim): Promise<ViewportPick> {
    const client = await this.api();
    const { width, height, camera } = aim;
    return unwrap(
      await client.POST('/api/v1/viewport/pick', {
        body: { x: stamp.x / width, y: stamp.y / height, camera, width, height }
      })
    );
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
    curve: 'free' | 'line' | 'path' | 'fill',
    since: number
  ): Promise<void> {
    const tool = this.tool();
    const picture = this.screenPicture();
    // A picture on the screen is in the viewport's units, so the 2D view has none to project or mask with.
    if (tool === 'projection' && (!view || !picture)) {
      return;
    }

    const symmetry = this.state()?.model.trim() && curve !== 'fill' ? this.symmetry() : null;
    // A size in the screen's units is pixels of the 2D view as drawn, which the stroke takes as texels of the map.
    const brush = this.wholeBrush();
    const radius =
      !view && brush.sizeSpace === 'screen' ? Math.max(0.5, brush.radius * this.atlasScale()) : brush.radius;
    const source = this.sourceOf(tool, view, curve);
    if (tool === 'clone' && !source) {
      this.failure.set(this.cloneSource() ? CloneOnModel : CloneFirst);
      return;
    }

    // A material writes every channel the layer has, from its own maps. A brush writes its colour and every channel
    // it also paints, each in the one stroke and the one undo step. A mask has one.
    const grey = this.maskGrey();
    const target = this.layers().find(row => row.layer.id === layer)?.layer;
    const writes = (usage: string) => !target || writesChannel(target, usage);
    const channels: [string, number[]][] = this.masking()
      ? [[this.channel(), [grey, grey, grey, 1]]]
      : source?.kind === 'material'
        ? (this.state()?.channels ?? [])
            .filter(channel => writes(channel.usage))
            .map((channel): [string, number[]] => [channel.usage, colourOf(this.colour())])
        : [
            [this.channel(), colourOf(this.colour())],
            ...(tool === 'projection'
              ? []
              : [...this.alsoPaints()]
                  .filter(([usage]) => usage !== this.channel() && writes(usage))
                  .map(([usage, colour]): [string, number[]] => [usage, colourOf(colour)]))
          ];
    const revision = await this.command({
      commands: channels.map(([channel, colour]) => ({
        op: 'stroke' as const,
        stroke,
        layer,
        channel,
        brush: { ...brush, radius },
        // A stamp mixes towards its colour, so towards nothing at all takes paint away.
        colour: tool === 'eraser' ? [0, 0, 0, 0] : colour,
        stamps,
        open,
        mask: this.masking(),
        maskLayer: this.masking() ? this.maskLayer() : null,
        view,
        curve,
        symmetry,
        source,
        stencil: view && this.stencil() && tool !== 'projection' ? picture : null,
        fill: curve === 'fill' ? this.fillMode() : null
      }))
    });
    if (revision !== null && revision > this.lastShown) {
      this.sentAt.set(revision, since);
    }
  }

  /**
   * Where a stroke's colour comes from, for the tool in hand: a smudge drags the canvas, a clone reads the composite
   * from its source, the projection tool paints the picture on the screen, and a brush or a fill with a material lays
   * the material's maps. Null paints the colours. A mask takes a grey, a smudge or a picture, never a material.
   */
  private sourceOf(tool: PaintTool, view: StackView | null, curve: string): StackStrokeSource | null {
    switch (tool) {
      case 'smudge':
        return { kind: 'smudge' };
      case 'clone': {
        const step = this.stepOfClone();
        return step ? { kind: 'clone', offset: step } : null;
      }
      case 'projection':
        return view && this.screenPicture() ? { kind: 'picture', picture: this.screenPicture() } : null;
      case 'brush':
      case 'fill': {
        const material = this.material();
        return material && !this.masking() && (tool === 'brush' || curve === 'fill')
          ? { kind: 'material', graph: material.graph }
          : null;
      }
      default:
        return null;
    }
  }

  /**
   * The clone's step for the stroke under way, in texels, from where it started to the source: kept from the first
   * stroke after the source was set when relative, measured at each stroke's start when absolute.
   */
  private stepOfClone(): [number, number] | null {
    const source = this.cloneSource();
    const state = this.state();
    if (this.cloneStep || !source || !state) {
      return this.cloneStep;
    }

    const kept = this.cloneRelative() ? this.keptStep : null;
    const start = this.startedAt;
    const step: [number, number] | null =
      kept ?? (start ? [(source.u - start.u) * state.width, (source.v - start.v) * state.height] : null);
    this.cloneStep = step;
    if (this.cloneRelative() && step) {
      this.keptStep = step;
    }

    return step;
  }

  /** Sends a batch, and answers the revision it made, or null when it was refused. */
  private async command(body: StackCommandBatch): Promise<number | null> {
    const id = this.state()?.id;
    if (!id) {
      return null;
    }

    this.unanswered.update(count => count + 1);
    try {
      const client = await this.api();
      const { revision } = unwrap(
        await client.POST('/api/v1/stacks/{id}/commands', {
          params: { path: { id } },
          body
        })
      );
      this.answered.update(held => (held?.id === id && held.revision >= revision ? held : { id, revision }));
      return revision;
    } catch (failure) {
      this.failure.set(messageOf(failure));
      return null;
    } finally {
      this.unanswered.update(count => count - 1);
    }
  }

  private adopt(state: StackState): void {
    this.state.set(state);
    this.sequence = state.sequence;
    this.channel.update(channel =>
      state.channels.some(c => c.usage === channel) ? channel : (state.channels[0]?.usage ?? channel)
    );
    const rows = rowsOf(state.layers);
    const arriving = this.arriving();
    if (arriving !== null && rows.some(row => row.layer.id === arriving)) {
      this.arriving.set(null);
      this.layer.set(arriving);
    } else if (!rows.some(row => row.layer.id === this.layer())) {
      this.layer.set(rows.find(row => row.layer.kind === 'paint')?.layer.id ?? null);
    }

    // A mask entry chosen that the stack no longer has, undone or lost with a Server that restarted, is let go of, so
    // the brush goes back to the mask's base rather than to an entry the Server would refuse as missing.
    const entry = this.maskLayer();
    const entries = rows.find(row => row.layer.id === this.layer())?.layer.mask.layers?.length ?? 0;
    if (entry !== null && entry >= entries) {
      this.maskLayer.set(null);
    }

    if (state.evaluation) {
      this.heardNotes(state.evaluation.diagnostics);
    }

    this.show(state.previews);
    this.thumbnailsSoon(state.id);
  }

  /** Looks at a channel, or goes back to the one being painted, without changing what a stroke writes. */
  viewChannel(channel: string | null): void {
    this.viewed.set(channel);
    this.show(this.state()?.previews ?? []);
  }

  /** Paints a channel, and shows it. */
  showChannel(channel: string): void {
    this.channel.set(channel);
    this.viewed.set(null);
    this.show(this.state()?.previews ?? []);
  }

  private show(previews: readonly StackPreview[]): void {
    const preview = previews.find(p => p.usage === (this.viewed() ?? this.channel()));
    if (preview) {
      void this.load(preview.hash);
    }
  }

  /** The fills drawn alone, once the stack has been quiet for a moment. */
  readonly thumbnails = signal<ReadonlyMap<string, string>>(new Map());
  private thumbnailWait: ReturnType<typeof setTimeout> | undefined;
  private readonly thumbnailUrls = new Map<string, string>();

  private thumbnailsSoon(id: string): void {
    clearTimeout(this.thumbnailWait);
    // A thumbnail is a nicety: without one a layer keeps its letter, so a failure here tells nobody anything.
    this.thumbnailWait = setTimeout(() => void this.loadThumbnails(id).catch(() => undefined), ThumbnailQuiet);
  }

  private async loadThumbnails(id: string): Promise<void> {
    const client = await this.api();
    const { data } = await client.GET('/api/v1/stacks/{id}/thumbnails', { params: { path: { id } } });
    if (!data || this.state()?.id !== id) {
      return;
    }

    const shown = new Map<string, string>();
    for (const { layer, hash } of data.items) {
      let url = this.thumbnailUrls.get(hash);
      if (!url) {
        const blob = await client.GET('/api/v1/previews/{hash}', { params: { path: { hash } }, parseAs: 'blob' });
        if (!blob.data) {
          continue;
        }

        url = URL.createObjectURL(blob.data);
        this.thumbnailUrls.set(hash, url);
      }

      shown.set(layer, url);
    }

    if (this.state()?.id === id) {
      this.thumbnails.set(shown);
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
    socket.onopen = () => {
      if (this.socket === socket) {
        this.comeback.opened();
      }
    };
    socket.onmessage = message => {
      this.hear(JSON.parse(String(message.data)) as StackEvent);
    };
    socket.onclose = () => {
      if (!this.stopped && this.socket === socket) {
        this.socket = null;
        this.comeback.dropped(id);
      }
    };
  }

  /**
   * The Server restarted, and the stack's session went with it: the stack opens again from its file, which is the
   * truth (plan §3.1). The layer chosen, the channel and what the viewport wore stay as they were, as far as the file
   * still has them, and the person is told if edits that were not saved are gone.
   */
  private async reopenLost(id: string): Promise<void> {
    const lost = this.state();
    if (lost?.id !== id || this.stopped) {
      return;
    }

    const client = await this.api();
    const state = unwrap(await client.POST('/api/v1/stacks', { body: { path: lost.path } }));
    if (this.state()?.id !== id) {
      return; // another document came to the front while this one was opened
    }

    this.failure.set(null);
    if (lost.history.dirty) {
      this.notice.set(RestartedNotice);
      this.recovery.ask(lost.path); // what autosave kept of the edits is offered back (#162)
    }

    this.adopt(state);
    await this.follow();
    if (this.worn()) {
      await this.wear();
    }
  }

  private hear(event: StackEvent): void {
    this.sequence = event.sequence;
    this.state.update(state =>
      state
        ? {
            ...state,
            history: event.history,
            revision: event.revision,
            ...(event.evaluation ? { evaluation: event.evaluation, settled: event.evaluation.revision } : {})
          }
        : state
    );
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
      this.thumbnailsSoon(this.state()?.id ?? '');
    }

    if (event.delta) {
      const { layers, anchors } = event.delta;
      this.state.update(state =>
        state ? { ...state, layers: [...layers], anchors: anchors ? [...anchors] : (state.anchors ?? null) } : state
      );
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

/** Whether a layer composites a channel. No list at all means every one of them. */
export function writesChannel(layer: StackLayer, usage: string): boolean {
  return layer.channels.length === 0 || layer.channels.includes(usage);
}

/** What a stack's toolbar says about its last evaluation, in the graph's words. */
export function stackSummary(state: Pick<StackState, 'revision' | 'settled' | 'evaluation'>): string {
  if (state.settled < state.revision) {
    return 'Evaluating…';
  }

  const evaluation = state.evaluation;
  if (!evaluation || evaluation.error) {
    return '';
  }

  if (evaluation.problems.some(problem => problem.severity === 'Error')) {
    return 'Not evaluated';
  }

  return evaluation.ran === 0
    ? 'Up to date'
    : `${String(evaluation.ran)} ran, ${evaluation.milliseconds.toFixed(0)} ms`;
}

/** A mask's effects with one more of a node, or none of it if it had one: what a toggle such as Invert sends. */
export function toggledEffect(effects: readonly StackMaskEffect[], node: string): StackMaskEffect[] {
  return effects.some(effect => effect.node === node)
    ? effects.filter(effect => effect.node !== node)
    : [...effects, { node, enabled: true }];
}

/** A brush without its size, which the size slider holds apart. */
export function settingsOf(brush: StackBrush): BrushSettings {
  const { radius: _, ...settings } = brush;
  return settings;
}

/** A colour swatch's value as the four numbers a stroke carries. */
export function colourOf(hex: string): number[] {
  const value = Number.parseInt(hex.slice(1), 16);
  return [((value >> 16) & 255) / 255, ((value >> 8) & 255) / 255, (value & 255) / 255, 1];
}
