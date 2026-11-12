import { ChangeDetectionStrategy, Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { type RampStop, colourAt, cssOfRamp, roundPosition } from './ramp';

const NudgeStep = 0.01;

function clamp(value: number): number {
  return Math.min(1, Math.max(0, value));
}

/**
 * A colour ramp. A press on the bar adds a stop and drags it; the selected stop takes a colour and an exact position. It
 * writes on every step of a drag; `released` says the hand has let go.
 */
@Component({
  selector: 'ptn-gradient-ramp',
  imports: [XuiButtonImports, XuiColorPickerImports, XuiNumericInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5' },
  template: `
    <div
      class="well relative h-6 w-full touch-none"
      data-testid="gradient-ramp"
      role="group"
      aria-label="Colour ramp"
      [style.background]="css()"
      (lostpointercapture)="release()"
      (pointerdown)="press($event)"
      (pointermove)="drag($event)"
      (pointerup)="release()"
    >
      @for (stop of working(); track $index) {
        <button
          class="absolute top-0 bottom-0 w-2 -translate-x-1/2 cursor-grab border"
          type="button"
          role="slider"
          aria-valuemin="0"
          aria-valuemax="1"
          [attr.aria-label]="'Stop ' + ($index + 1)"
          [title]="'Stop ' + ($index + 1)"
          [attr.aria-valuenow]="stop.position"
          [class.border-foreground]="$index === selectedIndex()"
          [class.border-border]="$index !== selectedIndex()"
          [disabled]="disabled()"
          [style.background]="'#' + stop.colour"
          [style.left.%]="stop.position * 100"
          (keydown)="nudge($event, $index)"
          (pointerdown)="grab($event, $index)"
        ></button>
      }
    </div>
    @if (selected(); as stop) {
      <div class="flex flex-wrap items-center gap-2">
        <xui-color-picker
          showAlpha
          [disabled]="disabled()"
          [value]="'#' + stop.colour"
          (valueChange)="recolour($event)"
        />
        <xui-numeric-input
          class="w-20 shrink-0"
          size="sm"
          aria-label="Stop position"
          [disabled]="disabled()"
          [max]="1"
          [min]="0"
          [stepSize]="0.01"
          [value]="stop.position"
          (valueChange)="position($event)"
        />
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          [disabled]="disabled() || working().length < 2"
          (click)="remove()"
        >
          Remove
        </button>
      </div>
    }
  `
})
export class GradientRamp {
  private dragging = false;

  protected readonly selectedIndex = signal(0);

  readonly stops = input.required<readonly RampStop[]>();
  readonly disabled = input(false);
  readonly stopsChange = output<readonly RampStop[]>();
  readonly released = output();

  /** What the hand has made so far, which the input only reaches on the next change detection. */
  protected readonly working = linkedSignal(() => this.stops());
  protected readonly css = computed(() => cssOfRamp(this.working()));
  protected readonly selected = computed(() => this.working()[this.selectedIndex()] ?? null);

  protected grab(event: PointerEvent, index: number): void {
    event.stopPropagation();
    this.selectedIndex.set(index);
    if (!this.disabled()) {
      this.start(event);
    }
  }

  /** A press on the bar: a new stop in the colour the ramp has there, already under the hand. */
  protected press(event: PointerEvent): void {
    if (this.disabled()) {
      return;
    }

    const position = this.pointOf(event);
    const stop: RampStop = { position, colour: colourAt(this.working(), position) };
    const stops = [...this.working(), stop].sort((a, b) => a.position - b.position);
    this.selectedIndex.set(stops.indexOf(stop));
    this.write(stops);
    this.start(event);
  }

  protected drag(event: PointerEvent): void {
    if (this.dragging) {
      this.place(this.pointOf(event));
    }
  }

  protected release(): void {
    if (this.dragging) {
      this.dragging = false;
      this.released.emit();
    }
  }

  protected nudge(event: KeyboardEvent, index: number): void {
    const step = event.key === 'ArrowRight' ? NudgeStep : event.key === 'ArrowLeft' ? -NudgeStep : 0;
    const stop = this.working()[index];
    if (stop && step !== 0 && !this.disabled()) {
      event.preventDefault();
      this.selectedIndex.set(index);
      this.place(stop.position + step);
      this.released.emit();
    }
  }

  protected recolour(hex: string): void {
    const colour = hex.replace('#', '').toUpperCase();
    const full = colour.length === 6 ? `${colour}FF` : colour;
    if (this.selected()?.colour !== full) {
      this.replace({ colour: full });
      this.released.emit();
    }
  }

  protected position(value: number | null): void {
    if (value !== null && Number.isFinite(value) && value !== this.selected()?.position) {
      this.place(value);
      this.released.emit();
    }
  }

  protected remove(): void {
    const index = this.selectedIndex();
    this.selectedIndex.set(Math.max(0, index - 1));
    this.write(this.working().filter((_, at) => at !== index));
    this.released.emit();
  }

  private start(event: PointerEvent): void {
    (event.currentTarget as Element).closest<HTMLElement>('[role=group]')?.setPointerCapture(event.pointerId);
    this.dragging = true;
  }

  private pointOf(event: PointerEvent): number {
    const box = (event.currentTarget as Element).closest('[role=group]')?.getBoundingClientRect();
    return box && box.width > 0 ? roundPosition(clamp((event.clientX - box.left) / box.width)) : 0;
  }

  /** Moves the selected stop between its neighbours, so that the drag keeps hold of this one. */
  private place(position: number): void {
    const index = this.selectedIndex();
    const before = this.working()[index - 1]?.position ?? 0;
    const after = this.working()[index + 1]?.position ?? 1;
    this.replace({ position: roundPosition(Math.min(after, Math.max(before, clamp(position)))) });
  }

  private replace(change: Partial<RampStop>): void {
    const index = this.selectedIndex();
    this.write(this.working().map((stop, at) => (at === index ? { ...stop, ...change } : stop)));
  }

  private write(stops: readonly RampStop[]): void {
    this.working.set(stops);
    this.stopsChange.emit(stops);
  }
}
