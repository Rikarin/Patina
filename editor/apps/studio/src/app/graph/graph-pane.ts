import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import type { GraphDiagnostic, GraphEdgeState, GraphNodeState, GraphNodeType } from '@patina/api-client';
import { provideIcons } from '@ng-icons/core';
import { matAdd, matClose, matDashboard, matNoteAdd, matRedo, matSave, matStickyNote2, matTune, matUndo, matWidgets } from '@ng-icons/material-icons/baseline';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import {
  type XuiGraphConnection,
  type XuiGraphConnectionDrop,
  type XuiGraphEdge,
  type XuiGraphNodeMove,
  type XuiGraphPoint,
  XuiNodeGraph,
  XuiNodeGraphImports,
  xuiGraphChartPortTypes
} from '@xui/node-graph';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiOmnibarImports } from '@xui/omnibar';
import { XuiSpinnerImports } from '@xui/spinner';
import { XuiTagImports } from '@xui/tag';
import { FileConflictDialog } from '../core/file-conflict-dialog';
import { IconImports } from '../core/icons';
import { NodeDragType } from './node-library';
import { SystemStatus } from '../core/system-status';
import { isWireable, wireFamily } from './graph-fields';
import { diagnosticsOf, edgeKey, evaluationSummary } from './graph-model';
import { GraphStore, type NodeLink, type NodeWire } from './graph-store';
import { NewGraphDialog } from './new-graph-dialog';
import { SaveCompoundDialog } from './save-compound-dialog';

const NodeWidth = 176;
const CategoryColours = 8;
const FitPadding = 48;
const NotePrefix = 'note-';
const GroupPrefix = 'group-';
const NoteOffset = 40;
/** How far to the right of the node it follows a new node lands, past the node's own width. */
const FollowingGap = 64;
/** How tall a node with a preview stands, near enough for keeping a new one off it. */
const NodeClearance = 260;
/** The canvas height a 128-pixel minimap may sit in: below this it covers more than it shows. */
const MinimapRoom = 480;
const portTypes = xuiGraphChartPortTypes(['Image', 'Float', 'Int', 'Bool']);
const none: ReadonlySet<string> = new Set();

interface PortView {
  name: string;
  direction: 'input' | 'output';
  summary: string;
  dataType: string;
  /** An input keeps room for a second wire: dropping one on a wired input replaces it, as the Server's connect does. */
  maxConnections: number;
}

interface NodeView {
  id: number;
  key: string;
  title: string;
  accent: string;
  ports: readonly PortView[];
  /** Which ports it has beyond its type's, as a key: the names of the number inputs it draws. */
  portKey: string;
  position: XuiGraphPoint;
  /** The frame it sits in, by the id xUI knows that frame by. */
  group: string | undefined;
  /** The graph behind it, when it is a compound. */
  compound: string | null;
  /** Whether its type has a picture worth a box, which an Output does not. */
  preview: boolean;
}

interface NoteView {
  id: number;
  key: string;
  text: string;
  width: number;
  position: XuiGraphPoint;
}

function hashOf(text: string): number {
  let hash = 0;
  for (let index = 0; index < text.length; index++) {
    hash = (hash * 31 + text.charCodeAt(index)) | 0;
  }

  return Math.abs(hash);
}

function accentOf(category: string): string {
  return `var(--color-chart-${String((hashOf(category) % CategoryColours) + 1)})`;
}

function portsOf(type: GraphNodeType | undefined, numbers: ReadonlySet<string>): readonly PortView[] {
  return (type?.ports ?? []).filter(port => isWireable(port) || numbers.has(port.name)).map(port => ({
    name: port.name,
    direction: port.direction === 'Output' ? 'output' : 'input',
    summary: port.summary,
    dataType: wireFamily(port.kind),
    maxConnections: port.direction === 'Output' ? Number.POSITIVE_INFINITY : 2
  }));
}

/** One node's picture, on a signal of its own so that a new preview redraws this and nothing else. */
@Component({
  selector: 'ptn-graph-thumbnail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block aspect-square w-full bg-surface-inset', '[attr.data-hash]': 'shown()?.hash' },
  template: `
    @if (shown(); as picture) {
      <img class="block size-full" alt="" decoding="async" draggable="false" [src]="picture.url" />
    }
  `
})
export class GraphThumbnail {
  private readonly store = inject(GraphStore);

  readonly node = input.required<number>();

  protected readonly shown = computed(() => this.store.thumbnail(this.node())());
}

