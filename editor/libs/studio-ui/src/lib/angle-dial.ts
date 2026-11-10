import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input, output } from '@angular/core';
import { angleOf, snapAngle } from './angle';

/**
 * A dial for an angle in degrees: press or drag anywhere on it to point it, with Shift to snap to fifteen degrees.
 * xUI has no angle control, so it lives here; the number beside it is the caller's, for typing.
 */
@Component({
  selector: 'ptn-angle-dial',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'inline-block size-7 shrink-0 touch-none select-none',
    role: 'slider',
    tabindex: '0',
    '[attr.aria-label]': 'ariaLabel()',
    '[attr.aria-valuemin]': '0',
    '[attr.aria-valuemax]': '360',
    '[attr.aria-valuenow]': 'value()',
    '[attr.aria-disabled]': 'disabled() || null',
    '(pointerdown)': 'press($event)',
    '(pointermove)': 'drag($event)',
    '(pointerup)': 'lift($event)',
    '(pointercancel)': 'lift($event)',
    '(keydown)': 'key($event)'
  },
  template: `
    <svg viewBox="-14 -14 28 28" class="size-full" aria-hidden="true">
      <circle r="12.5" class="fill-surface-inset stroke-border" stroke-width="1" />
      <line
        x1="0"
        y1="0"
        [attr.x2]="tip().x"
        [attr.y2]="tip().y"
        class="stroke-primary"
        stroke-width="2"
        stroke-linecap="round"
      />
      <circle r="1.5" class="fill-primary" />
    </svg>
  `
})
export class AngleDial {
  /** Degrees, 0 to 360. */
  readonly value = input(0);
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly ariaLabel = input('Angle');
  readonly valueChange = output<number>();
  /** The hand has let go, so the next turn is a new undo step. */
  readonly released = output();

  private dragging = false;

  protected readonly tip = computed(() => {
    const radians = (this.value() * Math.PI) / 180;
    return { x: Math.cos(radians) * 10, y: -Math.sin(radians) * 10 };
  });

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
    const step = event.shiftKey ? 15 : 1;
    const turn =
      event.key === 'ArrowUp' || event.key === 'ArrowRight'
        ? step
        : event.key === 'ArrowDown' || event.key === 'ArrowLeft'
          ? -step
          : 0;
    if (turn === 0 || this.disabled()) {
      return;
    }

    event.preventDefault();
    this.valueChange.emit((((this.value() + turn) % 360) + 360) % 360);
  }

  private point(event: PointerEvent): void {
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const degrees = angleOf(event.clientX - (box.left + box.width / 2), event.clientY - (box.top + box.height / 2));
    const next = Math.round(snapAngle(degrees, event.shiftKey ? 15 : 0) * 10) / 10;
    if (next !== this.value()) {
      this.valueChange.emit(next);
    }
  }
}
