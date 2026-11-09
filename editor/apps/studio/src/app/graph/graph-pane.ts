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
import { provideIcons } from '@ng-icons/core';
import {
  matAccountTree,
  matAdd,
  matClose,
  matDashboard,
  matNoteAdd,
  matPowerSettingsNew,
  matRedo,
  matSave,
  matStickyNote2,
  matTune,
  matUndo,
  matWidgets
} from '@ng-icons/material-icons/baseline';
import type { GraphDiagnostic, GraphEdgeState, GraphNodeState, GraphNodeType } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiContextMenuImports } from '@xui/context-menu';
import { XuiDialogImports } from '@xui/dialog';
import { XuiMenuImports } from '@xui/menu';
import {
  type XuiGraphConnection,
  type XuiGraphConnectionDrop,
  type XuiGraphEdge,
  type XuiGraphNodeHandle,
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
import { Commands } from '../core/commands';
import { FileConflictDialog } from '../core/file-conflict-dialog';
import { IconImports } from '../core/icons';
import { diagnosticOf } from '../core/messages';
import { Preferences, type WireStyle } from '../core/preferences';
import { SystemStatus } from '../core/system-status';
import { frameShown } from './frame-colours';
import {
  type Alignment,
  type NodeBox,
  type PaletteLine,
  aligned,
  distributed,
  findMatches,
  flowOf,
  paletteOf
} from './graph-authoring';
import { isWireable, wireFamily } from './graph-fields';
import { diagnosticsOf, edgeKey, evaluationSummary } from './graph-model';
import { GraphStore, type NodeLink, type NodeWire, RerouteIn, RerouteOut, RerouteType } from './graph-store';
import { NewGraphDialog } from './new-graph-dialog';
import { NodeDragType } from './node-library';
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
/** How wide a reroute dot's card is: room for its two connectors and nothing else. */
const RerouteWidth = 28;
/** How far below a reroute card's top its connectors are, in graph units, as measured on a drawn card. */
const RerouteConnectorDepth = 42;
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
  /** Switched off: what feeds it passes straight through. */
  bypassed: boolean;
  /** A reroute dot, drawn as a pill with a connector each side. */
  reroute: boolean;
}

/** What the compiler said about one node, for its badge. */
interface NodeProblem {
  severity: 'Error' | 'Warning';
  count: number;
  summary: string;
}

/** A line of the palette. A favourite is under its category as well. */
type PaletteItem = PaletteLine<GraphNodeType>;

/** A line of Find Node: a node of the graph, by the title its card shows. */
interface FoundNode {
  id: number;
  title: string;
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
  return (type?.ports ?? [])
    .filter(port => isWireable(port) || numbers.has(port.name))
    .map(port => ({
      name: port.name,
      direction: port.direction === 'Output' ? 'output' : 'input',
      summary: port.summary,
      dataType: wireFamily(port.kind),
      maxConnections: port.direction === 'Output' ? Number.POSITIVE_INFINITY : 2
    }));
}

/**
 * A reroute dot's two connectors. It carries whatever reaches it, so its ports declare no data type and xUI lets any
 * wire try; the Server says no to one it cannot carry.
 */
