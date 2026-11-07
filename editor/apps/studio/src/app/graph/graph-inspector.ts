import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import type { GraphNodeState } from '@patina/api-client';
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
import { XuiButtonImports } from '@xui/button';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { LibraryInspector } from '../library/library-inspector';
import { type GraphField, expressionKey, fieldsOf, hexOfLanes, lanesOfHex, sliderRange } from './graph-fields';
import { GraphNoteInspector, GraphSelection, GraphSettings } from './graph-settings';
import { GraphStore } from './graph-store';

type NumberField = Extract<GraphField, { control: 'number' }>;

const SliderSteps = 1000;

/** The parameters of the selected node. Every control writes through the store, as live edits. */
@Component({
  selector: 'ptn-graph-inspector',
  imports: [
    CurveEditor,
    GradientRamp,
    XuiButtonImports,
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
    @if (compound()) {
      <button
        class="mb-3"
        type="button"
        xuiButton
        size="sm"
        data-testid="graph-open-compound"
        (click)="store.enterCompound(node().type)"
      >
        Open compound
      </button>
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
                  @if (field.source === 'value') {
                    <button
                      type="button"
                      xuiButton
                      size="sm"
                      variant="ghost"
                      title="Drive this from a parameter"
                      [attr.aria-label]="field.name + ' expression'"
                      [attr.data-testid]="'expression-toggle-' + field.name"
                      [active]="showsExpression(field.name)"
                      (click)="toggleExpression(field.name)"
                    >
                      =
                    </button>
                    @if (canExpose(field)) {
                      <button
                        type="button"
                        xuiButton
                        size="sm"
                        variant="ghost"
                        title="Make this a parameter of the graph"
                        [attr.aria-label]="'Expose ' + field.name"
                        [attr.data-testid]="'expose-' + field.name"
                        [disabled]="!store.editable()"
                        (click)="expose(field)"
                      >
                        ⇱
                      </button>
                    }
                  }
                </div>
                @if (field.source === 'value' && showsExpression(field.name)) {
                  <input
                    xuiInput
                    size="sm"
                    placeholder="Parameter * 2"
                    [attr.aria-label]="field.name + ' expression text'"
                    [attr.data-testid]="'expression-' + field.name"
                    [disabled]="!store.editable()"
                    [value]="expressionOf(field.name)"
                    (change)="setExpression(field.name, $event)"
                  />
                }
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
                  [aria-label]="field.name"
                  [disabled]="!store.editable()"
                  [items]="field.choices"
                  [value]="textOf(field)"
                  (valueChange)="setChoice(field, $event)"
                />
              }
              @case ('curve') {
                @if (curvesOf(field); as curves) {
                  <ptn-curve-editor
                    [curves]="curves"
                    [disabled]="!store.editable()"
                    (curvesChange)="setCurves(field, $event)"
                    (released)="release()"
                  />
                } @else {
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
              @case ('ramp') {
                @if (stopsOf(field); as stops) {
                  <ptn-gradient-ramp
                    [disabled]="!store.editable()"
                    [stops]="stops"
                    (released)="release()"
                    (stopsChange)="setStops(field, $event)"
                  />
                } @else {
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
  protected readonly compound = computed(() => this.store.compounds().has(this.node().type));
  /** Expression boxes opened by hand. One that holds text is open anyway. */
  private readonly openExpressions = signal<ReadonlySet<string>>(new Set());

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
    return sliderRange(value, field.source === 'value' ? (field.fallback[0] ?? 0) : Number(field.fallback), field);
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

  /** A number that nothing drives yet can become a parameter in one step. */
  protected canExpose(field: NumberField): boolean {
    return field.fallback.length === 1 && this.expressionOf(field.name) === '';
  }

  /** Makes a parameter of the field's name, starting where the field is, and drives the field from it. */
  protected expose(field: NumberField): void {
    const taken = new Set(this.store.document()?.parameters.map(parameter => parameter.name) ?? []);
    const stem = field.name.replace(/[^A-Za-z0-9]/gu, '') || 'Parameter';
    let name = stem;
    for (let count = 2; taken.has(name); count += 1) {
      name = `${stem}${String(count)}`;
    }

    const value = this.numberOf(field);
    this.store.addParameter({
      name,
      default: String(value),
      summary: field.summary,
      kind: field.whole ? 'Int' : 'Float',
      minimum: field.minimum ?? Math.min(0, value),
      maximum: field.maximum ?? Math.max(1, value * 2),
      group: '',
      accepted: []
    });
    this.store.setText(this.node().id, expressionKey(field.name), name);
    this.store.endGesture();
  }

  protected expressionOf(port: string): string {
    return this.store.textOf(this.node().id, expressionKey(port), '');
  }

  protected showsExpression(port: string): boolean {
    return this.openExpressions().has(port) || this.expressionOf(port) !== '';
  }

  protected toggleExpression(port: string): void {
    this.openExpressions.update(open => {
      const next = new Set(open);
      if (!next.delete(port)) {
        next.add(port);
      }

      return next;
    });
  }

  protected setExpression(port: string, event: Event): void {
    this.store.setText(this.node().id, expressionKey(port), (event.target as HTMLInputElement).value.trim());
    this.store.endGesture();
  }

  protected curvesOf(field: Extract<GraphField, { control: 'curve' }>): Curves | null {
    return parseCurves(this.textOf(field));
  }

  protected setCurves(field: Extract<GraphField, { control: 'curve' }>, curves: Curves): void {
    this.store.setText(this.node().id, field.name, formatCurves(curves));
  }

  protected stopsOf(field: Extract<GraphField, { control: 'ramp' }>): readonly RampStop[] | null {
    return parseRamp(this.textOf(field));
  }

  protected setStops(field: Extract<GraphField, { control: 'ramp' }>, stops: readonly RampStop[]): void {
    this.store.setText(this.node().id, field.name, formatRamp(stops));
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

  protected setText(field: Extract<GraphField, { source: 'text' }>, event: Event): void {
    this.store.setText(this.node().id, field.name, (event.target as HTMLInputElement).value);
    this.store.endGesture();
  }
}

/** The Inspector pane: what is selected on the canvas, the graph itself, or else whatever the Library has selected. */
@Component({
  selector: 'ptn-inspector',
  imports: [GraphInspector, GraphNoteInspector, GraphSelection, GraphSettings, LibraryInspector],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block h-full min-h-0',
    '(change)': 'typing = null',
    '(focusout)': 'typing = null',
    '(input)': 'typed($event)',
    '(keydown)': 'onKeydown($event)'
  },
  template: `
    @if (store.selection().size > 1) {
      <ptn-graph-selection />
    } @else if (store.selectedNode(); as node) {
      <ptn-graph-inspector [node]="node" />
    } @else if (store.selectedNote() !== null) {
      <ptn-graph-note [note]="store.selectedNote() ?? 0" />
    } @else if (store.graphInspected()) {
      <ptn-graph-settings />
    } @else {
      <ptn-library-inspector />
    }
  `
})
export class Inspector {
  protected readonly store = inject(GraphStore);
  /** The box holding text not yet committed: Undo there undoes the typing, as anywhere. */
  protected typing: EventTarget | null = null;

  protected typed(event: Event): void {
    this.typing = event.target;
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (this.store.document() === null) {
      return;
    }

    if (event.target !== this.typing) {
      this.store.shortcut(event);
    } else if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 's') {
      // Leaving the box commits what it holds, so that this saves what the user can see.
      (event.target as HTMLElement).blur();
      this.store.shortcut(event);
    }
  }
}
