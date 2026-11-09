import {
  type AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { BakeStore } from '../bake/bake-store';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { termName } from '../core/labels';
import { SystemStatus } from '../core/system-status';
import { ViewOptions } from '../core/view-options';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore, pressureOf } from '../paint/paint-store';
import { symmetryGuide } from '../paint/symmetry-guide';
import { StrokeGesture } from '../paint/stroke-gesture';
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
  'zoomOut'
];

/** How far a zoom drag goes per pixel, as a share of a wheel's. */
const DragZoom = 4;

/** What a value of the view list means when it is a mesh map rather than a view. */
const MapPrefix = 'map:';
/** What the look list means by none, and by asking for a file. */
const NoLook = '';
const PickLook = '…';

/** The docked viewport: frames from the Worker on a canvas, and orbit input going the other way. */
@Component({
  selector: 'ptn-viewport',
  imports: [XuiButtonImports, ViewportDisplayPanel],
  providers: [ViewportClient],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="chrome-bar flex min-h-9 shrink-0 flex-wrap items-center gap-x-2 gap-y-1 px-2 py-1">
      @if (!paint.worn()) {
        <button type="button" xuiButton size="sm" [disabled]="!canOpen" (click)="open()">Open mesh…</button>
      }
      <span class="readout truncate" data-testid="viewport-mesh">{{ meshName() }}</span>
      @if (graph.document(); as document) {
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="viewport-show-graph"
          [active]="followed() === document.id"
          [title]="'Lays the outputs of ' + document.name + ' over the mesh, or a sphere, and keeps up with edits'"
          (click)="follow(document.id)"
        >
          Show {{ document.name }}
        </button>
      }
      @if (paint.worn() && project.active()?.kind === 'texturing') {
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="viewport-paint"
          [variant]="painting() ? 'default' : 'ghost'"
          [title]="'Paints the mesh. Orbit: ' + commands.navigation().orbit + '.'"
          (click)="painting.set(!painting())"
        >
          Paint
        </button>
      }
      <span class="flex-1"></span>
      <select
        class="well h-6 max-w-28 px-1 text-xs"
        aria-label="View"
        [title]="'What the model shows: lit, its shape, or one stored channel as the file holds it' + (commands.keyText('nextView') ? ' (' + commands.keyText('nextView') + ' steps through them)' : '')"
        data-testid="viewport-view"
        (change)="chooseView($event)"
      >
        @for (view of views; track view) {
          <option [value]="view" [selected]="view === display().view">{{ viewName(view) }}</option>
        }
        @if (meshMaps().length > 0 || shownPicture()) {
          <optgroup label="Mesh maps">
            @for (map of meshMaps(); track map.path) {
              <option [value]="mapPrefix + map.path" [selected]="isShown(map.path)">{{ map.label }}</option>
            }
            @if (shownPicture(); as file) {
              <option [value]="mapPrefix + display().picture" selected>{{ file }}</option>
            }
          </optgroup>
        }
      </select>
      <select
        class="well h-6 max-w-28 px-1 text-xs"
        aria-label="Environment"
        title="What lights the model"
        data-testid="viewport-environment"
        (change)="chooseEnvironment($event)"
      >
        @for (room of rooms; track room.value) {
          <option [value]="room.value" [selected]="room.value === display().environment">{{ room.label }}</option>
        }
        @if (panorama(); as file) {
          <option [value]="display().environment" selected>{{ file }}</option>
        }
        @if (canPick) {
          <option value="">From a file…</option>
        }
      </select>
      <select
        class="well h-6 max-w-28 px-1 text-xs"
        aria-label="Look"
        title="A .cube table the picture is viewed through. Nothing that is exported sees it."
        data-testid="viewport-look"
        (change)="chooseLook($event)"
      >
        <option [value]="noLook" [selected]="display().look === null">No look</option>
        @if (lookName(); as file) {
          <option [value]="display().look" selected>{{ file }}</option>
        }
        @if (canPick) {
          <option [value]="pickLook">From a file…</option>
        }
      </select>
      <button
        type="button"
        xuiButton
        size="sm"
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
          class="readout"
          [class.hidden]="!options.stats()"
          data-testid="viewport-stamp"
          [attr.data-stamp-ms]="stampTimes()"
        >
          stamp {{ stampMs() }} ms
        </span>
      }
      @if (viewport.stats(); as stats) {
        <span class="readout" data-testid="viewport-stats" [class.hidden]="!options.stats()">
          {{ stats.width }}×{{ stats.height }} · {{ stats.fps }} fps · {{ stats.latencyMs.toFixed(0) }} ms
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
      ></canvas>
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
        }
        @if (guide(); as points) {
          <svg class="pointer-events-none absolute inset-0 size-full" data-testid="viewport-symmetry" aria-hidden="true">
            @if (paint.symmetry()?.kind === 'radial') {
              <polyline class="fill-none stroke-sky-300/80" stroke-dasharray="6 4" stroke-width="1.5" [attr.points]="points" />
            } @else {
              <polygon class="fill-sky-300/10 stroke-sky-300/80" stroke-dasharray="6 4" stroke-width="1.5" [attr.points]="points" />
            }
          </svg>
        }
        @if (gesture.points().length > 1) {
          <svg class="pointer-events-none absolute inset-0 size-full" data-testid="viewport-curve">
            <polyline class="fill-none stroke-white/70" [attr.points]="curve()" />
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
  protected readonly graph = inject(GraphStore);
  protected readonly paint = inject(PaintStore);
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
      (this.paint.worn() && (this.paint.tool() === 'picker' || this.paint.finding()))
  );
  private panning = false;
  private zooming = false;
  /** Where the brush is over the canvas, in the units of a stroke. */
  protected readonly brushAt = signal<{ x: number; y: number } | null>(null);
  protected readonly options = inject(ViewOptions);
  protected readonly gesture = new StrokeGesture(this.paint, () => this.aim());
  protected readonly curve = computed(() =>
    this.gesture
      .points()
      .map(point => `${point.x},${point.y}`)
      .join(' ')
  );

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
      .map(chord => chord.replace('Mod+', 'Meta+').replace(/\b(Up|Down|Left|Right)$/u, 'Arrow$1').replace('Plus', '+'))
      .join(' ')
  );

  constructor() {
    this.installCommands();
    // A path's points are places on this picture, so one the view has left is painted as it was placed.
    effect(() => {
      this.view();
      untracked(() => this.gesture.finish());
    });
    effect(() => {
      const followed = this.followed();
      const document = this.graph.document();
      const revision = document?.evaluation?.revision;
      if (followed && document?.id === followed && revision !== undefined) {
        void this.viewport.showGraph(followed);
      }
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

  protected readonly rooms = Rooms;
  protected readonly views = Views;
  protected readonly viewName = (view: string): string => ViewNames[view] ?? termName(view);
  protected readonly mapPrefix = MapPrefix;
  protected readonly noLook = NoLook;
  protected readonly pickLook = PickLook;
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
  /** The picture on view when it is none of the last bake's maps, such as one a project kept. */
  protected readonly shownPicture = computed(() => {
    const { view, picture } = this.display();
    return view === 'picture' && picture && !this.meshMaps().some(map => map.path === picture)
      ? fileName(picture)
      : null;
  });

  /** The graph on the mesh, which follows its evaluations until asked to stop. */
  protected readonly followed = signal<string | null>(null);

  protected follow(graph: string): void {
    this.followed.update(now => (now === graph ? null : graph));
  }

  protected async chooseEnvironment(event: Event): Promise<void> {
    const select = event.target as HTMLSelectElement;
    const source = select.value || (await desktop()?.openEnvironment(this.status.environments()));
    if (source) {
      this.viewport.setDisplay({ environment: source });
    } else {
      select.value = this.display().environment;
    }
  }

  protected async chooseLook(event: Event): Promise<void> {
    const select = event.target as HTMLSelectElement;
    const chosen = select.value === PickLook ? await desktop()?.openLook() : select.value || null;
    if (chosen === undefined || (chosen === null && select.value === PickLook)) {
      select.value = this.display().look ?? NoLook;
      return;
    }

    this.viewport.setDisplay({ look: chosen });
  }

  protected chooseView(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.viewport.setDisplay(
      value.startsWith(MapPrefix)
        ? { view: 'picture', picture: value.slice(MapPrefix.length) }
        : { view: value, picture: null }
    );
  }

  protected isShown(path: string): boolean {
    const { view, picture } = this.display();
    return view === 'picture' && picture === path;
  }

  /** Looks along an axis at the whole model. */
  protected snap(view: AxisView): void {
    this.viewport.moveCamera(snapped(this.viewport.camera(), view), false);
  }

  protected async open(): Promise<void> {
    const path = await desktop()?.openMesh();
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
    handle('cancelStroke', () => this.gesture.cancel(), () => this.gesture.points().length > 0 || this.paint.picking() !== null);
    handle('finishPath', () => this.gesture.finish(), () => this.paint.tool() === 'path' && this.gesture.points().length > 1);
  }

  protected press(event: PointerEvent): void {
    if (this.paint.finding() && event.button === 0) {
      void this.paint.findLayerOnMesh(this.stamp(event), this.aim());
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
          ? zoom(camera, event.movementY * DragZoom)
          : (this.panning ? pan : orbit)(camera, event.movementX, event.movementY)
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
    this.viewport.moveCamera(zoom(this.viewport.camera(), event.deltaY), false);
  }

  private spot(event: PointerEvent): { x: number; y: number } {
    const box = this.canvas().nativeElement.getBoundingClientRect();
    return { x: event.clientX - box.left, y: event.clientY - box.top };
  }

  /** A stroke is measured in the canvas's layout pixels, so a brush is the same size whatever the render scale. */
  private stamp(event: PointerEvent) {
    return { ...this.spot(event), pressure: pressureOf(event) };
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
