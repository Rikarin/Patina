import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import type { StackSymmetry, StackTerm } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { hexColourName } from '../core/colour-names';
import { Commands } from '../core/commands';
import { IconImports } from '../core/icons';
import { channelName, labelIn, labelsOf, termName, valuesOf } from '../core/labels';
import { degrees, percent, times, toDegrees, toRadians } from '../core/numbers';
import { ProjectStore } from '../library/project-store';
import { LibraryThumbnails } from '../library/thumbnails';
import { BrushPresets } from './brush-presets';
import { maximumRadius } from './paint-commands';
import { type BrushSettings, type FillMode, type FillShape, PaintStore } from './paint-store';
import { FillModes, FillShapes, PaintToolIcons, PaintTools } from './paint-tools';

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
  { key: 'lazy', label: 'Lazy mouse', max: 100, shown: 'size' },
  // How far a face may turn from the camera and still take paint: 90° paints all it looks at.
  { key: 'facing', label: 'Face angle', max: 90, shown: 'angle' }
];

/** The size slider's positions, which run on a log scale so a small brush is as easy to set as a big one. */
const SizeSteps = 1000;

/** How long the brush rests before its picture is drawn, so a slider being dragged asks once at the end. */
const PreviewQuiet = 200;

/** The brush: everything a stroke carries. A setting is read when a stroke starts, and nothing is sent until then. */
@Component({
  selector: 'ptn-brush-panel',
  viewProviders: [provideIcons(PaintToolIcons)],
  imports: [
    BrushPresets,
    IconImports,
    XuiButtonImports,
    XuiColorPickerImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
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
    @if (preview(); as url) {
      <img
        class="well h-12 w-full object-contain"
        alt="A stroke of the brush"
        data-testid="paint-brush-preview"
        title="A stroke of this brush at a size of its own, pressed harder towards the middle"
        [src]="url"
      />
    }
    @switch (store.tool()) {
      @case ('fill') {
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs" title="How much of the mesh a click fills"
            >Fills</span
          >
          <xui-select
            class="min-w-0 flex-1"
            aria-label="Fill mode"
            data-testid="paint-fill-mode"
            [filterable]="false"
            [itemText]="labelsOf(terms().fills)"
            [items]="valuesOf(terms().fills)"
            [value]="store.fillMode()"
            (valueChange)="store.fillMode.set(fillMode($event))"
          />
        </div>
        <div class="flex items-center gap-2">
          <span
            class="text-foreground-muted w-16 shrink-0 text-xs"
            title="Whether a click fills what is under it, or a drag fills everything a rectangle or a lasso holds"
            >Selects</span
          >
          <xui-select
            class="min-w-0 flex-1"
            aria-label="Fill selection"
            data-testid="paint-fill-shape"
            [filterable]="false"
            [itemText]="fillShapeName"
            [items]="fillShapeValues"
            [value]="store.fillShape()"
            (valueChange)="store.fillShape.set(fillShape($event))"
          />
        </div>
        @if (store.fillShape() !== 'click') {
          <span
            class="flex items-center gap-1 ps-[4.5rem] text-xs"
            title="Also fills what the viewport cannot see inside the outline: the far side, and what is hidden behind"
          >
            <xui-switch aria-label="Through" data-testid="paint-fill-through" [(checked)]="store.fillThrough" />
            Through
          </span>
        }
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
          <span
            class="flex w-full items-center gap-2 text-xs"
            title="Keeps the step from the source to where the first stroke after it started. Off, every stroke starts at the source."
          >
            <xui-switch aria-label="Relative" data-testid="paint-clone-relative" [(checked)]="store.cloneRelative" />
            Relative
          </span>
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
          @if (material.source && pictures.url(material.source); as picture) {
            <img class="size-7 shrink-0 rounded-sm" alt="" data-testid="paint-material-picture" [src]="picture" />
          }
          <span class="readout min-w-0 flex-1 truncate text-xs" data-testid="paint-material-name">{{
            material.name
          }}</span>
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            data-testid="paint-material-clear"
            title="Paints the colors again"
            (click)="store.material.set(null)"
          >
            Clear
          </button>
        } @else {
          <span class="text-foreground-muted min-w-0 flex-1 truncate text-xs">Colors · Alt-click a shelf material</span>
        }
      </div>
      @if (store.material()) {
        <div class="flex items-center gap-2 ps-[4.5rem]" data-testid="paint-material-placement">
          <xui-select
            class="min-w-0 flex-1"
            aria-label="Material projection"
            data-testid="paint-material-projection"
            title="How the material's maps land: over the UV layout, or in the model's space at one size everywhere"
            [filterable]="false"
            [itemDisabled]="needsModel()"
            [itemText]="labelsOf(terms().materialProjections)"
            [items]="valuesOf(terms().materialProjections)"
            [value]="store.materialProjection()"
            (valueChange)="$event && store.materialProjection.set($event)"
          />
          <xui-slider
            class="w-24 shrink-0"
            labelStepSize="0"
            aria-label="Material scale"
            data-testid="paint-material-scale"
            title="How many times the material repeats"
            [max]="16"
            [min]="0.25"
            [stepSize]="0.25"
            [value]="store.materialScale()"
            (valueChange)="store.materialScale.set($event)"
          />
          <span class="readout w-10 shrink-0 text-end text-xs">{{ store.materialScale() }}×</span>
        </div>
      }
    }
    @if (store.tool() === 'picker' && !store.masking()) {
      <span
        class="flex items-center gap-2 text-xs"
        title="Takes every channel the layer writes at the click, as Also paints, besides the painted channel's color"
      >
        <xui-switch
          aria-label="Every channel"
          data-testid="paint-pick-every-channel"
          [(checked)]="store.pickEveryChannel"
        />
        Every channel
      </span>
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
        <span class="flex items-center gap-1 text-xs" title="Repeats the picture past its edges">
          <xui-switch
            aria-label="Tiled"
            data-testid="paint-screen-picture-tiled"
            [checked]="picture.tiled"
            (checkedChange)="store.screenPicture.set({ ...picture, tiled: $event })"
          />
          Tiled
        </span>
        <span class="flex items-center gap-1 text-xs" [title]="commands.tooltip('toggleStencil')">
          <xui-switch aria-label="Stencil" data-testid="paint-stencil" [(checked)]="store.stencil" />
          Stencil
        </span>
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
      <xui-select
        class="min-w-0 flex-1"
        aria-label="Symmetry"
        data-testid="paint-symmetry"
        placeholder="Off"
        [disabled]="!store.state()?.model"
        [filterable]="false"
        [itemText]="symmetryName()"
        [items]="symmetries()"
        [value]="store.symmetry()?.kind ?? ''"
        (valueChange)="setKind($event ?? '')"
      />
    </div>
    @if (store.symmetry(); as symmetry) {
      <div class="flex items-center gap-2" data-testid="paint-symmetry-settings">
        <span class="text-foreground-muted w-16 shrink-0 text-xs">{{
          symmetry.kind === 'radial' ? 'Axis' : 'Plane'
        }}</span>
        <xui-select
          class="min-w-0 flex-1"
          aria-label="Symmetry axis"
          data-testid="paint-symmetry-axis"
          [filterable]="false"
          [itemText]="labelsOf(terms().axes)"
          [items]="valuesOf(terms().axes)"
          [value]="symmetry.axis"
          (valueChange)="$event && setSymmetry({ axis: $event })"
        />
        <xui-select
          class="min-w-0 flex-1"
          aria-label="Symmetry centre"
          data-testid="paint-symmetry-centre"
          [filterable]="false"
          [itemText]="labelsOf(terms().centres)"
          [items]="valuesOf(terms().centres)"
          [value]="symmetry.centre"
          (valueChange)="$event && setSymmetry({ centre: $event })"
        />
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
      <xui-select
        class="min-w-0 flex-1"
        aria-label="Size space"
        data-testid="paint-size-space"
        [filterable]="false"
        [itemDisabled]="sizeNeedsModel()"
        [itemText]="labelsOf(terms().sizeSpaces)"
        [items]="valuesOf(terms().sizeSpaces)"
        [value]="store.brush().sizeSpace"
        (valueChange)="$event && choose('sizeSpace', $event)"
      />
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
        <span class="text-foreground-muted w-16 shrink-0 text-xs">Color</span>
        <xui-color-picker
          aria-label="Brush color"
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
          title="Keeps this color in the project's palette"
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
              [attr.aria-label]="'Paint with ' + colourName(colour) + '. Alt-click takes it out of the palette'"
              [title]="colourName(colour) + ' (Alt-click takes it out)'"
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
        <xui-select
          class="min-w-0 flex-1"
          [aria-label]="'Brush ' + choice.label.toLowerCase()"
          [attr.data-testid]="'paint-' + choice.key"
          [filterable]="false"
          [itemText]="labelsOf(terms()[choice.terms])"
          [items]="valuesOf(terms()[choice.terms])"
          [value]="store.brush()[choice.key]"
          (valueChange)="$event && choose(choice.key, $event)"
        />
      </div>
    }
    <span
      class="flex items-center gap-2 text-xs"
      title="Off, a place keeps the strongest stamp: a slow drag paints what a fast one does"
    >
      <xui-switch
        aria-label="Stamps build up where they overlap"
        data-testid="paint-accumulate"
        [checked]="store.brush().accumulate"
        (checkedChange)="set('accumulate', $event)"
      />
      Stamps build up where they overlap
    </span>
    <span
      class="flex items-center gap-2 text-xs"
      title="A stroke started where the last one stopped carries it on: nothing is laid twice where they meet, and a lazy brush keeps its string"
    >
      <xui-switch
        aria-label="Carry on the last stroke"
        data-testid="paint-resume"
        [checked]="store.brush().resume"
        (checkedChange)="set('resume', $event)"
      />
      Carry on the last stroke
    </span>
    <span
      class="flex items-center gap-2 text-xs"
      title="On the mesh, paints only what the camera sees: off, a stroke also reaches what lies close behind the surface under it"
    >
      <xui-switch
        aria-label="Only what the camera sees"
        data-testid="paint-occlusion"
        [checked]="store.brush().occlusion"
        (checkedChange)="set('occlusion', $event)"
      />
      Only what the camera sees
    </span>
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
        <span class="readout w-12 shrink-0 text-end text-xs">{{
          percent(store.brush().occlusionTolerance / 1000, 1)
        }}</span>
      </div>
    }
    <div class="flex flex-col gap-1" data-testid="paint-channels" [hidden]="store.masking()">
      <span class="section-label">Also paints</span>
      @for (channel of others(); track channel.usage) {
        <div class="flex items-center gap-2">
          <xui-switch
            [aria-label]="'Paint ' + nameOf(channel.usage)"
            [attr.data-testid]="'paint-also-' + channel.usage"
            [checked]="store.alsoPaints().has(channel.usage)"
            (checkedChange)="toggleChannel(channel.usage, $event)"
          />
          <span class="min-w-0 flex-1 truncate text-xs">{{ nameOf(channel.usage) }}</span>
          <xui-color-picker
            [attr.aria-label]="nameOf(channel.usage) + ' color'"
            [value]="store.alsoPaints().get(channel.usage) ?? '#808080'"
            (valueChange)="setChannel(channel.usage, $event)"
          />
        </div>
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
  protected readonly pictures = inject(LibraryThumbnails);
  protected readonly nameOf = channelName;

  /** A stroke of the brush as it stands, drawn by the Worker, or null until there is one (#123). */
  protected readonly preview = signal<string | null>(null);

  constructor() {
    let wait: ReturnType<typeof setTimeout> | undefined;
    let asked = 0;
    effect(() => {
      // Its size is left out: the picture is drawn at a size of its own, so dragging the size draws nothing.
      const brush = { ...this.store.brush(), radius: 1 };
      const stack = this.store.state()?.id;
      clearTimeout(wait);
      if (!stack) {
        return;
      }

      wait = setTimeout(() => {
        const ticket = ++asked;
        void this.store.previewBrush(brush).then(
          blob => {
            if (ticket === asked) {
              this.show(blob);
            }
          },
          () => undefined // a brush the Server refused keeps the last picture, as a slider passes through it
        );
      }, PreviewQuiet);
    });
    effect(() => {
      const source = this.store.material()?.source;
      if (source) {
        this.pictures.want([source]);
      }
    });
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(wait);
      asked++;
      this.show(null);
    });
  }

  private show(blob: Blob | null): void {
    const held = this.preview();
    if (held) {
      URL.revokeObjectURL(held);
    }

    this.preview.set(blob ? URL.createObjectURL(blob) : null);
  }

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

  protected fillShape(value: string | null): FillShape {
    return FillShapes.find(shape => shape.value === value)?.value ?? 'click';
  }

  protected readonly fillShapeValues: readonly string[] = FillShapes.map(shape => shape.value);
  protected readonly fillShapeName = (value: string): string =>
    FillShapes.find(shape => shape.value === value)?.label ?? value;

  protected fillMode(value: string | null): FillMode {
    return FillModes.find(mode => mode === value) ?? 'polygon';
  }

  protected readonly valuesOf = valuesOf;
  protected readonly labelsOf = labelsOf;
  protected readonly colourName = hexColourName;
  /** A material lands over the UV layout alone where there is no mesh to place it in the space of. */
  protected readonly needsModel = computed(() => {
    const model = !!this.store.state()?.model;
    return (projection: string) => !model && projection !== 'Uv';
  });
  /** A size is a share of the model only where there is one. */
  protected readonly sizeNeedsModel = computed(() => {
    const model = !!this.store.state()?.model;
    return (space: string) => !model && space === 'model';
  });
  /** Off first, then each kind of symmetry. */
  protected readonly symmetries = computed(() => ['', ...valuesOf(this.terms().symmetries)]);
  protected readonly symmetryName = computed(() => {
    const terms = this.terms().symmetries;
    return (kind: string) => (kind ? labelIn(terms, kind) : 'Off');
  });
  protected readonly termName = termName;
  protected readonly percent = percent;
  protected readonly toRadians = toRadians;

  /** The words a brush takes, with the labels the Server gives them, or the values in words until it answers. */
  protected readonly terms = computed(() => {
    const brush = this.store.vocabulary()?.brush;
    const plain = (values: readonly string[]): StackTerm[] => values.map(value => ({ value, label: termName(value) }));
    return {
      rotations: brush?.rotations ?? plain(['fixed', 'along', 'random']),
      falloffs: brush?.falloffs ?? plain(['smooth', 'linear', 'spherical', 'tip']),
      scatterAxes: brush?.scatterAxes ?? plain(['both', 'across']),
      sizeSpaces: brush?.sizeSpaces ?? plain(['screen', 'texture', 'model']),
      symmetries: brush?.symmetries ?? plain(['mirror', 'radial']),
      axes: brush?.axes ?? plain(['x', 'y', 'z']),
      centres: brush?.centres ?? plain(['model', 'origin']),
      fills: brush?.fills ?? plain([...FillModes]),
      materialProjections: brush?.materialProjections ?? plain(['Uv']),
      maximumCount: brush?.maximumCount ?? 64
    };
  });

  protected readonly others = computed(() =>
    (this.store.state()?.channels ?? []).filter(channel => channel.usage !== this.store.channel())
  );

  protected toggleChannel(usage: string, on: boolean): void {
    const next = new Map(this.store.alsoPaints());
    if (on) {
      next.set(usage, next.get(usage) ?? '#808080');
    } else {
      next.delete(usage);
    }

    this.store.alsoPaints.set(next);
  }

  protected setChannel(usage: string, colour: string): void {
    this.store.alsoPaints.set(new Map(this.store.alsoPaints()).set(usage, colour));
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
        return times(value);
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
        return percent(value / 1000, 1);
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
    Math.round((Math.log(Math.max(1, this.store.radius())) / Math.log(maximumRadius(this.store))) * SizeSteps)
  );

  protected setSize(position: number): void {
    const most = maximumRadius(this.store);
    this.store.radius.set(Math.max(1, Math.round(Math.exp((position / SizeSteps) * Math.log(most)))));
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
}
