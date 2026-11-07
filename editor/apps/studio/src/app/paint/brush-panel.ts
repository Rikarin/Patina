import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { channelName } from '../core/labels';
import { type BrushSettings, type Mirror, PaintStore, type PaintTool } from './paint-store';

const Tools: readonly { id: PaintTool; label: string }[] = [
  { id: 'brush', label: 'Brush' },
  { id: 'line', label: 'Line' },
  { id: 'path', label: 'Path' }
];
const Mirrors: readonly { value: string; label: string }[] = [
  { value: 'none', label: 'Off' },
  { value: 'x', label: 'X' },
  { value: 'y', label: 'Y' },
  { value: 'z', label: 'Z' }
];
const Rotations: readonly { value: string; label: string }[] = [
  { value: 'fixed', label: 'Fixed' },
  { value: 'along', label: 'Follow stroke' },
  { value: 'random', label: 'Random' }
];

type NumberSetting = {
  [K in keyof BrushSettings]-?: NonNullable<BrushSettings[K]> extends number ? K : never;
}[keyof BrushSettings];

/** The sliders of a brush, which all run from nothing to all of it but the angle's. */
const Sliders: readonly { key: NumberSetting; label: string; max: number }[] = [
  { key: 'flow', label: 'Flow', max: 1 },
  { key: 'opacity', label: 'Opacity', max: 1 },
  { key: 'spacing', label: 'Spacing', max: 2 },
  { key: 'falloff', label: 'Softness', max: 1 },
  { key: 'angle', label: 'Angle', max: 6.28 },
  { key: 'positionJitter', label: 'Scatter', max: 1 },
  { key: 'angleJitter', label: 'Rotation jitter', max: 3.14 },
  { key: 'sizeJitter', label: 'Size jitter', max: 1 },
  { key: 'pressureSize', label: 'Pressure to size', max: 1 },
  { key: 'pressureFlow', label: 'Pressure to flow', max: 1 }
];

