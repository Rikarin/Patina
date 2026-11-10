import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import type { StackDecal, StackLayer, StackSurfacePath } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { PaintStore } from './paint-store';
import { Unwarped } from './surface-frame';

/** How the picture is read between its pixels, as a decal stores it. */
const Samplings: readonly { value: string; label: string }[] = [
  { value: 'Bilinear', label: 'Smooth' },
  { value: 'Point', label: 'Pixelated' }
];

/**
 * A decal's box and a path's curve, for a fill whose projection is one of them (#130, #131, #133, #134). Where they
 * are on the mesh is placed in the viewport, by a click and by the handles there; what is here is their size and how
 * they read their picture. Each change is one edit to the stack.
 */
@Component({
  selector: 'ptn-surface-fields',
  imports: [XuiButtonImports, XuiNumericInputImports, XuiSliderImports, XuiSwitchImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5' },
  template: `
    @if (layer().projection === 'Decal') {
      <div class="flex items-center gap-2">
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="surface-place-decal"
          [variant]="paint.placing() === 'decal' ? 'default' : 'ghost'"
          [disabled]="!onMesh()"
          title="The next click on the mesh in the viewport puts the decal there, facing out of the face it meets"
          (click)="paint.placing.set(paint.placing() === 'decal' ? null : 'decal')"
        >
          {{ decal() ? 'Move on Mesh' : 'Place on Mesh' }}
        </button>
        @if (decal()) {
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="surface-warp"
            [variant]="paint.warping() ? 'default' : 'ghost'"
            title="Shows a grid over the decal in the viewport: drag its points to bend the picture"
            (click)="paint.warping.set(!paint.warping())"
          >
            Warp
          </button>
          @if (warped()) {
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              data-testid="surface-warp-reset"
              title="Straightens the picture again"
              (click)="setDecal({ warp: [] })"
            >
              Straighten
            </button>
          }
        }
      </div>
      @if (decal(); as box) {
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Size</span>
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            ariaLabel="Decal width, in the model's units"
            data-testid="surface-decal-width"
            [stepSize]="step(box.width)"
            [value]="box.width"
            (valueChange)="positive($event, width => setDecal({ width }))"
          />
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            ariaLabel="Decal height, in the model's units"
            data-testid="surface-decal-height"
            [stepSize]="step(box.height)"
            [value]="box.height"
            (valueChange)="positive($event, height => setDecal({ height }))"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Depth</span>
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            ariaLabel="How deep the decal reaches through the surface, in the model's units"
            data-testid="surface-decal-depth"
            [stepSize]="step(box.depth)"
            [value]="box.depth"
            (valueChange)="positive($event, depth => setDecal({ depth }))"
          />
        </div>
        <div class="flex items-center gap-2" title="How far a face may turn from the decal and still take it">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Angle</span>
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            ariaLabel="Widest angle a face takes the decal at, in degrees"
            data-testid="surface-decal-angle"
            [max]="180"
            [min]="0"
            [stepSize]="5"
            [value]="degrees(box.angle)"
            (valueChange)="setDecal({ angle: radians($event) })"
          />
        </div>
        <div class="flex items-center gap-2" title="How much of its depth the decal fades over at each side">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Fade</span>
          <xui-slider
            class="min-w-0 flex-1"
            labelStepSize="0"
            aria-label="Decal fade"
            data-testid="surface-decal-fade"
            [max]="1"
            [min]="0"
            [stepSize]="0.05"
            [value]="box.fade"
            (valueChange)="setDecal({ fade: $event })"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Tile</span>
          <xui-switch
            ariaLabel="Repeat the picture past its edges"
            data-testid="surface-decal-tiled"
            [checked]="box.tiled"
            (checkedChange)="setDecal({ tiled: $event })"
          />
          <select
            class="well h-7 min-w-0 flex-1 px-1 text-sm"
            aria-label="How the picture is read between its pixels"
            data-testid="surface-decal-sampling"
            (change)="setDecal({ sampling: text($event) })"
          >
            @for (option of samplings; track option.value) {
              <option [value]="option.value" [selected]="option.value === box.sampling">{{ option.label }}</option>
            }
          </select>
        </div>
      }
    } @else if (layer().projection === 'Path') {
      <div class="flex items-center gap-2">
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="surface-add-points"
          [variant]="paint.placing() === 'path' ? 'default' : 'ghost'"
          [disabled]="!onMesh()"
          title="Each click on the mesh in the viewport adds a point to the end of the path; Enter or Escape ends it"
          (click)="paint.placing.set(paint.placing() === 'path' ? null : 'path')"
        >
          {{ paint.placing() === 'path' ? 'Done' : 'Add Points' }}
        </button>
        <span class="readout text-xs" data-testid="surface-path-count">{{ count() }} points</span>
        @if (count() > 0) {
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            data-testid="surface-path-clear"
            title="Takes every point away, to draw the path again"
            (click)="setPath({ points: [] })"
          >
            Clear
          </button>
        }
      </div>
      @if (path(); as curve) {
        <div class="flex items-center gap-2" title="Half the path's width, in the model's units">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Radius</span>
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            ariaLabel="Path radius, in the model's units"
            data-testid="surface-path-radius"
            [stepSize]="step(curve.radius)"
            [value]="curve.radius"
            (valueChange)="positive($event, radius => setPath({ radius }))"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Hardness</span>
          <xui-slider
            class="min-w-0 flex-1"
            labelStepSize="0"
            aria-label="Path hardness"
            data-testid="surface-path-hardness"
            [max]="1"
            [min]="0"
            [stepSize]="0.05"
            [value]="curve.hardness"
            (valueChange)="setPath({ hardness: $event })"
          />
        </div>
        <div
          class="flex items-center gap-2"
          title="Lays the fill's picture along the path, rather than showing it through"
        >
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Ribbon</span>
          <xui-switch
            ariaLabel="Lay the picture along the path"
            data-testid="surface-path-ribbon"
            [checked]="curve.ribbon"
            (checkedChange)="setPath({ ribbon: $event })"
          />
          @if (curve.ribbon) {
            <xui-numeric-input
              class="min-w-0 flex-1"
              size="sm"
              ariaLabel="How far along the path the picture repeats, in the model's units; 0 keeps its shape"
              data-testid="surface-path-repeat"
              [min]="0"
              [stepSize]="step(curve.radius * 4)"
              [value]="curve.repeat"
              (valueChange)="setPath({ repeat: $event ?? 0 })"
            />
          }
        </div>
        <div class="flex items-center gap-2" title="How far a face may turn from the path and still take it">
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Angle</span>
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            ariaLabel="Widest angle a face takes the path at, in degrees"
            data-testid="surface-path-angle"
            [max]="180"
            [min]="0"
            [stepSize]="5"
            [value]="degrees(curve.angle)"
            (valueChange)="setPath({ angle: radians($event) })"
          />
        </div>
      }
    }
  `
})
export class SurfaceFields {
  protected readonly paint = inject(PaintStore);
  readonly layer = input.required<StackLayer>();
  protected readonly samplings = Samplings;
  /** Whether the stack is on a mesh a click can land on. */
  protected readonly onMesh = computed(() => !!this.paint.state()?.model.trim());
  protected readonly decal = computed(() => this.layer().decal ?? null);
  protected readonly path = computed(() => this.layer().path ?? null);
  protected readonly count = computed(() => this.path()?.points.length ?? 0);
  protected readonly warped = computed(() => {
    const warp = this.decal()?.warp ?? [];
    return warp.length === Unwarped.length && warp.some((value, at) => Math.abs(value - (Unwarped[at] ?? 0)) > 1e-6);
  });

  /** The layer the fields were last shown for. */
  private shownFor: string | null = null;

  constructor() {
    // Placing and the warp grid belong to the layer they were started for.
    effect(() => {
      const chosen = this.layer().id;
      if (chosen !== this.shownFor) {
        this.shownFor = chosen;
        untracked(() => {
          this.paint.placing.set(null);
          this.paint.warping.set(false);
        });
      }
    });
  }

  protected setDecal(change: Partial<StackDecal>): void {
    const decal = this.decal();
    if (decal) {
      void this.paint.edit({ op: 'setLayer', layer: this.layer().id, decal: { ...decal, ...change } });
    }
  }

  protected setPath(change: Partial<StackSurfacePath>): void {
    const path = this.path();
    if (path) {
      void this.paint.edit({ op: 'setLayer', layer: this.layer().id, path: { ...path, ...change } });
    }
  }

  /** A length only above zero goes out: a decal or a path of no size is refused. */
  protected positive(value: number | null, set: (value: number) => void): void {
    if (value !== null && value > 0) {
      set(value);
    }
  }

  /** A step a tenth the size of a length, to the nearest power of ten, so a model in millimetres or metres both nudge. */
  protected step(value: number): number {
    return 10 ** Math.floor(Math.log10(Math.max(value, 1e-6) / 10) + 1) / 10;
  }

  protected degrees(radians: number): number {
    return Math.round((radians * 180) / Math.PI);
  }

  protected radians(degrees: number | null): number {
    return ((degrees ?? 0) * Math.PI) / 180;
  }

  protected text(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }
}
