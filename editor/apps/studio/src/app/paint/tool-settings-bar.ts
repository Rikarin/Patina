import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { Commands } from '../core/commands';
import { labelsOf, termName, valuesOf } from '../core/labels';
import { percent } from '../core/numbers';
import { SizeSteps, radiusAt, sizeMeaning, sizePosition, sizeShown } from './brush-size';
import { type BrushSettings, type FillMode, type FillShape, PaintStore, type PaintTool } from './paint-store';
import { FillModes, FillShapes, PaintTools } from './paint-tools';

/** The tools that lay a colour of their own, which the bar offers while they are in hand and not on a mask. */
const Colouring: ReadonlySet<PaintTool> = new Set(['brush', 'fill', 'line', 'path']);

/** The tools a click of which is not a stroke, so a brush's size, flow and opacity mean nothing to them. */
const NotStrokes: ReadonlySet<PaintTool> = new Set(['fill', 'picker']);

/**
 * The settings of the tool in hand, in a bar over the viewport (#201, #23): what a painter changes stroke by stroke,
 * where the strokes are, so that Properties can keep to what is chosen. A stroke's size, flow and opacity, its colour or
 * a mask's grey, and the fill's and the picker's own choices. Every setting here is the brush's, so Properties' Brush
 * section shows the same values and holds the rest.
 */
@Component({
  selector: 'ptn-tool-settings-bar',
  imports: [XuiColorPickerImports, XuiSelectImports, XuiSliderImports, XuiSwitchImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'contents' },
  template: `
    <div
      class="pane-bar @container flex-nowrap gap-2 overflow-hidden px-2"
      role="toolbar"
      aria-label="Tool settings"
      data-testid="tool-settings-bar"
      [attr.data-tool]="paint.tool()"
    >
      <span class="section-label shrink-0 @max-[44rem]:hidden" data-testid="tool-settings-name">{{ toolName() }}</span>
      @if (strokes()) {
        <span class="text-foreground-muted shrink-0 text-xs" [title]="sizeMeans()">Size</span>
        <xui-slider
          class="max-w-28 min-w-16 flex-1"
          labelStepSize="0"
          aria-label="Brush size"
          data-testid="tool-settings-size"
          [max]="sizeSteps"
          [min]="0"
          [stepSize]="1"
          [value]="sizePosition()"
          (valueChange)="paint.radius.set(radiusAt($event))"
        />
        <span class="readout w-12 shrink-0 text-end text-xs" data-testid="tool-settings-size-readout">{{
          sizeShown()
        }}</span>
        @for (slider of sliders; track slider.key) {
          <span class="text-foreground-muted shrink-0 text-xs @max-[30rem]:hidden">{{ slider.label }}</span>
          <xui-slider
            class="max-w-20 min-w-12 flex-1 @max-[30rem]:hidden"
            labelStepSize="0"
            [attr.aria-label]="slider.label"
            [attr.data-testid]="'tool-settings-' + slider.key"
            [max]="1"
            [min]="0"
            [stepSize]="0.01"
            [value]="paint.brush()[slider.key]"
            (valueChange)="set(slider.key, $event)"
          />
          <span class="readout w-9 shrink-0 text-end text-xs @max-[30rem]:hidden">{{
            percent(paint.brush()[slider.key])
          }}</span>
        }
      }
      @if (paint.tool() === 'fill') {
        <xui-select
          class="w-32 shrink"
          aria-label="Fill mode"
          title="How much of the mesh a click fills"
          data-testid="tool-settings-fill-mode"
          [filterable]="false"
          [itemText]="labelsOf(fills())"
          [items]="valuesOf(fills())"
          [value]="paint.fillMode()"
          (valueChange)="paint.fillMode.set(fillMode($event))"
        />
        <xui-select
          class="w-28 shrink"
          aria-label="Fill selection"
          title="Whether a click fills what is under it, or a drag fills everything a rectangle or a lasso holds"
          data-testid="tool-settings-fill-shape"
          [filterable]="false"
          [itemText]="fillShapeName"
          [items]="fillShapeValues"
          [value]="paint.fillShape()"
          (valueChange)="paint.fillShape.set(fillShape($event))"
        />
      }
      @if (paint.tool() === 'picker' && !paint.masking()) {
        <span
          class="flex shrink-0 items-center gap-1 text-xs"
          title="Takes every channel the layer writes at the click, as Also paints, besides the painted channel's color"
        >
          <xui-switch
            aria-label="Every channel"
            data-testid="tool-settings-pick-every-channel"
            [(checked)]="paint.pickEveryChannel"
          />
          Every channel
        </span>
      }
      @if (paint.masking()) {
        @if (paint.tool() !== 'picker') {
          <span class="text-foreground-muted shrink-0 text-xs" title="Black hides the layer and white shows it"
            >Grey</span
          >
          <xui-slider
            class="w-20 shrink"
            labelStepSize="0"
            aria-label="Mask grey"
            data-testid="tool-settings-mask-grey"
            [max]="1"
            [min]="0"
            [stepSize]="0.01"
            [value]="paint.maskGrey()"
            (valueChange)="paint.maskGrey.set($event)"
          />
          <span class="readout w-10 shrink-0 text-end text-xs">{{ percent(paint.maskGrey()) }}</span>
        }
      } @else if (colours()) {
        <xui-color-picker
          class="shrink-0"
          aria-label="Brush color"
          data-testid="tool-settings-colour"
          [presets]="paint.recentColours()"
          [value]="paint.colour()"
          (valueChange)="paint.colour.set($event)"
        />
      }
    </div>
  `
})
export class ToolSettingsBar {
  protected readonly paint = inject(PaintStore);
  private readonly commands = inject(Commands);
  protected readonly sizeSteps = SizeSteps;
  protected readonly percent = percent;
  protected readonly valuesOf = valuesOf;
  protected readonly labelsOf = labelsOf;
  /** A stroke's two shares besides its size, the ones a painter reaches for most. */
  protected readonly sliders: readonly { key: 'flow' | 'opacity'; label: string }[] = [
    { key: 'flow', label: 'Flow' },
    { key: 'opacity', label: 'Opacity' }
  ];

