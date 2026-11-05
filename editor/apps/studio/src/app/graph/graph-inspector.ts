import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import type { GraphNodeState } from '@patina/api-client';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { LibraryInspector } from '../library/library-inspector';
import { type GraphField, fieldsOf, hexOfLanes, lanesOfHex, sliderRange } from './graph-fields';
import { GraphStore } from './graph-store';

type NumberField = Extract<GraphField, { control: 'number' }>;

const SliderSteps = 1000;

/** The parameters of the selected node. Every control writes through the store, as live edits. */
@Component({
  selector: 'ptn-graph-inspector',
  imports: [
    XuiColorPickerImports,
    XuiFormFieldImports,
    XuiInputImports,
    XuiNumericInputImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block h-full overflow-auto p-3', '(pointerup)': 'release()', '(focusout)': 'release()' },
  template: `
    <h2 class="truncate text-sm font-semibold" data-testid="inspector-title">{{ title() }}</h2>
    <p class="section-label mb-1">{{ category() }}</p>
    @if (summary(); as text) {
      <p class="text-foreground-muted mb-3 text-xs">{{ text }}</p>
    }

    @for (group of groups(); track group.name) {
      @if (group.name) {
        <p class="section-label mt-3 mb-1">{{ group.name }}</p>
      }
      <div class="flex flex-col gap-2.5">
        @for (field of group.fields; track field.name) {
          <div class="flex flex-col gap-1" [attr.data-testid]="'field-' + field.name" [title]="field.summary">
            <span class="text-foreground-muted truncate text-xs">{{ field.name }}</span>
            @switch (field.control) {
              @case ('number') {
                @let current = numberOf(field);
                <div class="flex items-center gap-2">
                  @if (field.source === 'value' && !field.whole) {
                    @let range = rangeOf(field, current);
                    <xui-slider
                      class="min-w-0 flex-1"
                      labelStepSize="0"
                      [attr.aria-label]="field.name"
                      [disabled]="!store.editable()"
                      [max]="range.max"
                      [min]="range.min"
                      [stepSize]="(range.max - range.min) / steps"
                      [value]="current"
                      (valueChange)="setNumber(field, $event)"
                    />
                  } @else if (field.source === 'text' && field.minimum !== null && field.maximum !== null) {
                    <xui-slider
                      class="min-w-0 flex-1"
                      labelStepSize="0"
                      [attr.aria-label]="field.name"
                      [disabled]="!store.editable()"
                      [max]="field.maximum"
                      [min]="field.minimum"
                      [stepSize]="field.whole ? 1 : (field.maximum - field.minimum) / steps"
                      [value]="current"
                      (valueChange)="setNumber(field, $event)"
                    />
                  }
                  <xui-numeric-input
                    class="w-24 shrink-0"
                    size="sm"
                    [ariaLabel]="field.name + ' value'"
                    [disabled]="!store.editable()"
                    [stepSize]="field.whole ? 1 : 0.1"
                    [value]="current"
                    (valueChange)="setNumber(field, $event)"
                  />
                </div>
              }
              @case ('switch') {
                <xui-switch
                  [attr.aria-label]="field.name"
                  [checked]="isOn(field)"
                  [disabled]="!store.editable()"
                  (checkedChange)="setOn(field, $event)"
                />
              }
              @case ('colour') {
                <xui-color-picker
                  showAlpha
                  [disabled]="!store.editable()"
                  [value]="hexOf(field)"
                  (valueChange)="setHex(field, $event)"
                />
              }
              @case ('choice') {
                <xui-select
                  [ariaLabel]="field.name"
                  [disabled]="!store.editable()"
                  [items]="field.choices"
                  [value]="textOf(field)"
                  (valueChange)="setChoice(field, $event)"
                />
              }
              @case ('text') {
                <input
                  xuiInput
                  size="sm"
                  [attr.aria-label]="field.name"
                  [disabled]="!store.editable()"
                  [value]="textOf(field)"
                  (change)="setText(field, $event)"
                />
              }
            }
          </div>
        }
      </div>
    } @empty {
      <p class="text-foreground-muted">This node has nothing to set.</p>
    }
  `
})
export class GraphInspector {
  protected readonly store = inject(GraphStore);
  protected readonly steps = SliderSteps;

  readonly node = input.required<GraphNodeState>();

  private readonly type = computed(() => this.store.types().get(this.node().type) ?? null);

  protected readonly title = computed(() => this.type()?.title ?? this.node().type);
  protected readonly category = computed(() => this.type()?.category ?? 'Unknown node type');
  protected readonly summary = computed(() => this.type()?.summary ?? '');
  protected readonly groups = computed(() => {
    const type = this.type();
    return type ? fieldsOf(type) : [];
  });

  protected release(): void {
    this.store.endGesture();
  }

  protected numberOf(field: NumberField): number {
    if (field.source === 'value') {
      return this.store.valueOf(this.node().id, field.name, field.fallback)[0] ?? 0;
    }

    const value = Number(this.textOf(field));
    return Number.isFinite(value) ? value : 0;
  }

  protected rangeOf(field: NumberField, value: number): { min: number; max: number } {
    return field.source === 'value'
      ? sliderRange(value, field.fallback[0] ?? 0)
      : sliderRange(value, Number(field.fallback), field);
  }

  protected setNumber(field: NumberField, value: number | null): void {
    if (value === null || !Number.isFinite(value)) {
      return;
    }

    const whole = field.whole ? Math.round(value) : value;
    if (whole === this.numberOf(field)) {
      return;
    }

    if (field.source === 'value') {
      this.store.setValue(this.node().id, field.name, [whole]);
    } else {
      this.store.setText(this.node().id, field.name, String(whole));
    }
  }

  protected isOn(field: Extract<GraphField, { control: 'switch' }>): boolean {
    return field.source === 'value'
      ? (this.store.valueOf(this.node().id, field.name, field.fallback)[0] ?? 0) !== 0
      : this.textOf(field) === 'true';
  }

  protected setOn(field: Extract<GraphField, { control: 'switch' }>, on: boolean): void {
    if (field.source === 'value') {
      this.store.setValue(this.node().id, field.name, [on ? 1 : 0]);
    } else {
      this.store.setText(this.node().id, field.name, String(on));
    }

    this.store.endGesture();
  }

  protected hexOf(field: Extract<GraphField, { control: 'colour' }>): string {
    return hexOfLanes(this.store.valueOf(this.node().id, field.name, field.fallback));
  }

  protected setHex(field: Extract<GraphField, { control: 'colour' }>, hex: string): void {
    const lanes = lanesOfHex(hex);
    if (lanes && hex.toUpperCase() !== this.hexOf(field)) {
      this.store.setValue(this.node().id, field.name, lanes);
    }
  }

  protected textOf(field: Extract<GraphField, { source: 'text' }>): string {
    return this.store.textOf(this.node().id, field.name, field.fallback);
  }

  protected setChoice(field: Extract<GraphField, { control: 'choice' }>, value: string | null): void {
    if (value !== null && value !== this.textOf(field)) {
      this.store.setText(this.node().id, field.name, value);
      this.store.endGesture();
    }
  }

  protected setText(field: Extract<GraphField, { control: 'text' }>, event: Event): void {
    this.store.setText(this.node().id, field.name, (event.target as HTMLInputElement).value);
    this.store.endGesture();
  }
}

/** The Inspector pane: a selected graph node, or else whatever the Library has selected. */
@Component({
  selector: 'ptn-inspector',
  imports: [GraphInspector, LibraryInspector],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block h-full min-h-0' },
  template: `
    @if (store.selectedNode(); as node) {
      <ptn-graph-inspector [node]="node" />
    } @else {
      <ptn-library-inspector />
    }
  `
})
export class Inspector {
  protected readonly store = inject(GraphStore);
}
