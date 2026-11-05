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
import { XuiButtonImports } from '@xui/button';
import { desktop } from '../core/desktop';
import { GraphStore } from '../graph/graph-store';
import { PaintStore, pressureOf } from '../paint/paint-store';
import { StrokeGesture } from '../paint/stroke-gesture';
import { FramePainter } from './frame-painter';
import type { ViewportFrame } from './frame-transport';
import { orbit, zoom } from './orbit';
import { ViewportClient } from './viewport-client';

/** The docked viewport: frames from the Worker on a canvas, and orbit input going the other way. */
@Component({
  selector: 'ptn-viewport',
  imports: [XuiButtonImports],
  providers: [ViewportClient],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="chrome-bar flex h-9 shrink-0 items-center gap-2 px-2">
      <button type="button" xuiButton size="sm" [disabled]="!canOpen" (click)="open()">Open mesh…</button>
      <span class="readout truncate" data-testid="viewport-mesh">{{ meshName() }}</span>
      @if (graph.document(); as document) {
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="viewport-show-graph"
          [disabled]="!viewport.mesh()"
          [title]="'Lays the outputs of ' + document.name + ' over this mesh, as they stand now'"
          (click)="viewport.showGraph(document.id)"
        >
          Show {{ document.name }}
        </button>
      }
      @if (paint.worn()) {
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
      @if (stampMs() !== null) {
        <span class="readout" data-testid="viewport-stamp" [attr.data-stamp-ms]="stampTimes()">
          stamp {{ stampMs() }} ms
        </span>
      }
      @if (viewport.stats(); as stats) {
        <span class="readout" data-testid="viewport-stats">
          {{ stats.width }}×{{ stats.height }} · {{ stats.fps }} fps · {{ stats.latencyMs.toFixed(0) }} ms
        </span>
      }
    </div>
    <div #host class="well relative min-h-0 flex-1 overflow-hidden">
      <canvas
        #canvas
        class="absolute inset-0 size-full touch-none"
        aria-label="Viewport"
        role="img"
        [attr.data-frames]="viewport.frames()"
        [class.cursor-grabbing]="dragging()"
        [class.cursor-grab]="!dragging() && !painting()"
        [class.cursor-crosshair]="painting()"
        (pointerdown)="press($event)"
        (pointermove)="drag($event)"
        (pointerup)="release($event)"
        (pointercancel)="release($event)"
        (pointerleave)="brushAt.set(null)"
        (dblclick)="gesture.finish()"
        (wheel)="wheel($event)"
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
  protected readonly viewport = inject(ViewportClient);
  protected readonly graph = inject(GraphStore);
  protected readonly paint = inject(PaintStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private painter: FramePainter | null = null;
  private pending: ViewportFrame | null = null;
  private animation = 0;
  private cameraDirty = false;
  private stroking = false;

  /** Whether a left drag paints rather than orbits. */
  protected readonly painting = signal(false);
  /** Where the brush is over the canvas, in the units of a stroke. */
  protected readonly brushAt = signal<{ x: number; y: number } | null>(null);
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
    // What the viewport shows changed from elsewhere, and the label needs telling.
    effect(() => {
      if (this.paint.worn()) {
        void this.viewport.sync();
      } else {
        this.painting.set(false);
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

  protected async open(): Promise<void> {
    const path = await desktop()?.openMesh();
    if (path) {
      await this.viewport.openMesh(path);
    }
  }

  protected press(event: PointerEvent): void {
    this.canvas().nativeElement.setPointerCapture(event.pointerId);
    if (this.painting() && event.button === 0 && !event.altKey) {
      this.stroking = true;
      this.gesture.press(this.stamp(event));
      return;
    }

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
      this.viewport.camera.set(orbit(this.viewport.camera(), event.movementX, event.movementY));
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
