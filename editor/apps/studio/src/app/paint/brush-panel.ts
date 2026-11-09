import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import type { StackSymmetry, StackTerm } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { Commands } from '../core/commands';
import { IconImports } from '../core/icons';
import { channelName, termName } from '../core/labels';
import { degrees, percent, toDegrees, toRadians } from '../core/numbers';
import { ProjectStore } from '../library/project-store';
import { BrushPresets } from './brush-presets';
import { MaximumRadius } from './paint-commands';
import { type BrushSettings, type FillMode, PaintStore } from './paint-store';
import { FillModes, PaintToolIcons, PaintTools } from './paint-tools';

type NumberSetting = {
  [K in keyof BrushSettings]-?: NonNullable<BrushSettings[K]> extends number ? K : never;
}[keyof BrushSettings];

type ChoiceSetting = 'rotation' | 'curve' | 'scatterAxis' | 'sizeSpace';

/** How a slider's number is shown: a share as %, an angle in °, a stretch as ×, a distance in the size's units. */
type Shown = 'share' | 'angle' | 'stretch' | 'size';

/**
 * The sliders of a brush. Spacing is a share of the radius, so stamps touch at 200%, and past that it is a pattern.
 * Nothing is never a spacing: stamps have to move. The lazy mouse is how far the brush trails the pointer, in the units
 * of the size; Shift-click draws a line on from where the last stroke ended, and Cmd or Ctrl snaps it to 5°. Stretch
 * makes a stamp longer than wide and keeps its area, so a longer one is narrower.
 */
const Sliders: readonly { key: NumberSetting; label: string; max: number; min?: number; shown: Shown }[] = [
  { key: 'flow', label: 'Flow', max: 1, shown: 'share' },
  { key: 'opacity', label: 'Opacity', max: 1, shown: 'share' },
  { key: 'spacing', label: 'Spacing', max: 4, min: 0.01, shown: 'share' },
  { key: 'falloff', label: 'Softness', max: 1, shown: 'share' },
  // An angle slides in whole degrees, and the brush keeps radians.
  { key: 'angle', label: 'Angle', max: 360, shown: 'angle' },
  { key: 'aspect', label: 'Stretch', max: 16, min: 1, shown: 'stretch' },
  { key: 'aspectAngle', label: 'Stretch angle', max: 180, shown: 'angle' },
  { key: 'positionJitter', label: 'Scatter', max: 1, shown: 'share' },
  { key: 'angleJitter', label: 'Rotation jitter', max: 180, shown: 'angle' },
  { key: 'sizeJitter', label: 'Size jitter', max: 1, shown: 'share' },
  { key: 'flowJitter', label: 'Flow jitter', max: 1, shown: 'share' },
  { key: 'pressureSize', label: 'Pressure to size', max: 1, shown: 'share' },
  { key: 'pressureFlow', label: 'Pressure to flow', max: 1, shown: 'share' },
  { key: 'lazy', label: 'Lazy mouse', max: 100, shown: 'size' }
];

/** The size slider's positions, which run on a log scale so a small brush is as easy to set as a big one. */
const SizeSteps = 1000;

