import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  input,
  linkedSignal,
  output
} from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import { matLink, matLinkOff } from '@ng-icons/material-icons/baseline';
import { type Position, PositionPad } from '@patina/studio-ui';
import { XuiButtonImports } from '@xui/button';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { IconImports } from '../core/icons';
import { pairOfText, pairTextOf } from './graph-fields';

/**
 * A Float2 parameter's two numbers (#181), drawn as its widget asks: `Position` places a point on a square beside the
 * numbers, `Tiling` keeps the two counts equal until they are unlinked, and anything else is the two numbers alone.
 * What it edits is the parameter's text, `x,y`, which is what every caller stores.
 */
@Component({
  selector: 'ptn-pair-field',
  imports: [IconImports, PositionPad, XuiButtonImports, XuiNumericInputImports],
  viewProviders: [provideIcons({ matLink, matLinkOff })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex min-w-0 flex-1 items-center gap-1' },
  template: `
    @if (widget() === 'Position') {
      <ptn-position-pad
        [ariaLabel]="label()"
        [disabled]="disabled()"
        [maximum]="maximum() ?? 1"
        [minimum]="minimum() ?? 0"
        [value]="position()"
        (released)="released.emit()"
        (valueChange)="setBoth($event.x, $event.y)"
      />
    }
    <div class="flex min-w-0 flex-1 flex-col gap-1">
      <xui-numeric-input
        size="sm"
        buttonPosition="none"
        [aria-label]="label() + (tiling() ? ' across' : ' x')"
        [disabled]="disabled()"
        [stepSize]="tiling() ? 1 : 0.01"
        [value]="pair()[0]"
        (valueChange)="setX($event ?? 0)"
      />
      <xui-numeric-input
        size="sm"
        buttonPosition="none"
        [aria-label]="label() + (tiling() ? ' down' : ' y')"
        [disabled]="disabled()"
        [stepSize]="tiling() ? 1 : 0.01"
        [value]="pair()[1]"
        (valueChange)="setY($event ?? 0)"
      />
    </div>
    @if (tiling()) {
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="pair-linked"
        [attr.aria-pressed]="linked()"
        [attr.aria-label]="linked() ? 'Unlink across and down' : 'Keep across and down equal'"
        [title]="linked() ? 'Across and down move together' : 'Across and down move apart'"
        [disabled]="disabled()"
        (click)="linked.set(!linked())"
      >
        <ng-icon xui size="sm" [name]="linked() ? 'matLink' : 'matLinkOff'" />
      </button>
    }
  `
})
export class PairField {
  /** The parameter's text, `x,y`. */
  readonly value = input('0,0');
  /** `Position`, `Tiling`, or anything else for two numbers. */
  readonly widget = input<string | null | undefined>(null);
  readonly minimum = input<number | null | undefined>(null);
  readonly maximum = input<number | null | undefined>(null);
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly label = input('Value');
  /** The new text, `x,y`. */
  readonly valueChange = output<string>();
  /** A drag on the square ended or a number was typed, so the next change is a new undo step. */
  readonly released = output();

  protected readonly pair = computed<readonly [number, number]>(() => pairOfText(this.value()) ?? [0, 0]);
  protected readonly position = computed<Position>(() => ({ x: this.pair()[0], y: this.pair()[1] }));
  protected readonly tiling = computed(() => this.widget() === 'Tiling');
  /** A tiling starts linked while its two counts are equal, as a square tiling is. */
  protected readonly linked = linkedSignal(() => this.pair()[0] === this.pair()[1]);

  protected setX(x: number): void {
    this.setBoth(x, this.tiling() && this.linked() ? x : this.pair()[1]);
    this.released.emit(); // a number typed is one step, as a drag let go is
  }

  protected setY(y: number): void {
    this.setBoth(this.tiling() && this.linked() ? y : this.pair()[0], y);
    this.released.emit();
  }

  protected setBoth(x: number, y: number): void {
    const held = (lane: number) => Math.min(this.maximum() ?? Infinity, Math.max(this.minimum() ?? -Infinity, lane));
    const text = pairTextOf(held(x), held(y));
    if (text !== pairTextOf(...this.pair())) {
      this.valueChange.emit(text);
    }
  }
}