  /** The tool in hand, by its command's name. */
  protected readonly toolName = computed(() => {
    const tool = PaintTools.find(entry => entry.id === this.paint.tool());
    return (tool && this.commands.spec(tool.command)?.label) ?? termName(this.paint.tool());
  });

  protected readonly strokes = computed(() => !NotStrokes.has(this.paint.tool()));
  protected readonly colours = computed(() => Colouring.has(this.paint.tool()));
  protected readonly sizePosition = computed(() => sizePosition(this.paint));
  protected readonly sizeShown = computed(() => sizeShown(this.paint.brush().sizeSpace, this.paint.radius()));
  protected readonly sizeMeans = computed(() => sizeMeaning(this.paint.brush().sizeSpace));

  /** What a click of the fill tool can take, in the Server's words once it has said them. */
  protected readonly fills = computed(
    () => this.paint.vocabulary()?.brush.fills ?? FillModes.map(value => ({ value, label: termName(value) }))
  );

  protected readonly fillShapeValues: readonly string[] = FillShapes.map(shape => shape.value);
  protected readonly fillShapeName = (value: string): string =>
    FillShapes.find(shape => shape.value === value)?.label ?? value;

  protected radiusAt(position: number): number {
    return radiusAt(this.paint, position);
  }

  protected fillMode(value: string | null): FillMode {
    return FillModes.find(mode => mode === value) ?? 'polygon';
  }

  protected fillShape(value: string | null): FillShape {
    return FillShapes.find(shape => shape.value === value)?.value ?? 'click';
  }

  protected set<K extends keyof BrushSettings>(key: K, value: BrushSettings[K]): void {
    this.paint.brush.update(brush => ({ ...brush, [key]: value }));
  }
}
