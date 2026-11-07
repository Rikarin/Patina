import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import type {
  GraphSettingDefinition,
  MaterialInfo,
  StackEffectPort,
  StackLayer,
  StackMask,
  StackMaskEffect
} from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { blendName, channelName } from '../core/labels';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { LiveValue } from './live-value';
import { colourOf, PaintStore } from './paint-store';

const Fills: readonly string[] = ['Constant', 'Texture', 'Graph'];
const Projections: readonly { value: string; label: string }[] = [
  { value: 'Uv', label: 'UV' },
  { value: 'Triplanar', label: 'Tri-planar' },
  { value: 'Planar', label: 'Planar' },
  { value: 'Spherical', label: 'Spherical' },
  { value: 'Cylindrical', label: 'Cylindrical' }
];
const Axes: readonly string[] = ['X', 'Y', 'Z'];

/** Four numbers as a swatch's value. */
/** Channels that hold one number, which a slider says better than a colour. */
const ScalarChannels: ReadonlySet<string> = new Set([
  'roughness',
  'metalness',
  'height',
  'occlusion',
  'opacity',
  'mask'
]);

export function hexOf(colour: readonly number[] | undefined): string {
  const [r = 0, g = 0, b = 0] = colour ?? [];
  const lane = (value: number) =>
    Math.round(Math.min(1, Math.max(0, value)) * 255)
      .toString(16)
      .padStart(2, '0');
  return `#${lane(r)}${lane(g)}${lane(b)}`;
}

/**
 * What the chosen layer is: what it reads, how it lands on the mesh, its mask and how it blends. The Server owns all of it, so a
 * row shows the last event it heard, and a field under the hand shows the hand ({@link LiveValue}).
 */