/** The toolbar, the canvas and what the Server has to say about the open graph. */
@Component({
  selector: 'ptn-graph-canvas',
  viewProviders: [provideIcons({ matAdd, matClose, matDashboard, matNoteAdd, matRedo, matSave, matStickyNote2, matTune, matUndo, matWidgets })],
  imports: [IconImports, 
    GraphThumbnail,
    NewGraphDialog,
    SaveCompoundDialog,
    XuiButtonImports,
    XuiCalloutImports,
    XuiNodeGraphImports,
    XuiOmnibarImports,
    XuiTagImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col', '(keydown)': 'onKeydown($event)' },
  template: `
    @let current = document();
    <div class="chrome-bar flex h-9 shrink-0 items-center gap-1 px-2">
      @for (crumb of store.trail(); track crumb.id) {
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="graph-crumb"
          [title]="crumb.path"
          (click)="store.back($index)"
        >
          {{ crumb.name }}
        </button>
        <span class="text-foreground-muted" aria-hidden="true">/</span>
      }
      <span class="truncate font-medium" data-testid="graph-name" [title]="current?.path ?? ''">{{
        current?.name
      }}</span>
      @if (current?.readOnly) {
        <xui-tag class="ms-1 shrink-0" minimal data-testid="graph-read-only">Ships with Patina</xui-tag>
        <button
          class="ms-1 shrink-0"
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="graph-copy-compound"
          (click)="store.copyToLibrary()"
        >
          Copy to edit
        </button>
      }
      @if (current?.history?.dirty) {
        <xui-tag class="ms-1 shrink-0" color="warning" minimal data-testid="graph-unsaved">Unsaved</xui-tag>
      }
      <span class="readout text-foreground-muted ms-2 truncate" data-testid="graph-evaluation">{{ evaluated() }}</span>
      <span class="flex-1"></span>
      <button
        type="button"
        xuiButton
        aria-label="Graph settings"
        title="Graph settings"
        size="sm"
        variant="ghost"
        data-testid="graph-settings"
        [active]="store.graphInspected()"
        (click)="store.inspectGraph()"
      >
        <ng-icon xui size="sm" name="matTune" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Add a note"
        title="Add a note"
        size="sm"
        variant="ghost"
        data-testid="graph-note"
        [disabled]="!store.editable()"
        (click)="addNote()"
      >
        <ng-icon xui size="sm" name="matStickyNote2" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Group the selection"
        title="Group the selection"
        size="sm"
        variant="ghost"
        data-testid="graph-group"
        [disabled]="!store.editable() || store.selection().size === 0"
        (click)="store.groupSelection()"
      >
        <ng-icon xui size="sm" name="matDashboard" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Save the selection as a compound"
        title="Save the selection as a compound"
        size="sm"
        variant="ghost"
        data-testid="graph-save-compound"
        [disabled]="!store.editable() || store.selection().size === 0"
        (click)="savingCompound.set(true)"
      >
        <ng-icon xui size="sm" name="matWidgets" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Add a node"
        title="Add a node"
        size="sm"
        variant="ghost"
        data-testid="graph-add"
        [disabled]="!store.editable()"
        (click)="openPalette(null)"
      >
        <ng-icon xui size="sm" name="matAdd" />
      </button>
      <button type="button" xuiButton size="sm" variant="ghost" aria-label="New graph" title="New graph" data-testid="graph-new" (click)="creating.set(true)">
        <ng-icon xui size="sm" name="matNoteAdd" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Undo"
        size="sm"
        variant="ghost"
        data-testid="graph-undo"
        [disabled]="!store.editable() || !current?.history?.canUndo"
        [title]="current?.history?.undoLabel ?? 'Undo'"
        (click)="store.undo()"
      >
        <ng-icon xui size="sm" name="matUndo" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Redo"
        size="sm"
        variant="ghost"
        data-testid="graph-redo"
        [disabled]="!store.editable() || !current?.history?.canRedo"
        [title]="current?.history?.redoLabel ?? 'Redo'"
        (click)="store.redo()"
      >
        <ng-icon xui size="sm" name="matRedo" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Save"
        title="Save"
        size="sm"
        data-testid="graph-save"
        [disabled]="!store.editable() || !current?.history?.dirty"
        (click)="store.save()"
      >
        <ng-icon xui size="sm" name="matSave" />
      </button>
      <button type="button" xuiButton size="sm" variant="ghost" data-testid="graph-close" (click)="store.close()">
        <ng-icon xui size="sm" name="matClose" />
      </button>
    </div>

    @if (store.link() === 'reconnecting') {
      <xui-callout class="shrink-0" color="error" compact data-testid="graph-offline">
        Server unreachable. Editing is paused until it is back.
      </xui-callout>
    } @else if (workerMissing()) {
      <xui-callout class="shrink-0" color="warning" compact data-testid="graph-no-worker">
        The renderer is not running. Edits are kept, and thumbnails catch up when it is back.
      </xui-callout>
    }
    @if (current?.evaluation?.error; as reason) {
      <xui-callout class="shrink-0" color="error" compact data-testid="graph-evaluation-error">{{
        reason
      }}</xui-callout>
    }
    @if (store.problem(); as reason) {
      <xui-callout class="shrink-0" color="error" compact data-testid="graph-problem">
        <span class="flex items-center gap-2">
          <span class="min-w-0 flex-1 truncate" [title]="reason">{{ reason }}</span>
          <button type="button" xuiButton size="sm" variant="ghost" (click)="store.dismissProblem()">Dismiss</button>
        </span>
      </xui-callout>
    }
    @if (store.notice(); as text) {
      <xui-callout class="shrink-0" color="warning" compact data-testid="graph-notice">
        <span class="flex items-center gap-2">
          <span class="min-w-0 flex-1 truncate">{{ text }}</span>
          <button type="button" xuiButton size="sm" variant="ghost" (click)="store.dismissNotice()">Dismiss</button>
        </span>
      </xui-callout>
    }

    <xui-node-graph
      class="min-h-0 flex-1"
      background="dots"
      data-testid="graph-canvas"
      [edges]="edges()"
      [locked]="!store.editable()"
      [maxZoom]="2"
      [minZoom]="0.05"
      [portTypes]="portTypes"
      (canvasContextMenu)="openContextPalette($event.event, $event.position)"
      (connect)="connect($event)"
      (connectionDrop)="openDropPalette($event)"
      (deleteSelection)="remove($event)"
      (nodeMove)="settle($event)"
      (dragover)="overCanvas($event)"
      (drop)="dropOnCanvas($event)"
      (selectionChange)="select($event)"
    >
      @for (group of document()?.groups; track group.id) {
        <xui-graph-group
          data-testid="graph-frame"
          [color]="'var(--color-chart-' + ((group.id % 8) + 1) + ')'"
          [groupId]="groupPrefix + group.id"
          [label]="group.title"
        />
      }
      @for (note of notes(); track note.id) {
        <xui-graph-node
          data-testid="graph-note-card"
          [attr.data-note]="note.id"
          [label]="'Note'"
          [nodeId]="note.key"
          [position]="note.position"
          [width]="note.width"
          (positionChange)="store.moveComment(note.id, $event)"
        >
          <p class="m-0 p-2 text-xs whitespace-pre-wrap">{{ note.text }}</p>
        </xui-graph-node>
      }
      @for (view of views(); track view.id) {
        <xui-graph-node
          data-testid="graph-node"
          [accent]="view.accent"
          [attr.data-compound]="view.compound"
          [attr.data-node]="view.id"
          [group]="view.group"
          [label]="view.title"
          [nodeId]="view.key"
          [portLayout]="'columns'"
          [position]="view.position"
          [width]="nodeWidth"
          (dblclick)="enter(view)"
          (positionChange)="move(view.id, $event)"
        >
          @if (view.preview && store.thumbnail(view.id)()) {
            <xui-graph-node-preview><ptn-graph-thumbnail [node]="view.id" /></xui-graph-node-preview>
          }
          @for (port of view.ports; track port.name) {
            <xui-graph-port
              [dataType]="port.dataType"
              [direction]="port.direction"
              [label]="port.name"
              [maxConnections]="port.maxConnections"
              [portId]="port.name"
              [title]="port.summary"
            />
          }
        </xui-graph-node>
      }
      <xui-graph-controls class="bottom-3 left-3" orientation="horizontal" showZoomLevel />
      <xui-graph-minimap
        data-testid="graph-minimap"
        [class]="minimapHidden() ? 'invisible right-3 bottom-3' : 'right-3 bottom-3'"
      />
    </xui-node-graph>

    @if (diagnostics().length > 0) {
      <ul class="well max-h-24 shrink-0 overflow-auto" aria-label="Diagnostics" data-testid="graph-diagnostics">
        @for (item of diagnostics(); track $index) {
          <li>
            <button
              type="button"
              class="hover:bg-surface-hover flex w-full items-baseline gap-2 px-2 py-0.5 text-start"
              (click)="reveal(item)"
            >
              <span class="readout shrink-0" [class.text-error]="item.severity === 'Error'">{{ item.id }}</span>
              <span class="truncate" [title]="item.message">{{ item.message }}</span>
            </button>
          </li>
        }
      </ul>
    }

    <ptn-new-graph [(open)]="creating" />
    <ptn-save-compound [(open)]="savingCompound" />

    <xui-omnibar
      ariaLabel="Add a node"
      debounce="0"
      noResultsText="No node by that name"
      placeholder="Search nodes"
      [hotkey]="null"
      [itemGroup]="paletteRanked() ? null : categoryOf"
      [itemText]="titleOf"
      [itemsProvider]="searchPalette"
      [open]="paletteOpen()"
      (itemSelected)="add($event)"
      (openChange)="paletteOpen.set($event)"
    />
  `
})
export class GraphCanvas {
  protected readonly store = inject(GraphStore);
  private readonly status = inject(SystemStatus);
  private readonly destroyed = inject(DestroyRef);
  private readonly graph = viewChild.required(XuiNodeGraph);
  private readonly graphElement = viewChild.required(XuiNodeGraph, { read: ElementRef<HTMLElement> });
  private nodeViews = new WeakMap<GraphNodeState, NodeView>();
  private readonly edgeViews = new WeakMap<GraphEdgeState, XuiGraphEdge>();
  private readonly portViews = new Map<string, readonly PortView[]>();
  private viewedTypes: ReadonlyMap<string, GraphNodeType> | null = null;
  private addAt: XuiGraphPoint | null = null;
  private addLink: NodeLink | null = null;
  /** The kinds of wire the open palette has to suit. */
  private addWire: NodeWire = {};
  /** The last query the Server has answered, for telling a list on screen from one on its way. */
  private answered: string | null = null;
  /** The pointer, in the window's coordinates, while it is over the canvas. */
  private pointer: { x: number; y: number } | null = null;
  private fitted: string | null = null;

  protected readonly document = this.store.document;
  protected readonly portTypes = portTypes;
  protected readonly nodeWidth = NodeWidth;
  protected readonly groupPrefix = GroupPrefix;
  protected readonly paletteOpen = signal(false);
  protected readonly creating = signal(false);
  protected readonly savingCompound = signal(false);
  /** Whether the palette shows a ranking, which grouping by category would shuffle. */
  protected readonly paletteRanked = signal(false);

  protected readonly categoryOf = (type: GraphNodeType): string => type.category || 'Compounds';
  protected readonly titleOf = (type: GraphNodeType): string => type.title;
  protected readonly searchPalette = (query: string): Promise<readonly GraphNodeType[]> => {
    const wire = this.addWire;
    return this.store.findNodes(query, wire).then(types => {
      this.answered = query;
      this.paletteRanked.set(query.trim() !== '');
      return types;
    });
  };

  /** The inputs each node has to draw beyond its type's: every wire's, and the ones someone asked for. */
  private readonly wantedPorts = computed(() => {
    const wanted = new Map<number, Set<string>>();
    const add = (node: number, port: string) => {
      const ports = wanted.get(node);
      if (ports) {
        ports.add(port);
      } else {
        wanted.set(node, new Set([port]));
      }
    };
    for (const edge of this.document()?.edges.values() ?? []) {
      add(edge.toNode, edge.toPort);
    }

    for (const key of this.store.shownPorts()) {
      const at = key.indexOf('/');
      add(Number(key.slice(0, at)), key.slice(at + 1));
    }

    return wanted;
  });

  /** The frame each node sits in. xUI gives a node one frame, so the first that names it wins. */
  private readonly frames = computed(() => {
    const frames = new Map<number, string>();
    for (const group of this.document()?.groups ?? []) {
      for (const node of group.nodes) {
        if (!frames.has(node)) {
          frames.set(node, `${GroupPrefix}${String(group.id)}`);
        }
      }
    }

    return frames;
  });

  protected readonly views = computed(() => {
    const nodes = this.document()?.nodes;
    const types = this.store.types();
    if (types !== this.viewedTypes) {
      // A boundary node's ports are its graph's interface, so a type's ports can change while its name does not.
      this.viewedTypes = types;
      this.nodeViews = new WeakMap();
      this.portViews.clear();
    }

    const drafts = this.store.positions();
    const frames = this.frames();
    const compounds = this.store.compounds();
    const wanted = this.wantedPorts();
    const views: NodeView[] = [];
    for (const node of nodes?.values() ?? []) {
      const view = this.viewOf(node, types.get(node.type), wanted.get(node.id));
      const draft = drafts.get(String(node.id));
      const group = frames.get(node.id);
      const compound = compounds.has(node.type) ? node.type : null;
      views.push(
        draft || group !== view.group || compound !== view.compound
          ? { ...view, position: draft ?? view.position, group, compound }
          : view
      );
    }

    return views;
  });

  protected readonly notes = computed(() => {
    const drafts = this.store.positions();
    return (this.document()?.comments ?? []).map((comment): NoteView => ({
      id: comment.id,
      key: `${NotePrefix}${String(comment.id)}`,
      text: comment.text,
      width: comment.width,
      position: drafts.get(`note/${String(comment.id)}`) ?? { x: comment.x, y: comment.y }
    }));
  });

  protected readonly edges = computed(
    () => [...(this.document()?.edges.values() ?? [])].map(edge => this.edgeView(edge)),
    { equal: (a, b) => a.length === b.length && a.every((edge, index) => edge === b[index]) }
  );

  /**
   * xUI gap: the minimap is an overlay, and a port under it cannot be seen or reached. A wire lands on whatever
   * `elementFromPoint` finds, so one dropped there was lost, and a press there never starts one. It slid with the height
   * of the canvas too, so a diagnostics update could put it over a port between two drags. So it stays away from a canvas
   * too small to spare it, and steps aside for as long as a wire is in the air. The whole class string is bound because
   * the minimap has a `class` input, which `[class.x]` does not reach.
   */
  protected readonly minimapHidden = computed(() => {
    const store = this.graph().store;
    return store.linking() !== null || store.surfaceSize().height < MinimapRoom;
  });

  protected readonly diagnostics = computed(() => {
    const document = this.document();
    return document ? diagnosticsOf(document) : [];
  });

  protected readonly evaluated = computed(() => {
    const document = this.document();
    return document ? evaluationSummary(document) : '';
  });

  protected readonly workerMissing = computed(() => {
    const state = this.status.state();
    return state.kind === 'online' && !state.system.worker.connected;
  });

  constructor() {
    effect(() => {
      const type = this.store.adding();
      if (type) {
        this.store.adding.set(null);
        untracked(() => void this.place(type, null, this.selectionLink()));
      }
    });
    // xUI gap: a press on a node stops at the node, so the canvas never takes focus from it, and Delete, the arrows and
    // select-all only work after a press on empty canvas. Listening in the capture phase gets there first.
    afterNextRender(() => {
      const element = this.graphElement().nativeElement as HTMLElement;
      element.addEventListener(
        'pointerdown',
        () => {
          element.focus({ preventScroll: true });
        },
        { capture: true }
      );
      // Not through the template: a move is not a reason to run change detection.
      element.addEventListener('pointermove', event => {
        this.pointer = { x: event.clientX, y: event.clientY };
      });
      element.addEventListener('pointerleave', () => {
        this.pointer = null;
      });
      const space = (event: KeyboardEvent): void => {
        this.openOnSpace(event, element);
      };
      const enter = (event: KeyboardEvent): void => {
        this.enterEarly(event);
      };
      document.addEventListener('keydown', space);
      // Capturing, so that it is ahead of the omnibar's own listener.
      document.addEventListener('keydown', enter, { capture: true });
      this.destroyed.onDestroy(() => {
        document.removeEventListener('keydown', space);
        document.removeEventListener('keydown', enter, { capture: true });
      });
    });

    // The canvas selects what the store says, whichever side changed it.
    effect(() => {
      const note = this.store.selectedNote();
      const wanted = [
        ...[...this.store.selection()].map(String),
        ...(note === null ? [] : [`${NotePrefix}${String(note)}`])
      ];
      const canvas = this.graph().store;
      untracked(() => {
        const held = canvas.selectedNodes();
        if (wanted.length !== held.size || wanted.some(key => !held.has(key))) {
          canvas.setSelection(wanted, []);
        }
      });
    });

    // Once per graph, as soon as every node has been measured.
    effect(() => {
      const document = this.document();
      const canvas = this.graph().store;
      const { width } = canvas.surfaceSize();
      const measured = canvas.nodes().every(node => node.size().height > 0);
      if (
        document &&
        width > 0 &&
        measured &&
        canvas.nodes().length === document.nodes.size + document.comments.length &&
        document.id !== this.fitted
      ) {
        this.fitted = document.id;
        untracked(() => {
          canvas.fitView(FitPadding);
        });
      }
    });
  }

  /** Opens the palette to add at a place, or wherever suits what is selected. */
  protected openPalette(position: XuiGraphPoint | null): void {
    const link = this.selectionLink();
    this.showPalette(position, link, link?.kind === 'wire' ? this.wireOf(link) : {});
  }

  protected openContextPalette(event: MouseEvent, position: XuiGraphPoint): void {
    event.preventDefault();
    this.openPalette(position);
  }

  protected openDropPalette(drop: XuiGraphConnectionDrop): void {
    if (!this.store.editable()) {
      return;
    }

    const node = Number(drop.source.nodeId);
    const port = drop.source.portId;
    if (drop.port.direction === 'input') {
      this.showPalette(drop.position, { kind: 'to', node, port }, { gives: this.kindOf(node, port, 'Input') });
    } else {
      this.showPalette(drop.position, { kind: 'from', node, port }, { takes: this.kindOf(node, port, 'Output') });
    }
  }

  /** Only a node from the library may land here, and only on a graph that can change. */
  protected overCanvas(event: DragEvent): void {
    if (this.store.editable() && event.dataTransfer?.types.includes(NodeDragType)) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  protected dropOnCanvas(event: DragEvent): void {
    const type = this.store.types().get(event.dataTransfer?.getData(NodeDragType) ?? '');
    if (type) {
      event.preventDefault();
      const at = this.graph().store.toGraphPoint(event.clientX, event.clientY);
      void this.place(type, at, this.selectionLink());
    }
  }

  protected add(type: GraphNodeType): void {
    const [at, link] = [this.addAt, this.addLink];
    this.addAt = null;
    this.addLink = null;
    void this.place(type, at, link);
  }

  protected connect(connection: XuiGraphConnection): void {
    this.store.connect(
      Number(connection.source.nodeId),
      connection.source.portId,
      Number(connection.target.nodeId),
      connection.target.portId
    );
  }

  protected move(node: number, position: XuiGraphPoint): void {
    this.store.moveNode(node, position);
  }

  protected select(selection: { nodes: string[] }): void {
    const nodes = selection.nodes.filter(key => !key.startsWith(NotePrefix));
    this.store.select(nodes.map(Number));
    const note = selection.nodes.find(key => key.startsWith(NotePrefix));
    this.store.selectNote(nodes.length === 0 && note !== undefined ? Number(note.slice(NotePrefix.length)) : null);
  }

  protected remove(selection: { nodes: string[]; edges: string[] }): void {
    const nodes = selection.nodes.filter(key => !key.startsWith(NotePrefix));
    this.store.remove(nodes.map(Number), selection.edges);
    const notes = selection.nodes
      .filter(key => key.startsWith(NotePrefix))
      .map(key => Number(key.slice(NotePrefix.length)));
    for (const id of notes) {
      this.store.removeComment(id);
    }
  }

  /** A drag came to rest. Dropping a node on a frame, or off one, changes its group. */
  protected settle(move: XuiGraphNodeMove): void {
    this.store.endMove();
    if (move.source === 'node') {
      this.store.regroup(
        move.nodes
          .filter(item => !item.nodeId.startsWith(NotePrefix))
          .map(item => ({ node: Number(item.nodeId), group: item.group }))
      );
    }
  }

  protected enter(view: NodeView): void {
    if (view.compound !== null) {
      void this.store.enterCompound(view.compound);
    }
  }

  protected addNote(): void {
    const { x, y } = this.viewCentre();
    void this.store.addComment({ x: x - NoteOffset, y: y - NoteOffset });
  }

  protected reveal(item: GraphDiagnostic): void {
    if (item.node !== undefined && item.node !== null) {
      this.store.select([item.node]);
      this.graph().store.centerOn(String(item.node));
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (target.closest('input, textarea, [contenteditable]')) {
      return;
    }

    const command = event.metaKey || event.ctrlKey;
    if (this.store.shortcut(event)) {
      return;
    }

    if (command && event.key.toLowerCase() === 'g') {
      event.preventDefault();
      this.store.groupSelection();
    }
  }

  /** Space adds at the pointer, from anywhere the pointer is over the canvas, and not only once it has focus. */
  private openOnSpace(event: KeyboardEvent, canvas: HTMLElement): void {
    const target = event.target as HTMLElement;
    const here = this.pointer !== null || canvas.contains(target);
    if (
      event.key === ' ' &&
      here &&
      this.store.editable() &&
      !this.paletteOpen() &&
      !target.closest('input, textarea, select, button, [contenteditable], [role=button], [role=dialog]')
    ) {
      event.preventDefault();
      const { x, y } = this.pointer ?? { x: null, y: null };
      this.openPalette(x === null ? null : this.graph().store.toGraphPoint(x, y));
    }
  }

  private showPalette(at: XuiGraphPoint | null, link: NodeLink | null, wire: NodeWire): void {
    this.addAt = at;
    this.addLink = link;
    this.addWire = wire;
    this.answered = null;
    this.paletteOpen.set(true);
  }

  /**
   * xUI gap: the omnibar acts on the list it holds, and a provider's list arrives a moment after the keys. Enter
   * straight after typing would add the first match of an earlier query, or nothing while the list is still empty. So
   * an Enter that comes before the answer to what the box holds is taken here, before the omnibar sees it, and adds the
   * best match of what was typed. Once the answer is on screen, Enter, a click and the arrows are the omnibar's.
   */
  private enterEarly(event: KeyboardEvent): void {
    const active = document.activeElement;
    if (event.key !== 'Enter' || !this.paletteOpen() || !(active instanceof HTMLInputElement)) {
      return;
    }

    const typed = active.value;
    if (typed === this.answered) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    const [at, link, wire] = [this.addAt, this.addLink, this.addWire];
    this.paletteOpen.set(false);
    void this.store.findNodes(typed, wire).then(([best]) => (best ? this.place(best, at, link) : undefined));
  }

  /**
   * Adds a node where it was asked for, or else beside what it joins. A wire only takes a node that fits both of its
   * ends, since the other half of the job would leave the graph with one end cut: a palette offered only those, and a
   * node from the library is checked.
   */
  private async place(type: GraphNodeType, at: XuiGraphPoint | null, link: NodeLink | null): Promise<void> {
    const fits =
      link?.kind !== 'wire' ||
      (await this.store.findNodes(type.title, this.wireOf(link))).some(found => found.path === type.path);
    await this.store.addNode(type, at ?? this.spotFor(link), (fits ? link : null) ?? undefined);
  }

  /** What a new node joins: the selected wire, or the one selected node that has an output. */
  private selectionLink(): NodeLink | null {
    const edges = [...this.graph().store.selectedEdges()];
    const [onlyEdge] = edges;
    const edge = edges.length === 1 && onlyEdge !== undefined ? this.document()?.edges.get(onlyEdge) : undefined;
    if (edge) {
      const { fromNode, fromPort, toNode, toPort } = edge;
      return { kind: 'wire', node: fromNode, port: fromPort, toNode, toPort };
    }

    const nodes = [...this.store.selection()];
    const [only] = nodes;
    return nodes.length === 1 && only !== undefined ? { kind: 'from', node: only } : null;
  }

  private wireOf(link: Extract<NodeLink, { kind: 'wire' }>): NodeWire {
    return {
      takes: this.kindOf(link.node, link.port, 'Output'),
      gives: this.kindOf(link.toNode, link.toPort, 'Input')
    };
  }

  private kindOf(node: number, port: string, direction: 'Input' | 'Output'): string | undefined {
    const type = this.store.types().get(this.document()?.nodes.get(node)?.type ?? '');
    return type?.ports.find(held => held.name === port && held.direction === direction)?.kind;
  }

  /** Where a node goes when nothing said: after what it follows, between the ends of its wire, or in the view. */
  private spotFor(link: NodeLink | null): XuiGraphPoint {
    const nodes = this.document()?.nodes;
    const from = link === null ? undefined : nodes?.get(link.node);
    const to = link?.kind === 'wire' ? nodes?.get(link.toNode) : undefined;
    if (from && to) {
      return this.clearOf({ x: (from.x + to.x) / 2, y: (from.y + to.y) / 2 });
    }

    return from && link?.kind !== 'to'
      ? this.clearOf({ x: from.x + NodeWidth + FollowingGap, y: from.y })
      : this.viewCentre();
  }

  /** The first place at or below a spot that no node covers, so that a new node does not hide the one there. */
  private clearOf(spot: XuiGraphPoint): XuiGraphPoint {
    const nodes = [...(this.document()?.nodes.values() ?? [])];
    let { y } = spot;
    while (nodes.some(node => Math.abs(node.x - spot.x) < NodeWidth && Math.abs(node.y - y) < NodeClearance)) {
      y += NodeClearance;
    }

    return { x: spot.x, y };
  }

  private viewCentre(): XuiGraphPoint {
    const canvas = this.graph().store;
    const { width, height } = canvas.surfaceSize();
    const { x, y, zoom } = canvas.viewport();
    return { x: (width / 2 - x) / zoom, y: (height / 2 - y) / zoom };
  }

  /** Stable for as long as the node is unchanged, so that one delta touches one node in a graph of 500. */
  private viewOf(node: GraphNodeState, type: GraphNodeType | undefined, wanted: ReadonlySet<string> = none): NodeView {
    const numbers = (type?.ports ?? []).filter(port => wanted.has(port.name) && !isWireable(port));
    const portKey = numbers.map(port => port.name).join('\0');
    let view = this.nodeViews.get(node);
    if (view?.portKey !== portKey) {
      const portsKey = `${node.type}\0${portKey}`;
      let ports = this.portViews.get(portsKey);
      if (!ports) {
        ports = portsOf(type, wanted);
        this.portViews.set(portsKey, ports);
      }

      view = {
        id: node.id,
        key: String(node.id),
        title: type?.title ?? node.type,
        accent: accentOf(type?.category ?? ''),
        ports,
        portKey,
        position: { x: node.x, y: node.y },
        group: undefined,
        compound: null,
        preview: type?.hasPreview ?? true
      };
      this.nodeViews.set(node, view);
    }

    return view;
  }

  private edgeView(edge: GraphEdgeState): XuiGraphEdge {
    let view = this.edgeViews.get(edge);
    if (!view) {
      view = {
        id: edgeKey(edge),
        source: { nodeId: String(edge.fromNode), portId: edge.fromPort },
        target: { nodeId: String(edge.toNode), portId: edge.toPort }
      };
      this.edgeViews.set(edge, view);
    }

    return view;
  }
}

/** The Graph dock pane: the states around a graph, and the graph. */
@Component({
  selector: 'ptn-graph',
  imports: [
    FileConflictDialog,
    GraphCanvas,
    NewGraphDialog,
    XuiButtonImports,
    XuiDialogImports,
    XuiNonIdealStateImports,
    XuiSpinnerImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    @let session = store.session();
    @switch (session.kind) {
      @case ('none') {
        <xui-non-ideal-state
          class="flex-1"
          title="No graph open"
          description="Select a graph in the Library's Shared Assets, then choose Open in the Inspector."
          data-testid="graph-none"
        >
          <button action type="button" xuiButton size="sm" data-testid="graph-new" (click)="creating.set(true)">
            New graph
          </button>
        </xui-non-ideal-state>
      }
      @case ('opening') {
        <div class="grid flex-1 place-items-center">
          <xui-spinner size="md" aria-label="Opening the graph" />
        </div>
      }
      @case ('failed') {
        <xui-non-ideal-state
          class="flex-1"
          title="The graph did not open"
          data-testid="graph-failed"
          [description]="session.reason"
        >
          <button action type="button" xuiButton size="sm" (click)="store.reopen()">Try again</button>
          @if (store.trail().length > 0) {
            <button action type="button" xuiButton size="sm" (click)="store.back(store.trail().length - 1)">
              Back
            </button>
          }
        </xui-non-ideal-state>
      }
      @case ('ended') {
        <xui-non-ideal-state
          class="flex-1"
          title="This graph was closed"
          data-testid="graph-ended"
          [description]="session.name + ' was closed in another window.'"
        >
          <button action type="button" xuiButton size="sm" (click)="store.reopen()">Open it again</button>
        </xui-non-ideal-state>
      }
      @case ('open') {
        <ptn-graph-canvas />
      }
    }

    <xui-dialog
      size="sm"
      title="Unsaved changes"
      [open]="store.leaving() !== null"
      (openChange)="$event || store.resolveLeaving('cancel')"
    >
      <xui-dialog-body data-testid="graph-leaving">
        {{ store.document()?.name }} has edits that are not in its file.
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="store.resolveLeaving('cancel')">Cancel</button>
        <button type="button" xuiButton data-testid="graph-discard" (click)="store.resolveLeaving('discard')">
          Discard
        </button>
        <button type="button" xuiButton color="primary" (click)="store.resolveLeaving('save')">Save</button>
      </xui-dialog-footer>
    </xui-dialog>

    <ptn-file-conflict
      [name]="store.document()?.name ?? ''"
      [open]="store.saveConflict()"
      (cancelled)="store.dismissSaveConflict()"
      (overwrite)="store.save(true)"
    />

    <ptn-new-graph [(open)]="creating" />
  `
})
export class GraphPane {
  protected readonly store = inject(GraphStore);
  protected readonly creating = signal(false);
}
