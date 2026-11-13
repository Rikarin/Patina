import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import type { ViewportDisplay } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { type XuiSegmentedOption, XuiSegmentedControlImports } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { Commands } from '../core/commands';
import { labelsOf, valuesOf } from '../core/labels';
import { degrees, percent, stops, toRadians } from '../core/numbers';
import type { AxisView } from './orbit';
import { DefaultDisplay, Shapes, ViewportClient } from './viewport-client';

type NumberSetting = {
  [K in keyof ViewportDisplay]-?: ViewportDisplay[K] extends number ? K : never;
}[keyof ViewportDisplay];

interface Slider {
  key: NumberSetting;
  label: string;
  title: string;
  min: number;
  max: number;
  step: number;
  /** What the readout says: angles in degrees, exposure in stops, a grade either way of none, the rest as shares. */
  unit: 'degrees' | 'stops' | 'signed' | 'share';
}

const Lighting: readonly Slider[] = [
  { key: 'exposure', label: 'Exposure', title: 'How bright the picture is', min: -5, max: 5, step: 0.1, unit: 'stops' },
  {
    key: 'environmentRotation',
    label: 'Environment',
    title: 'Turns the environment around the model, and not the light',
    min: -Math.PI,
    max: Math.PI,
    step: toRadians(1),
    unit: 'degrees'
  },
  {
    key: 'lightRotation',
    label: 'Light',
    title: 'Turns the light around the model, and not the environment',
    min: -Math.PI,
    max: Math.PI,
    step: toRadians(1),
    unit: 'degrees'
  },
  {
    key: 'shadows',
    label: 'Shadows',
    title: 'How dark the model’s shadow is',
    min: 0,
    max: 1,
    step: 0.01,
    unit: 'share'
  },
  {
    key: 'backgroundOpacity',
    label: 'Background',
    title: 'How much of the environment shows behind the model',
    min: 0,
    max: 1,
    step: 0.01,
    unit: 'share'
  },
  {
    key: 'backgroundBlur',
    label: 'Blur',
    title: 'How blurred the background is. The light on the model is not blurred.',
    min: 0,
    max: 1,
    step: 0.01,
    unit: 'share'
  }
];

const Finish: readonly Slider[] = [
  {
    key: 'contrast',
    label: 'Contrast',
    title: 'Contrast about middle grey',
    min: 0,
    max: 2,
    step: 0.01,
    unit: 'share'
  },
  { key: 'saturation', label: 'Saturation', title: 'How much color', min: 0, max: 2, step: 0.01, unit: 'share' },
  { key: 'vignette', label: 'Vignette', title: 'Darkens the corners', min: 0, max: 1, step: 0.01, unit: 'share' },
  { key: 'grain', label: 'Grain', title: 'Film grain', min: 0, max: 1, step: 0.01, unit: 'share' },
  {
    key: 'aberration',
    label: 'Aberration',
    title: 'Parts red and blue towards the edges, as a lens does',
    min: 0,
    max: 1,
    step: 0.01,
    unit: 'share'
  },
  {
    key: 'sharpen',
    label: 'Sharpen',
    title: 'Contrast-adaptive sharpening',
    min: 0,
    max: 1,
    step: 0.01,
    unit: 'share'
  },
  {
    key: 'bloom',
    label: 'Bloom',
    title: 'Spills light past what is brighter than white, as a lens does',
    min: 0,
    max: 1,
    step: 0.01,
    unit: 'share'
  },
  {
    key: 'depthOfField',
    label: 'Depth of field',
    title: 'How shallow the focus is, as a lens opened wide on something small',
    min: 0,
    max: 1,
    step: 0.01,
    unit: 'share'
  },
  {
    key: 'focus',
    label: 'Focus',
    title: 'Where the lens focuses: nearer or further than the point the camera turns round',
    min: -1,
    max: 1,
    step: 0.01,
    unit: 'signed'
  },
  {
    key: 'warmth',
    label: 'Warmth',
    title: 'Takes blue towards red, or red towards blue',
    min: -1,
    max: 1,
    step: 0.01,
    unit: 'signed'
  },
  {
    key: 'tint',
    label: 'Tint',
    title: 'Takes green towards magenta, or magenta towards green',
    min: -1,
    max: 1,
    step: 0.01,
    unit: 'signed'
  }
];

/** The lanes the picture view shows one of, as a grey, or all of as its colour. */
const Lanes: readonly { value: string; label: string }[] = [
  { value: 'rgb', label: 'Color' },
  { value: 'r', label: 'Red' },
  { value: 'g', label: 'Green' },
  { value: 'b', label: 'Blue' },
  { value: 'a', label: 'Alpha' }
];

const Projections: readonly XuiSegmentedOption[] = [
  { value: 'perspective', label: 'Perspective' },
  { value: 'orthographic', label: 'Orthographic' }
];

