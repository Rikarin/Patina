import {
  type AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiSelectImports } from '@xui/select';
import { BakeStore } from '../bake/bake-store';
import { Activity } from '../core/activity';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { termName } from '../core/labels';
import { milliseconds } from '../core/numbers';
import { Preferences } from '../core/preferences';
import { SystemStatus } from '../core/system-status';
import { ViewOptions } from '../core/view-options';
import { GraphViews } from '../graph/graph-views';
import { DragType as LibraryDrag } from '../library/library-tree';
import { ProjectStore } from '../library/project-store';
import { MaterialUse } from '../paint/material-use';
import { PaintStore, penOf } from '../paint/paint-store';
import { ScreenPictureOverlay, handleStencilKeys } from '../paint/screen-picture';
import { StrokeGesture } from '../paint/stroke-gesture';
import { isPicture, pictureName } from '../paint/surface-frame';
import { SurfaceGuide } from '../paint/surface-guide';
import { project as onPicture, symmetryCopies, symmetryGuide } from '../paint/symmetry-guide';
import { ToolStrip } from '../paint/tool-strip';
import { FramePainter } from './frame-painter';
import type { ViewportFrame } from './frame-transport';
import { type AxisView, framed, nudge, orbit, pan, snapped, zoom } from './orbit';
import { ViewportClient } from './viewport-client';
import { ViewportDisplayPanel } from './viewport-display';

const Rooms: readonly { value: string; label: string }[] = [
  { value: 'studio', label: 'Studio' },
  { value: 'overcast', label: 'Overcast' },
  { value: 'sunset', label: 'Sunset' }
];

/**
 * The views, in the order V steps through them: the whole picture, then each stored channel, which goes by the label
 * the stack vocabulary gives it. A picture is chosen from the mesh maps, so V passes it by.
 */
const ViewNames: Readonly<Record<string, string>> = {
  lit: 'Lit',
  clay: 'Clay',
  unlit: 'Unlit',
  texelDensity: 'Texel density'
};

const Views: readonly string[] = [
  'lit',
  'clay',
  'unlit',
  'baseColor',
  'roughness',
  'metallic',
  'normal',
  'occlusion',
  'emissive',
  'opacity',
  'texelDensity'
];

/** The viewport's own commands, for the keys a screen reader is told about. */
const ViewportCommands = [
  'frameModel',
  'nextView',
  'previousView',
  'orbitLeft',
  'orbitRight',
  'orbitUp',
  'orbitDown',
  'panLeft',
  'panRight',
  'panUp',
  'panDown',
  'zoomIn',
  'zoomOut',
  'moveStencilLeft',
  'moveStencilRight',
  'moveStencilUp',
  'moveStencilDown',
  'growStencil',
  'shrinkStencil',
  'turnStencilLeft',
  'turnStencilRight'
];

/** How far a zoom drag goes per pixel, as a share of a wheel's. */
const DragZoom = 4;

/** What a value of the view list means when it is a mesh map rather than a view. */
const MapPrefix = 'map:';
/** What the view list shows while the viewport shows a node's picture, which only the graph can change. */
const NodeView = 'node:';
/** What the look list means by none. */
const NoLook = 'none:';
/** What the environment and look lists mean by asking for a file. */
const PickFile = '…';