/** The brush: everything a stroke carries. A setting is read when a stroke starts, and nothing is sent until then. */
@Component({
  selector: 'ptn-brush-panel',
  imports: [XuiButtonImports, XuiSelectImports, XuiSliderImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5' },
  template: `
    <div class="flex flex-wrap items-center gap-1">
      @for (tool of tools; track tool.id) {
        <button
          type="button"
          xuiButton
          size="sm"
          [attr.data-testid]="'paint-tool-' + tool.id"
          [variant]="store.tool() === tool.id ? 'default' : 'ghost'"
          (click)="store.tool.set(tool.id)"
        >
          {{ tool.label }}
        </button>
      }
    </div>
    <div class="flex items-center gap-2">
      <span class="text-foreground-muted w-16 shrink-0 text-xs">Mirror</span>
      <select
        class="well h-7 min-w-0 flex-1 px-1 text-sm"
        aria-label="Mirror"
        data-testid="paint-mirror"
        (change)="store.mirror.set(asMirror(text($event)))"
      >
        @for (mirror of mirrors; track mirror.value) {
          <option [value]="mirror.value" [selected]="mirror.value === store.mirror()">{{ mirror.label }}</option>
        }
      </select>
    </div>
    <div class="flex items-center gap-2">
      <span class="text-foreground-muted w-16 shrink-0 text-xs">Size</span>
      <input
        class="min-w-0 flex-1"
        type="range"
        min="1"
        max="256"
        aria-label="Brush size"
        data-testid="paint-size"
        [value]="store.radius()"
        (input)="store.radius.set(number($event))"
      />
      <input
        type="color"
        aria-label="Brush colour"
        data-testid="paint-colour"
        [value]="store.colour()"
        (input)="store.colour.set(text($event))"
      />
    </div>
    <div class="flex items-center gap-1">
      <span class="text-foreground-muted w-16 shrink-0 text-xs">Alpha</span>
      <xui-select
        class="min-w-0 flex-1"
        aria-label="Brush alpha"
        data-testid="paint-alpha"
        [items]="alphas()"
        [value]="store.brush().alpha"
        (valueChange)="set('alpha', $event)"
      />
    </div>
    <div class="flex items-center gap-1">
      <span class="text-foreground-muted w-16 shrink-0 text-xs">Rotation</span>
      <select
        class="well h-7 min-w-0 flex-1 px-1 text-sm"
        aria-label="Brush rotation"
        data-testid="paint-rotation"
        (change)="set('rotation', text($event))"
      >
        @for (rotation of rotations; track rotation.value) {
          <option [value]="rotation.value" [selected]="rotation.value === store.brush().rotation">
            {{ rotation.label }}
          </option>
        }
      </select>
    </div>
    <div class="flex flex-col gap-1" data-testid="paint-channels">
      <span class="section-label">Also paints</span>
      @for (channel of others(); track channel.usage) {
        <div class="flex items-center gap-2">
          <input
            type="checkbox"
            [attr.aria-label]="'Paint ' + nameOf(channel.usage)"
            [attr.data-testid]="'paint-also-' + channel.usage"
            [checked]="store.alsoPaints().has(channel.usage)"
            (change)="toggleChannel(channel.usage, $event)"
          />
          <span class="min-w-0 flex-1 truncate text-xs">{{ nameOf(channel.usage) }}</span>
          <input
            type="color"
            [attr.aria-label]="nameOf(channel.usage) + ' colour'"
            [value]="store.alsoPaints().get(channel.usage) ?? '#808080'"
            (input)="setChannel(channel.usage, text($event))"
          />
        </div>
      }
      @if (addable().length > 0) {
        <select
          class="well h-7 min-w-0 px-1 text-sm"
          aria-label="Add a channel to this stack"
          data-testid="paint-add-channel"
          (change)="addChannel($event)"
        >
          <option value="">Add a channel…</option>
          @for (usage of addable(); track usage) {
            <option [value]="usage">{{ nameOf(usage) }}</option>
          }
        </select>
      }
    </div>
    @for (slider of sliders; track slider.key) {
      <div class="flex items-center gap-2">
        <span class="text-foreground-muted w-24 shrink-0 truncate text-xs">{{ slider.label }}</span>
        <xui-slider
          class="min-w-0 flex-1"
          labelStepSize="0"
          [attr.aria-label]="slider.label"
          [attr.data-testid]="'paint-' + slider.key"
          [max]="slider.max"
          [min]="0"
          [stepSize]="slider.max / 100"
          [value]="store.brush()[slider.key]"
          (valueChange)="set(slider.key, $event)"
        />
        <span class="readout w-10 shrink-0 text-end text-xs">{{ shown(slider.key) }}</span>
      </div>
    }
  `
})
export class BrushPanel {
  protected readonly store = inject(PaintStore);
  protected readonly tools = Tools;
  protected readonly mirrors = Mirrors;
  protected readonly rotations = Rotations;
  protected readonly sliders = Sliders;
  protected readonly nameOf = channelName;
  protected readonly others = computed(() =>
    (this.store.state()?.channels ?? []).filter(channel => channel.usage !== this.store.channel())
  );
  protected readonly addable = computed(() => {
    const held = this.store.state()?.channels.map(channel => channel.usage) ?? [];
    return (this.store.vocabulary()?.usages ?? []).filter(usage => !held.includes(usage));
  });

  protected toggleChannel(usage: string, event: Event): void {
    const next = new Map(this.store.alsoPaints());
    if ((event.target as HTMLInputElement).checked) {
      next.set(usage, next.get(usage) ?? '#808080');
    } else {
      next.delete(usage);
    }

    this.store.alsoPaints.set(next);
  }

  protected setChannel(usage: string, colour: string): void {
    this.store.alsoPaints.set(new Map(this.store.alsoPaints()).set(usage, colour));
  }

  protected addChannel(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const usage = select.value;
    select.value = '';
    if (usage) {
      void this.store.edit({ op: 'addChannel', usage });
    }
  }

  /** What a slider stands at: angles in degrees and the rest as a percentage of what it holds. */
  protected shown(key: NumberSetting): string {
    const value = this.store.brush()[key];
    return key === 'angle' || key === 'angleJitter'
      ? `${Math.round((value * 180) / Math.PI)}°`
      : `${Math.round(value * 100)}%`;
  }

  protected readonly alphas = computed(() => {
    const named = this.store.vocabulary()?.alphas ?? ['Round'];
    const alpha = this.store.brush().alpha;
    return alpha && !named.includes(alpha) ? [...named, alpha] : named;
  });

  protected set<K extends keyof BrushSettings>(key: K, value: BrushSettings[K]): void {
    this.store.brush.update(brush => ({ ...brush, [key]: value }));
  }

  protected asMirror(value: string | null): Mirror {
    return (value ?? 'none') as Mirror;
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected number(event: Event): number {
    return Number((event.target as HTMLInputElement).value);
  }
}
