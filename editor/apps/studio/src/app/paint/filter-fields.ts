import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { StackFilterKind } from '@patina/api-client';
import {
  CurveEditor,
  type Curves,
  GradientRamp,
  type RampStop,
  formatCurves,
  formatRamp,
  parseCurves,
  parseRamp
} from '@patina/studio-ui';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import {
  type GraphField,
  colorTextOf,
  fieldsOf,
  hexOfLanes,
  keptLanes,
  lanesOfColorText,
  lanesOfHex,
  sliderRange
} from '../graph/graph-fields';

type NumberField = Extract<GraphField, { control: 'number' }>;

/** How finely a slider moves across its range. */
const SliderSteps = 200;

/** A number a filter's port changed to. */
export interface FilterNumber {
  name: string;
  value: number[];
}

/** A setting a filter's text changed to. */
export interface FilterText {
  name: string;
  value: string;
}

/**
 * The fields of one filter, as the graph's Inspector shows a node's: what its ports and settings declare, with the
 * same curve and ramp editors. A filter layer and a mask effect both hold numbers by port and texts by setting, so
 * what is set and where it goes is the caller's.
 */
@Component({
  selector: 'ptn-filter-fields',
  imports: [
    CurveEditor,
    GradientRamp,
    XuiColorPickerImports,
    XuiInputImports,
    XuiNumericInputImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5' },
  template: `
    @for (group of groups(); track group.name) {
      @if (group.name) {
        <span class="text-foreground-muted text-xs font-medium">{{ group.name }}</span>
      }
      @for (field of group.fields; track field.name) {
        <div class="flex flex-col gap-1" [attr.data-testid]="testId() + field.name" [title]="field.summary">
          <div class="flex items-center gap-2">
            <span class="text-foreground-muted w-24 shrink-0 truncate text-xs">{{ field.name }}</span>
            @switch (field.control) {
              @case ('number') {
                @let current = numberOf(field);
                @if (hasSlider(field)) {
                  @let range = rangeOf(field, current);
                  <xui-slider
                    class="min-w-0 flex-1"
                    labelStepSize="0"
                    [attr.aria-label]="field.name + ' slider'"
                    [max]="range.max"
                    [min]="range.min"
                    [stepSize]="(range.max - range.min) / steps"
                    [value]="current"
                    (valueChange)="setNumber(field, $event)"
                  />
                }
                <xui-numeric-input
                  class="w-20 shrink-0"
                  size="sm"
                  [aria-label]="field.name"
                  [stepSize]="field.whole ? 1 : 0.05"
                  [value]="current"
                  (valueChange)="setNumber(field, $event)"
                />
              }
              @case ('switch') {
                <xui-switch [aria-label]="field.name" [checked]="isOn(field)" (checkedChange)="setOn(field, $event)" />
              }
              @case ('colour') {
                <xui-color-picker showAlpha [value]="hexOf(field)" (valueChange)="setHex(field, $event)" />
              }
              @case ('choice') {
                <xui-select
                  class="min-w-0 flex-1"
                  [aria-label]="field.name"
                  [filterable]="false"
                  [itemText]="field.label"
                  [items]="field.choices"
                  [value]="textOf(field)"
                  (valueChange)="setChoice(field, $event)"
                />
              }
              @case ('text') {
                <input
                  xuiInput
                  size="sm"
                  class="min-w-0 flex-1"
                  [attr.aria-label]="field.name"
                  [value]="textOf(field)"
                  (change)="setText(field, $event)"
                />
              }
            }
          </div>
          @switch (field.control) {
            @case ('curve') {
              @if (curvesOf(field); as curves) {
                <ptn-curve-editor [curves]="curves" (curvesChange)="setCurves(field, $event)" />
              }
            }
            @case ('ramp') {
              @if (stopsOf(field); as stops) {
                <ptn-gradient-ramp [stops]="stops" (stopsChange)="setStops(field, $event)" />
              }
            }
          }
        </div>
      }
    }
  `
})
export class FilterFields {
  /** What the filter's node declares. */
  readonly kind = input.required<Pick<StackFilterKind, 'ports' | 'settings'>>();
  /** Its numbers as set, by port. What is not here is at its default. */
  readonly numbers = input<Readonly<Record<string, readonly number[]>>>({});
  /** Its settings as set, by name. */
  readonly texts = input<Readonly<Record<string, string>>>({});
  /** What a field's test id starts with. */
  readonly testId = input('filter-field-');

  readonly numberChange = output<FilterNumber>();
  readonly textChange = output<FilterText>();

  protected readonly steps = SliderSteps;
  protected readonly groups = computed(() => fieldsOf(this.kind()));

  protected hasSlider(field: NumberField): boolean {
    return field.source === 'value' ? !field.whole : field.minimum !== null && field.maximum !== null;
  }

  protected numberOf(field: NumberField): number {
    if (field.source === 'value') {
      return this.valueOf(field.name, field.fallback)[0] ?? 0;
    }

    const value = Number(this.textOf(field));
    return Number.isFinite(value) ? value : 0;
  }

  protected rangeOf(field: NumberField, value: number): { min: number; max: number } {
    return sliderRange(value, field.source === 'value' ? (field.fallback[0] ?? 0) : Number(field.fallback), field);
  }

  protected setNumber(field: NumberField, value: number | null): void {
    if (value === null || !Number.isFinite(value)) {
      return;
    }

    const kept = field.whole ? Math.round(value) : value;
    if (kept === this.numberOf(field)) {
      return;
    }

    if (field.source === 'value') {
      this.numberChange.emit({ name: field.name, value: [kept] });
    } else {
      this.textChange.emit({ name: field.name, value: String(kept) });
    }
  }

  protected isOn(field: Extract<GraphField, { control: 'switch' }>): boolean {
    return field.source === 'value'
      ? (this.valueOf(field.name, field.fallback)[0] ?? 0) !== 0
      : this.textOf(field) === 'true';
  }

  protected setOn(field: Extract<GraphField, { control: 'switch' }>, on: boolean): void {
    if (field.source === 'value') {
      this.numberChange.emit({ name: field.name, value: [on ? 1 : 0] });
    } else {
      this.textChange.emit({ name: field.name, value: String(on) });
    }
  }

  protected hexOf(field: Extract<GraphField, { control: 'colour' }>): string {
    return hexOfLanes(this.lanesOf(field));
  }

  protected setHex(field: Extract<GraphField, { control: 'colour' }>, hex: string): void {
    const lanes = lanesOfHex(hex);
    if (!lanes || hex.toUpperCase() === this.hexOf(field)) {
      return;
    }

    if (field.source === 'value') {
      this.numberChange.emit({ name: field.name, value: [...lanes] });
    } else {
      // A compound's Color setting is text, and keeps a lane the eight-bit picker did not move.
      this.textChange.emit({ name: field.name, value: colorTextOf(keptLanes(this.lanesOf(field), lanes)) });
    }
  }

  private lanesOf(field: Extract<GraphField, { control: 'colour' }>): readonly number[] {
    return field.source === 'value'
      ? this.valueOf(field.name, field.fallback)
      : (lanesOfColorText(this.textOf(field)) ?? [1, 1, 1, 1]);
  }

  protected textOf(field: Extract<GraphField, { source: 'text' }>): string {
    return this.texts()[field.name] ?? field.fallback;
  }

  protected setChoice(field: Extract<GraphField, { control: 'choice' }>, value: string | null): void {
    if (value !== null && value !== this.textOf(field)) {
      this.textChange.emit({ name: field.name, value });
    }
  }

  protected setText(field: Extract<GraphField, { source: 'text' }>, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (value !== this.textOf(field)) {
      this.textChange.emit({ name: field.name, value });
    }
  }

  protected curvesOf(field: Extract<GraphField, { control: 'curve' }>): Curves | null {
    return parseCurves(this.textOf(field));
  }

  protected setCurves(field: Extract<GraphField, { control: 'curve' }>, curves: Curves): void {
    this.textChange.emit({ name: field.name, value: formatCurves(curves) });
  }

  protected stopsOf(field: Extract<GraphField, { control: 'ramp' }>): readonly RampStop[] | null {
    return parseRamp(this.textOf(field));
  }

  protected setStops(field: Extract<GraphField, { control: 'ramp' }>, stops: readonly RampStop[]): void {
    this.textChange.emit({ name: field.name, value: formatRamp(stops) });
  }

  private valueOf(name: string, fallback: readonly number[]): readonly number[] {
    return this.numbers()[name] ?? fallback;
  }
}