const reroutePorts: readonly PortView[] = [
  { name: RerouteIn, direction: 'input', summary: 'In', dataType: '', maxConnections: 2 },
  { name: RerouteOut, direction: 'output', summary: 'Out', dataType: '', maxConnections: Number.POSITIVE_INFINITY }
];

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
  viewProviders: [
    provideIcons({
      matAccountTree,
      matAdd,
      matClose,
      matDashboard,
      matNoteAdd,
      matPowerSettingsNew,
      matRedo,
      matSave,
      matStickyNote2,
      matTune,
      matUndo,
      matWidgets
    })
  ],
  imports: [
    IconImports,
    GraphThumbnail,
    NewGraphDialog,
    SaveCompoundDialog,
    XuiButtonImports,
    XuiCalloutImports,
    XuiContextMenuImports,
    XuiMenuImports,
    XuiNodeGraphImports,
    XuiOmnibarImports,
    XuiTagImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col', 'data-commands': 'graph' },
  styles: `
    /* xUI gap: a node always draws a header, and a reroute dot is only its two connectors. */
    [data-reroute] [data-xui-graph-node-header] {
      display: none;
    }
    [data-reroute] {
      border-radius: 9999px;
    }
  `,
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
        aria-label="Bypass the selection"
        [title]="commands.tooltip('bypassNodes')"
        size="sm"
        variant="ghost"
        data-testid="graph-bypass"
        [disabled]="!store.editable() || store.selection().size === 0"
        (click)="store.toggleBypass()"
      >
        <ng-icon xui size="sm" name="matPowerSettingsNew" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Arrange"
        title="Arrange: lay out, align or distribute"
        size="sm"
        variant="ghost"
        data-testid="graph-arrange"
        [disabled]="!store.editable()"
        [xuiMenuTriggerFor]="arrangeMenu"
      >
        <ng-icon xui size="sm" name="matAccountTree" />
      </button>
      <button
        type="button"
        xuiButton
        aria-label="Group the selection"
        [title]="commands.tooltip('groupNodes')"
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
        [title]="commands.tooltip('addNode')"
        size="sm"
        variant="ghost"
        data-testid="graph-add"
        [disabled]="!store.editable()"
        (click)="openPalette(null)"
      >
        <ng-icon xui size="sm" name="matAdd" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        aria-label="New graph"
        title="New graph"
        data-testid="graph-new"
        (click)="creating.set(true)"
      >
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
        [title]="commands.tooltip('undo', current?.history?.undoLabel ? 'Undo ' + current.history.undoLabel : 'Undo')"
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
        [title]="commands.tooltip('redo', current?.history?.redoLabel ? 'Redo ' + current.history.redoLabel : 'Redo')"
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
        Lost touch with Patina’s engine. Editing is paused until it reconnects.
      </xui-callout>
    } @else if (workerMissing()) {
      <xui-callout class="shrink-0" color="warning" compact data-testid="graph-no-worker">
        The GPU engine is not running. Edits are kept, and thumbnails catch up when it starts again.
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
      [routing]="preferences.graphWires()"
      [snapToGrid]="preferences.graphSnap()"
      (canvasContextMenu)="openContextPalette($event.event, $event.position)"
      (connect)="connect($event)"
      (connectionDrop)="openDropPalette($event)"
      (deleteSelection)="remove($event)"
      (edgeClick)="clickEdge($event)"
      (nodeMove)="settle($event)"
      (dragover)="overCanvas($event)"
      (drop)="dropOnCanvas($event)"
      (selectionChange)="select($event)"
    >
      @for (group of document()?.groups; track group.id) {
        <xui-graph-group
          data-testid="graph-frame"
          [attr.title]="group.description || null"
          [color]="frameShown(group.color, group.id)"
          [groupId]="groupPrefix + group.id"
          [label]="group.title"
        >
          @if (group.description) {
            <span class="text-foreground-muted min-w-0 truncate font-normal" data-testid="graph-frame-description">{{
              group.description
            }}</span>
          }
        </xui-graph-group>
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
        @let problem = problems().get(view.id);
        <xui-graph-node
          data-testid="graph-node"
          [accent]="view.reroute ? undefined : view.accent"
          [attr.aria-label]="view.reroute ? 'Reroute' : null"
          [attr.data-bypassed]="view.bypassed || null"
          [attr.data-compound]="view.compound"
          [attr.data-node]="view.id"
          [attr.data-reroute]="view.reroute || null"
          [dimmed]="view.bypassed"
          [group]="view.group"
          [label]="view.title"
          [nodeId]="view.key"
          [portLayout]="'columns'"
          [position]="view.position"
          [width]="view.reroute ? rerouteWidth : nodeWidth"
          [xuiContextMenuTriggerFor]="nodeMenu"
          (contextmenu)="aimNode(view.id)"
          (dblclick)="enter(view)"
          (positionChange)="move(view.id, $event)"
        >
          @if (view.bypassed || problem) {
            <xui-graph-node-actions>
              @if (view.bypassed) {
                <xui-tag
                  minimal
                  size="sm"
                  data-testid="graph-node-bypassed"
                  title="Bypassed: what feeds it passes through"
                  >Off</xui-tag
                >
              }
              @if (problem) {
                <span
                  class="readout rounded px-1 text-[10px] leading-4 font-semibold"
                  data-testid="graph-node-problem"
                  [attr.data-severity]="problem.severity"
                  [class.bg-error]="problem.severity === 'Error'"
                  [class.bg-warning]="problem.severity === 'Warning'"
                  [class.text-error-foreground]="problem.severity === 'Error'"
                  [class.text-warning-foreground]="problem.severity === 'Warning'"
                  [title]="problem.summary"
                  >{{ problem.count }}</span
                >
              }
            </xui-graph-node-actions>
          }
          @if (view.preview && store.thumbnail(view.id)()) {
            <xui-graph-node-preview><ptn-graph-thumbnail [node]="view.id" /></xui-graph-node-preview>
          }
          @for (port of view.ports; track port.name) {
            <xui-graph-port
              [dataType]="port.dataType"
              [direction]="port.direction"
              [label]="view.reroute ? '' : port.name"
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
      <div class="well relative max-h-24 shrink-0 overflow-auto">
        <button
          type="button"
          class="text-foreground-muted hover:text-foreground absolute end-1 top-0.5 text-xs"
          data-testid="graph-diagnostics-details"
          [attr.aria-pressed]="diagnosticDetails()"
          (click)="diagnosticDetails.set(!diagnosticDetails())"
        >
          {{ diagnosticDetails() ? 'Hide Details' : 'Details' }}
        </button>
        <ul aria-label="Problems" data-testid="graph-diagnostics">
          @for (item of diagnostics(); track $index) {
            @let said = diagnosticOf(item.message);
            <li>
              <button
                type="button"
                class="hover:bg-surface-hover flex w-full items-baseline gap-2 px-2 py-0.5 pe-20 text-start"
                (click)="reveal(item)"
              >
                <span class="readout shrink-0" [class.text-error]="item.severity === 'Error'">{{
                  item.severity === 'Error' ? 'Error' : 'Warning'
                }}</span>
                @if (diagnosticDetails()) {
                  <span class="min-w-0" data-testid="graph-diagnostic-detail">
                    <span class="readout text-foreground-muted">{{ item.id }}</span> {{ item.message }}
                  </span>
                } @else {
                  <span class="truncate" [title]="said.summary">{{ said.summary }}</span>
                }
              </button>
            </li>
          }
        </ul>
      </div>
    }

    <ptn-new-graph [(open)]="creating" />
    <ptn-save-compound [(open)]="savingCompound" />

    <ng-template #arrangeMenu>
      <xui-menu aria-label="Arrange" data-testid="graph-arrange-menu">
        <button type="button" xuiMenuItem data-testid="arrange-layout" (triggered)="store.arrange()">
          {{ store.selection().size > 1 ? 'Lay Out Selection' : 'Lay Out Graph' }}
        </button>
        <xui-menu-divider />
        @for (item of alignments; track item.alignment) {
          <button
            type="button"
            xuiMenuItem
            [attr.data-testid]="'arrange-' + item.alignment"
            [disabled]="store.selection().size < 2"
            (triggered)="align(item.alignment, item.label)"
          >
            {{ item.label }}
          </button>
        }
        <xui-menu-divider />
        <button
          type="button"
          xuiMenuItem
          data-testid="arrange-distribute-horizontal"
          [disabled]="store.selection().size < 3"
          (triggered)="distribute('horizontal')"
        >
          Distribute Horizontally
        </button>
        <button
          type="button"
          xuiMenuItem
          data-testid="arrange-distribute-vertical"
          [disabled]="store.selection().size < 3"
          (triggered)="distribute('vertical')"
        >
          Distribute Vertically
        </button>
        <xui-menu-divider />
        <button
          type="button"
          xuiMenuItem
          data-testid="arrange-snap"
          role="menuitemcheckbox"
          [attr.aria-checked]="preferences.graphSnap()"
          [selected]="preferences.graphSnap()"
          (triggered)="commands.run('toggleGraphSnap')"
        >
          Snap to Grid
        </button>
        @for (style of wireStyles; track style.command) {
          <button
            type="button"
            xuiMenuItem
            role="menuitemradio"
            [attr.aria-checked]="preferences.graphWires() === style.value"
            [attr.data-testid]="'arrange-' + style.command"
            [selected]="preferences.graphWires() === style.value"
            (triggered)="commands.run(style.command)"
          >
            {{ style.label }}
          </button>
        }
      </xui-menu>
    </ng-template>

    <ng-template #nodeMenu>
      <xui-menu aria-label="Node" data-testid="graph-node-menu">
        <button
          type="button"
          xuiMenuItem
          data-testid="node-bypass"
          [disabled]="!store.editable()"
          (triggered)="store.toggleBypass()"
        >
          {{ menuBypassed() ? 'Stop Bypassing' : 'Bypass' }}
          <span menuItemLabel>{{ commands.keyText('bypassNodes') }}</span>
        </button>
        <button
          type="button"
          xuiMenuItem
          data-testid="node-relink"
          [disabled]="!store.editable()"
          (triggered)="store.removeRelinking(selectedNodes())"
        >
          Delete and Relink <span menuItemLabel>{{ commands.keyText('deleteRelink') }}</span>
        </button>
        <button
          type="button"
          xuiMenuItem
          [disabled]="!store.editable()"
          (triggered)="store.remove(selectedNodes(), [])"
        >
          Delete <span menuItemLabel>{{ commands.keyText('delete') }}</span>
        </button>
        <xui-menu-divider />
        <button
          type="button"
          xuiMenuItem
          [disabled]="!store.editable() || store.selection().size < 2"
          (triggered)="store.arrange()"
        >
          Lay Out Selection
        </button>
      </xui-menu>
    </ng-template>

    <xui-omnibar
      ariaLabel="Add a node"
      debounce="0"
      noResultsText="No node by that name"
      placeholder="Search nodes"
      [hotkey]="null"
      [itemGroup]="paletteRanked() ? null : groupOf"
      [itemText]="titleOf"
      [itemsProvider]="searchPalette"
      [open]="paletteOpen()"
      (itemSelected)="add($event)"
      (openChange)="paletteOpen.set($event)"
    />
    <xui-omnibar
      ariaLabel="Find a node"
      debounce="0"
      noResultsText="No node by that name in this graph"
      placeholder="Find in this graph"
      data-testid="graph-find"
      [hotkey]="null"
      [itemText]="foundTitle"
      [itemsProvider]="searchGraph"
      [open]="finding()"
      (itemSelected)="reveal({ node: $event.id })"
      (openChange)="finding.set($event)"
    />
  `
})
export class GraphCanvas {
  protected readonly store = inject(GraphStore);
  protected readonly preferences = inject(Preferences);
  private readonly status = inject(SystemStatus);
  private readonly destroyed = inject(DestroyRef);
  protected readonly commands = inject(Commands);
  private readonly graph = viewChild.required(XuiNodeGraph);
  private readonly graphElement = viewChild.required(XuiNodeGraph, { read: ElementRef<HTMLElement> });
  private nodeViews = new WeakMap<GraphNodeState, NodeView>();
  private readonly edgeViews = new WeakMap<GraphEdgeState, XuiGraphEdge>();
  private readonly flowingViews = new WeakMap<GraphEdgeState, XuiGraphEdge>();
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
  protected readonly rerouteWidth = RerouteWidth;
  protected readonly alignments: readonly { alignment: Alignment; command: string; label: string }[] = [
    { alignment: 'left', command: 'alignLeft', label: 'Align Left' },
    { alignment: 'centre', command: 'alignCentres', label: 'Align Centres' },
    { alignment: 'right', command: 'alignRight', label: 'Align Right' },
    { alignment: 'top', command: 'alignTop', label: 'Align Top' },
    { alignment: 'middle', command: 'alignMiddles', label: 'Align Middles' },
    { alignment: 'bottom', command: 'alignBottom', label: 'Align Bottom' }
  ];
  /** Where the last press on the canvas was, and whether Alt was held, for a click on a wire. */
  private lastPress: { x: number; y: number; alt: boolean } | null = null;
  protected readonly groupPrefix = GroupPrefix;
  protected readonly frameShown = frameShown;
  protected readonly paletteOpen = signal(false);
  protected readonly finding = signal(false);
  protected readonly wireStyles: readonly { value: WireStyle; command: string; label: string }[] = [
    { value: 'bezier', command: 'curvedWires', label: 'Curved Wires' },
    { value: 'smoothstep', command: 'angledWires', label: 'Angled Wires' },
    { value: 'straight', command: 'straightWires', label: 'Straight Wires' }
  ];
  protected readonly foundTitle = (found: FoundNode): string => found.title;
  /** Every node of the graph by the title its card shows, those that start with what was typed first. */
  protected readonly searchGraph = (query: string): Promise<readonly FoundNode[]> => {
    const types = this.store.types();
    const nodes = [...(this.document()?.nodes.values() ?? [])]
      .filter(node => node.type !== RerouteType)
      .map(node => ({ id: node.id, title: types.get(node.type)?.title ?? node.type }));
    return Promise.resolve(findMatches(nodes, query));
  };
  protected readonly creating = signal(false);
  protected readonly savingCompound = signal(false);
  /** Whether the palette shows a ranking, which grouping by category would shuffle. */
  protected readonly paletteRanked = signal(false);

  protected readonly groupOf = (item: PaletteItem): string => item.group;
  protected readonly titleOf = (item: PaletteItem): string => item.type.title;
  /**
   * What the palette offers: with nothing typed, the starred node types, then the ones added last, then every one by
   * category; typed, the Server's ranking. Each list keeps only what the wire being made can use.
   */
  protected readonly searchPalette = (query: string): Promise<readonly PaletteItem[]> => {
    const wire = this.addWire;
    return this.store.findNodes(query, wire).then(types => {
      this.answered = query;
      const ranked = query.trim() !== '';
      this.paletteRanked.set(ranked);
      return ranked
        ? types.map(type => ({ type, group: '' }))
        : paletteOf(types, this.preferences.favouriteNodes(), this.preferences.recentNodes());
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

  /**
   * The wires, those a picture takes into and out of the selection marching the way it flows, so a node's sources and
   * what it ends up in show at a glance.
   */
  protected readonly edges = computed(
    () => {
      const wires = [...(this.document()?.edges.values() ?? [])];
      const flowing = flowOf(wires, this.store.selection());
      return wires.map(edge => (flowing.has(edge) ? this.flowingView(edge) : this.edgeView(edge)));
    },
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

  /** Whether the problems list shows each one's code and its whole text, which is for whoever reports it. */
  protected readonly diagnosticDetails = signal(false);
  protected readonly diagnosticOf = diagnosticOf;

  protected readonly diagnostics = computed(() => {
    const document = this.document();
    return document ? diagnosticsOf(document) : [];
  });

  protected readonly evaluated = computed(() => {
    const document = this.document();
    return document ? evaluationSummary(document) : '';
  });

  /** Each node's worst problem and how many it has, from the compiler's last word. */
  protected readonly problems = computed(() => {
    const problems = new Map<number, NodeProblem>();
    for (const item of this.diagnostics()) {
      if (item.node === undefined || item.node === null) {
        continue;
      }

      const held = problems.get(item.node);
      const severity = item.severity === 'Error' ? 'Error' : 'Warning';
      if (!held) {
        problems.set(item.node, { severity, count: 1, summary: diagnosticOf(item.message).summary });
      } else {
        held.count++;
        if (severity === 'Error' && held.severity === 'Warning') {
          held.severity = 'Error';
          held.summary = diagnosticOf(item.message).summary;
        }
      }
    }

    return problems;
  });

  protected readonly selectedNodes = computed(() => [...this.store.selection()]);

  /** Whether everything the node menu acts on is already switched off. */
  protected readonly menuBypassed = computed(() => {
    const nodes = this.document()?.nodes;
    const chosen = this.selectedNodes();
    return chosen.length > 0 && chosen.every(id => nodes?.get(id)?.bypassed);
  });

  protected readonly workerMissing = computed(() => {
    const state = this.status.state();
    return state.kind === 'online' && !state.system.worker.connected;
  });

  constructor() {
    this.installCommands();
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
        event => {
          element.focus({ preventScroll: true });
          this.lastPress = { x: event.clientX, y: event.clientY, alt: event.altKey };
        },
        { capture: true }
      );
      // Ahead of xUI's own Delete, which would take Delete and Relink's keys for a plain delete.
      element.addEventListener(
        'keydown',
        event => {
          this.relinkOnDelete(event);
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
      const enter = (event: KeyboardEvent): void => {
        this.enterEarly(event);
      };
      // Capturing, so that it is ahead of the omnibar's own listener.
      document.addEventListener('keydown', enter, { capture: true });
      this.destroyed.onDestroy(() => {
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

  protected add(item: PaletteItem): void {
    const [at, link] = [this.addAt, this.addLink];
    this.addAt = null;
    this.addLink = null;
    void this.place(item.type, at, link);
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

  /** Alt and a click on a wire bends it through a new reroute dot where it was clicked. */
  protected clickEdge(edge: XuiGraphEdge): void {
    const press = this.lastPress;
    const wire = this.document()?.edges.get(edge.id);
    if (!press?.alt || !wire || !this.store.editable()) {
      return;
    }

    const at = this.graph().store.toGraphPoint(press.x, press.y);
    // The connectors sit below the top of the card by the ports row's padding and half its height.
    void this.store.insertReroute(wire, { x: at.x - RerouteWidth / 2, y: at.y - RerouteConnectorDepth });
  }

  /** A right-click on a node acts on it, and on the selection it belongs to. */
  protected aimNode(node: number): void {
    if (!this.store.selection().has(node)) {
      this.store.select([node]);
    }
  }

  protected align(alignment: Alignment, label: string): void {
    this.store.place(aligned(this.selectedBoxes(), alignment), label);
  }

  protected distribute(axis: 'horizontal' | 'vertical'): void {
    this.store.place(
      distributed(this.selectedBoxes(), axis),
      axis === 'horizontal' ? 'Distribute Horizontally' : 'Distribute Vertically'
    );
  }

  /** The selected nodes as the canvas has them: where they are now, and how big xUI measured them. */
  private selectedBoxes(): NodeBox[] {
    const canvas = this.graph().store;
    const nodes = this.document()?.nodes;
    const drafts = this.store.positions();
    return this.selectedNodes().flatMap(id => {
      const node = nodes?.get(id);
      const handle: XuiGraphNodeHandle | undefined = canvas.node(String(id));
      if (!node) {
        return [];
      }

      const at = drafts.get(String(id)) ?? { x: node.x, y: node.y };
      const size = handle?.size() ?? { width: NodeWidth, height: NodeClearance };
      return [{ id, x: at.x, y: at.y, width: size.width, height: size.height }];
    });
  }

  /**
   * Delete and Relink's keys, whatever the table says they are, go to the table before xUI's own Delete sees them: xUI
   * deletes on Delete with any modifier, which would break the chain this is meant to keep.
   */
  private relinkOnDelete(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (!this.commands.isKeyOf('deleteRelink', event) || target.closest('input, textarea, [contenteditable]')) {
      return;
    }

    event.stopPropagation();
    this.commands.press(event);
    event.preventDefault();
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

  protected reveal(item: Pick<GraphDiagnostic, 'node'>): void {
    if (item.node !== undefined && item.node !== null) {
      this.store.select([item.node]);
      this.graph().store.centerOn(String(item.node));
    }
  }

  /**
   * What the canvas answers. The Edit commands work on the selection while focus is in the graph, or nowhere; Add Node
   * opens the palette at the pointer when it is over the canvas, and beside the selection otherwise.
   */
  private installCommands(): void {
    const handle = (id: string, enabled: () => boolean, run: (via: string) => unknown) =>
      this.commands.handle(id, { enabled, run }, this.destroyed);
    const here = () => this.commands.focus().area === 'graph' || this.commands.focus().area === null;
    const editable = () => this.store.editable() && this.store.document() !== null;
    const selected = () => here() && this.store.selection().size > 0;
    handle('copy', selected, () => this.store.copySelection());
    handle(
      'cut',
      () => selected() && editable(),
      () => {
        this.store.copySelection();
        this.store.remove([...this.store.selection()], []);
      }
    );
    handle(
      'paste',
      () => here() && editable(),
      () => this.store.paste()
    );
    handle(
      'duplicate',
      () => selected() && editable(),
      () => this.store.duplicate()
    );
    handle(
      'delete',
      () =>
        here() && editable() && this.graph().store.selectedNodes().size + this.graph().store.selectedEdges().size > 0,
      () =>
        this.remove({ nodes: [...this.graph().store.selectedNodes()], edges: [...this.graph().store.selectedEdges()] })
    );
    handle(
      'selectAll',
      () => here() && this.store.document() !== null,
      () => this.graph().store.selectAll()
    );
    handle(
      'groupNodes',
      () => editable() && this.store.selection().size > 0,
      () => this.store.groupSelection()
    );
    handle(
      'bypassNodes',
      () => editable() && this.store.selection().size > 0,
      () => this.store.toggleBypass()
    );
    handle(
      'deleteRelink',
      () => here() && editable() && this.store.selection().size > 0,
      () => this.store.removeRelinking(this.selectedNodes())
    );
    this.commands.handle(
      'arrangeGraph',
      {
        enabled: editable,
        label: () => (this.store.selection().size > 1 ? 'Lay Out Selection' : 'Lay Out Graph'),
        run: () => this.store.arrange()
      },
      this.destroyed
    );
    for (const { alignment, command, label } of this.alignments) {
      handle(
        command,
        () => editable() && this.store.selection().size > 1,
        () => this.align(alignment, label)
      );
    }
    handle(
      'distributeHorizontally',
      () => editable() && this.store.selection().size > 2,
      () => this.distribute('horizontal')
    );
    handle(
      'distributeVertically',
      () => editable() && this.store.selection().size > 2,
      () => this.distribute('vertical')
    );
    handle(
      'findNode',
      () => this.store.document() !== null && !this.finding(),
      () => {
        this.finding.set(true);
      }
    );
    this.commands.handle(
      'toggleGraphSnap',
      {
        checked: () => this.preferences.graphSnap(),
        run: () => this.preferences.set({ graphSnap: !this.preferences.graphSnap() })
      },
      this.destroyed
    );
    for (const style of this.wireStyles) {
      this.commands.handle(
        style.command,
        {
          checked: () => this.preferences.graphWires() === style.value,
          run: () => this.preferences.set({ graphWires: style.value })
        },
        this.destroyed
      );
    }
    handle(
      'addNode',
      () => editable() && !this.paletteOpen(),
      via => {
        const at = via === 'key' ? this.pointer : null;
        this.openPalette(at === null ? null : this.graph().store.toGraphPoint(at.x, at.y));
      }
    );
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
    this.preferences.usedNode(type.path);
    await this.store.addNode(type, at ?? this.spotFor(link), (fits ? link : null) ?? undefined);
  }

  /**
   * What a new node joins: the selected wire, the one selected node, or several selected nodes, top first, which fill its
   * inputs in turn.
   */
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
    if (nodes.length > 1) {
      const held = this.document()?.nodes;
      const top = (id: number) => held?.get(id)?.y ?? 0;
      return { kind: 'many', nodes: nodes.sort((a, b) => top(a) - top(b)) };
    }

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
    if (link?.kind === 'many') {
      const sources = link.nodes.flatMap(id => nodes?.get(id) ?? []);
      if (sources.length > 0) {
        const right = Math.max(...sources.map(node => node.x));
        const middle = sources.reduce((sum, node) => sum + node.y, 0) / sources.length;
        return this.clearOf({ x: right + NodeWidth + FollowingGap, y: middle });
      }
    }

    const from = link === null || link.kind === 'many' ? undefined : nodes?.get(link.node);
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
      const reroute = node.type === RerouteType;
      const portsKey = `${node.type}\0${portKey}`;
      let ports = this.portViews.get(portsKey);
      if (!ports) {
        ports = reroute ? reroutePorts : portsOf(type, wanted);
        this.portViews.set(portsKey, ports);
      }

      view = {
        id: node.id,
        key: String(node.id),
        title: reroute ? '' : (type?.title ?? node.type),
        accent: accentOf(type?.category ?? ''),
        ports,
        portKey,
        position: { x: node.x, y: node.y },
        group: undefined,
        compound: null,
        preview: !reroute && (type?.hasPreview ?? true),
        bypassed: node.bypassed,
        reroute
      };
      this.nodeViews.set(node, view);
    }

    return view;
  }

  private flowingView(edge: GraphEdgeState): XuiGraphEdge {
    let view = this.flowingViews.get(edge);
    if (!view) {
      view = { ...this.edgeView(edge), animated: true };
      this.flowingViews.set(edge, view);
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