@Component({
  selector: 'ptn-layer-panel',
  imports: [
    XuiButtonImports,
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
    @if (store.chosen(); as layer) {
      <div class="well flex flex-col gap-1.5 p-2" data-testid="paint-details">
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Name</span>
          <input
            class="min-w-0 flex-1"
            xuiInput
            size="sm"
            autocomplete="off"
            aria-label="Layer name"
            data-testid="paint-layer-name"
            [value]="name.shown()"
            (input)="name.edit(text($event))"
            (blur)="commitName(layer)"
            (keydown.enter)="commitName(layer)"
          />
        </div>
        <p class="section-label mt-1">Material Channels</p>
        <div class="flex flex-wrap items-center gap-1">
          @for (channel of store.state()?.channels ?? []; track channel.usage) {
            <button
              type="button"
              xuiButton
              size="sm"
              [attr.data-testid]="'paint-writes-' + channel.usage"
              [variant]="writes(layer, channel.usage) ? 'default' : 'ghost'"
              (click)="toggleChannel(layer, channel.usage)"
            >
              {{ channelName(channel.usage) }}
            </button>
          }
        </div>

        <p class="section-label mt-1">Content</p>
        @switch (layer.kind) {
          @case ('fill') {
            <div class="flex items-center gap-2">
              <span class="text-foreground-muted w-16 shrink-0 text-xs">Reads</span>
              <xui-select
                class="min-w-0 flex-1"
                aria-label="Fill source"
                data-testid="paint-fill-source"
                [items]="fills"
                [value]="layer.fill"
                (valueChange)="store.edit({ op: 'setLayer', layer: layer.id, fill: $event })"
              />
            </div>
            @if (layer.fill !== 'Constant') {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Projection</span>
                <select
                  class="well h-7 min-w-0 flex-1 px-1 text-sm"
                  aria-label="Projection"
                  data-testid="paint-fill-projection"
                  (change)="store.edit({ op: 'setLayer', layer: layer.id, projection: text($event) })"
                >
                  @for (option of projections; track option.value) {
                    <option [value]="option.value" [selected]="option.value === layer.projection">
                      {{ option.label }}
                    </option>
                  }
                </select>
              </div>
              @if (layer.projection === 'Planar' || layer.projection === 'Cylindrical') {
                <div class="flex items-center gap-2">
                  <span class="text-foreground-muted w-16 shrink-0 text-xs">Axis</span>
                  <select
                    class="well h-7 min-w-0 flex-1 px-1 text-sm"
                    aria-label="Projection axis"
                    data-testid="paint-fill-axis"
                    (change)="store.edit({ op: 'setLayer', layer: layer.id, planarAxis: text($event) })"
                  >
                    @for (axis of axes; track axis) {
                      <option [value]="axis" [selected]="axis === layer.planarAxis">{{ axis }}</option>
                    }
                  </select>
                </div>
              }
              <p class="section-label mt-1">UV Transformations</p>
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Scale</span>
                <xui-numeric-input
                  class="min-w-0 flex-1"
                  size="sm"
                  ariaLabel="Projection scale"
                  data-testid="paint-fill-scale"
                  [stepSize]="0.1"
                  [value]="layer.placement?.scale ?? 1"
                  (valueChange)="place(layer, { scale: $event ?? 1 })"
                />
              </div>
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Rotation</span>
                <xui-numeric-input
                  class="min-w-0 flex-1"
                  size="sm"
                  ariaLabel="Projection rotation in degrees"
                  data-testid="paint-fill-rotation"
                  [stepSize]="5"
                  [value]="degrees(layer.placement?.rotation)"
                  (valueChange)="place(layer, { rotation: radians($event) })"
                />
              </div>
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Offset</span>
                <xui-numeric-input
                  class="min-w-0 flex-1"
                  size="sm"
                  ariaLabel="Projection offset across"
                  data-testid="paint-fill-offset-x"
                  [stepSize]="0.05"
                  [value]="layer.placement?.offsetX ?? 0"
                  (valueChange)="place(layer, { offsetX: $event ?? 0 })"
                />
                <xui-numeric-input
                  class="min-w-0 flex-1"
                  size="sm"
                  ariaLabel="Projection offset along"
                  data-testid="paint-fill-offset-y"
                  [stepSize]="0.05"
                  [value]="layer.placement?.offsetY ?? 0"
                  (valueChange)="place(layer, { offsetY: $event ?? 0 })"
                />
              </div>
              @if (layer.projection === 'Triplanar') {
                <div class="flex items-center gap-2">
                  <span class="text-foreground-muted w-16 shrink-0 text-xs">Blending</span>
                  <xui-numeric-input
                    class="min-w-0 flex-1"
                    size="sm"
                    ariaLabel="Projection blending"
                    data-testid="paint-fill-sharpness"
                    [stepSize]="0.5"
                    [value]="layer.placement?.sharpness ?? 4"
                    (valueChange)="place(layer, { sharpness: $event ?? 4 })"
                  />
                </div>
              }
            }
            @for (channel of store.state()?.channels ?? []; track channel.usage) {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-20 shrink-0 truncate text-xs">{{
                  channelName(channel.usage)
                }}</span>
                @switch (layer.fill) {
                  @case ('Constant') {
                    @if (isScalar(channel.usage)) {
                      <xui-slider
                        class="min-w-0 flex-1"
                        labelStepSize="0"
                        [attr.aria-label]="channelName(channel.usage)"
                        [attr.data-testid]="'paint-fill-' + channel.usage"
                        [max]="1"
                        [min]="0"
                        [stepSize]="0.01"
                        [value]="scalar(layer, channel.usage, channel.default)"
                        (valueChange)="setScalar(layer, channel.usage, $event)"
                        (pointerup)="scalarDraft.set(null)"
                      />
                      <span class="readout w-8 shrink-0 text-end text-xs">{{
                        scalar(layer, channel.usage, channel.default).toFixed(2)
                      }}</span>
                    } @else {
                      <xui-color-picker
                        [attr.aria-label]="channelName(channel.usage)"
                        [attr.data-testid]="'paint-fill-' + channel.usage"
                        [value]="hex(layer, channel.usage, channel.default)"
                        (valueChange)="setValue(layer, channel.usage, $event)"
                      />
                    }
                  }
                  @case ('Texture') {
                    <span class="min-w-0 flex-1 truncate text-xs" [title]="layer.textures[channel.usage] ?? ''">
                      {{ fileOf(layer.textures[channel.usage]) || 'None' }}
                    </span>
                    <button
                      type="button"
                      xuiButton
                      size="sm"
                      variant="ghost"
                      [attr.aria-label]="'Choose ' + channel.usage + ' picture'"
                      [attr.data-testid]="'paint-texture-' + channel.usage"
                      (click)="chooseTexture(layer, channel.usage)"
                    >
                      Choose…
                    </button>
                  }
                }
              </div>
            }
            @if (layer.fill === 'Graph') {
              <p class="text-foreground-muted text-xs" data-testid="paint-fill-graph">
                {{ layer.graph ? 'From ' + layer.graph.split('/').pop() : 'Choose a graph from the Materials shelf.' }}
              </p>
            }
          }
          @case ('filter') {
            <div class="flex items-center gap-2">
              <span class="text-foreground-muted w-16 shrink-0 text-xs">Filter</span>
              <xui-select
                class="min-w-0 flex-1"
                aria-label="Filter"
                data-testid="paint-filter"
                [items]="filters()"
                [value]="layer.filter"
                (valueChange)="store.edit({ op: 'setLayer', layer: layer.id, filter: $event })"
              />
            </div>
            @for (setting of settings(layer); track setting) {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-24 shrink-0 truncate text-xs">{{ setting }}</span>
                <xui-numeric-input
                  class="min-w-0 flex-1"
                  size="sm"
                  [ariaLabel]="setting"
                  [stepSize]="0.05"
                  [value]="layer.settings[setting]?.[0] ?? 0"
                  (valueChange)="setSetting(layer, setting, $event ?? 0)"
                />
              </div>
            }
          }
        }

        @for (parameter of knobs(); track parameter.name) {
          <div class="flex items-center gap-2">
            <span class="text-foreground-muted w-16 shrink-0 truncate text-xs" [title]="parameter.summary">{{
              parameter.name
            }}</span>
            @if (parameter.accepted.length > 0) {
              <select
                class="well h-7 min-w-0 flex-1 px-1 text-sm"
                [attr.aria-label]="parameter.name"
                [attr.data-testid]="'paint-knob-' + parameter.name"
                (change)="setKnob(layer, parameter.name, text($event))"
              >
                @for (value of parameter.accepted; track value) {
                  <option [value]="value" [selected]="value === knob(layer, parameter)">{{ value }}</option>
                }
              </select>
            } @else if (parameter.kind === 'Float' || parameter.kind === 'Int') {
              <xui-numeric-input
                class="min-w-0 flex-1"
                size="sm"
                [ariaLabel]="parameter.name"
                [attr.data-testid]="'paint-knob-' + parameter.name"
                [stepSize]="parameter.kind === 'Int' ? 1 : 0.1"
                [value]="+knob(layer, parameter)"
                (valueChange)="setKnob(layer, parameter.name, ($event ?? 0).toString())"
              />
            } @else if (parameter.kind === 'Bool') {
              <input
                type="checkbox"
                [attr.aria-label]="parameter.name"
                [attr.data-testid]="'paint-knob-' + parameter.name"
                [checked]="knob(layer, parameter) === 'true'"
                (change)="setKnob(layer, parameter.name, checked($event).toString())"
              />
            } @else {
              <input
                class="min-w-0 flex-1"
                xuiInput
                size="sm"
                autocomplete="off"
                [attr.aria-label]="parameter.name"
                [attr.data-testid]="'paint-knob-' + parameter.name"
                [value]="knob(layer, parameter)"
                (change)="setKnob(layer, parameter.name, text($event))"
              />
            }
          </div>
        }

        <p class="section-label mt-1">Mask</p>
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Mask</span>
          <xui-select
            class="min-w-0 flex-1"
            aria-label="Mask source"
            data-testid="paint-mask-source"
            [items]="masks()"
            [value]="layer.mask.source"
            (valueChange)="setMask(layer, { source: $event ?? 'None' })"
          />
        </div>
        @if (store.masking() && !store.canPaint()) {
          <p class="text-foreground-muted text-xs" data-testid="paint-mask-no-brush">
            A brush would replace this mask. Set Mask to Paint to paint it.
          </p>
        }
        @switch (layer.mask.source) {
          @case ('Constant') {
            <xui-slider
              labelStepSize="0"
              aria-label="Mask value"
              data-testid="paint-mask-value"
              [max]="1"
              [min]="0"
              [stepSize]="0.01"
              [value]="layer.mask.value"
              (valueChange)="setMask(layer, { value: $event })"
            />
          }
          @case ('Texture') {
            <div class="flex items-center gap-2">
              <span class="min-w-0 flex-1 truncate text-xs" [title]="layer.mask.asset">{{
                fileOf(layer.mask.asset) || 'None'
              }}</span>
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="paint-mask-picture"
                (click)="chooseMaskPicture(layer)"
              >
                Choose…
              </button>
            </div>
          }
          @case ('Anchor') {
            <xui-select
              aria-label="Anchor layer"
              data-testid="paint-mask-anchor"
              [items]="anchors(layer)"
              [value]="layer.mask.anchor"
              (valueChange)="setMask(layer, { anchor: $event ?? '' })"
            />
          }
          @case ('Generator') {
            <xui-select
              aria-label="Generator"
              data-testid="paint-mask-generator"
              [items]="generatorsWith(layer.mask.generator)"
              [value]="layer.mask.generator"
              (valueChange)="setMask(layer, { generator: $event ?? '' })"
            />
            @for (parameter of generatorKnobs(); track parameter.name) {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 truncate text-xs" [title]="parameter.summary">{{
                  parameter.name
                }}</span>
                @if (parameter.accepted.length > 0) {
                  <select
                    class="well h-7 min-w-0 flex-1 px-1 text-sm"
                    [attr.aria-label]="parameter.name"
                    [attr.data-testid]="'paint-mask-knob-' + parameter.name"
                    (change)="setGeneratorKnob(layer, parameter.name, text($event))"
                  >
                    @for (value of parameter.accepted; track value) {
                      <option [value]="value" [selected]="value === generatorKnob(layer, parameter)">{{ value }}</option>
                    }
                  </select>
                } @else {
                  <xui-numeric-input
                    class="min-w-0 flex-1"
                    size="sm"
                    [ariaLabel]="parameter.name"
                    [attr.data-testid]="'paint-mask-knob-' + parameter.name"
                    [stepSize]="parameter.kind === 'Int' ? 1 : 0.05"
                    [value]="+generatorKnob(layer, parameter)"
                    (valueChange)="setGeneratorKnob(layer, parameter.name, ($event ?? 0).toString())"
                  />
                }
              </div>
            }
          }
          @case ('Bake') {
            <xui-select
              aria-label="Mesh map"
              data-testid="paint-mask-map"
              [items]="meshMaps()"
              [value]="layer.mask.map"
              (valueChange)="setMask(layer, { map: $event ?? '' })"
            />
            @if (layer.mask.map === 'id') {
              <div class="flex flex-col gap-1.5" data-testid="paint-colour-selection">
                <div class="flex flex-wrap items-center gap-1" role="group" aria-label="Colours in the id map">
                  @for (entry of store.idColours() ?? []; track $index) {
                    <button
                      type="button"
                      class="border-hairline size-6 rounded-sm border"
                      data-testid="paint-id-colour"
                      [attr.aria-label]="'Select ' + hexOf(entry.colour)"
                      [class.outline]="same(layer.mask.colour, entry.colour)"
                      [style.background]="hexOf(entry.colour)"
                      [title]="hexOf(entry.colour) + ', ' + percent(entry.share) + '%'"
                      (click)="setMask(layer, { colour: [...entry.colour] })"
                    ></button>
                  } @empty {
                    <span class="text-foreground-muted text-xs" data-testid="paint-id-none">
                      Bake with the ID map on to pick from its colours.
                    </span>
                  }
                </div>
                <div class="flex items-center gap-1">
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    data-testid="paint-eyedropper"
                    [variant]="store.picking() === layer.id ? 'default' : 'ghost'"
                    (click)="store.picking.set(store.picking() === layer.id ? null : layer.id)"
                  >
                    {{ store.picking() === layer.id ? 'Click the mesh or map…' : 'Pick from Mesh' }}
                  </button>
                  @if (layer.mask.colour; as colour) {
                    <span
                      class="border-hairline size-5 rounded-sm border"
                      data-testid="paint-picked"
                      [style.background]="hexOf(colour)"
                    ></span>
                  }
                </div>
                <div class="flex items-center gap-2">
                  <span class="text-foreground-muted w-16 shrink-0 text-xs">Tolerance</span>
                  <xui-slider
                    class="min-w-0 flex-1"
                    labelStepSize="0"
                    aria-label="Colour tolerance"
                    data-testid="paint-mask-tolerance"
                    [max]="0.5"
                    [min]="0"
                    [stepSize]="0.005"
                    [value]="layer.mask.tolerance"
                    (valueChange)="setMask(layer, { tolerance: $event })"
                  />
                </div>
              </div>
            }
          }
        }
      </div>

      @if (store.chosen(); as layer) {
        @if (layer.mask.source !== 'None') {
          <div class="flex items-center gap-2" data-testid="paint-mask-effects">
            <span class="text-foreground-muted w-16 shrink-0 text-xs">Effects</span>
            <select
              class="well h-7 min-w-0 flex-1 px-1 text-sm"
              aria-label="Add mask effect"
              data-testid="paint-mask-effect-add"
              (change)="addEffect(layer, $event)"
            >
              <option value="">Add…</option>
              @for (effect of maskEffects(); track effect.node) {
                <option [value]="effect.node">{{ effect.name }}</option>
              }
            </select>
          </div>
          @for (effect of layer.mask.effects ?? []; track $index; let index = $index) {
            <div class="well flex flex-col gap-1 p-1" [attr.data-testid]="'paint-mask-effect-' + index">
              <div class="flex items-center gap-2">
                <input
                  type="checkbox"
                  [attr.aria-label]="'Run ' + effectName(effect.node)"
                  [checked]="effect.enabled"
                  (change)="setEffect(layer, index, { enabled: checked($event) })"
                />
                <span class="min-w-0 flex-1 truncate text-xs">{{ effectName(effect.node) }}</span>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  [attr.aria-label]="'Remove ' + effectName(effect.node)"
                  (click)="removeEffect(layer, index)"
                >
                  ✕
                </button>
              </div>
              @for (port of effectPorts(effect.node); track port.name) {
                <div class="flex items-center gap-2">
                  <span class="text-foreground-muted w-16 shrink-0 truncate text-xs">{{ port.name }}</span>
                  <xui-numeric-input
                    class="min-w-0 flex-1"
                    size="sm"
                    [ariaLabel]="port.name"
                    [attr.data-testid]="'paint-effect-' + index + '-' + port.name"
                    [stepSize]="0.05"
                    [min]="port.minimum ?? undefined"
                    [max]="port.maximum ?? undefined"
                    [value]="effect.values?.[port.name]?.[0] ?? port.default"
                    (valueChange)="setEffectValue(layer, index, port.name, $event ?? port.default)"
                  />
                </div>
              }
            </div>
          }
        }
      }

      <p class="text-foreground-muted text-xs" data-testid="paint-mesh-maps">
        {{ store.state()?.meshMaps ? 'Using the bake.' : 'No bake yet.' }}
      </p>
    }
  `
})
export class LayerPanel {
  protected readonly store = inject(PaintStore);
  private readonly project = inject(ProjectStore);
  private readonly library = inject(LibraryStore);
  protected readonly fills = Fills;
  protected readonly projections = Projections;
  protected readonly axes = Axes;
  protected readonly blends = computed(() => this.store.vocabulary()?.blends ?? []);
  protected readonly filters = computed(() => this.store.vocabulary()?.filters.map(filter => filter.kind) ?? []);
  protected readonly masks = computed(() => this.store.vocabulary()?.masks ?? []);
  protected readonly generators = computed(() => this.store.vocabulary()?.generators ?? []);
  protected readonly meshMaps = computed(() => this.store.vocabulary()?.meshMaps ?? []);
  protected readonly name = new LiveValue<string>(() => this.store.chosen()?.name ?? '');
  protected readonly scalarDraft = signal<{ usage: string; value: number } | null>(null);

  constructor() {
    effect(() => {
      const layer = this.store.chosen();
      const graph = layer?.graph || layer?.filterNode;
      const project = this.project.current()?.path;
      if (graph && project) {
        void untracked(() => this.loadShelf(project));
      }
    });

    // An id map is what a colour selection chooses from, so it is read when one is on screen.
    effect(() => {
      const mask = this.store.chosen()?.mask;
      const meshMaps = this.store.state()?.meshMaps;
      if (mask?.source === 'Bake' && mask.map === 'id' && meshMaps) {
        untracked(() => void this.store.loadIdColours());
      }
    });

    // Another layer has no draft of this one's.
    effect(() => {
      this.store.layer();
      untracked(() => {
        this.name.release();
      });
    });
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected writes(layer: StackLayer, usage: string): boolean {
    return layer.channels.length === 0 || layer.channels.includes(usage);
  }

  protected toggleChannel(layer: StackLayer, usage: string): void {
    const all = this.store.state()?.channels.map(channel => channel.usage) ?? [];
    const now = layer.channels.length === 0 ? all : layer.channels;
    const next = now.includes(usage) ? now.filter(channel => channel !== usage) : [...now, usage];
    void this.store.edit({ op: 'setLayer', layer: layer.id, channels: next.length === all.length ? [] : next });
  }

  protected commitName(layer: StackLayer): void {
    const name = this.name.shown();
    this.name.release();
    if (name !== layer.name) {
      void this.store.edit({ op: 'setLayer', layer: layer.id, name });
    }
  }

  protected hex(layer: StackLayer, usage: string, fallback: readonly number[]): string {
    return hexOf(layer.values[usage] ?? fallback);
  }

  protected setValue(layer: StackLayer, usage: string, hex: string): void {
    void this.store.edit({ op: 'setLayer', layer: layer.id, values: { [usage]: colourOf(hex) } });
  }

  protected isScalar(usage: string): boolean {
    return ScalarChannels.has(usage);
  }

  /** A grey channel's value, or what the hand says while it is on the slider. */
  protected scalar(layer: StackLayer, usage: string, fallback: readonly number[]): number {
    const draft = this.scalarDraft();
    return draft?.usage === usage ? draft.value : ((layer.values[usage] ?? fallback)[0] ?? 0);
  }

  protected setScalar(layer: StackLayer, usage: string, value: number): void {
    this.scalarDraft.set({ usage, value });
    void this.store.edit({ op: 'setLayer', layer: layer.id, values: { [usage]: [value, value, value, 1] } });
  }

  protected setTexture(layer: StackLayer, usage: string, path: string): void {
    void this.store.edit({ op: 'setLayer', layer: layer.id, textures: { [usage]: path } });
  }

  protected settings(layer: StackLayer): readonly string[] {
    return this.store.vocabulary()?.filters.find(filter => filter.kind === layer.filter)?.settings ?? [];
  }

  protected place(layer: StackLayer, change: Partial<NonNullable<StackLayer['placement']>>): void {
    const placement = { scale: 1, rotation: 0, offsetX: 0, offsetY: 0, sharpness: 4, ...layer.placement, ...change };
    void this.store.edit({ op: 'setLayer', layer: layer.id, placement });
  }

  protected degrees(radians: number | undefined): number {
    return Math.round(((radians ?? 0) * 180) / Math.PI);
  }

  protected radians(degrees: number | null): number {
    return ((degrees ?? 0) * Math.PI) / 180;
  }

  protected setSetting(layer: StackLayer, setting: string, value: number): void {
    void this.store.edit({ op: 'setLayer', layer: layer.id, settings: { [setting]: [value] } });
  }

  protected anchors(layer: StackLayer): readonly string[] {
    return this.store
      .layers()
      .map(row => row.layer.id)
      .filter(id => id !== layer.id);
  }

  protected readonly hexOf = hexOf;
  protected readonly channelName = channelName;
  protected readonly blendName = blendName;

  /** The chosen layer's graph, which knows its own knobs. */
  private readonly shelf = signal<readonly MaterialInfo[]>([]);
  protected readonly knobs = computed(() => {
    const layer = this.store.chosen();
    const graph = (layer?.kind === 'filter' ? layer.filterNode : layer?.fill === 'Graph' ? layer.graph : '')
      .split('/')
      .pop();
    if (!graph) {
      return [];
    }

    const stem = (source: string): string | undefined =>
      source
        .split('/')
        .pop()
        ?.replace(/\.[^.]*$/u, '');
    return (
      this.shelf().find(material => material.kind === 'graph' && stem(material.source) === graph)?.parameters ?? []
    );
  });

  private async loadShelf(project: string): Promise<void> {
    const client = await this.library.api();
    const list = await client.GET('/api/v1/library/materials', { params: { query: { project } } });
    this.shelf.set(list.data?.items ?? []);
  }

  protected knob(layer: StackLayer, parameter: GraphSettingDefinition): string {
    return layer.parameters?.[parameter.name] ?? parameter.default;
  }

  protected setKnob(layer: StackLayer, name: string, value: string): void {
    void this.store.edit({ op: 'setLayer', layer: layer.id, parameters: { [name]: value } });
  }

  /** What the chosen mask's generator can be told. */
  protected readonly generatorKnobs = computed(() => {
    const generator = this.store.chosen()?.mask.generator ?? '';
    return this.store.vocabulary()?.generatorParameters[generator] ?? [];
  });

  protected generatorKnob(layer: StackLayer, parameter: GraphSettingDefinition): string {
    return layer.mask.parameters?.[parameter.name] ?? parameter.default;
  }

  protected setGeneratorKnob(layer: StackLayer, name: string, value: string): void {
    this.setMask(layer, { parameters: { ...layer.mask.parameters, [name]: value } });
  }

  protected readonly maskEffects = computed(() => this.store.vocabulary()?.maskEffects ?? []);

  protected generatorsWith(chosen: string): readonly string[] {
    const named = this.generators();
    return chosen && !named.includes(chosen) ? [...named, chosen] : named;
  }

  protected effectName(node: string): string {
    return this.maskEffects().find(effect => effect.node === node)?.name ?? node.split('/').pop() ?? node;
  }

  protected effectPorts(node: string): readonly StackEffectPort[] {
    return this.maskEffects().find(effect => effect.node === node)?.ports ?? [];
  }

  protected addEffect(layer: StackLayer, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const node = select.value;
    select.value = '';
    if (node) {
      this.setEffects(layer, [...(layer.mask.effects ?? []), { node, enabled: true }]);
    }
  }

  protected removeEffect(layer: StackLayer, index: number): void {
    this.setEffects(
      layer,
      (layer.mask.effects ?? []).filter((_, at) => at !== index)
    );
  }

  protected setEffect(layer: StackLayer, index: number, change: Partial<StackMaskEffect>): void {
    this.setEffects(
      layer,
      (layer.mask.effects ?? []).map((effect, at) => (at === index ? { ...effect, ...change } : effect))
    );
  }

  protected setEffectValue(layer: StackLayer, index: number, port: string, value: number): void {
    const values = { ...layer.mask.effects?.[index]?.values, [port]: [value] };
    this.setEffect(layer, index, { values });
  }

  private setEffects(layer: StackLayer, effects: readonly StackMaskEffect[]): void {
    this.setMask(layer, { effects: [...effects] });
  }

  protected fileOf(path: string | undefined): string {
    return path?.split('/').pop() ?? '';
  }

  protected async chooseTexture(layer: StackLayer, usage: string): Promise<void> {
    const picture = await this.project.pickPicture();
    if (picture) {
      this.setTexture(layer, usage, picture);
    }
  }

  protected async chooseMaskPicture(layer: StackLayer): Promise<void> {
    const picture = await this.project.pickPicture();
    if (picture) {
      this.setMask(layer, { asset: picture });
    }
  }

  protected same(left: readonly number[] | null | undefined, right: readonly number[]): boolean {
    return !!left && right.every((lane, index) => Math.abs(lane - (left[index] ?? 0)) < 0.002);
  }

  protected percent(share: number): number {
    return Math.round(share * 100);
  }

  protected setMask(layer: StackLayer, change: Partial<StackMask>): void {
    void this.store.setMask(layer, { ...layer.mask, ...change });
  }
}
