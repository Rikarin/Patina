import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { type BrushSettings, type Mirror, PaintStore, type PaintTool } from './paint-store';

const Tools: readonly { id: PaintTool; label: string }[] = [
  { id: 'brush', label: 'Brush' },
  { id: 'line', label: 'Line' },
  { id: 'path', label: 'Path' }
];
const Mirrors: readonly string[] = ['none', 'x', 'y', 'z'];
const Rotations: readonly string[] = ['fixed', 'along', 'random'];

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
  { key: 'angleJitter', label: 'Turn jitter', max: 3.14 },
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
      <xui-select
        class="min-w-0 flex-1"
        aria-label="Mirror"
        data-testid="paint-mirror"
        [items]="mirrors"
        [value]="store.mirror()"
        (valueChange)="store.mirror.set(asMirror($event))"
      />
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
      <span class="text-foreground-muted w-16 shrink-0 text-xs">Turn</span>
      <xui-select
        class="min-w-0 flex-1"
        aria-label="Brush rotation"
        data-testid="paint-rotation"
        [items]="rotations"
        [value]="store.brush().rotation"
        (valueChange)="set('rotation', $event ?? 'fixed')"
      />
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
  protected readonly alphas = computed(() => this.store.vocabulary()?.alphas ?? ['Round']);

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
