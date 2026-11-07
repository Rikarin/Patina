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
  viewChild
} from '@angular/core';
import type { ViewportCameraState } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiSliderImports } from '@xui/slider';
import { desktop } from '../core/desktop';
import { SystemStatus } from '../core/system-status';
import { ViewOptions } from '../core/view-options';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore, pressureOf } from '../paint/paint-store';
import { StrokeGesture } from '../paint/stroke-gesture';
import { FramePainter } from './frame-painter';
import type { ViewportFrame } from './frame-transport';
import { framed, nudge, orbit, pan, zoom } from './orbit';
import { ViewportClient } from './viewport-client';

const Rooms: readonly { value: string; label: string }[] = [
  { value: 'studio', label: 'Studio' },
  { value: 'overcast', label: 'Overcast' },
  { value: 'sunset', label: 'Sunset' }
];

/** The docked viewport: frames from the Worker on a canvas, and orbit input going the other way. */
@Component({
  selector: 'ptn-viewport',
  imports: [XuiSliderImports, XuiButtonImports],
  providers: [ViewportClient],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'flex h-full min-h-0 flex-col',
    '(document:keydown.escape)': 'gesture.cancel()',
    '(document:keydown.f)': 'frame($event)'
  },
  template: `
    <div class="chrome-bar flex h-9 shrink-0 items-center gap-2 px-2">
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
          [title]="'Paints the mesh. Hold Alt to orbit.'"
          (click)="painting.set(!painting())"
        >
          Paint
        </button>
      }
      <span class="flex-1"></span>
      <select
        class="well h-6 max-w-28 px-1 text-xs"
        aria-label="Environment"
        title="What lights the model"
        data-testid="viewport-environment"
        (change)="chooseEnvironment($event)"
      >
        @for (room of rooms; track room.value) {
          <option [value]="room.value" [selected]="room.value === viewport.environment()">{{ room.label }}</option>
        }
        @if (panorama(); as file) {
          <option [value]="viewport.environment()" selected>{{ file }}</option>
        }
        @if (canPick) {
          <option value="">From a file…</option>
        }
      </select>
      <label class="flex items-center gap-1 text-xs" title="How bright the picture is">
        Exposure
        <xui-slider
          class="w-20"
          labelStepSize="0"
          aria-label="Exposure"
          data-testid="viewport-exposure"
          [max]="4"
          [min]="0.1"
          [stepSize]="0.05"
          [value]="viewport.camera().exposure"
          (valueChange)="light({ exposure: $event })"
        />
      </label>
      <label class="flex items-center gap-1 text-xs" title="Turns the light around the model">
        Light
        <xui-slider
          class="w-20"
          labelStepSize="0"
          aria-label="Light rotation"
          data-testid="viewport-light"
          [max]="3.14"
          [min]="-3.14"
          [stepSize]="0.02"
          [value]="viewport.camera().lightRotation"
          (valueChange)="light({ lightRotation: $event })"
        />
      </label>
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
        aria-keyshortcuts="ArrowLeft ArrowRight ArrowUp ArrowDown Shift+ArrowLeft Shift+ArrowRight Shift+ArrowUp Shift+ArrowDown + - F"
        role="application"
        tabindex="0"
        [attr.data-frames]="viewport.frames()"
        [class.cursor-grabbing]="dragging()"
        [class.cursor-grab]="!dragging() && !painting()"
        [class.cursor-crosshair]="painting() || paint.picking() !== null"
        (contextmenu)="$event.preventDefault()"
        (pointerdown)="press($event)"
        (pointermove)="drag($event)"
        (pointerup)="release($event)"
        (pointercancel)="release($event)"
        (pointerleave)="brushAt.set(null)"
        (dblclick)="gesture.finish()"
        (wheel)="wheel($event)"
        (keydown)="key($event)"
      ></canvas>
      @if (painting()) {
        @if (brushAt(); as at) {
          <div
            class="pointer-events-none absolute rounded-full border border-white/70 mix-blend-difference"
            data-testid="viewport-brush"
            [style.height.px]="paint.radius() * 2"
            [style.left.px]="at.x - paint.radius()"
            [style.top.px]="at.y - paint.radius()"
            [style.width.px]="paint.radius() * 2"
          ></div>
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
    </div>
  `
})
export class ViewportPane implements AfterViewInit {
  private readonly status = inject(SystemStatus);
  protected readonly viewport = inject(ViewportClient);
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

  /** Whether a left drag paints rather than orbits. */
  /** Left paints as soon as a stack is on the mesh. Alt-left and right turn the model, middle or Shift moves it. */
  protected readonly painting = signal(true);
  private panning = false;
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

  constructor() {
    effect(() => {
      const followed = this.followed();
      const document = this.graph.document();
      const revision = document?.evaluation?.revision;
      if (followed && document?.id === followed && revision !== undefined) {
        void this.viewport.showGraph(followed);
      }
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
  protected readonly canPick = desktop() !== undefined;
  /** The file lighting the model, when it is not a built-in room. */
  protected readonly panorama = computed(() => {
    const source = this.viewport.environment();
    return Rooms.some(room => room.value === source) ? null : source.split(/[/\\]/u).pop();
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
      await this.viewport.setEnvironment(source);
    } else {
      select.value = this.viewport.environment();
    }
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

  protected light(change: Partial<ViewportCameraState>): void {
    this.viewport.moveCamera({ ...this.viewport.camera(), ...change }, false);
  }

  /** F brings the whole model back, as it does in every 3D tool. */
  protected frame(event: Event): void {
    if (!(event.target as Element).closest('input, textarea, select, [contenteditable=true]')) {
      const { exposure, lightRotation } = this.viewport.camera();
      this.viewport.moveCamera({ ...framed(), exposure, lightRotation }, false);
    }
  }

  protected press(event: PointerEvent): void {
    if ((this.paint.picking() !== null || this.paint.tool() === 'picker') && event.button === 0) {
      void this.paint.pickColourOnMesh(this.stamp(event), this.aim());
      return;
    }

    this.canvas().nativeElement.setPointerCapture(event.pointerId);
    if (this.paint.worn() && this.painting() && event.button === 0 && !event.altKey && !event.shiftKey) {
      this.stroking = true;
      this.gesture.press(this.stamp(event));
      return;
    }

    this.panning = event.button === 1 || event.shiftKey;
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
    } else if (this.dragging()) {
      const move = this.panning ? pan : orbit;
      this.viewport.camera.set(move(this.viewport.camera(), event.movementX, event.movementY));
      this.scheduleCamera();
    }
  }

  protected release(event: PointerEvent): void {
    if (this.stroking) {
      this.stroking = false;
      this.gesture.release();
    } else if (this.dragging()) {
      this.canvas().nativeElement.releasePointerCapture(event.pointerId);
      this.dragging.set(false);
      this.viewport.moveCamera(this.viewport.camera(), false);
    }
  }

  protected wheel(event: WheelEvent): void {
    event.preventDefault();
    this.viewport.moveCamera(zoom(this.viewport.camera(), event.deltaY), false);
  }

  /** The keyboard does what the pointer does, so the view needs no mouse. */
  protected key(event: KeyboardEvent): void {
    const moved = nudge(this.viewport.camera(), event.key, event.shiftKey);
    if (moved) {
      event.preventDefault();
      this.viewport.moveCamera(moved, false);
    }
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
