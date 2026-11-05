import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { type CurveChannel, type CurveKey, type Curves, evaluateCurve } from './curve';

/** The curve being edited: one channel, or red, green and blue together. */
type EditedChannel = CurveChannel | 'rgb';

const Samples = 64;
const ViewSize = 100;
const NudgeStep = 0.01;

const channelOptions: readonly XuiSegmentedOption<EditedChannel>[] = [
  { value: 'rgb', label: 'RGB' },
  { value: 'r', label: 'R' },
  { value: 'g', label: 'G' },
  { value: 'b', label: 'B' },
  { value: 'a', label: 'A' }
];

const strokes: Record<EditedChannel, string> = {
  rgb: 'var(--color-foreground)',
  r: '#e5484d',
  g: '#30a46c',
  b: '#3e8ed0',
  a: 'var(--color-foreground-muted)'
};

function clamp(value: number): number {
  return Math.min(1, Math.max(0, value));
}

function pathOf(keys: readonly CurveKey[]): string {
  const points: string[] = [];
  for (let sample = 0; sample <= Samples; sample++) {
    const x = sample / Samples;
    points.push(`${String(x * ViewSize)},${String((1 - clamp(evaluateCurve(keys, x))) * ViewSize)}`);
  }

  return `M${points.join('L')}`;
}

/**
 * A spline per channel of a picture. A press on empty ground adds a key and drags it; a double click or Delete takes one
 * away. It writes on every step of a drag; `released` says the hand has let go.
 */
@Component({
  selector: 'ptn-curve-editor',
  imports: [XuiSegmentedControlImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5' },
  template: `
    <xui-segmented-control
      size="sm"
      fill
      ariaLabel="Curve channel"
      [disabled]="disabled()"
      [options]="options"
      [value]="channel()"
      (valueChange)="pick($event)"
    />
    <svg
      class="well aspect-square w-full touch-none select-none"
      role="group"
      aria-label="Curve"
      data-testid="curve-editor"
      viewBox="0 0 100 100"
      (lostpointercapture)="release()"
      (pointerdown)="press($event)"
      (pointermove)="drag($event)"
      (pointerup)="release()"
    >
      <path
        d="M25 0V100M50 0V100M75 0V100M0 25H100M0 50H100M0 75H100"
        fill="none"
        stroke="var(--color-border)"
        stroke-width="0.5"
      />
      <path d="M0 100L100 0" fill="none" stroke="var(--color-border)" stroke-dasharray="2 2" stroke-width="0.5" />
      <path fill="none" stroke-width="1.5" [attr.d]="path()" [attr.stroke]="stroke()" />
      @for (key of keys(); track $index) {
        <circle
          class="cursor-grab"
          r="3"
          role="slider"
          tabindex="0"
          fill="var(--color-surface)"
          aria-valuemin="0"
          aria-valuemax="1"
          [attr.aria-label]="'Key ' + ($index + 1)"
          [attr.aria-valuenow]="key.y"
          [attr.cx]="key.x * 100"
          [attr.cy]="(1 - key.y) * 100"
          [attr.stroke]="stroke()"
          (dblclick)="remove($index)"
          (keydown)="nudge($event, $index)"
          (pointerdown)="grab($event, $index)"
        />
      }
    </svg>
  `
})
export class CurveEditor {
  protected readonly options = channelOptions;
  protected readonly channel = signal<EditedChannel>('rgb');
  private dragging: number | null = null;

  readonly curves = input.required<Curves>();
  readonly disabled = input(false);
  readonly curvesChange = output<Curves>();
  readonly released = output();

  protected readonly keys = computed(() => this.curves()[this.lane()]);
  protected readonly path = computed(() => pathOf(this.keys()));
  protected readonly stroke = computed(() => strokes[this.channel()]);

  private lane(): CurveChannel {
    const channel = this.channel();
    return channel === 'rgb' ? 'r' : channel;
  }

  protected pick(channel: EditedChannel | null): void {
    if (channel !== null) {
      this.channel.set(channel);
    }
  }

  protected grab(event: PointerEvent, index: number): void {
    event.stopPropagation();
    if (!this.disabled()) {
      this.start(event, index);
    }
  }

  /** A press on empty ground: a new key, already under the hand. */
  protected press(event: PointerEvent): void {
    if (this.disabled()) {
      return;
    }

    const at = this.pointOf(event);
    const keys = [...this.keys(), at].sort((a, b) => a.x - b.x);
    this.write(keys);
    this.start(event, keys.indexOf(at));
  }

  protected drag(event: PointerEvent): void {
    if (this.dragging !== null) {
      this.place(this.dragging, this.pointOf(event));
    }
  }

  protected release(): void {
    if (this.dragging !== null) {
      this.dragging = null;
      this.released.emit();
    }
  }

  protected remove(index: number): void {
    const keys = this.keys();
    if (keys.length > 2 && !this.disabled()) {
      this.write(keys.filter((_, at) => at !== index));
      this.released.emit();
    }
  }

  protected nudge(event: KeyboardEvent, index: number): void {
    const key = this.keys()[index];
    const steps: Record<string, CurveKey> = {
      ArrowUp: { x: 0, y: NudgeStep },
      ArrowDown: { x: 0, y: -NudgeStep },
      ArrowRight: { x: NudgeStep, y: 0 },
      ArrowLeft: { x: -NudgeStep, y: 0 }
    };
    const step = steps[event.key];
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this.remove(index);
    } else if (key && step && !this.disabled()) {
      event.preventDefault();
      this.place(index, { x: key.x + step.x, y: key.y + step.y });
      this.released.emit();
    }
  }

  private start(event: PointerEvent, index: number): void {
    (event.currentTarget as Element).closest('svg')?.setPointerCapture(event.pointerId);
    this.dragging = index;
  }

  private pointOf(event: PointerEvent): CurveKey {
    const box = (event.currentTarget as Element).closest('svg')?.getBoundingClientRect();
    if (!box || box.width === 0 || box.height === 0) {
      return { x: 0, y: 0 };
    }

    return { x: clamp((event.clientX - box.left) / box.width), y: clamp(1 - (event.clientY - box.top) / box.height) };
  }

  /** Moves a key between its neighbours, so that the keys stay in order and the drag keeps hold of this one. */
  private place(index: number, at: CurveKey): void {
    const keys = [...this.keys()];
    const before = keys[index - 1];
    const after = keys[index + 1];
    const x = Math.min(after?.x ?? 1, Math.max(before?.x ?? 0, clamp(at.x)));
    keys[index] = { x, y: clamp(at.y) };
    this.write(keys);
  }

  private write(keys: readonly CurveKey[]): void {
    const channel = this.channel();
    this.curvesChange.emit(
      channel === 'rgb' ? { ...this.curves(), r: keys, g: keys, b: keys } : { ...this.curves(), [channel]: keys }
    );
  }
}