/** The docked viewport: frames from the Worker on a canvas, and orbit input going the other way. */
@Component({
  selector: 'ptn-viewport',
  imports: [XuiButtonImports, XuiSelectImports, ViewportDisplayPanel, ToolStrip, ScreenPictureOverlay, SurfaceGuide],
  providers: [ViewportClient],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <!--
      The bar keeps to one line, as every pane's does (#200). As it narrows, the readouts go first, then names truncate
      and the choices shrink; the buttons keep their size.
    -->
    <div
      class="chrome-bar @container flex h-9 shrink-0 flex-nowrap items-center gap-2 overflow-hidden px-2"
      role="toolbar"
      aria-label="Viewport"
      data-testid="viewport-bar"
    >
      @if (!paint.worn()) {
        <button type="button" xuiButton size="sm" class="shrink-0" [disabled]="!canOpen" (click)="open()">
          Open mesh…
        </button>
      }
      <span class="readout min-w-0 truncate" data-testid="viewport-mesh" [title]="meshName()">{{ meshName() }}</span>
      @if (graphs.front().document(); as document) {
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="max-w-48 min-w-0"
          data-testid="viewport-show-graph"
          [active]="followed() === document.id"
          [title]="'Lays the outputs of ' + document.name + ' over the mesh, or a sphere, and keeps up with edits'"
          (click)="follow(document.id)"
        >
          <span class="min-w-0 truncate">Show {{ document.name }}</span>
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="shrink-0"
          data-testid="viewport-show-node"
          title="Shows the selected node's picture alone, as stored and unlit, in place of the material, and follows the selection"
          [active]="followsNode() && followed() === document.id"
          [attr.aria-pressed]="followsNode() && followed() === document.id"
          (click)="toggleNode(document.id)"
        >
          Node
        </button>
      }
      @if (paint.worn() && project.active()?.kind === 'texturing') {
        <button
          type="button"
          xuiButton
          size="sm"
          class="shrink-0"
          data-testid="viewport-paint"
          [variant]="painting() ? 'default' : 'ghost'"
          [title]="'Paints the mesh. Orbit: ' + commands.navigation().orbit + '.'"
          (click)="painting.set(!painting())"
        >
          Paint
        </button>
      }
      <span class="min-w-0 flex-1"></span>
      <xui-select
        class="w-28 min-w-16 [&_button]:text-xs"
        style="--control-height-md: 1.5rem; --control-padding-md: 0.375rem"
        aria-label="View"
        [title]="
          'What the model shows: lit, its shape, or one stored channel as the file holds it' +
          (commands.keyText('nextView') ? ' (' + commands.keyText('nextView') + ' steps through them)' : '')
        "
        data-testid="viewport-view"
        [disabled]="viewport.node() !== null"
        [filterable]="false"
        [itemText]="viewChoiceName()"
        [items]="viewChoices()"
        [matchTargetWidth]="false"
        [value]="viewChoice()"
        (valueChange)="$event && chooseView($event)"
      />
      <xui-select
        class="w-28 min-w-16 [&_button]:text-xs"
        style="--control-height-md: 1.5rem; --control-padding-md: 0.375rem"
        aria-label="Environment"
        title="What lights the model"
        data-testid="viewport-environment"
        [filterable]="false"
        [itemText]="environmentName()"
        [items]="environments()"
        [matchTargetWidth]="false"
        [(value)]="environmentChoice"
        (valueChange)="chooseEnvironment($event)"
      />
      <xui-select
        class="w-28 min-w-16 [&_button]:text-xs"
        style="--control-height-md: 1.5rem; --control-padding-md: 0.375rem"
        aria-label="Look"
        title="A .cube table the picture is viewed through. Nothing that is exported sees it."
        data-testid="viewport-look"
        [filterable]="false"
        [itemText]="lookChoiceName()"
        [items]="looks()"
        [matchTargetWidth]="false"
        [(value)]="lookChoice"
        (valueChange)="chooseLook($event)"
      />
      <button
        type="button"
        xuiButton
        size="sm"
        class="shrink-0"
        data-testid="viewport-display-toggle"
        title="The camera, light and finish of the view"
        [variant]="showDisplay() ? 'default' : 'ghost'"
        [attr.aria-expanded]="showDisplay()"
        (click)="showDisplay.set(!showDisplay())"
      >
        Display
      </button>
      @if (stampMs() !== null) {
        <span
          class="readout shrink-0 @max-[48rem]:sr-only"
          [class.hidden]="!options.stats()"
          data-testid="viewport-stamp"
          [attr.data-stamp-ms]="stampTimes()"
        >
          Stamp {{ milliseconds(stampMs() ?? 0) }}
        </span>
      }
      @if (viewport.stats(); as stats) {
        <span
          class="readout shrink-0 @max-[48rem]:sr-only"
          data-testid="viewport-stats"
          [class.hidden]="!options.stats()"
        >
          {{ stats.width }}×{{ stats.height }} · {{ stats.fps }} fps · {{ milliseconds(stats.latencyMs) }}
        </span>
      }
    </div>
    <div #host class="well relative min-h-0 flex-1 overflow-hidden">
      <canvas
        #canvas
        class="absolute inset-0 size-full touch-none"
        aria-label="Viewport"
        data-commands="viewport"
        [attr.aria-keyshortcuts]="keyShortcuts()"
        role="application"
        tabindex="0"
        [attr.data-frames]="viewport.frames()"
        [class.cursor-grabbing]="dragging()"
        [class.cursor-grab]="!dragging() && !aiming()"
        [class.cursor-crosshair]="!dragging() && aiming()"
        (contextmenu)="$event.preventDefault()"
        (pointerdown)="press($event)"
        (pointermove)="drag($event)"
        (pointerup)="release($event)"
        (pointercancel)="release($event)"
        (pointerleave)="brushAt.set(null)"
        (dblclick)="gesture.finish()"
        (wheel)="wheel($event)"
        (dragover)="overMesh($event)"
        (drop)="dropOnMesh($event)"
      ></canvas>
      @if (paint.worn() && project.active()?.kind === 'texturing') {
        <ptn-screen-picture of="material" />
        <ptn-screen-picture />
        <ptn-surface-guide />
        <ptn-tool-strip class="absolute start-2 top-2" />
      }
      @if (paints()) {
        @if (brushAt(); as at) {
          <div
            class="pointer-events-none absolute rounded-full border border-white/70 mix-blend-difference"
            data-testid="viewport-brush"
            [attr.data-space]="paint.brush().sizeSpace"
            [style.height.px]="cursor() * 2"
            [style.left.px]="at.x - cursor()"
            [style.top.px]="at.y - cursor()"
            [style.width.px]="cursor() * 2"
          ></div>
          @for (copy of mirrored(); track $index) {
            <div
              class="pointer-events-none absolute rounded-full border border-dashed border-sky-300/80"
              data-testid="viewport-brush-copy"
              [style.height.px]="copy.radius * 2"
              [style.left.px]="copy.x - copy.radius"
              [style.top.px]="copy.y - copy.radius"
              [style.width.px]="copy.radius * 2"
            ></div>
          }
        }
        @if (guide(); as points) {
          <svg
            class="pointer-events-none absolute inset-0 size-full"
            data-testid="viewport-symmetry"
            aria-hidden="true"
          >
            @if (paint.symmetry()?.kind === 'radial') {
              <polyline
                class="fill-none stroke-sky-300/80"
                stroke-dasharray="6 4"
                stroke-width="1.5"
                [attr.points]="points"
              />
            } @else {
              <polygon
                class="fill-sky-300/10 stroke-sky-300/80"
                stroke-dasharray="6 4"
                stroke-width="1.5"
                [attr.points]="points"
              />
            }
          </svg>
        }
        @if (gesture.points().length > 1) {
          <svg class="pointer-events-none absolute inset-0 size-full" data-testid="viewport-curve">
            @if (gesture.outlining()) {
              <polygon
                class="fill-white/10 stroke-white/80"
                data-testid="viewport-outline"
                stroke-dasharray="4 3"
                [attr.points]="curve()"
              />
            } @else {
              <polyline class="fill-none stroke-white/70" [attr.points]="curve()" />
            }
          </svg>
        }
      }
      @if (message(); as text) {
        <p class="text-foreground-muted absolute inset-x-0 top-1/2 text-center">{{ text }}</p>
      }
      @if (showDisplay()) {
        <ptn-viewport-display class="absolute inset-y-2 end-2 max-w-[calc(100%-1rem)]" (snap)="snap($event)" />
      }
    </div>
  `
})
export class ViewportPane implements AfterViewInit {
  private readonly status = inject(SystemStatus);
  protected readonly viewport = inject(ViewportClient);
  protected readonly display = this.viewport.display;
  private readonly bakes = inject(BakeStore);
  /** The graph touched last, which Show and Node lay on the mesh (#40). */
  protected readonly graphs = inject(GraphViews);
  protected readonly paint = inject(PaintStore);
  private readonly using = inject(MaterialUse);
  private readonly activity = inject(Activity);
  private readonly preferences = inject(Preferences);
  protected readonly project = inject(ProjectStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private painter: FramePainter | null = null;
  private pending: ViewportFrame | null = null;
  private animation = 0;
  private cameraDirty = false;
  private stroking = false;

  /** A Shift-press that draws a line on release, unless the pointer moves first and it is a pan after all. */
  private lining: { x: number; y: number; snap: boolean } | null = null;

  /** Whether a left drag paints rather than orbits. */
  /**
   * Left paints as soon as a stack is on the mesh, if a stroke can land. Otherwise it turns the model, as Alt-left and
   * right do, and middle or Shift moves it.
   */
  protected readonly painting = signal(true);
  protected readonly paints = computed(() => this.paint.worn() && this.painting() && this.paint.canPaintMesh());

  /**
   * The ring under the pointer, in pixels: the brush's radius when it is in the screen's units. A size in texels or in
   * the model's units is only measured where a stroke starts, so until then the ring is a small mark, never a wrong one.
   */
  protected readonly cursor = computed(() => (this.paint.brush().sizeSpace === 'screen' ? this.paint.radius() : 3));

  /** The camera and model box the symmetry guide was last drawn from, asked of the Worker as the view settles. */
  private readonly looked = signal<{ key: string; pick: Awaited<ReturnType<PaintStore['look']>> } | null>(null);
  private looking: ReturnType<typeof setTimeout> | undefined;

  /** The symmetry's plane or axis over the picture, where the Worker will put it, or null with none to show. */
  protected readonly guide = computed(() => {
    const symmetry = this.paint.symmetry();
    const looked = this.looked();
    const bounds = looked?.key === this.view() ? looked.pick.bounds : null;
    if (!symmetry || !looked || !bounds) {
      return null;
    }

    return symmetryGuide(symmetry, looked.pick.view, bounds);
  });
  /** Whether a press does something at the point under it, which is what the cursor says. */
  protected readonly aiming = computed(
    () =>
      this.paints() ||
      this.paint.picking() !== null ||
      this.paint.placing() !== null ||
      this.paint.pickingPart() !== null ||
      (this.paint.worn() && (this.paint.tool() === 'picker' || this.paint.finding()))
  );
  private panning = false;
  private zooming = false;
  /** Where the brush is over the canvas, in the units of a stroke. */
  protected readonly brushAt = signal<{ x: number; y: number } | null>(null);
  /**
   * Where the symmetry copies the point under the brush, each a ring on the picture sized by its depth (#132), from the
   * newest pick of that point. A pick is a round trip to the Worker, so one is in flight at a time.
   */
  protected readonly mirrored = signal<readonly { x: number; y: number; radius: number }[]>([]);
  private mirroring = false;
  private mirrorWanted = false;
  protected readonly options = inject(ViewOptions);
  protected readonly gesture = new StrokeGesture(this.paint, () => this.aim());
  protected readonly curve = computed(() =>
    this.gesture
      .points()
      .map(point => `${point.x},${point.y}`)
      .join(' ')
  );

  protected readonly milliseconds = milliseconds;
  protected readonly stampTimes = computed(() => this.paint.stampTimes().join(','));
  protected readonly stampMs = computed(() => {
    const times = [...this.paint.stampTimes().slice(-60)].sort((a, b) => a - b);
    const middle = times[Math.floor(times.length / 2)];
    return middle === undefined ? null : Math.round(middle);
  });

  protected readonly canOpen = desktop() !== undefined;
  protected readonly dragging = signal(false);
  protected readonly meshName = computed(() => this.viewport.mesh()?.split(/[\\/]/).pop() ?? 'No mesh');
  private readonly unsupported = signal(false);
  protected readonly message = computed(() => {
    if (this.unsupported()) {
      return 'This view needs WebGL2.';
    }

    return this.viewport.error() ?? (this.viewport.mesh() ? null : 'Open a mesh to see it here.');
  });

  /** Where the view is, and nothing about how it is lit. */
  private readonly view = computed(() => {
    const { yaw, pitch, distance, panX, panY, panZ } = this.viewport.camera();
    return [yaw, pitch, distance, panX, panY, panZ].join();
  });

  protected readonly commands = inject(Commands);

  /** The viewport's keys, for a screen reader, as ARIA writes them. */
  protected readonly keyShortcuts = computed(() =>
    ViewportCommands.flatMap(id => this.commands.keys().get(id) ?? [])
      .map(chord =>
        chord
          .replace('Mod+', 'Meta+')
          .replace(/\b(Up|Down|Left|Right)$/u, 'Arrow$1')
          .replace('Plus', '+')
      )
      .join(' ')
  );

  constructor() {
    this.installCommands();
    // Properties is outside this pane and cannot reach its client, so the camera it measures through is kept on the store.
    effect(() => this.paint.viewCamera.set(this.viewport.camera()));
    this.destroyRef.onDestroy(() => this.paint.viewCamera.set(null));
    // A path's points are places on this picture, so one the view has left is painted as it was placed.
    effect(() => {
      this.view();
      untracked(() => this.gesture.finish());
    });
    // The guide follows the camera once it rests, since each look is a round trip to the Worker.
    effect(() => {
      const key = this.view();
      if (!this.paints() || !this.paint.symmetry()) {
        return;
      }

      clearTimeout(this.looking);
      this.looking = setTimeout(() => {
        void this.paint
          .look(this.aim())
          .then(pick => this.looked.set({ key, pick }))
          // A guide is a help: without one the strokes still copy where they would.
          .catch(() => undefined);
      }, 150);
    });
    // The brush's copies follow the pointer, as soon as the last pick of it is back.
    effect(() => {
      const at = this.brushAt();
      const symmetry = this.paint.symmetry();
      if (!at || !symmetry || !this.paints()) {
        untracked(() => this.mirrored.set([]));
        return;
      }

      untracked(() => this.mirror());
    });
    // What the viewport shows changed from elsewhere, and the label needs telling.
    effect(() => {
      if (this.paint.worn()) {
        void this.viewport.sync();
      } else {
        this.painting.set(true);
      }
    });
  }

  ngAfterViewInit(): void {
    const canvas = this.canvas().nativeElement;
    this.painter = FramePainter.create(canvas);
    this.unsupported.set(this.painter === null);
    this.viewport.onFrame = frame => this.queue(frame);

    const observer = new ResizeObserver(() => this.fit());
    observer.observe(this.host().nativeElement);
    this.destroyRef.onDestroy(() => {
      observer.disconnect();
      cancelAnimationFrame(this.animation);
      this.viewport.onFrame = null;
    });
    this.fit();
  }

  protected readonly viewName = (view: string): string => ViewNames[view] ?? termName(view);
  protected readonly canPick = desktop() !== undefined;
  /** Whether the display settings are open beside the picture. */
  protected readonly showDisplay = signal(false);
  /** The file lighting the model, when it is not a built-in room. */
  protected readonly panorama = computed(() => {
    const source = this.display().environment;
    return Rooms.some(room => room.value === source) ? null : fileName(source);
  });
  protected readonly lookName = computed(() => {
    const look = this.display().look;
    return look ? fileName(look) : null;
  });
  /** The maps of the last bake, which the view list offers on the mesh as they are stored. */
  protected readonly meshMaps = computed(() =>
    (this.bakes.current()?.result?.maps ?? []).map(map => ({ path: map.path, label: termName(map.map) }))
  );
  /** What the view list offers: the node on view, while one is, each view, then the mesh maps it can show. */
  protected readonly viewChoices = computed(() => [
    ...(this.viewport.node() === null ? [] : [NodeView]),
    ...Views,
    ...this.meshMaps().map(map => MapPrefix + map.path),
    ...(this.shownPicture() ? [MapPrefix + (this.display().picture ?? '')] : [])
  ]);
  protected readonly viewChoice = computed(() => {
    const { view, picture } = this.display();
    return this.viewport.node() !== null ? NodeView : view === 'picture' ? MapPrefix + (picture ?? '') : view;
  });
  protected readonly viewChoiceName = computed(() => {
    const maps = new Map(this.meshMaps().map(map => [MapPrefix + map.path, `Mesh map · ${map.label}`] as const));
    return (choice: string): string =>
      choice === NodeView
        ? 'Node picture'
        : choice.startsWith(MapPrefix)
          ? (maps.get(choice) ?? fileName(choice.slice(MapPrefix.length)))
          : this.viewName(choice);
  });
  /** The rooms, the file lighting the model when it is none of them, and a file to choose, where there are files. */
  protected readonly environments = computed(() => [
    ...Rooms.map(room => room.value),
    ...(this.panorama() ? [this.display().environment] : []),
    ...(this.canPick ? [PickFile] : [])
  ]);
  protected readonly environmentChoice = linkedSignal(() => this.display().environment);
  protected readonly environmentName = computed(() => {
    const panorama = this.panorama();
    return (source: string): string =>
      source === PickFile
        ? 'From a file…'
        : (Rooms.find(room => room.value === source)?.label ?? panorama ?? fileName(source));
  });
  protected readonly looks = computed(() => [
    NoLook,
    ...(this.display().look ? [this.display().look ?? ''] : []),
    ...(this.canPick ? [PickFile] : [])
  ]);
  protected readonly lookChoice = linkedSignal(() => this.display().look ?? NoLook);
  protected readonly lookChoiceName = computed(() => {
    const look = this.lookName();
    return (choice: string): string =>
      choice === PickFile ? 'From a file…' : choice === NoLook ? 'No look' : (look ?? fileName(choice));
  });

  /** The picture on view when it is none of the last bake's maps, such as one a project kept. */
  protected readonly shownPicture = computed(() => {
    const { view, picture } = this.display();
    return view === 'picture' && picture && !this.meshMaps().some(map => map.path === picture)
      ? fileName(picture)
      : null;
  });

  /**
   * The graph on the mesh, which the Server keeps up with its evaluations until something else is laid there (#244).
   * Any client can ask for it, and the Server says which it follows.
   */
  protected readonly followed = computed(() => this.viewport.following()?.graph ?? null);

  /** Whether the graph on the mesh shows the node its selection views alone rather than its Output nodes. */
  protected readonly followsNode = computed(() => this.viewport.following()?.node ?? false);

  protected follow(graph: string): void {
    void this.viewport.follow(this.followed() === graph ? null : graph, this.followsNode());
  }

  /** Shows the node on view alone, or the material again. Either way the graph is followed from here on. */
  protected toggleNode(graph: string): void {
    void this.viewport.follow(graph, !(this.followsNode() && this.followed() === graph));
  }

  /** A room, or a panorama from a file. A file not chosen leaves the list on what lights the model now. */
  protected async chooseEnvironment(choice: string | null): Promise<void> {
    const source = choice === PickFile ? await desktop()?.openEnvironment(this.status.environments()) : choice;
    if (source) {
      this.viewport.setDisplay({ environment: source });
    } else {
      this.environmentChoice.set(this.display().environment);
    }
  }

  /** No look, or a table from a file. A file not chosen leaves the list on the look there is. */
  protected async chooseLook(choice: string | null): Promise<void> {
    const chosen = choice === PickFile ? await desktop()?.openLook() : choice === NoLook ? null : choice;
    if (chosen === undefined || (chosen === null && choice === PickFile)) {
      this.lookChoice.set(this.display().look ?? NoLook);
      return;
    }

    this.viewport.setDisplay({ look: chosen });
  }

  protected chooseView(value: string): void {
    if (value === NodeView) {
      return;
    }

    this.viewport.setDisplay(
      value.startsWith(MapPrefix)
        ? { view: 'picture', picture: value.slice(MapPrefix.length) }
        : { view: value, picture: null }
    );
  }

  /** Looks along an axis at the whole model. */
  protected snap(view: AxisView): void {
    this.viewport.moveCamera(snapped(this.viewport.camera(), view), false);
  }

  protected async open(): Promise<void> {
    const path = await desktop()?.openMesh(this.status.meshes());
    if (path) {
      await this.viewport.openMesh(path);
    }
  }

  protected value(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  /**
   * What the viewport answers. Framing works wherever focus is, as F does in every 3D tool; the rest while the viewport
   * has focus, so that the keyboard does what the pointer does. The number pad snaps to an axis as Blender's does, and
   * Next View steps through the views.
   */
  private installCommands(): void {
    const handle = (id: string, run: () => unknown, enabled?: () => boolean) =>
      this.commands.handle(id, { run, ...(enabled ? { enabled } : {}) }, this.destroyRef);
    const move = (key: string, shift: boolean) => () => {
      const moved = nudge(this.viewport.camera(), key, shift);
      if (moved) {
        this.viewport.moveCamera(moved, false);
      }
    };
    const step = (by: number) => () => {
      const at = Views.indexOf(this.display().view);
      const next = Views[(at + by + Views.length) % Views.length];
      this.viewport.setDisplay({ view: next ?? 'lit', picture: null });
    };
    handle('frameModel', () => this.viewport.moveCamera(framed(this.viewport.camera()), false));
    handle('nextView', step(1));
    handle('previousView', step(-1));
    this.commands.handle(
      'toggleWireframe',
      {
        checked: () => this.display().wireframe,
        run: () => this.viewport.setDisplay({ wireframe: !this.display().wireframe })
      },
      this.destroyRef
    );
    this.commands.handle(
      'toggleGrid',
      { checked: () => this.display().grid, run: () => this.viewport.setDisplay({ grid: !this.display().grid }) },
      this.destroyRef
    );
    // The picture is written where the Server runs, so it needs a path the Server can reach: the desktop's.
    handle(
      'saveScreenshot',
      async () => {
        const path = await desktop()?.saveScreenshot();
        if (path) {
          await this.viewport.screenshot(path);
        }
      },
      () => this.canPick && this.viewport.mesh() !== null
    );
    const axes: Readonly<Record<string, AxisView>> = {
      viewFront: 'front',
      viewBack: 'back',
      viewRight: 'right',
      viewLeft: 'left',
      viewTop: 'top',
      viewBottom: 'bottom'
    };
    for (const [id, axis] of Object.entries(axes)) {
      handle(id, () => this.snap(axis));
    }

    for (const [side, key] of [
      ['Left', 'ArrowLeft'],
      ['Right', 'ArrowRight'],
      ['Up', 'ArrowUp'],
      ['Down', 'ArrowDown']
    ] as const) {
      handle(`orbit${side}`, move(key, false));
      handle(`pan${side}`, move(key, true));
    }

    handle('zoomIn', move('+', false));
    handle('zoomOut', move('-', false));
    // What is fixed on the view moves by keys too (#128), a step at a time, while a stencil or the projection reads it.
    handleStencilKeys(this.commands, this.paint, this.destroyRef);
    handle(
      'cancelStroke',
      () => {
        this.gesture.cancel();
        this.paint.placing.set(null);
        this.paint.pickingPart.set(null);
      },
      () =>
        this.gesture.points().length > 0 ||
        this.paint.picking() !== null ||
        this.paint.placing() !== null ||
        this.paint.pickingPart() !== null
    );
    handle(
      'finishPath',
      () => (this.paint.placing() === 'path' ? this.paint.placing.set(null) : this.gesture.finish()),
      () => this.paint.placing() === 'path' || (this.paint.tool() === 'path' && this.gesture.points().length > 1)
    );
  }

  protected press(event: PointerEvent): void {
    // A click takes the mesh under it into the layer's meshes, or out of them (#110).
    if (this.paint.pickingPart() !== null && event.button === 0) {
      void this.paint.togglePartOnMesh(this.stamp(event), this.aim());
      return;
    }

    if (this.paint.finding() && event.button === 0) {
      void this.paint.findLayerOnMesh(this.stamp(event), this.aim());
      return;
    }

    // A decal goes where the click lands, and a path gains a point there (#130, #133).
    if (this.paint.placing() !== null && event.button === 0 && !event.altKey) {
      void this.paint.placeOnMesh(this.stamp(event), this.aim());
      return;
    }

    // The clone's source, from Set Clone Source or a Cmd- or Ctrl-click with the clone in hand.
    if (
      event.button === 0 &&
      this.paint.worn() &&
      (this.paint.settingCloneSource() ||
        (this.paint.tool() === 'clone' && (event.metaKey || event.ctrlKey) && !event.shiftKey))
    ) {
      void this.paint.setCloneSource(this.stamp(event), this.aim());
      return;
    }

    if ((this.paint.picking() !== null || this.paint.tool() === 'picker') && event.button === 0) {
      void this.paint.pickColourOnMesh(this.stamp(event), this.aim());
      return;
    }

    this.canvas().nativeElement.setPointerCapture(event.pointerId);
    const drag = this.commands.navigation().drag(event, this.paints());
    if (drag === 'stroke') {
      this.stroking = true;
      this.gesture.press(this.stamp(event));
      return;
    }

    // Shift pans, and Shift-click without moving draws a line from where the last stroke ended.
    this.lining =
      this.paints() && event.button === 0 && event.shiftKey && this.gesture.canLine()
        ? { ...this.spot(event), snap: event.ctrlKey || event.metaKey }
        : null;
    this.panning = drag === 'pan';
    this.zooming = drag === 'zoom';
    this.dragging.set(true);
    this.viewport.moveCamera(this.viewport.camera(), true);
  }

  protected drag(event: PointerEvent): void {
    this.brushAt.set(this.spot(event));
    if (this.stroking) {
      // A display frame can hold several pointer moves, and a stroke wants all of them.
      const moves = event.getCoalescedEvents();
      for (const move of moves.length > 0 ? moves : [event]) {
        this.gesture.move(this.stamp(move));
      }
    } else if (this.lining && Math.hypot(this.spot(event).x - this.lining.x, this.spot(event).y - this.lining.y) < 4) {
      // Still a click: a hand is never quite still, and a line should not cost the view a nudge.
    } else if (this.dragging()) {
      this.lining = null;
      const camera = this.viewport.camera();
      this.viewport.camera.set(
        this.zooming
          ? zoom(camera, event.movementY * DragZoom * this.preferences.zoomScale())
          : this.panning
            ? pan(camera, event.movementX, event.movementY)
            : orbit(
                camera,
                event.movementX * this.preferences.orbitScale(),
                event.movementY * this.preferences.orbitScale()
              )
      );
      this.scheduleCamera();
    }
  }

  protected release(event: PointerEvent): void {
    if (this.stroking) {
      this.stroking = false;
      this.gesture.release();
    } else if (this.dragging()) {
      if (this.lining) {
        this.gesture.lineTo(this.stamp(event), this.lining.snap);
        this.lining = null;
      }

      this.canvas().nativeElement.releasePointerCapture(event.pointerId);
      this.dragging.set(false);
      this.viewport.moveCamera(this.viewport.camera(), false);
    }
  }

  protected wheel(event: WheelEvent): void {
    event.preventDefault();
    this.viewport.moveCamera(zoom(this.viewport.camera(), event.deltaY * this.preferences.zoomScale()), false);
  }

  private spot(event: MouseEvent): { x: number; y: number } {
    const box = this.canvas().nativeElement.getBoundingClientRect();
    return { x: event.clientX - box.left, y: event.clientY - box.top };
  }

  /** A stroke is measured in the canvas's layout pixels, so a brush is the same size whatever the render scale. */
  private stamp(event: PointerEvent) {
    return { ...this.spot(event), ...penOf(event) };
  }

  /** A material from the shelf can land on a region of the mesh (#110), and a picture becomes a decal there (#133). */
  protected overMesh(event: DragEvent): void {
    const carries = this.carriedPicture(event);
    if (carries) {
      // Not the window's: a file dropped here is a decal, not a file brought into the project.
      event.preventDefault();
      event.stopPropagation();
      if (event.dataTransfer) {
        event.dataTransfer.dropEffect = carries === 'file' ? 'copy' : 'move';
      }
      return;
    }

    if (this.using.takesRegion(this.using.carried())) {
      event.preventDefault();
      if (event.dataTransfer) {
        event.dataTransfer.dropEffect = 'copy';
      }
    }
  }

  /** Fills the region of the id map under the drop with the material, as a new layer kept to that region. */
  protected async dropOnMesh(event: DragEvent): Promise<void> {
    if (this.carriedPicture(event)) {
      await this.dropDecal(event);
      return;
    }

    const material = this.using.carried();
    if (!material || !this.using.takesRegion(material)) {
      return;
    }

    event.preventDefault();
    this.using.carried.set(null);
    const colour = await this.paint.idColourOnMesh({ ...this.spot(event), pressure: 1 }, this.aim());
    await this.using.useOnRegion(material, colour);
    if (this.using.failure()) {
      this.activity.report({ tone: 'warning', source: 'Viewport', text: this.using.failure() ?? '' });
    }
  }

  /**
   * What a drag over the mesh carries that could become a decal: a file from this machine, or something from the library
   * tree, while a texture set is on the mesh. Which file it is is known only on the drop.
   */
  private carriedPicture(event: DragEvent): 'file' | 'library' | null {
    const types = event.dataTransfer?.types ?? [];
    if (!this.paint.worn() || this.project.active()?.kind !== 'texturing') {
      return null;
    }

    return types.includes('Files') ? 'file' : types.includes(LibraryDrag) ? 'library' : null;
  }

  /**
   * A picture dropped on the mesh, from this machine or the library, is brought into the project and laid where it
   * landed as a decal on a new fill, at the picture's own shape (#133).
   */
  private async dropDecal(event: DragEvent): Promise<void> {
    event.preventDefault();
    event.stopPropagation();
    // What the drop carried is read now: it is gone once this handler awaits.
    const file = event.dataTransfer?.files[0] ?? null;
    const listed = event.dataTransfer?.getData(LibraryDrag) ?? '';
    const at = { ...this.spot(event), pressure: 1 };
    const aim = this.aim();
    const bridge = desktop();
    let source: string | null = null;
    if (file) {
      source = bridge ? bridge.pathOf(file) : null;
    } else if (listed) {
      try {
        const paths: unknown = JSON.parse(listed);
        source = Array.isArray(paths) && typeof paths[0] === 'string' ? paths[0] : null;
      } catch {
        source = null;
      }
    }

    if (!source || !isPicture(source, this.status.pictures())) {
      this.activity.report({ tone: 'warning', source: 'Viewport', text: 'Only a picture can be placed as a decal.' });
      return;
    }

    const aspect = file ? await aspectOf(file) : 1;
    const picture = await this.project.bringPicture(source);
    if (picture) {
      await this.paint.addDecalOnMesh(picture, pictureName(source), at, aim, aspect);
    }
  }

  /** Picks the point under the brush and rings where the symmetry copies it, then the newest point if it moved. */
  private mirror(): void {
    if (this.mirroring) {
      this.mirrorWanted = true;
      return;
    }

    const at = this.brushAt();
    const symmetry = this.paint.symmetry();
    if (!at || !symmetry) {
      return;
    }

    this.mirroring = true;
    this.mirrorWanted = false;
    const radius = this.cursor();
    void this.paint
      .lookAt(at, this.aim())
      .then(pick => {
        const position = pick.position;
        const bounds = pick.bounds;
        if (!pick.found || position?.length !== 3 || bounds?.length !== 6 || !this.brushAt()) {
          this.mirrored.set([]);
          return;
        }

        const point = [position[0] ?? 0, position[1] ?? 0, position[2] ?? 0] as const;
        const depth = (of: readonly number[]) =>
          [0, 1, 2].reduce(
            (sum, axis) => sum + ((of[axis] ?? 0) - (pick.view.eye[axis] ?? 0)) * (pick.view.forward[axis] ?? 0),
            0
          );
        const near = depth(point);
        this.mirrored.set(
          symmetryCopies(symmetry, bounds, point).flatMap(copy => {
            const shown = onPicture(pick.view, copy);
            return shown ? [{ x: shown[0], y: shown[1], radius: (radius * near) / depth(copy) }] : [];
          })
        );
      })
      // A guide is a help: without one the strokes still copy where they would.
      .catch(() => this.mirrored.set([]))
      .finally(() => {
        this.mirroring = false;
        if (this.mirrorWanted) {
          this.mirror();
        }
      });
  }

  private aim() {
    const { clientWidth, clientHeight } = this.canvas().nativeElement;
    return { width: clientWidth, height: clientHeight, camera: this.viewport.camera() };
  }

  /** Pointer events outrun the display: the camera goes out once per frame, carrying the newest position. */
  private scheduleCamera(): void {
    this.cameraDirty = true;
    this.schedule();
  }

  private queue(frame: ViewportFrame): void {
    this.pending = frame;
    this.schedule();
  }

  private schedule(): void {
    if (this.animation === 0) {
      this.animation = requestAnimationFrame(() => this.tick());
    }
  }

  private tick(): void {
    this.animation = 0;
    if (this.cameraDirty) {
      this.cameraDirty = false;
      this.viewport.moveCamera(this.viewport.camera(), true);
    }

    const frame = this.pending;
    if (frame && this.painter) {
      this.pending = null;
      this.painter.paint(frame);
      this.viewport.framePainted(frame);
      this.paint.framePainted(frame.painted);
    }
  }

  private fit(): void {
    const { clientWidth, clientHeight } = this.host().nativeElement;
    const scale = globalThis.devicePixelRatio || 1;
    const width = Math.round(clientWidth * scale);
    const height = Math.round(clientHeight * scale);
    if (width > 0 && height > 0) {
      const canvas = this.canvas().nativeElement;
      canvas.width = width;
      canvas.height = height;
      this.viewport.resize(width, height);
    }
  }
}

function fileName(path: string): string {
  return path.split(/[/\\]/u).pop() ?? path;
}

/** A bake's map as the view list names it: <code>ao</code> as Ao would read badly. */

/** A picture's height over its width, or one when the browser cannot read it (an EXR, say): the decal starts square. */
async function aspectOf(file: Blob): Promise<number> {
  try {
    const bitmap = await createImageBitmap(file);
    const aspect = bitmap.width > 0 ? bitmap.height / bitmap.width : 1;
    bitmap.close();
    return aspect;
  } catch {
    return 1;
  }
}
