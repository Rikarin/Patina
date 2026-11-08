import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import type {
  GraphSettingDefinition,
  MaterialInfo,
  StackAnchorRef,
  StackFilterKind,
  StackInstanceRef,
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
import { colourName } from '../core/colour-names';
import { blendName, channelName, choiceName, termName } from '../core/labels';
import { amount, percent } from '../core/numbers';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { FilterFields } from './filter-fields';
import { LayerDraft } from './live-value';
import { MaterialUse } from './material-use';
import { colourOf, PaintStore, writesChannel } from './paint-store';

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

/** What a graph is called by a layer: its file's name without the extension. */
function stemOf(source: string): string | undefined {
  return source
    .split('/')
    .pop()
    ?.replace(/\.[^.]*$/u, '');
}

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
    FilterFields,
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
            (input)="name.edit(layer.id, text($event))"
            (blur)="commitName(layer)"
            (keydown.enter)="commitName(layer)"
          />
        </div>
        <div class="flex items-center gap-2">
          <span
            class="text-foreground-muted w-16 shrink-0 text-xs"
            title="A name other layers read the stack by, as it stands just above this layer"
            >Anchor</span
          >
          <input
            class="min-w-0 flex-1"
            xuiInput
            size="sm"
            autocomplete="off"
            placeholder="None"
            aria-label="Anchor point name"
            data-testid="paint-anchor-point"
            [value]="point.shown()"
            (input)="point.edit(layer.id, text($event))"
            (blur)="commitPoint(layer)"
            (keydown.enter)="commitPoint(layer)"
          />
        </div>
        @if (usesOf(layer); as uses) {
          <ul class="text-foreground-muted flex flex-col gap-0.5 ps-[4.5rem] text-xs" data-testid="paint-anchor-uses">
            @for (use of uses; track $index) {
              <li>Read by {{ layerName(use.layer) }} · {{ use.where }}{{ use.channel ? ' · ' + channelName(use.channel) : '' }}</li>
            } @empty {
              <li>Nothing reads it yet.</li>
            }
          </ul>
        }
        @if (layer.instance; as source) {
          <p class="section-label mt-1">Instance</p>
          <div class="flex items-center gap-2" data-testid="paint-instance">
            <span class="min-w-0 flex-1 truncate text-xs" data-testid="paint-instance-source">
              Stands for {{ sourceName(source) }}{{ source.set ? ' in ' + source.set : '' }}
            </span>
            <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-instance-go" (click)="goToSource(source)">
              Go to Source
            </button>
          </div>
          <p class="text-foreground-muted text-xs">
            Its name, eye, opacity, blend and anchor are its own; everything else follows the source.
          </p>
        } @else if (instancesOf(layer); as instances) {
          <p class="section-label mt-1">Instances</p>
          <ul class="flex flex-col gap-0.5" data-testid="paint-instances">
            @for (instance of instances; track instance.id) {
              <li class="flex items-center gap-2">
                <span class="min-w-0 flex-1 truncate text-xs">{{ instance.name }}</span>
                <button type="button" xuiButton size="sm" variant="ghost" (click)="store.layer.set(instance.id)">Go</button>
              </li>
            }
          </ul>
        }
        @if (!layer.instance) {
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
        @if (layer.kind === 'paint' && !store.masking() && !writes(layer, store.channel())) {
          <p class="text-foreground-muted text-xs" data-testid="paint-not-written">
            This layer leaves {{ channelName(store.channel()) }} alone, so turn it on here to paint it.
          </p>
        }

        }
        @if (!layer.instance) {
        <p class="section-label mt-1">Content</p>
        @switch (layer.kind) {
          @case ('fill') {
            <div class="flex items-center gap-2">
              <span class="text-foreground-muted w-16 shrink-0 text-xs">Reads</span>
              <xui-select
                class="min-w-0 flex-1"
                aria-label="Fill source"
                data-testid="paint-fill-source"
                [itemText]="termName"
                [items]="fills()"
                [value]="layer.fill"
                (valueChange)="store.edit({ op: 'setLayer', layer: layer.id, fill: $event })"
              />
            </div>
            @if (layer.fill !== 'Constant' && layer.fill !== 'Anchor') {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Projection</span>
                <select
                  class="well h-7 min-w-0 flex-1 px-1 text-sm"
                  aria-label="Projection"
                  data-testid="paint-fill-projection"
                  (change)="store.edit({ op: 'setLayer', layer: layer.id, projection: text($event) })"
                >
                  @for (option of projections(); track option.value) {
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
            @for (channel of layer.fill === 'Anchor' ? [] : (store.state()?.channels ?? []); track channel.usage) {
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
                      <span class="readout w-9 shrink-0 text-end text-xs">{{
                        amount(scalar(layer, channel.usage, channel.default))
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
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Graph</span>
                <xui-select
                  class="min-w-0 flex-1"
                  aria-label="Graph"
                  data-testid="paint-fill-graph"
                  [items]="graphs(layer)"
                  [value]="graphOf(layer)"
                  (valueChange)="chooseGraph(layer, $event)"
                />
              </div>
            }
            @if (layer.fill === 'Anchor') {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Anchor</span>
                <xui-select
                  class="min-w-0 flex-1"
                  aria-label="Anchor point the fill reads"
                  data-testid="paint-fill-anchor"
                  [items]="anchorNames(layer)"
                  [value]="layer.anchor?.anchor || null"
                  (valueChange)="setFillAnchor(layer, { anchor: $event ?? '' })"
                />
              </div>
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Channel</span>
                <select
                  class="well h-7 min-w-0 flex-1 px-1 text-sm"
                  aria-label="Channel the fill reads"
                  data-testid="paint-fill-anchor-channel"
                  (change)="setFillAnchor(layer, { channel: text($event) })"
                >
                  <option value="" [selected]="!layer.anchor?.channel">Each channel its own</option>
                  @for (channel of store.state()?.channels ?? []; track channel.usage) {
                    <option [value]="channel.usage" [selected]="channel.usage === layer.anchor?.channel">
                      {{ channelName(channel.usage) }}
                    </option>
                  }
                </select>
              </div>
            }
          }
          @case ('filter') {
            <div class="flex items-center gap-2">
              <span class="text-foreground-muted w-16 shrink-0 text-xs">Filter</span>
              <xui-select
                class="min-w-0 flex-1"
                ariaLabel="Filter"
                data-testid="paint-filter"
                [itemText]="filterName"
                [items]="filtersWith(layer.filterNode)"
                [matchTargetWidth]="false"
                [value]="layer.filterNode"
                (valueChange)="chooseFilter(layer, $event)"
              />
            </div>
            @if (filterOf(layer.filterNode); as kind) {
              <p class="text-foreground-muted text-xs" data-testid="paint-filter-summary">{{ kind.summary }}</p>
              <ptn-filter-fields
                testId="paint-setting-"
                [kind]="kind"
                [numbers]="layer.settings"
                [texts]="layer.parameters ?? {}"
                (numberChange)="setSetting(layer, $event.name, $event.value)"
                (textChange)="setKnob(layer, $event.name, $event.value)"
              />
            } @else if (layer.filterNode) {
              <p class="text-foreground-muted text-xs" data-testid="paint-filter-node">
                From {{ layer.filterNode.split('/').pop() }}. Choosing a filter above replaces it.
              </p>
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
                  <option [value]="value" [selected]="value === knob(layer, parameter)">
                    {{ choiceName(parameter, value) }}
                  </option>
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

        }
        @if (maskEntry(); as entry) {
          <p class="section-label mt-1">Mask Layer</p>
          <div class="flex flex-col gap-1.5" data-testid="paint-mask-layer">
            <div class="flex items-center gap-2">
              <span class="text-foreground-muted w-16 shrink-0 text-xs">Source</span>
              <xui-select
                class="min-w-0 flex-1"
                aria-label="Mask layer source"
                data-testid="paint-mask-layer-source"
                [itemText]="termName"
                [items]="entrySources()"
                [value]="entry.layer.source"
                (valueChange)="$event && setEntry(layer, entry.index, { source: $event })"
              />
            </div>
            @switch (entry.layer.source) {
              @case ('Constant') {
                <div class="flex items-center gap-2">
                  <xui-slider
                    class="min-w-0 flex-1"
                    labelStepSize="0"
                    aria-label="Mask layer value"
                    data-testid="paint-mask-layer-value"
                    [max]="1"
                    [min]="0"
                    [stepSize]="0.01"
                    [value]="entry.layer.value"
                    (valueChange)="setEntry(layer, entry.index, { value: $event })"
                  />
                  <span class="readout w-9 shrink-0 text-end text-xs">{{ percent(entry.layer.value) }}</span>
                </div>
              }
              @case ('Generator') {
                <xui-select
                  aria-label="Mask layer generator"
                  data-testid="paint-mask-layer-generator"
                  [itemText]="termName"
                  [items]="generatorsWith(entry.layer.generator)"
                  [value]="entry.layer.generator"
                  (valueChange)="setEntry(layer, entry.index, { generator: $event ?? '' })"
                />
              }
              @case ('Bake') {
                <xui-select
                  aria-label="Mask layer mesh map"
                  data-testid="paint-mask-layer-map"
                  [itemText]="termName"
                  [items]="meshMaps()"
                  [value]="entry.layer.map"
                  (valueChange)="setEntry(layer, entry.index, { map: $event ?? '' })"
                />
                <xui-select
                  aria-label="Mask layer lane"
                  data-testid="paint-mask-layer-lane"
                  [itemText]="termName"
                  [items]="lanes()"
                  [value]="entry.layer.lane"
                  (valueChange)="setEntry(layer, entry.index, { lane: $event ?? 'Red' })"
                />
              }
              @case ('Texture') {
                <div class="flex items-center gap-2">
                  <span class="min-w-0 flex-1 truncate text-xs" [title]="entry.layer.asset">{{
                    fileOf(entry.layer.asset) || 'None'
                  }}</span>
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    data-testid="paint-mask-layer-picture"
                    (click)="chooseEntryPicture(layer, entry.index)"
                  >
                    Choose…
                  </button>
                </div>
              }
              @case ('Anchor') {
                <xui-select
                  aria-label="Anchor point the mask layer reads"
                  data-testid="paint-mask-layer-anchor"
                  [items]="anchorNames(layer)"
                  [value]="entry.layer.anchor?.anchor || null"
                  (valueChange)="
                    setEntry(layer, entry.index, {
                      anchor: { anchor: $event ?? '', channel: entry.layer.anchor?.channel || store.channel() }
                    })
                  "
                />
              }
              @case ('Paint') {
                <p class="text-foreground-muted text-xs" data-testid="paint-mask-layer-paint">
                  Paint white to show the layer and black to hide it. Where nothing is painted, what is beneath shows.
                </p>
              }
            }
          </div>
        }

        @if (!layer.instance) {
        <p class="section-label mt-1">Mask</p>
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Mask</span>
          <xui-select
            class="min-w-0 flex-1"
            aria-label="Mask source"
            data-testid="paint-mask-source"
            [itemText]="termName"
            [items]="masks()"
            [value]="layer.mask.source"
            (valueChange)="setMask(layer, { source: $event ?? 'None' })"
          />
        </div>
        @if (store.masking() && store.maskLayer() === null && !store.canPaint()) {
          <p class="text-foreground-muted text-xs" data-testid="paint-mask-no-brush">
            A brush would replace this mask. Set Mask to Paint to paint it.
          </p>
        }
        @switch (layer.mask.source) {
          @case ('Constant') {
            <div class="flex items-center gap-2">
              <xui-slider
                class="min-w-0 flex-1"
                labelStepSize="0"
                aria-label="Mask value"
                data-testid="paint-mask-value"
                [max]="1"
                [min]="0"
                [stepSize]="0.01"
                [value]="layer.mask.value"
                (valueChange)="setMask(layer, { value: $event })"
              />
              <span class="readout w-9 shrink-0 text-end text-xs" data-testid="paint-mask-value-readout">{{
                percent(layer.mask.value)
              }}</span>
            </div>
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
              aria-label="Anchor point the mask reads"
              data-testid="paint-mask-anchor"
              [items]="anchorNames(layer)"
              [value]="layer.mask.anchor?.anchor || null"
              (valueChange)="setMaskAnchor(layer, { anchor: $event ?? '' })"
            />
            <select
              class="well h-7 px-1 text-sm"
              aria-label="Channel the mask reads"
              data-testid="paint-mask-anchor-channel"
              (change)="setMaskAnchor(layer, { channel: text($event) })"
            >
              @for (channel of store.state()?.channels ?? []; track channel.usage) {
                <option [value]="channel.usage" [selected]="channel.usage === maskChannel(layer)">
                  {{ channelName(channel.usage) }}
                </option>
              }
            </select>
          }
          @case ('Generator') {
            <xui-select
              aria-label="Generator"
              data-testid="paint-mask-generator"
              [itemText]="termName"
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
                      <option [value]="value" [selected]="value === generatorKnob(layer, parameter)">
                        {{ choiceName(parameter, value) }}
                      </option>
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
              [itemText]="termName"
              [items]="meshMaps()"
              [value]="layer.mask.map"
              (valueChange)="setMask(layer, { map: $event ?? '' })"
            />
            <div class="flex items-center gap-2">
              <span class="text-foreground-muted w-16 shrink-0 text-xs" title="Which lane of the map the mask reads">Lane</span>
              <xui-select
                class="min-w-0 flex-1"
                aria-label="Mask lane"
                data-testid="paint-mask-lane"
                [itemText]="termName"
                [items]="lanes()"
                [value]="layer.mask.lane"
                (valueChange)="setMask(layer, { lane: $event ?? 'Red' })"
              />
            </div>
            @if (layer.mask.map === 'id') {
              <div class="flex flex-col gap-1.5" data-testid="paint-colour-selection">
                <div class="flex flex-wrap items-center gap-1" role="group" aria-label="Colours in the id map">
                  @for (entry of store.idColours() ?? []; track $index) {
                    <button
                      type="button"
                      class="border-hairline size-6 rounded-sm border"
                      data-testid="paint-id-colour"
                      [attr.aria-label]="'Select ' + colourName(entry.colour) + ', ' + percent(entry.share) + ' of the map'"
                      [class.outline]="same(layer.mask.colour, entry.colour)"
                      [style.background]="hexOf(entry.colour)"
                      [title]="colourName(entry.colour) + ', ' + percent(entry.share) + ' of the map'"
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
                  <span class="readout w-9 shrink-0 text-end text-xs">{{ percent(layer.mask.tolerance) }}</span>
                </div>
                <div class="flex items-center gap-2">
                  <span class="text-foreground-muted w-16 shrink-0 text-xs">Softness</span>
                  <xui-slider
                    class="min-w-0 flex-1"
                    labelStepSize="0"
                    aria-label="Colour softness"
                    data-testid="paint-mask-softness"
                    [max]="0.5"
                    [min]="0"
                    [stepSize]="0.005"
                    [value]="layer.mask.softness"
                    (valueChange)="setMask(layer, { softness: $event })"
                  />
                  <span class="readout w-9 shrink-0 text-end text-xs">{{ percent(layer.mask.softness) }}</span>
                </div>
              </div>
            }
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
              @for (group of effectGroups(); track group.category) {
                <optgroup [label]="group.category">
                  @for (effect of group.effects; track effect.node) {
                    <option [value]="effect.node">{{ effect.name }}</option>
                  }
                </optgroup>
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
              @if (filterOf(effect.node); as kind) {
                <ptn-filter-fields
                  [kind]="kind"
                  [numbers]="effect.values ?? {}"
                  [testId]="'paint-effect-' + index + '-'"
                  [texts]="effect.texts ?? {}"
                  (numberChange)="setEffectValue(layer, index, $event.name, $event.value)"
                  (textChange)="setEffectText(layer, index, $event.name, $event.value)"
                />
              }
            </div>
          }
        }
      }

      <div class="flex items-center gap-2">
        <span class="text-foreground-muted w-16 shrink-0 text-xs" title="What the stack's generators and graphs vary by">Seed</span>
        <xui-numeric-input
          class="min-w-0 flex-1"
          size="sm"
          ariaLabel="Seed"
          data-testid="paint-seed"
          [min]="0"
          [stepSize]="1"
          [value]="store.state()?.seed ?? 0"
          (valueChange)="setSeed($event)"
        />
      </div>
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
  private readonly using = inject(MaterialUse);
  protected readonly fills = computed(() => this.store.vocabulary()?.fills.map(term => term.value) ?? []);
  protected readonly projections = computed(() => this.store.vocabulary()?.projections ?? []);
  protected readonly axes = Axes;
  protected readonly blends = computed(() => this.store.vocabulary()?.blends.map(term => term.value) ?? []);
  /** Every filter, by the node it runs. */
  private readonly filters = computed(
    () => new Map((this.store.vocabulary()?.filters ?? []).map(filter => [filter.node, filter] as const))
  );
  protected readonly masks = computed(() => this.store.vocabulary()?.masks.map(term => term.value) ?? []);
  protected readonly generators = computed(() => this.store.vocabulary()?.generators.map(term => term.value) ?? []);
  protected readonly meshMaps = computed(() => this.store.vocabulary()?.meshMaps.map(term => term.value) ?? []);
  /** The lanes of a picture a mask can read, which a direction's mesh map holds its axes in. */
  protected readonly lanes = computed(() => this.store.vocabulary()?.lanes?.map(term => term.value) ?? ['Red']);
  protected readonly termName = termName;
  protected readonly choiceName = choiceName;
  /** A layer by its name, which is what the anchor list offers rather than its id. */
  protected readonly layerName = (id: string): string =>
    this.store.layers().find(row => row.layer.id === id)?.layer.name ?? id;
  /** The name and anchor point fields, each committed only to the layer it was typed for. */
  protected readonly name = new LayerDraft<string>(() => this.store.chosen()?.name ?? '');
  protected readonly point = new LayerDraft<string>(() => this.store.chosen()?.anchorPoint ?? '');
  protected readonly scalarDraft = signal<{ usage: string; value: number } | null>(null);

  constructor() {
    effect(() => {
      const layer = this.store.chosen();
      const wanted = layer?.fill === 'Graph' || layer?.graph || layer?.filterNode;
      const project = this.project.current()?.path;
      if (wanted && project) {
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
        this.point.release();
      });
    });
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected readonly writes = writesChannel;

  protected toggleChannel(layer: StackLayer, usage: string): void {
    const all = this.store.state()?.channels.map(channel => channel.usage) ?? [];
    const now = layer.channels.length === 0 ? all : layer.channels;
    const next = now.includes(usage) ? now.filter(channel => channel !== usage) : [...now, usage];
    void this.store.edit({ op: 'setLayer', layer: layer.id, channels: next.length === all.length ? [] : next });
  }

  /** Sends what was typed, for the layer it was typed for; see {@link LayerDraft}. */
  protected commitName(layer: StackLayer): void {
    const name = this.name.take(layer.id);
    if (name !== null && name !== layer.name) {
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

  /** What a filter runs, or nothing for a compound of the project's own, whose knobs come from the shelf. */
  protected filterOf(node: string): StackFilterKind | undefined {
    return this.filters().get(node);
  }

  /** Every filter there is, and the one a layer runs even when it is the project's own. */
  protected filtersWith(node: string): readonly string[] {
    const all = [...this.filters().keys()];
    return node && !this.filters().has(node) ? [...all, node] : all;
  }

  protected readonly filterName = (node: string): string => {
    const kind = this.filters().get(node);
    return kind ? `${kind.name} · ${kind.category}` : (node.split('/').pop() ?? node);
  };

  /** Another filter starts from its own defaults, which the Server sees to. */
  protected chooseFilter(layer: StackLayer, node: string | null): void {
    if (node && node !== layer.filterNode) {
      void this.store.edit({ op: 'setLayer', layer: layer.id, filterNode: node });
    }
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

  protected setSetting(layer: StackLayer, setting: string, value: readonly number[]): void {
    void this.store.edit({ op: 'setLayer', layer: layer.id, settings: { [setting]: [...value] } });
  }

  /** The anchor points a layer may read, by name: every one but its own, which would read itself. */
  protected anchorNames(layer: StackLayer): readonly string[] {
    return (this.store.state()?.anchors ?? []).map(anchor => anchor.name).filter(name => name !== layer.anchorPoint);
  }

  /** Who reads the anchor point a layer publishes, or nothing when it publishes none. */
  protected usesOf(layer: StackLayer) {
    return layer.anchorPoint
      ? ((this.store.state()?.anchors ?? []).find(anchor => anchor.name === layer.anchorPoint)?.uses ?? [])
      : null;
  }

  /** Sends what was typed, for the layer it was typed for; see {@link LayerDraft}. */
  protected commitPoint(layer: StackLayer): void {
    const point = this.point.take(layer.id)?.trim();
    if (point !== undefined && point !== layer.anchorPoint) {
      void this.store.edit({ op: 'setLayer', layer: layer.id, anchorPoint: point });
    }
  }

  protected setFillAnchor(layer: StackLayer, change: Partial<StackAnchorRef>): void {
    const anchor = { anchor: '', channel: '', ...layer.anchor, ...change };
    void this.store.edit({ op: 'setLayer', layer: layer.id, anchor });
  }

  /**
   * The channel a mask reads its anchor point in. A mask is one picture whichever channel it is applied to, so it
   * always names one: height when the stack has it, which is what wear is usually read from, or the first.
   */
  protected maskChannel(layer: StackLayer): string {
    const channels = (this.store.state()?.channels ?? []).map(channel => channel.usage);
    const held = layer.mask.anchor?.channel;
    return held && channels.includes(held) ? held : channels.includes('height') ? 'height' : (channels[0] ?? '');
  }

  protected setMaskAnchor(layer: StackLayer, change: Partial<StackAnchorRef>): void {
    const anchor = { anchor: layer.mask.anchor?.anchor ?? '', channel: this.maskChannel(layer), ...change };
    this.setMask(layer, { anchor });
  }

  protected readonly hexOf = hexOf;
  protected readonly colourName = colourName;
  protected readonly channelName = channelName;
  protected readonly blendName = blendName;

  /** The chosen layer's graph, which knows its own knobs. */
  private readonly shelf = signal<readonly MaterialInfo[]>([]);
  protected readonly knobs = computed(() => {
    const layer = this.store.chosen();
    // A filter from the vocabulary has its fields already; only a project's own compound has knobs on the shelf.
    const filter = layer?.kind === 'filter' && !this.filters().has(layer.filterNode) ? layer.filterNode : '';
    const graph = (layer?.kind === 'filter' ? filter : layer?.fill === 'Graph' ? layer.graph : '')
      .split('/')
      .pop();
    if (!graph) {
      return [];
    }

    return (
      this.shelf().find(material => material.kind === 'graph' && stemOf(material.source) === graph)?.parameters ?? []
    );
  });

  private async loadShelf(project: string): Promise<void> {
    const client = await this.library.api();
    const list = await client.GET('/api/v1/library/materials', { params: { query: { project } } });
    this.shelf.set(list.data?.items ?? []);
  }

  /** The shelf's graphs a fill can read, and the one it reads even if the shelf has lost it. */
  protected graphs(layer: StackLayer): readonly string[] {
    const named = this.fillGraphs().map(material => material.name);
    const held = this.graphOf(layer);
    return held && !named.includes(held) ? [...named, held] : named;
  }

  protected graphOf(layer: StackLayer): string | null {
    const stem = layer.graph.split('/').pop();
    return stem ? (this.fillGraphs().find(material => stemOf(material.source) === stem)?.name ?? stem) : null;
  }

  protected chooseGraph(layer: StackLayer, name: string | null): void {
    const material = this.fillGraphs().find(held => held.name === name);
    if (material) {
      void this.using.fillWith(layer, material);
    }
  }

  private readonly fillGraphs = computed(() =>
    this.shelf().filter(
      material =>
        (material.kind === 'graph' || material.kind === 'image-material') &&
        material.role !== 'mask' &&
        material.role !== 'filter' &&
        material.role !== 'brush'
    )
  );

  protected setSeed(seed: number | null): void {
    if (seed !== null && Number.isInteger(seed) && seed >= 0 && seed !== this.store.state()?.seed) {
      void this.store.edit({ op: 'setSeed', seed });
    }
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

  /** The steps a mask can run, under the headings the node menu uses. */
  protected readonly effectGroups = computed(() => {
    const groups = new Map<string, StackFilterKind[]>();
    for (const effect of this.maskEffects()) {
      groups.set(effect.category, [...(groups.get(effect.category) ?? []), effect]);
    }

    return [...groups].map(([category, effects]) => ({ category, effects }));
  });

  protected generatorsWith(chosen: string): readonly string[] {
    const named = this.generators();
    return chosen && !named.includes(chosen) ? [...named, chosen] : named;
  }

  protected effectName(node: string): string {
    return this.maskEffects().find(effect => effect.node === node)?.name ?? node.split('/').pop() ?? node;
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

  protected setEffectValue(layer: StackLayer, index: number, port: string, value: readonly number[]): void {
    const values = { ...layer.mask.effects?.[index]?.values, [port]: [...value] };
    this.setEffect(layer, index, { values });
  }

  protected setEffectText(layer: StackLayer, index: number, setting: string, value: string): void {
    const texts = { ...layer.mask.effects?.[index]?.texts, [setting]: value };
    this.setEffect(layer, index, { texts });
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

  protected readonly percent = percent;
  protected readonly amount = amount;

  protected setMask(layer: StackLayer, change: Partial<StackMask>): void {
    void this.store.setMask(layer, { ...layer.mask, ...change });
  }

  /** A source's name in this set, or its id when it is in another, which this client has not read. */
  protected sourceName(source: StackInstanceRef): string {
    return source.set ? source.layer : this.layerName(source.layer);
  }

  /** The instances of a layer in this set, or null when there are none. */
  protected instancesOf(layer: StackLayer): readonly StackLayer[] | null {
    const found = this.store
      .layers()
      .map(row => row.layer)
      .filter(held => held.instance && !held.instance.set && held.instance.layer === layer.id);
    return found.length > 0 ? found : null;
  }

  /** Chooses the source: here, or in its texture set, which opens and then chooses it. */
  protected goToSource(source: StackInstanceRef): void {
    if (!source.set) {
      this.store.layer.set(source.layer);
      this.store.masking.set(false);
      return;
    }

    const set = this.project.textureSets().find(held => held.material === source.set);
    const project = this.project.current();
    if (set && project) {
      this.store.arriving.set(source.layer);
      void this.project.openDocument(project.path, set.path);
    }
  }

  /** The entry of the chosen layer's mask stack in front of the brush, with its place counted from the bottom. */
  protected readonly maskEntry = computed(() => {
    const index = this.store.maskLayer();
    const entry = index === null || !this.store.masking() ? undefined : this.store.chosen()?.mask.layers?.[index];
    return entry && index !== null ? { index, layer: entry } : null;
  });

  /** What an entry of a mask's stack can be: any mask source but none. */
  protected readonly entrySources = computed(() => this.masks().filter(source => source !== 'None'));

  protected setEntry(
    layer: StackLayer,
    index: number,
    change: Parameters<PaintStore['setMaskLayer']>[2]
  ): void {
    void this.store.setMaskLayer(layer, index, change);
  }

  protected async chooseEntryPicture(layer: StackLayer, index: number): Promise<void> {
    const picture = await this.project.pickPicture();
    if (picture) {
      this.setEntry(layer, index, { asset: picture });
    }
  }
}
