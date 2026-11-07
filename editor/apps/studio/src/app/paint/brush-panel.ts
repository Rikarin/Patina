import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import { matAutoFixOff, matBrush, matColorize, matHorizontalRule, matPolyline } from '@ng-icons/material-icons/baseline';
import { XuiButtonImports } from '@xui/button';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { IconImports } from '../core/icons';
import { channelName } from '../core/labels';
import { type BrushSettings, type Mirror, PaintStore, type PaintTool } from './paint-store';

const Tools: readonly { id: PaintTool; label: string; icon: string }[] = [
  { id: 'brush', label: 'Brush (B)', icon: 'matBrush' },
  { id: 'eraser', label: 'Eraser (E)', icon: 'matAutoFixOff' },
  { id: 'picker', label: 'Picker (C)', icon: 'matColorize' },
  { id: 'line', label: 'Line (L)', icon: 'matHorizontalRule' },
  { id: 'path', label: 'Path (P)', icon: 'matPolyline' }
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

/**
 * The sliders of a brush, which all run from nothing to all of it but the angle's and the lazy mouse's. Spacing is a
 * share of the radius, so stamps touch at 200%, and past that it is a pattern. Nothing is never a spacing: stamps have
 * to move. The lazy mouse is how far the brush trails the pointer, in the units of the size; Shift-click draws a line on
 * from where the last stroke ended, and Cmd or Ctrl snaps it to 5°.
 */
const Sliders: readonly { key: NumberSetting; label: string; max: number; min?: number }[] = [
  { key: 'flow', label: 'Flow', max: 1 },
  { key: 'opacity', label: 'Opacity', max: 1 },
  { key: 'spacing', label: 'Spacing', max: 4, min: 0.01 },
  { key: 'falloff', label: 'Softness', max: 1 },
  { key: 'angle', label: 'Angle', max: 6.28 },
  { key: 'positionJitter', label: 'Scatter', max: 1 },
  { key: 'angleJitter', label: 'Rotation jitter', max: 3.14 },
  { key: 'sizeJitter', label: 'Size jitter', max: 1 },
  { key: 'pressureSize', label: 'Pressure to size', max: 1 },
  { key: 'pressureFlow', label: 'Pressure to flow', max: 1 },
  { key: 'lazy', label: 'Lazy mouse', max: 100 }
];

/** The brush: everything a stroke carries. A setting is read when a stroke starts, and nothing is sent until then. */
@Component({
  selector: 'ptn-brush-panel',
  viewProviders: [provideIcons({ matAutoFixOff, matBrush, matColorize, matHorizontalRule, matPolyline })],
  imports: [IconImports, XuiButtonImports, XuiColorPickerImports, XuiSelectImports, XuiSliderImports],
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
          [attr.aria-label]="tool.label"
          [title]="tool.label"
          [variant]="store.tool() === tool.id ? 'default' : 'ghost'"
          (click)="store.tool.set(tool.id)"
        >
          <ng-icon xui size="sm" [name]="tool.icon" />
        </button>
      }
    </div>
    <div class="flex items-center gap-2">
      <span class="text-foreground-muted w-16 shrink-0 text-xs" title="Mirrors strokes on the mesh, not on the flat map"
        >Mirror</span
      >
      <select
        class="well h-7 min-w-0 flex-1 px-1 text-sm"
        aria-label="Mirror"
        data-testid="paint-mirror"
        [disabled]="!store.state()?.model"
        (change)="store.mirror.set(asMirror(text($event)))"
      >
        @for (mirror of mirrors; track mirror.value) {
          <option [value]="mirror.value" [selected]="mirror.value === store.mirror()">{{ mirror.label }}</option>
        }
      </select>
    </div>
    <div class="flex items-center gap-2">
      <span class="text-foreground-muted w-16 shrink-0 text-xs">Size</span>
      <xui-slider
        class="min-w-0 flex-1"
        labelStepSize="0"
        aria-label="Brush size"
        data-testid="paint-size"
        [max]="256"
        [min]="1"
        [stepSize]="1"
        [value]="store.radius()"
        (valueChange)="store.radius.set($event)"
      />
      <span class="readout w-12 shrink-0 text-end text-xs">{{ store.radius() }} px</span>
    </div>
    @if (store.masking()) {
      <div class="flex items-center gap-2">
        <span class="text-foreground-muted w-16 shrink-0 text-xs" title="Black hides the layer and white shows it">Grey</span>
        <xui-slider
          class="min-w-0 flex-1"
          labelStepSize="0"
          aria-label="Mask grey"
          data-testid="paint-mask-grey"
          [max]="1"
          [min]="0"
          [stepSize]="0.01"
          [value]="store.maskGrey()"
          (valueChange)="store.maskGrey.set($event)"
        />
        <span class="readout w-12 shrink-0 text-end text-xs">{{ store.maskGrey().toFixed(2) }}</span>
      </div>
    } @else {
      <div class="flex items-center gap-2">
        <span class="text-foreground-muted w-16 shrink-0 text-xs">Colour</span>
        <xui-color-picker
          aria-label="Brush colour"
          data-testid="paint-colour"
          [presets]="store.recentColours()"
          [value]="store.colour()"
          (valueChange)="store.colour.set($event)"
        />
      </div>
    }
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
    <div class="flex flex-col gap-1" data-testid="paint-channels" [hidden]="store.masking()">
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
          <xui-color-picker
            [attr.aria-label]="nameOf(channel.usage) + ' colour'"
            [value]="store.alsoPaints().get(channel.usage) ?? '#808080'"
            (valueChange)="setChannel(channel.usage, $event)"
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
          [min]="slider.min ?? 0"
          [stepSize]="slider.min ?? slider.max / 100"
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
    if (key === 'lazy') {
      return `${Math.round(value)} px`;
    }

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

}
