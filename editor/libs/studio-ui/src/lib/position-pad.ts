import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input, output } from '@angular/core';
import { type Position, type PositionRange, nudged, positionOf, shareOf } from './position';

/** How far an arrow key moves the point, as a share of the range; with Shift, ten times as far. */
const KeyShare = 0.01;

/**
 * A square to place a point on, for a Float2 parameter drawn as a position (#181): press or drag anywhere on it, or
 * move it with the arrow keys. xUI has no such control, so it lives here; the numbers beside it are the caller's, for
 * typing.
 */
@Component({
  selector: 'ptn-position-pad',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'inline-block size-16 shrink-0 touch-none select-none',
    role: 'group',
    tabindex: '0',
    '[attr.aria-label]': 'ariaLabel()',
    '[attr.aria-disabled]': 'disabled() || null',
    '[attr.aria-valuetext]': 'spoken()',
    '(pointerdown)': 'press($event)',
    '(pointermove)': 'drag($event)',
    '(pointerup)': 'lift($event)',
    '(pointercancel)': 'lift($event)',
    '(keydown)': 'key($event)'
  },
  template: `
    <svg viewBox="0 0 64 64" class="size-full" aria-hidden="true">
      <rect x="0.5" y="0.5" width="63" height="63" rx="3" class="fill-surface-inset stroke-border" stroke-width="1" />
      <line [attr.x1]="at().x" y1="1" [attr.x2]="at().x" y2="63" class="stroke-border" stroke-width="1" />
      <line x1="1" [attr.y1]="at().y" x2="63" [attr.y2]="at().y" class="stroke-border" stroke-width="1" />
      <circle [attr.cx]="at().x" [attr.cy]="at().y" r="3.5" class="fill-primary" />
    </svg>
  `
})
export class PositionPad {
  readonly value = input<Position>({ x: 0, y: 0 });
  /** The bottom of each lane's range, the pad's top left. */
  readonly minimum = input(0);
  /** The top of each lane's range, the pad's bottom right. */
  readonly maximum = input(1);
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly ariaLabel = input('Position');
  readonly valueChange = output<Position>();
  /** The hand has let go, so the next move is a new undo step. */
  readonly released = output();

  private dragging = false;

  private readonly range = computed<PositionRange>(() => ({ minimum: this.minimum(), maximum: this.maximum() }));

  protected readonly at = computed(() => {
    const share = shareOf(this.value(), this.range());
    return { x: 1 + share.x * 62, y: 1 + share.y * 62 };
  });

  protected readonly spoken = computed(() => `${String(this.value().x)}, ${String(this.value().y)}`);

  protected press(event: PointerEvent): void {
    if (this.disabled() || event.button !== 0) {
      return;
    }

    this.dragging = true;
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
    this.point(event);
  }

  protected drag(event: PointerEvent): void {
    if (this.dragging) {
      this.point(event);
    }
  }

  protected lift(event: PointerEvent): void {
    if (this.dragging) {
      this.dragging = false;
      (event.currentTarget as HTMLElement).releasePointerCapture(event.pointerId);
      this.released.emit();
    }
  }

  protected key(event: KeyboardEvent): void {
    const range = this.range();
    const step = (range.maximum - range.minimum) * KeyShare * (event.shiftKey ? 10 : 1);
    const moves: Record<string, ['x' | 'y', number]> = {
      ArrowLeft: ['x', -step],
      ArrowRight: ['x', step],
      ArrowUp: ['y', -step],
      ArrowDown: ['y', step]
    };
    const move = moves[event.key];
    if (!move || this.disabled()) {
      return;
    }

    event.preventDefault();
    this.valueChange.emit(nudged(this.value(), move[0], move[1], range));
  }

  private point(event: PointerEvent): void {
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const next = positionOf(
      (event.clientX - box.left) / box.width,
      (event.clientY - box.top) / box.height,
      this.range()
    );
    const rounded = { x: Math.round(next.x * 1000) / 1000, y: Math.round(next.y * 1000) / 1000 };
    if (rounded.x !== this.value().x || rounded.y !== this.value().y) {
      this.valueChange.emit(rounded);
    }
  }
}
