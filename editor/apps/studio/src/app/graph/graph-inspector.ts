import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import { matPublish, matRadioButtonUnchecked } from '@ng-icons/material-icons/baseline';
import type { GraphEdgeState, GraphNodeState, GraphPresetState, GraphSettingDefinition } from '@patina/api-client';
import {
  AngleDial,
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
import { IconImports } from '../core/icons';
import { LibraryInspector } from '../library/library-inspector';
import { holds } from './graph-authoring';
import {
  type GraphField,
  colorTextOf,
  expressionKey,
  fieldsOf,
  hexOfLanes,
  keptLanes,
  lanesOfColorText,
  lanesOfHex,
  sliderRange
} from './graph-fields';
import { edgeKey } from './graph-model';
import { GraphNoteInspector, GraphSelection, GraphSettings } from './graph-settings';
import { GraphStore } from './graph-store';

type NumberField = Extract<GraphField, { control: 'number' }>;
type ColourField = Extract<GraphField, { control: 'colour' }>;

/** A field made a parameter: the parameter, and the text that points the field at it. */
interface Exposure {
  parameter: GraphSettingDefinition;
  port: string;
  value: string;
}

const SliderSteps = 1000;
/** How a setting names a parameter of its graph instead of holding a value: Vixen's name-knob reference. */
const ReferencePrefix = '$';

export { keptLanes } from './graph-fields';

/** The parameters of the selected node. Every control writes through the store, as live edits. */
@Component({
  selector: 'ptn-graph-inspector',
  imports: [
    IconImports,
    AngleDial,
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
  viewProviders: [provideIcons({ matPublish, matRadioButtonUnchecked })],
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
    @if (presets().length > 0) {
      <div class="mb-3 flex flex-col gap-1" data-testid="node-presets">
        <span class="text-foreground-muted text-xs">Preset</span>
        <xui-select
          aria-label="Preset"
          placeholder="Choose a preset"
          [disabled]="!store.editable()"
          [itemText]="presetName"
          [items]="presets()"
          [value]="null"
          (valueChange)="choosePreset($event)"
        />
      </div>
    }

    @if (exposable().length > 1) {
      <button
        class="mb-3"
        type="button"
        xuiButton
        size="sm"
        data-testid="graph-expose-all"
        title="Makes each number, choice and color on this node that nothing drives a parameter of the graph, as one undo step"
        [disabled]="!store.editable()"
        (click)="exposeAll()"
      >
        Expose All ({{ exposable().length }})
      </button>
    }
    @for (group of groups(); track group.name) {
      @if (group.name) {
        <p class="section-label mt-3 mb-1">{{ group.name }}</p>
      }
      <div class="flex flex-col gap-2.5">
        @for (field of group.fields; track field.name) {
          @if (shown(field)) {
            <div class="flex flex-col gap-1" [attr.data-testid]="'field-' + field.name" [title]="field.summary">
              <div class="flex items-center justify-between gap-2">
                <span class="text-foreground-muted truncate text-xs">{{ field.title }}</span>
                @if (canExposeSetting(field)) {
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    title="Make this a parameter of the graph"
                    [attr.aria-label]="'Expose ' + field.name"
                    [attr.data-testid]="'expose-' + field.name"
                    [disabled]="!store.editable()"
                    (click)="exposeSetting(field)"
                  >
                    <ng-icon xui size="sm" name="matPublish" />
                  </button>
                }
                @if (canShowPort(field)) {
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    title="Show this as a port, to wire a number into it"
                    [active]="portShown(field)"
                    [attr.aria-label]="'Port for ' + field.name"
                    [attr.data-testid]="'port-' + field.name"
                    (click)="store.togglePort(node().id, field.name)"
                  >
                    <ng-icon xui size="sm" name="matRadioButtonUnchecked" />
                  </button>
                }
              </div>
              @if (wireInto(field); as wire) {
                <div class="flex items-center gap-2" [attr.data-testid]="'wired-' + field.name">
                  <span class="readout min-w-0 flex-1 truncate">{{ wire.fromPort }}</span>
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    [disabled]="!store.editable()"
                    (click)="store.disconnect(node().id, field.name)"
                  >
                    Disconnect
                  </button>
                </div>
              } @else {
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
                      } @else if (field.source === 'text' && field.widget === 'Angle') {
                        <ptn-angle-dial
                          [ariaLabel]="field.title"
                          [disabled]="!store.editable()"
                          [value]="current"
                          (released)="release()"
                          (valueChange)="setNumber(field, $event)"
                        />
                        <span class="flex-1"></span>
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
                        [aria-label]="field.name + ' value'"
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
                            <ng-icon xui size="sm" name="matPublish" />
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
                    @if (field.widget === 'Toggle') {
                      <button
                        class="self-start"
                        type="button"
                        xuiButton
                        size="sm"
                        [active]="isOn(field)"
                        [disabled]="!store.editable()"
                        (click)="setOn(field, !isOn(field))"
                      >
                        {{ field.title }}
                      </button>
                    } @else {
                      <xui-switch
                        [aria-label]="field.title"
                        [checked]="isOn(field)"
                        [disabled]="!store.editable()"
                        (checkedChange)="setOn(field, $event)"
                      />
                    }
                  }
                  @case ('colour') {
                    <div class="flex items-center gap-2">
                      <xui-color-picker
                        class="min-w-0 flex-1"
                        showAlpha
                        [disabled]="!store.editable() || drivenColour(field)"
                        [value]="hexOf(field)"
                        (valueChange)="setHex(field, $event)"
                      />
                      @if (field.source === 'value') {
                        <button
                          type="button"
                          xuiButton
                          size="sm"
                          variant="ghost"
                          title="Drive this from a Color parameter"
                          [attr.aria-label]="field.name + ' expression'"
                          [attr.data-testid]="'expression-toggle-' + field.name"
                          [active]="showsExpression(field.name)"
                          (click)="toggleExpression(field.name)"
                        >
                          =
                        </button>
                        @if (expressionOf(field.name) === '') {
                          <button
                            type="button"
                            xuiButton
                            size="sm"
                            variant="ghost"
                            title="Make this a Color parameter of the graph"
                            [attr.aria-label]="'Expose ' + field.name"
                            [attr.data-testid]="'expose-' + field.name"
                            [disabled]="!store.editable()"
                            (click)="exposeColour(field)"
                          >
                            <ng-icon xui size="sm" name="matPublish" />
                          </button>
                        }
                      }
                    </div>
                    @if (field.source === 'value' && showsExpression(field.name)) {
                      <input
                        xuiInput
                        size="sm"
                        placeholder="A Color parameter's name"
                        [attr.aria-label]="field.name + ' expression text'"
                        [attr.data-testid]="'expression-' + field.name"
                        [disabled]="!store.editable()"
                        [value]="expressionOf(field.name)"
                        (change)="setExpression(field.name, $event)"
                      />
                    }
                    <div class="mt-1 grid grid-cols-4 gap-1" data-testid="colour-lanes">
                      @for (lane of lanes; track lane) {
                        <xui-numeric-input
                          size="sm"
                          [aria-label]="field.name + ' ' + lane"
                          [disabled]="!store.editable() || drivenColour(field)"
                          buttonPosition="none"
                          [stepSize]="0.01"
                          [value]="laneOf(field, $index)"
                          (valueChange)="setLane(field, $index, $event)"
                        />
                      }
                    </div>
                  }
                  @case ('choice') {
                    <xui-select
                      [aria-label]="field.name"
                      [disabled]="!store.editable()"
                      [itemText]="field.label"
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
              }
            </div>
          }
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
  /** A value a wire could reach, where there is something to wire to it and no wire yet. */
  protected canShowPort(field: GraphField): boolean {
    return field.source === 'value' && this.store.hasNumberSource() && !this.wireInto(field);
  }

  protected portShown(field: GraphField): boolean {
    return this.store.shownPorts().has(edgeKey({ toNode: this.node().id, toPort: field.name }));
  }

  /** What drives a value, which then has no control: the wire has the last word on it. */
  protected wireInto(field: GraphField): GraphEdgeState | undefined {
    return field.source === 'value' ? this.store.wireInto(this.node().id, field.name) : undefined;
  }

  protected readonly groups = computed(() => {
    const type = this.type();
    return type ? fieldsOf(type) : [];
  });
  protected readonly compound = computed(() => this.store.compounds().has(this.node().type));
  /** A compound's presets, which set this node's settings. */
  protected readonly presets = computed(() => this.type()?.presets ?? []);
  protected readonly presetName = (preset: GraphPresetState): string => preset.name;

  protected choosePreset(preset: GraphPresetState | null): void {
    if (preset) {
      this.store.applyNodePreset(this.node().id, preset);
    }
  }

  /** A setting is shown unless its condition names another setting of this node that does not hold the value. */
  protected shown(field: GraphField): boolean {
    const condition = field.visibleIf;
    if (!condition) {
      return true;
    }

    const other = this.type()?.settings.find(setting => setting.name === condition.setting);
    return holds(condition, this.store.textOf(this.node().id, condition.setting, other?.default ?? ''));
  }

  /** A named choice that does not already follow a parameter can become one, as a number can. */
  protected canExposeSetting(field: GraphField): boolean {
    return field.control === 'choice' && !this.textOf(field).startsWith(ReferencePrefix);
  }

  /** Makes a parameter offering the setting's choices, and points the setting at it with Vixen's `$` reference. */
  protected exposeSetting(field: GraphField): void {
    if (field.control !== 'choice') {
      return;
    }

    this.exposeOne(field);
  }

  /** Makes one field a parameter: the parameter, then the field pointed at it. */
  private exposeOne(field: GraphField): void {
    const exposure = this.exposureOf(field, this.freeName(field.name));
    if (exposure) {
      this.store.addParameter(exposure.parameter);
      this.store.setText(this.node().id, exposure.port, exposure.value);
      this.store.endGesture();
    }
  }

  /** Every field shown that one click could make a parameter, which Expose All does at once (#181). */
  protected readonly exposable = computed(() =>
    this.groups()
      .flatMap(group => group.fields)
      .filter(
        field =>
          this.shown(field) &&
          !this.wireInto(field) &&
          ((field.control === 'number' && this.canExpose(field)) ||
            (field.control === 'colour' && field.source === 'value' && this.expressionOf(field.name) === '') ||
            this.canExposeSetting(field))
      )
  );

  /** Makes every field {@link exposable} lists a parameter, each named apart, as one undo step. */
  protected exposeAll(): void {
    const taken = new Set(this.store.document()?.parameters.map(parameter => parameter.name) ?? []);
    const exposures = this.exposable().flatMap(field => {
      const exposure = this.exposureOf(field, this.freeName(field.name, taken));
      if (exposure) {
        taken.add(exposure.parameter.name);
      }

      return exposure ? [exposure] : [];
    });
    this.store.exposeAll(this.node().id, exposures);
  }

  /** The parameter a field becomes under a name, and how the field then names it; null for one that cannot be. */
  private exposureOf(field: GraphField, name: string): Exposure | null {
    const node = { name, summary: field.summary, group: '', minimum: null, maximum: null };
    if (field.control === 'choice') {
      const parameter = { ...node, default: this.textOf(field), kind: 'Text', accepted: [...field.choices] };
      return { parameter, port: field.name, value: `${ReferencePrefix}${name}` };
    }

    if (field.control === 'colour' && field.source === 'value') {
      const parameter = { ...node, default: colorTextOf(this.lanesOf(field)), kind: 'Color', accepted: [] };
      return { parameter, port: expressionKey(field.name), value: name };
    }

    if (field.control === 'number') {
      const value = this.numberOf(field);
      const parameter = {
        ...node,
        default: String(value),
        kind: field.whole ? 'Int' : 'Float',
        minimum: field.minimum ?? Math.min(0, value),
        maximum: field.maximum ?? Math.max(1, value * 2),
        accepted: []
      };
      return { parameter, port: expressionKey(field.name), value: name };
    }

    return null;
  }

  private freeName(
    field: string,
    taken: ReadonlySet<string> = new Set(this.store.document()?.parameters.map(parameter => parameter.name) ?? [])
  ): string {
    const stem = field.replace(/[^A-Za-z0-9]/gu, '') || 'Parameter';
    let name = stem;
    for (let count = 2; taken.has(name); count += 1) {
      name = `${stem}${String(count)}`;
    }

    return name;
  }
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

  protected readonly lanes = ['R', 'G', 'B', 'A'];

  protected hexOf(field: ColourField): string {
    return hexOfLanes(this.lanesOf(field));
  }

  protected setHex(field: ColourField, hex: string): void {
    const lanes = lanesOfHex(hex);
    if (lanes && hex.toUpperCase() !== this.hexOf(field)) {
      this.writeLanes(field, keptLanes(this.lanesOf(field), lanes));
    }
  }

  protected laneOf(field: ColourField, lane: number): number {
    return this.lanesOf(field)[lane] ?? (lane === 3 ? 1 : 0);
  }

  protected setLane(field: ColourField, lane: number, value: number | null): void {
    if (value === null) {
      return;
    }

    const lanes = [0, 1, 2, 3].map(at => this.laneOf(field, at));
    lanes[lane] = value;
    this.writeLanes(field, lanes);
    this.store.endGesture();
  }

  /** A colour port a Color parameter drives shows the port's own lanes, which the parameter replaces. */
  protected drivenColour(field: ColourField): boolean {
    return field.source === 'value' && this.expressionOf(field.name) !== '';
  }

  /** Makes a Color parameter from the port's colour, and drives the port from it. */
  protected exposeColour(field: ColourField): void {
    if (field.source !== 'value') {
      return;
    }

    this.exposeOne(field);
  }

  /** A port's lanes, or a compound's Color setting read from its text. */
  private lanesOf(field: ColourField): readonly number[] {
    if (field.source === 'value') {
      return this.store.valueOf(this.node().id, field.name, field.fallback);
    }

    return lanesOfColorText(this.textOf(field)) ?? lanesOfColorText(field.fallback) ?? [1, 1, 1, 1];
  }

  private writeLanes(field: ColourField, lanes: readonly number[]): void {
    if (field.source === 'value') {
      this.store.setValue(this.node().id, field.name, lanes);
    } else {
      this.store.setText(this.node().id, field.name, colorTextOf(lanes));
    }
  }

  /** A number that nothing drives yet can become a parameter in one step. */
  protected canExpose(field: NumberField): boolean {
    return field.fallback.length === 1 && this.expressionOf(field.name) === '';
  }

  /** Makes a parameter of the field's name, starting where the field is, and drives the field from it. */
  protected expose(field: NumberField): void {
    this.exposeOne(field);
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
  // Undo in one of its boxes is the graph's, unless something was typed there (core/commands.ts).
  host: { class: 'block h-full min-h-0', 'data-commands': 'graph', 'data-document-undo': '' },
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
}
