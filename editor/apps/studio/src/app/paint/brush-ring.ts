import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Injectable,
  computed,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { PaintStore } from './paint-store';

/** How long the brush keeps still before its stamp is drawn again, so a slider dragged through asks once. */
const Quiet = 150;
/** Below this radius on the screen a stamp's shape cannot be told from a dot, so a ring shows. */
const SmallestStamp = 6;

/**
 * One stamp of the brush as it stands (#137), drawn by the Worker under /previews: the shape a cursor shows, its alpha,
 * angle and stretch, with nothing random. Its size is not part of it, so a brush resized keeps its picture.
 */
@Injectable({ providedIn: 'root' })
export class BrushStamp {
  private readonly paint = inject(PaintStore);
  /** The stamp, as an address an image can show, or null until there is one. */
  readonly url = signal<string | null>(null);
  /** What the stamp was asked for, without the brush's size. */
  private readonly shape = computed(() => {
    const id = this.paint.state()?.id;
    return id ? JSON.stringify([id, { ...this.paint.wholeBrush(), radius: 1 }]) : null;
  });

  constructor() {
    let wait: ReturnType<typeof setTimeout> | undefined;
    let asked = 0;
    effect(() => {
      const shape = this.shape();
      const brush = this.paint.wholeBrush();
      clearTimeout(wait);
      const ticket = ++asked;
      if (!shape) {
        this.show(null);
        return;
      }

      wait = setTimeout(() => {
        void this.paint.stampPreview(brush).then(
          blob => {
            if (ticket === asked) {
              this.show(blob);
            }
          },
          () => undefined // a brush the Server refused keeps the last stamp, as a slider passes through it
        );
      }, Quiet);
    });
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(wait);
      asked++;
      this.show(null);
    });
  }

  private show(blob: Blob | null): void {
    const held = this.url();
    if (held) {
      URL.revokeObjectURL(held);
    }

    this.url.set(blob ? URL.createObjectURL(blob) : null);
  }
}

/**
 * The brush under the pointer on a view: its stamp's own shape when it is large enough to read, and a ring otherwise.
 * The box is the brush's radius either way, so what measures it measures the brush; a stretched stamp reaches past it.
 */
@Component({
  selector: 'ptn-brush-ring',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': 'ring()',
    '[attr.data-shape]': 'shaped() ? "stamp" : "ring"',
    '[style.height.px]': 'radius() * 2',
    '[style.left.px]': 'x() - radius()',
    '[style.top.px]': 'y() - radius()',
    '[style.width.px]': 'radius() * 2'
  },
  template: `
    @if (shaped()) {
      <img
        class="absolute start-1/2 top-1/2 max-w-none -translate-x-1/2 -translate-y-1/2 opacity-60 mix-blend-difference select-none"
        alt=""
        draggable="false"
        data-testid="brush-stamp"
        [style.height.px]="reach() * 2"
        [style.width.px]="reach() * 2"
        [src]="stamp.url()"
      />
    }
  `
})
export class BrushRing {
  protected readonly stamp = inject(BrushStamp);
  private readonly paint = inject(PaintStore);
  /** Where the pointer is, in the view's pixels. */
  readonly x = input.required<number>();
  readonly y = input.required<number>();
  /** The brush's radius there, in the view's pixels. */
  readonly radius = input.required<number>();

  protected readonly shaped = computed(() => this.stamp.url() !== null && this.radius() >= SmallestStamp);
  protected readonly ring = computed(() =>
    this.shaped()
      ? 'pointer-events-none absolute'
      : 'pointer-events-none absolute rounded-full border border-white/70 mix-blend-difference'
  );

  /**
   * How far the stamp's picture reaches from its middle: a stretched stamp keeps its area, so its long half-axis is the
   * radius times the square root of the stretch, and the picture's half-side is that.
   */
  protected readonly reach = computed(() => {
    const aspect = this.paint.brush().aspect;
    return this.radius() * Math.sqrt(aspect > 1 && Number.isFinite(aspect) ? Math.min(aspect, 16) : 1);
  });
}
