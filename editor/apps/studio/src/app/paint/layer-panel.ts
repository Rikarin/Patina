import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import type { StackLayer, StackMask } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { colourOf } from './paint-store';
import { LiveValue } from './live-value';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from './paint-store';

const Fills: readonly string[] = ['Constant', 'Texture', 'Graph'];
const Projections: readonly { value: string; label: string }[] = [
  { value: 'Uv', label: 'UV' },
  { value: 'Triplanar', label: 'Tri-planar' },
  { value: 'Planar', label: 'Planar' }
];
const Axes: readonly string[] = ['X', 'Y', 'Z'];

/** Four numbers as a swatch's value. */
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
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Opacity</span>
          <xui-slider
            class="min-w-0 flex-1"
            labelStepSize="0"
            aria-label="Layer opacity"
            data-testid="paint-layer-opacity"
            [max]="1"
            [min]="0"
            [stepSize]="0.01"
            [value]="opacity.shown()"
            (valueChange)="setOpacity(layer, $event)"
            (pointerup)="opacity.release()"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Blend</span>
          <xui-select
            class="min-w-0 flex-1"
            aria-label="Blend mode"
            data-testid="paint-layer-blend"
            [items]="blends()"
            [value]="layer.blend"
            (valueChange)="store.edit({ op: 'setLayer', layer: layer.id, blend: $event })"
          />
        </div>
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
              {{ channel.usage }}
            </button>
          }
        </div>

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
                    <option [value]="option.value" [selected]="option.value === layer.projection">{{ option.label }}</option>
                  }
                </select>
              </div>
              @if (layer.projection === 'Planar') {
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
              @if (layer.projection !== 'Uv') {
                <div class="flex items-center gap-2">
                  <span class="text-foreground-muted w-16 shrink-0 text-xs">Scale</span>
                  <xui-numeric-input
                    class="min-w-0 flex-1"
                    size="sm"
                    ariaLabel="Projection scale"
                    data-testid="paint-fill-scale"
                    [stepSize]="0.1"
                    [value]="layer.settings['Scale']?.[0] ?? 1"
                    (valueChange)="setSetting(layer, 'Scale', $event ?? 1)"
                  />
                </div>
              }
              @if (layer.projection === 'Triplanar') {
                <div class="flex items-center gap-2">
                  <span class="text-foreground-muted w-16 shrink-0 text-xs">Blending</span>
                  <xui-numeric-input
                    class="min-w-0 flex-1"
                    size="sm"
                    ariaLabel="Projection blending"
                    data-testid="paint-fill-sharpness"
                    [stepSize]="0.5"
                    [value]="layer.settings['Sharpness']?.[0] ?? 4"
                    (valueChange)="setSetting(layer, 'Sharpness', $event ?? 4)"
                  />
                </div>
              }
            }
            @for (channel of store.state()?.channels ?? []; track channel.usage) {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-20 shrink-0 truncate text-xs">{{ channel.usage }}</span>
                @switch (layer.fill) {
                  @case ('Constant') {
                    <input
                      type="color"
                      [attr.aria-label]="channel.usage + ' value'"
                      [attr.data-testid]="'paint-fill-' + channel.usage"
                      [value]="hex(layer, channel.usage, channel.default)"
                      (change)="setValue(layer, channel.usage, text($event))"
                    />
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
              <span class="min-w-0 flex-1 truncate text-xs" [title]="layer.mask.asset">{{ fileOf(layer.mask.asset) || 'None' }}</span>
              <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-mask-picture" (click)="chooseMaskPicture(layer)">
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
              [items]="generators()"
              [value]="layer.mask.generator"
              (valueChange)="setMask(layer, { generator: $event ?? '' })"
            />
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
                    <span class="border-hairline size-5 rounded-sm border" data-testid="paint-picked" [style.background]="hexOf(colour)"></span>
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

      <p class="text-foreground-muted text-xs" data-testid="paint-mesh-maps">
        {{ store.state()?.meshMaps ? 'Using the bake.' : 'No bake yet.' }}
      </p>
    }
  `
})
export class LayerPanel {
  protected readonly store = inject(PaintStore);
  private readonly project = inject(ProjectStore);
  protected readonly fills = Fills;
  protected readonly projections = Projections;
  protected readonly axes = Axes;
  protected readonly blends = computed(() => this.store.vocabulary()?.blends ?? []);
  protected readonly filters = computed(() => this.store.vocabulary()?.filters.map(filter => filter.kind) ?? []);
  protected readonly masks = computed(() => this.store.vocabulary()?.masks ?? []);
  protected readonly generators = computed(() => this.store.vocabulary()?.generators ?? []);
  protected readonly meshMaps = computed(() => this.store.vocabulary()?.meshMaps ?? []);
  protected readonly name = new LiveValue<string>(() => this.store.chosen()?.name ?? '');
  protected readonly opacity = new LiveValue<number>(() => this.store.chosen()?.opacity ?? 1);
  private readonly sending = signal(false);
  private latest: { layer: string; opacity: number } | null = null;

  constructor() {
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
        this.opacity.release();
      });
    });
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

  /** One request out at a time, the newest value next, so that a fast slider costs what the Server can take. */
  protected setOpacity(layer: StackLayer, opacity: number): void {
    this.opacity.edit(opacity);
    this.latest = { layer: layer.id, opacity };
    void this.sendOpacity();
  }

  private async sendOpacity(): Promise<void> {
    if (this.sending()) {
      return;
    }

    this.sending.set(true);
    while (this.latest) {
      const { layer, opacity } = this.latest;
      this.latest = null;
      await this.store.edit({ op: 'setLayer', layer, opacity });
    }

    this.sending.set(false);
  }

  protected hex(layer: StackLayer, usage: string, fallback: readonly number[]): string {
    return hexOf(layer.values[usage] ?? fallback);
  }

  protected setValue(layer: StackLayer, usage: string, hex: string): void {
    void this.store.edit({ op: 'setLayer', layer: layer.id, values: { [usage]: colourOf(hex) } });
  }

  protected setTexture(layer: StackLayer, usage: string, path: string): void {
    void this.store.edit({ op: 'setLayer', layer: layer.id, textures: { [usage]: path } });
  }

  protected settings(layer: StackLayer): readonly string[] {
    return this.store.vocabulary()?.filters.find(filter => filter.kind === layer.filter)?.settings ?? [];
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