export const ToneMappings: readonly { value: string; label: string }[] = [
  { value: 'aces', label: 'ACES' },
  { value: 'agx', label: 'AgX' },
  { value: 'filmic', label: 'Filmic (Hable)' },
  { value: 'reinhard', label: 'Reinhard' },
  { value: 'none', label: 'None (clip)' }
];

const Snaps: readonly { view: AxisView; label: string; key: string }[] = [
  { view: 'front', label: 'Front', key: 'Numpad 1' },
  { view: 'back', label: 'Back', key: 'Ctrl Numpad 1' },
  { view: 'right', label: 'Right', key: 'Numpad 3' },
  { view: 'left', label: 'Left', key: 'Ctrl Numpad 3' },
  { view: 'top', label: 'Top', key: 'Numpad 7' },
  { view: 'bottom', label: 'Bottom', key: 'Ctrl Numpad 7' }
];

/**
 * The viewport's display settings beside the picture: the camera's lens and the views it snaps to, the light and the
 * environment, and the finish. The Server holds every one of them, and the open project keeps them.
 */
@Component({
  selector: 'ptn-viewport-display',
  imports: [
    XuiButtonImports,
    XuiColorPickerImports,
    XuiNumericInputImports,
    XuiSegmentedControlImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'pane flex w-64 flex-col gap-2 overflow-y-auto p-2', 'data-testid': 'viewport-display' },
  template: `
    <div class="flex items-center gap-2" title="What a material or a graph with no mesh of its own is seen on">
      <span class="text-foreground-muted w-20 shrink-0 text-xs">Shape</span>
      <xui-select
        class="min-w-0 flex-1"
        aria-label="Shape"
        data-testid="viewport-shape"
        [filterable]="false"
        [itemText]="shapeName"
        [items]="shapes"
        [value]="display().shape"
        (valueChange)="$event && viewport.setDisplay({ shape: $event })"
      />
    </div>

    <span class="section-label mt-1">Camera</span>
    <xui-segmented-control
      size="sm"
      aria-label="Projection"
      data-testid="viewport-projection"
      [options]="projections"
      [value]="display().orthographic ? 'orthographic' : 'perspective'"
      (valueChange)="viewport.setDisplay({ orthographic: $event === 'orthographic' })"
    />
    <div class="flex items-center gap-2" title="How wide the lens is, top to bottom. Orthographic keeps the size.">
      <span class="text-foreground-muted w-20 shrink-0 text-xs">Field of view</span>
      <xui-slider
        class="min-w-0 flex-1"
        labelStepSize="0"
        aria-label="Field of view"
        data-testid="viewport-fov"
        [max]="2.6"
        [min]="0.1"
        [stepSize]="0.01"
        [value]="display().fieldOfView"
        (valueChange)="viewport.setDisplay({ fieldOfView: $event })"
      />
      <span class="readout w-16 shrink-0 text-end text-xs">{{ degrees(display().fieldOfView) }}</span>
    </div>
    <div class="grid grid-cols-3 gap-1">
      @for (axis of snaps; track axis.view) {
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          [attr.data-testid]="'viewport-snap-' + axis.view"
          [title]="'Looks at the model from the ' + axis.view + ' (' + axis.key + ')'"
          (click)="snap.emit(axis.view)"
        >
          {{ axis.label }}
        </button>
      }
    </div>

    <span class="section-label mt-1">Light</span>
    @for (slider of lighting; track slider.key) {
      <div class="flex items-center gap-2" [title]="slider.title">
        <span class="text-foreground-muted w-20 shrink-0 text-xs">{{ slider.label }}</span>
        <xui-slider
          class="min-w-0 flex-1"
          labelStepSize="0"
          [attr.aria-label]="slider.label"
          [attr.data-testid]="'viewport-' + slider.key"
          [max]="slider.max"
          [min]="slider.min"
          [stepSize]="slider.step"
          [value]="display()[slider.key]"
          (valueChange)="set(slider.key, $event)"
        />
        <span class="readout w-16 shrink-0 text-end text-xs">{{ shown(slider) }}</span>
      </div>
    }
    <div class="flex items-center gap-2" title="What shows where the environment does not, as Background takes it away">
      <span class="text-foreground-muted w-20 shrink-0 text-xs">Behind</span>
      <xui-color-picker
        aria-label="Background color"
        data-testid="viewport-background-colour"
        [value]="display().backgroundColor"
        (valueChange)="viewport.setDisplay({ backgroundColor: $event })"
      />
    </div>
    <div class="flex items-center gap-2" [title]="commands.tooltip('toggleGrid')">
      <span class="text-foreground-muted w-20 shrink-0 text-xs">Grid</span>
      <xui-switch
        aria-label="Grid"
        data-testid="viewport-grid"
        [checked]="display().grid"
        (checkedChange)="viewport.setDisplay({ grid: $event })"
      />
    </div>

    @if (display().view === 'picture' || viewport.node() !== null) {
      <span class="section-label mt-1">Picture</span>
      <div class="flex items-center gap-2" title="One lane of the picture as a grey, or all of it as its color">
        <span class="text-foreground-muted w-20 shrink-0 text-xs">Lane</span>
        <xui-select
          class="min-w-0 flex-1"
          aria-label="Picture lane"
          data-testid="viewport-picture-lane"
          [filterable]="false"
          [itemText]="laneName"
          [items]="lanes"
          [value]="display().pictureLane"
          (valueChange)="$event && viewport.setDisplay({ pictureLane: $event })"
        />
      </div>
      <div
        class="flex items-center gap-2"
        title="What the picture's numbers are multiplied by before they show, for a height or a position"
      >
        <span class="text-foreground-muted w-20 shrink-0 text-xs">Scale</span>
        <xui-numeric-input
          class="min-w-0 flex-1"
          size="sm"
          aria-label="Picture scale"
          data-testid="viewport-picture-scale"
          [max]="1000"
          [min]="0.001"
          [stepSize]="0.1"
          [value]="display().pictureScale"
          (valueChange)="scale($event)"
        />
      </div>
    }

    <span class="section-label mt-1">Finish</span>
    <div class="flex items-center gap-2" title="The curve that takes the light to the screen">
      <span class="text-foreground-muted w-20 shrink-0 text-xs">Tone map</span>
      <xui-select
        class="min-w-0 flex-1"
        aria-label="Tone mapping"
        data-testid="viewport-tonemap"
        [filterable]="false"
        [itemText]="curveName"
        [items]="curves"
        [value]="display().toneMapping"
        (valueChange)="$event && viewport.setDisplay({ toneMapping: $event })"
      />
    </div>
    @for (slider of finish; track slider.key) {
      <div class="flex items-center gap-2" [title]="slider.title">
        <span class="text-foreground-muted w-20 shrink-0 text-xs">{{ slider.label }}</span>
        <xui-slider
          class="min-w-0 flex-1"
          labelStepSize="0"
          [attr.aria-label]="slider.label"
          [attr.data-testid]="'viewport-' + slider.key"
          [max]="slider.max"
          [min]="slider.min"
          [stepSize]="slider.step"
          [value]="display()[slider.key]"
          (valueChange)="set(slider.key, $event)"
        />
        <span class="readout w-16 shrink-0 text-end text-xs">{{ shown(slider) }}</span>
      </div>
    }
    <div class="flex items-center gap-2" [title]="commands.tooltip('toggleWireframe')">
      <span class="text-foreground-muted w-20 shrink-0 text-xs">Wireframe</span>
      <xui-switch
        aria-label="Wireframe"
        data-testid="viewport-wireframe"
        [checked]="display().wireframe"
        (checkedChange)="viewport.setDisplay({ wireframe: $event })"
      />
    </div>
    <div class="mt-1 flex items-center gap-1">
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="viewport-display-reset"
        title="Puts every setting here back as the viewport opens, but the environment, look and view"
        (click)="reset()"
      >
        Reset
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="viewport-screenshot"
        [disabled]="!commands.enabled('saveScreenshot')"
        [title]="commands.tooltip('saveScreenshot')"
        (click)="commands.run('saveScreenshot')"
      >
        Screenshot…
      </button>
    </div>
  `
})
export class ViewportDisplayPanel {
  protected readonly viewport = inject(ViewportClient);
  protected readonly display = this.viewport.display;
  protected readonly commands = inject(Commands);
  protected readonly projections = Projections;
  protected readonly lighting = Lighting;
  protected readonly finish = Finish;
  protected readonly curves = valuesOf(ToneMappings);
  protected readonly curveName = labelsOf(ToneMappings);
  protected readonly snaps = Snaps;
  protected readonly lanes = valuesOf(Lanes);
  protected readonly laneName = labelsOf(Lanes);
  protected readonly shapes = valuesOf(Shapes);
  protected readonly shapeName = labelsOf(Shapes);
  /** A snap moves the camera, which is the pane's. */
  readonly snap = output<AxisView>();

  protected set(key: NumberSetting, value: number): void {
    this.viewport.setDisplay({ [key]: value });
  }

  /** A scale the Server takes. The field keeps itself within the same bounds. */
  protected scale(value: number | null): void {
    if (value !== null && Number.isFinite(value) && value >= 0.001 && value <= 1000) {
      this.viewport.setDisplay({ pictureScale: value });
    }
  }

  protected readonly degrees = degrees;

  protected shown(slider: Slider): string {
    const value = this.display()[slider.key];
    switch (slider.unit) {
      case 'degrees':
        return degrees(value);
      case 'stops':
        return stops(value);
      case 'signed':
        return `${value > 0 ? '+' : ''}${percent(value)}`;
      default:
        return percent(value);
    }
  }

  /** Back to how the viewport opens, but for what it is lit by, looked through, showing and on, which are choices. */
  protected reset(): void {
    const { environment, look = null, view, picture = null, pictureLane, pictureScale, shape } = this.display();
    this.viewport.setDisplay({ ...DefaultDisplay, environment, look, view, picture, pictureLane, pictureScale, shape });
  }
}
