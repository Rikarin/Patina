import {
  ChangeDetectionStrategy,
  Component,
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
import { SystemStatus } from '../core/system-status';
import { isWireable } from './graph-fields';
import { diagnosticsOf, edgeKey, evaluationSummary } from './graph-model';
import { GraphStore } from './graph-store';
import { NewGraphDialog } from './new-graph-dialog';
import { SaveCompoundDialog } from './save-compound-dialog';

const NodeWidth = 176;
const CategoryColours = 8;
const FitPadding = 48;
const NotePrefix = 'note-';
const GroupPrefix = 'group-';
const NoteOffset = 40;
const portTypes = xuiGraphChartPortTypes(['Image']);

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
  position: XuiGraphPoint;
  /** The frame it sits in, by the id xUI knows that frame by. */
  group: string | undefined;
  /** The graph behind it, when it is a compound. */
  compound: string | null;
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

function portsOf(type: GraphNodeType | undefined): readonly PortView[] {
  return (type?.ports ?? []).filter(isWireable).map(port => ({
    name: port.name,
    direction: port.direction === 'Output' ? 'output' : 'input',
    summary: port.summary,
    dataType: port.kind,
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
  imports: [
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
        size="sm"
        variant="ghost"
        data-testid="graph-settings"
        [active]="store.graphInspected()"
        (click)="store.inspectGraph()"
      >
        Graph
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="graph-note"
        [disabled]="!store.editable()"
        (click)="addNote()"
      >
        Note
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="graph-group"
        [disabled]="!store.editable() || store.selection().size === 0"
        (click)="store.groupSelection()"
      >
        Group
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="graph-save-compound"
        [disabled]="!store.editable() || store.selection().size === 0"
        (click)="savingCompound.set(true)"
      >
        Compound
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="graph-add"
        [disabled]="!store.editable()"
        (click)="openPalette(null)"
      >
        Add node
      </button>
      <button type="button" xuiButton size="sm" variant="ghost" data-testid="graph-new" (click)="creating.set(true)">
        New
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="graph-undo"
        [disabled]="!store.editable() || !current?.history?.canUndo"
        [title]="current?.history?.undoLabel ?? ''"
        (click)="store.undo()"
      >
        Undo
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="graph-redo"
        [disabled]="!store.editable() || !current?.history?.canRedo"
        [title]="current?.history?.redoLabel ?? ''"
        (click)="store.redo()"
      >
        Redo
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        data-testid="graph-save"
        [disabled]="!store.editable() || !current?.history?.dirty"
        (click)="store.save()"
      >
        Save
      </button>
      <button type="button" xuiButton size="sm" variant="ghost" data-testid="graph-close" (click)="store.close()">
        Close
      </button>
    </div>

    @if (store.link() === 'reconnecting') {
      <xui-callout class="shrink-0" color="error" compact data-testid="graph-offline">
        Server unreachable. Editing is paused until it is back.
      </xui-callout>
    } @else if (workerMissing()) {
      <xui-callout class="shrink-0" color="warning" compact data-testid="graph-no-worker">
        No Worker is connected. Edits are kept, and thumbnails catch up when one is.
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
          <xui-graph-node-preview><ptn-graph-thumbnail [node]="view.id" /></xui-graph-node-preview>
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
      <xui-graph-minimap class="right-3 bottom-3" />
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
      noResultsText="No node by that name"
      placeholder="Search nodes"
      [hotkey]="null"
      [itemGroup]="categoryOf"
      [itemPredicate]="matches"
      [itemText]="titleOf"
      [items]="paletteTypes()"
      [open]="paletteOpen()"
      (itemSelected)="add($event)"
      (openChange)="paletteOpen.set($event)"
    />
  `
})
export class GraphCanvas {
  protected readonly store = inject(GraphStore);
  private readonly status = inject(SystemStatus);
  private readonly graph = viewChild.required(XuiNodeGraph);
  private readonly graphElement = viewChild.required(XuiNodeGraph, { read: ElementRef<HTMLElement> });
  private readonly nodeViews = new WeakMap<GraphNodeState, NodeView>();
  private readonly edgeViews = new WeakMap<GraphEdgeState, XuiGraphEdge>();
  private readonly portViews = new Map<string, readonly PortView[]>();
  private addAt: XuiGraphPoint | null = null;
  private addFrom: { node: number; port: string; incoming: boolean } | null = null;
  private fitted: string | null = null;

  protected readonly document = this.store.document;
  protected readonly portTypes = portTypes;
  protected readonly nodeWidth = NodeWidth;
  protected readonly groupPrefix = GroupPrefix;
  protected readonly paletteOpen = signal(false);
  protected readonly creating = signal(false);
  protected readonly savingCompound = signal(false);
  /** The port kind a dropped wire needs, while the palette is open for one. */
  private readonly paletteFilter = signal<'Input' | 'Output' | null>(null);

  protected readonly categoryOf = (type: GraphNodeType): string => type.category || 'Compounds';
  protected readonly titleOf = (type: GraphNodeType): string => type.title;
  protected readonly matches = (query: string, type: GraphNodeType): boolean =>
    type.path.toLowerCase().includes(query.trim().toLowerCase());

  protected readonly paletteTypes = computed(() => {
    const filter = this.paletteFilter();
    const types = [...this.store.types().values()];
    return filter === null
      ? types
      : types.filter(type => type.ports.some(port => port.direction === filter && port.kind === 'Image'));
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
    const drafts = this.store.positions();
    const frames = this.frames();
    const compounds = this.store.compounds();
    const views: NodeView[] = [];
    for (const node of nodes?.values() ?? []) {
      const view = this.viewOf(node, types.get(node.type));
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

  protected openPalette(position: XuiGraphPoint | null): void {
    this.addAt = position;
    this.addFrom = null;
    this.paletteFilter.set(null);
    this.paletteOpen.set(true);
  }

  protected openContextPalette(event: MouseEvent, position: XuiGraphPoint): void {
    event.preventDefault();
    this.openPalette(position);
  }

  protected openDropPalette(drop: XuiGraphConnectionDrop): void {
    if (!this.store.editable()) {
      return;
    }

    const incoming = drop.port.direction === 'input';
    this.addAt = drop.position;
    this.addFrom = { node: Number(drop.source.nodeId), port: drop.source.portId, incoming };
    this.paletteFilter.set(incoming ? 'Output' : 'Input');
    this.paletteOpen.set(true);
  }

  protected add(type: GraphNodeType): void {
    const from = this.addFrom;
    void this.store.addNode(type, this.addAt ?? this.viewCentre(), from ?? undefined);
    this.addAt = null;
    this.addFrom = null;
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
    } else if (event.key === ' ' && this.store.editable() && !target.closest('button, [role=button]')) {
      event.preventDefault();
      this.openPalette(null);
    }
  }

  private viewCentre(): XuiGraphPoint {
    const canvas = this.graph().store;
    const { width, height } = canvas.surfaceSize();
    const { x, y, zoom } = canvas.viewport();
    return { x: (width / 2 - x) / zoom, y: (height / 2 - y) / zoom };
  }

  /** Stable for as long as the node is unchanged, so that one delta touches one node in a graph of 500. */
  private viewOf(node: GraphNodeState, type: GraphNodeType | undefined): NodeView {
    let view = this.nodeViews.get(node);
    if (!view) {
      let ports = this.portViews.get(node.type);
      if (!ports) {
        ports = portsOf(type);
        this.portViews.set(node.type, ports);
      }

      view = {
        id: node.id,
        key: String(node.id),
        title: type?.title ?? node.type,
        accent: accentOf(type?.category ?? ''),
        ports,
        position: { x: node.x, y: node.y },
        group: undefined,
        compound: null
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
          [description]="'Another client closed ' + session.name + '.'"
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

    <xui-dialog
      size="sm"
      title="The file changed"
      [open]="store.saveConflict()"
      (openChange)="$event || store.dismissSaveConflict()"
    >
      <xui-dialog-body>
        {{ store.document()?.name }} changed on disk after it was opened. Saving replaces what is there.
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="store.dismissSaveConflict()">Cancel</button>
        <button type="button" xuiButton color="primary" data-testid="graph-overwrite" (click)="store.save(true)">
          Overwrite
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <ptn-new-graph [(open)]="creating" />
  `
})
export class GraphPane {
  protected readonly store = inject(GraphStore);
  protected readonly creating = signal(false);
}