/** The brush: everything a stroke carries. A setting is read when a stroke starts, and nothing is sent until then. */
@Component({
  selector: 'ptn-brush-panel',
  viewProviders: [provideIcons(PaintToolIcons)],
  imports: [BrushPresets, IconImports, XuiButtonImports, XuiColorPickerImports, XuiSelectImports, XuiSliderImports],
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
          [attr.aria-label]="commands.spec(tool.command)?.label"
          [title]="commands.tooltip(tool.command)"
          [variant]="store.tool() === tool.id ? 'default' : 'ghost'"
          (click)="store.tool.set(tool.id)"
        >
          <ng-icon xui size="sm" [name]="tool.icon" />
        </button>
      }
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="ms-auto"
        data-testid="paint-reset-brush"
        title="Every setting of the brush back to a new brush's"
        (click)="store.resetBrush()"
      >
        Reset to Default
      </button>
    </div>
    <ptn-brush-presets />
    @switch (store.tool()) {
      @case ('fill') {
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs" title="How much of the mesh a click fills"
            >Fills</span
          >
          <select
            class="well h-7 min-w-0 flex-1 px-1 text-sm"
            aria-label="Fill mode"
            data-testid="paint-fill-mode"
            (change)="store.fillMode.set(fillMode($event))"
          >
            @for (term of terms().fills; track term.value) {
              <option [value]="term.value" [selected]="store.fillMode() === term.value">{{ term.label }}</option>
            }
          </select>
        </div>
      }
      @case ('clone') {
        <div class="flex flex-wrap items-center gap-2" data-testid="paint-clone">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Source</span>
          <span class="readout min-w-0 flex-1 truncate text-xs" data-testid="paint-clone-source">{{
            cloneWhere()
          }}</span>
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="paint-clone-set"
            [variant]="store.settingCloneSource() ? 'default' : 'ghost'"
            [title]="commands.tooltip('setCloneSource')"
            (click)="commands.run('setCloneSource')"
          >
            Set
          </button>
          <label
            class="flex w-full items-center gap-2 text-xs"
            title="Keeps the step from the source to where the first stroke after it started. Off, every stroke starts at the source."
          >
            <input
              type="checkbox"
              data-testid="paint-clone-relative"
              [checked]="store.cloneRelative()"
              (change)="store.cloneRelative.set(checked($event))"
            />
            Relative
          </label>
        </div>
      }
    }
    @if ((store.tool() === 'brush' || store.tool() === 'fill') && !store.masking()) {
      <div class="flex items-center gap-2" data-testid="paint-material">
        <span
          class="text-foreground-muted w-16 shrink-0 text-xs"
          title="A material paints every channel of the layer from its own maps. Alt-click one on the shelf to paint with it."
          >Material</span
        >
        @if (store.material(); as material) {
          <span class="readout min-w-0 flex-1 truncate text-xs" data-testid="paint-material-name">{{
            material.name
          }}</span>
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            data-testid="paint-material-clear"
            title="Paints the colours again"
            (click)="store.material.set(null)"
          >
            Clear
          </button>
        } @else {
          <span class="text-foreground-muted min-w-0 flex-1 truncate text-xs"
            >Colours · Alt-click a shelf material</span
          >
        }
      </div>
    }
    <div class="flex items-center gap-2" data-testid="paint-screen-picture">
      <span
        class="text-foreground-muted w-16 shrink-0 text-xs"
        title="A picture fixed on the viewport: the projection tool paints it, and a stencil masks any brush by its brightness"
        >Picture</span
      >
      <span class="readout min-w-0 flex-1 truncate text-xs" data-testid="paint-screen-picture-name">{{
        pictureName()
      }}</span>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="paint-screen-picture-choose"
        (click)="choosePicture()"
      >
        Choose…
      </button>
    </div>
    @if (store.screenPicture(); as picture) {
      <div class="flex items-center gap-3 ps-[4.5rem]">
        <label class="flex items-center gap-1 text-xs" title="Repeats the picture past its edges">
          <input
            type="checkbox"
            data-testid="paint-screen-picture-tiled"
            [checked]="picture.tiled"
            (change)="store.screenPicture.set({ ...picture, tiled: checked($event) })"
          />
          Tiled
        </label>
        <label class="flex items-center gap-1 text-xs" [title]="commands.tooltip('toggleStencil')">
          <input
            type="checkbox"
            data-testid="paint-stencil"
            [checked]="store.stencil()"
            (change)="store.stencil.set(checked($event))"
          />
          Stencil
        </label>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="paint-screen-picture-clear"
          (click)="store.screenPicture.set(null); store.stencil.set(false)"
        >
          Clear
        </button>
      </div>
    }
    <div class="flex items-center gap-2">
      <span
        class="text-foreground-muted w-16 shrink-0 text-xs"
        title="Copies strokes on the mesh, in the 3D view and the 2D view alike"
        >Symmetry</span
      >
      <select
        class="well h-7 min-w-0 flex-1 px-1 text-sm"
        aria-label="Symmetry"
        data-testid="paint-symmetry"
        [disabled]="!store.state()?.model"
        (change)="setKind(text($event))"
      >
        <option value="" [selected]="!store.symmetry()">Off</option>
        @for (term of terms().symmetries; track term.value) {
          <option [value]="term.value" [selected]="store.symmetry()?.kind === term.value">{{ term.label }}</option>
        }
      </select>
    </div>
    @if (store.symmetry(); as symmetry) {
      <div class="flex items-center gap-2" data-testid="paint-symmetry-settings">
        <span class="text-foreground-muted w-16 shrink-0 text-xs">{{
          symmetry.kind === 'radial' ? 'Axis' : 'Plane'
        }}</span>
        <select
          class="well h-7 min-w-0 flex-1 px-1 text-sm"
          aria-label="Symmetry axis"
          data-testid="paint-symmetry-axis"
          (change)="setSymmetry({ axis: text($event) })"
        >
          @for (term of terms().axes; track term.value) {
            <option [value]="term.value" [selected]="symmetry.axis === term.value">{{ term.label }}</option>
          }
        </select>
        <select
          class="well h-7 min-w-0 flex-1 px-1 text-sm"
          aria-label="Symmetry centre"
          data-testid="paint-symmetry-centre"
          (change)="setSymmetry({ centre: text($event) })"
        >
          @for (term of terms().centres; track term.value) {
            <option [value]="term.value" [selected]="symmetry.centre === term.value">{{ term.label }}</option>
          }
        </select>
      </div>
      @if (symmetry.kind === 'radial') {
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs" title="How many strokes, this one among them"
            >Copies</span
          >
          <xui-slider
            class="min-w-0 flex-1"
            labelStepSize="0"
            aria-label="Radial copies"
            data-testid="paint-symmetry-count"
            [max]="terms().maximumCount"
            [min]="2"
            [stepSize]="1"
            [value]="symmetry.count"
            (valueChange)="setSymmetry({ count: $event })"
          />
          <span class="readout w-12 shrink-0 text-end text-xs">{{ symmetry.count }}</span>
        </div>
      } @else {
        <div class="flex items-center gap-2">
          <span
            class="text-foreground-muted w-16 shrink-0 text-xs"
            title="How far the plane is moved along its axis, as a share of the model's bounding radius"
            >Offset</span
          >
          <xui-slider
            class="min-w-0 flex-1"
            labelStepSize="0"
            aria-label="Plane offset"
            data-testid="paint-symmetry-offset"
            [max]="1"
            [min]="-1"
            [stepSize]="0.01"
            [value]="symmetry.offset"
            (valueChange)="setSymmetry({ offset: $event })"
          />
          <span class="readout w-12 shrink-0 text-end text-xs">{{ percent(symmetry.offset) }}</span>
        </div>
      }
    }
    <div class="flex items-center gap-2">
      <span class="text-foreground-muted w-16 shrink-0 text-xs" [title]="sizeMeans()">Size</span>
      <xui-slider
        class="min-w-0 flex-1"
        labelStepSize="0"
        aria-label="Brush size"
        aria-describedby="paint-size-means"
        data-testid="paint-size"
        [max]="sizeSteps"
        [min]="0"
        [stepSize]="1"
        [value]="sizePosition()"
        (valueChange)="setSize($event)"
      />
      <span class="readout w-16 shrink-0 text-end text-xs" data-testid="paint-size-readout" [title]="sizeMeans()">{{
        sizeShown(store.radius())
      }}</span>
      <span id="paint-size-means" hidden>{{ sizeMeans() }}</span>
    </div>
    <div class="flex items-center gap-2">
      <span class="text-foreground-muted w-16 shrink-0 text-xs">Size in</span>
      <select
        class="well h-7 min-w-0 flex-1 px-1 text-sm"
        aria-label="Size space"
        data-testid="paint-size-space"
        (change)="choose('sizeSpace', text($event))"
      >
        @for (term of terms().sizeSpaces; track term.value) {
          <option
            [value]="term.value"
            [disabled]="term.value === 'model' && !store.state()?.model"
            [selected]="term.value === store.brush().sizeSpace"
          >
            {{ term.label }}
          </option>
        }
      </select>
    </div>
    @if (store.masking()) {
      <div class="flex items-center gap-2">
        <span class="text-foreground-muted w-16 shrink-0 text-xs" title="Black hides the layer and white shows it"
          >Grey</span
        >
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
        <span class="readout w-12 shrink-0 text-end text-xs">{{ percent(store.maskGrey()) }}</span>
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
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="paint-keep-colour"
          title="Keeps this colour in the project's palette"
          (click)="store.keepColour(store.colour())"
        >
          Keep
        </button>
      </div>
      @if (store.palette().length > 0) {
        <div class="flex flex-wrap items-center gap-1" data-testid="paint-palette" aria-label="Palette">
          @for (colour of store.palette(); track colour) {
            <button
              type="button"
              class="size-5 rounded-sm border border-black/30"
              [attr.aria-label]="'Paint with ' + colour + '. Alt-click takes it out of the palette'"
              [title]="colour + ' (Alt-click takes it out)'"
              [style.background-color]="colour"
              (click)="pick(colour, $event)"
            ></button>
          }
        </div>
      }
    }
    <div class="flex items-center gap-1">
      <span class="text-foreground-muted w-16 shrink-0 text-xs">Alpha</span>
      <xui-select
        class="min-w-0 flex-1"
        aria-label="Brush alpha"
        data-testid="paint-alpha"
        [itemText]="termName"
        [items]="alphas()"
        [value]="store.brush().alpha ?? null"
        (valueChange)="set('alpha', $event)"
      />
    </div>
    @for (choice of choices; track choice.key) {
      <div class="flex items-center gap-1">
        <span class="text-foreground-muted w-16 shrink-0 text-xs">{{ choice.label }}</span>
        <select
          class="well h-7 min-w-0 flex-1 px-1 text-sm"
          [attr.aria-label]="'Brush ' + choice.label.toLowerCase()"
          [attr.data-testid]="'paint-' + choice.key"
          (change)="choose(choice.key, text($event))"
        >
          @for (term of terms()[choice.terms]; track term.value) {
            <option [value]="term.value" [selected]="term.value === store.brush()[choice.key]">{{ term.label }}</option>
          }
        </select>
      </div>
    }
    <label
      class="flex items-center gap-2 text-xs"
      title="Off, a place keeps the strongest stamp: a slow drag paints what a fast one does"
    >
      <input
        type="checkbox"
        data-testid="paint-accumulate"
        [checked]="store.brush().accumulate"
        (change)="set('accumulate', checked($event))"
      />
      Stamps build up where they overlap
    </label>
    <label
      class="flex items-center gap-2 text-xs"
      title="On the mesh, paints only what the camera sees: off, a stroke also reaches what lies close behind the surface under it"
    >
      <input
        type="checkbox"
        data-testid="paint-occlusion"
        [checked]="store.brush().occlusion"
        (change)="set('occlusion', checked($event))"
      />
      Only what the camera sees
    </label>
    @if (store.brush().occlusion) {
      <div class="flex items-center gap-2">
        <span
          class="text-foreground-muted w-24 shrink-0 truncate text-xs"
          title="How close in front of the surface something may lie and still not hide it, as a share of the model's size"
          >Tolerance</span
        >
        <xui-slider
          class="min-w-0 flex-1"
          labelStepSize="0"
          aria-label="Occlusion tolerance"
          data-testid="paint-occlusion-tolerance"
          [max]="50"
          [min]="0"
          [stepSize]="1"
          [value]="store.brush().occlusionTolerance"
          (valueChange)="set('occlusionTolerance', $event)"
        />
        <span class="readout w-12 shrink-0 text-end text-xs"
          >{{ (store.brush().occlusionTolerance / 10).toFixed(1) }}%</span
        >
      </div>
    }
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
          [stepSize]="slider.shown === 'angle' ? 1 : (slider.min ?? slider.max / 100)"
          [value]="slider.shown === 'angle' ? inDegrees(store.brush()[slider.key]) : store.brush()[slider.key]"
          (valueChange)="set(slider.key, slider.shown === 'angle' ? toRadians($event) : $event)"
        />
        <span class="readout w-12 shrink-0 text-end text-xs">{{ shown(slider.key, slider.shown) }}</span>
      </div>
    }
  `
})
export class BrushPanel {
  protected readonly store = inject(PaintStore);
  protected readonly commands = inject(Commands);
  protected readonly tools = PaintTools;
  protected readonly sliders = Sliders;
  protected readonly sizeSteps = SizeSteps;
  protected readonly choices: readonly {
    key: ChoiceSetting;
    label: string;
    terms: 'rotations' | 'falloffs' | 'scatterAxes';
  }[] = [
    { key: 'rotation', label: 'Rotation', terms: 'rotations' },
    { key: 'curve', label: 'Falloff', terms: 'falloffs' },
    { key: 'scatterAxis', label: 'Scatter', terms: 'scatterAxes' }
  ];
  private readonly project = inject(ProjectStore);
  protected readonly nameOf = channelName;

  /** Where the clone reads from, as a place on the map, or what to do about having none. */
  protected readonly cloneWhere = computed(() => {
    const source = this.store.cloneSource();
    return source ? `${percent(source.u)} across, ${percent(source.v)} down` : 'None yet';
  });

  /** The picture on the viewport by its file's name. */
  protected readonly pictureName = computed(() => this.store.screenPicture()?.image.split(/[/\\]/u).pop() ?? 'None');

  /** Asks for a picture, brings it into the project, and puts it on the viewport, unplaced until it is drawn. */
  protected async choosePicture(): Promise<void> {
    const image = await this.project.pickPicture();
    if (image) {
      this.store.screenPicture.set({ image, centre: [0, 0], scale: 0, angle: 0, tiled: false });
      if (this.store.tool() !== 'projection' && !this.store.stencil()) {
        this.store.tool.set('projection');
      }
    }
  }

  protected fillMode(event: Event): FillMode {
    const value = (event.target as HTMLSelectElement).value;
    return FillModes.find(mode => mode === value) ?? 'polygon';
  }
  protected readonly termName = termName;
  protected readonly percent = percent;
  protected readonly toRadians = toRadians;

  /** The words a brush takes, with the labels the Server gives them, or the values alone until it answers. */
  protected readonly terms = computed(() => {
    const brush = this.store.vocabulary()?.brush;
    const plain = (values: readonly string[]): StackTerm[] => values.map(value => ({ value, label: value }));
    return {
      rotations: brush?.rotations ?? plain(['fixed', 'along', 'random']),
      falloffs: brush?.falloffs ?? plain(['smooth', 'linear', 'spherical', 'tip']),
      scatterAxes: brush?.scatterAxes ?? plain(['both', 'across']),
      sizeSpaces: brush?.sizeSpaces ?? plain(['screen', 'texture', 'model']),
      symmetries: brush?.symmetries ?? plain(['mirror', 'radial']),
      axes: brush?.axes ?? plain(['x', 'y', 'z']),
      centres: brush?.centres ?? plain(['model', 'origin']),
      fills: brush?.fills ?? plain([...FillModes]),
      maximumCount: brush?.maximumCount ?? 64
    };
  });

  protected readonly others = computed(() =>
    (this.store.state()?.channels ?? []).filter(channel => channel.usage !== this.store.channel())
  );
  protected readonly addable = computed(() => {
    const held = this.store.state()?.channels.map(channel => channel.usage) ?? [];
    return (this.store.vocabulary()?.usages ?? []).map(term => term.value).filter(usage => !held.includes(usage));
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

  /** A palette colour paints, or with Alt held leaves the palette. */
  protected pick(colour: string, event: MouseEvent): void {
    if (event.altKey) {
      this.store.dropColour(colour);
    } else {
      this.store.colour.set(colour);
    }
  }

  /** What a slider stands at: angles in degrees, a stretch as a multiple, the lazy mouse in the size's units. */
  protected shown(key: NumberSetting, shown: Shown): string {
    const value = this.store.brush()[key];
    switch (shown) {
      case 'angle':
        return degrees(value);
      case 'stretch':
        return `${value.toFixed(1)}×`;
      case 'size':
        return this.sizeShown(value);
      default:
        return percent(value);
    }
  }

  /** A size in its space's own words: pixels of the screen, texels of the map, or a share of the model. */
  protected sizeShown(value: number): string {
    switch (this.store.brush().sizeSpace) {
      case 'texture':
        return `${Math.round(value)} tx`;
      case 'model':
        return `${(value / 10).toFixed(1)}%`;
      default:
        return `${Math.round(value)} px`;
    }
  }

  /** What a size is, which depends on what it is measured in. */
  protected readonly sizeMeans = computed(() => {
    switch (this.store.brush().sizeSpace) {
      case 'texture':
        return 'The radius of the brush in texels of the map, the same at any zoom, measured where a stroke starts.';
      case 'model':
        return 'The radius of the brush as a share of the model’s size, the same at any zoom and on any part of it.';
      default:
        return 'The radius of the brush in pixels of the screen, the same under the pointer at any zoom.';
    }
  });

  protected readonly sizePosition = computed(() =>
    Math.round((Math.log(Math.max(1, this.store.radius())) / Math.log(MaximumRadius)) * SizeSteps)
  );

  protected setSize(position: number): void {
    this.store.radius.set(Math.max(1, Math.round(Math.exp((position / SizeSteps) * Math.log(MaximumRadius)))));
  }

  protected inDegrees(radians: number): number {
    return Math.round(toDegrees(radians));
  }

  protected readonly alphas = computed(() => {
    const named = this.store.vocabulary()?.alphas.map(term => term.value) ?? ['Round'];
    const alpha = this.store.brush().alpha;
    return alpha && !named.includes(alpha) ? [...named, alpha] : named;
  });

  protected set<K extends keyof BrushSettings>(key: K, value: BrushSettings[K]): void {
    this.store.brush.update(brush => ({ ...brush, [key]: value }));
  }

  protected choose(key: ChoiceSetting, value: string): void {
    this.set(key, value);
  }

  /** Turns symmetry on as a kind, keeping the rest of what it was, or off. */
  protected setKind(kind: string): void {
    const held = this.store.symmetry();
    this.store.symmetry.set(
      kind
        ? {
            kind,
            axis: held?.axis ?? 'x',
            centre: held?.centre ?? 'model',
            offset: held?.offset ?? 0,
            count: held?.count ?? 4
          }
        : null
    );
  }

  protected setSymmetry(change: Partial<StackSymmetry>): void {
    this.store.symmetry.update(held => (held ? { ...held, ...change } : held));
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }
}
