import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import type { StackDecal, StackLayer, StackSurfacePath, StackSurfaceSymmetry } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { labelsOf, valuesOf } from '../core/labels';
import { count as counted, toDegrees, toRadians } from '../core/numbers';
import { PaintStore } from './paint-store';
import { isWarped } from './surface-frame';
import { surfaceSymmetry } from './symmetry-guide';

/** How the picture is read between its pixels, as a decal stores it. */
const Samplings: readonly { value: string; label: string }[] = [
  { value: 'Bilinear', label: 'Smooth' },
  { value: 'Point', label: 'Pixelated' }
];

/** No symmetry. */
const Off = 'Off';

/** The brush's symmetry, as it is set now. */
const LikeTheBrush = 'Like the Brush';

/** What a decal or a path can be copied by without the brush. */
const SymmetryChoices: readonly string[] = [Off, 'Mirror on X', 'Mirror on Y', 'Mirror on Z'];

/**
 * A decal's box and a path's curve, for a fill whose projection is one of them (#130, #131, #133, #134). Where they
 * are on the mesh is placed in the viewport, by a click and by the handles there; what is here is their size and how
 * they read their picture. Each change is one edit to the stack.
 */
@Component({
  selector: 'ptn-surface-fields',
  imports: [XuiButtonImports, XuiNumericInputImports, XuiSelectImports, XuiSliderImports, XuiSwitchImports],
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
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            data-testid="surface-warp-geometry"
            [disabled]="!onMesh()"
            title="Spreads the picture evenly along the surface under the decal, measured along the way it faces"
            (click)="warpToGeometry()"
          >
            Warp to Geometry
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
            aria-label="Decal width, in the model's units"
            data-testid="surface-decal-width"
            [stepSize]="step(box.width)"
            [value]="box.width"
            (valueChange)="positive($event, width => setDecal({ width }))"
          />
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            aria-label="Decal height, in the model's units"
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
            aria-label="How deep the decal reaches through the surface, in the model's units"
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
            aria-label="Widest angle a face takes the decal at, in degrees"
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
            aria-label="Repeat the picture past its edges"
            data-testid="surface-decal-tiled"
            [checked]="box.tiled"
            (checkedChange)="setDecal({ tiled: $event })"
          />
          <xui-select
            class="min-w-0 flex-1"
            aria-label="How the picture is read between its pixels"
            data-testid="surface-decal-sampling"
            [filterable]="false"
            [itemText]="samplingName"
            [items]="samplings"
            [value]="box.sampling"
            (valueChange)="$event && setDecal({ sampling: $event })"
          />
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
            data-testid="surface-path-copy"
            title="Copies the path's points and settings, to paste onto another fill"
            (click)="copyPath()"
          >
            Copy
          </button>
        }
        @if (paint.pathClip()) {
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            data-testid="surface-path-paste"
            title="Puts the copied path here, in place of this one"
            (click)="pastePath()"
          >
            Paste
          </button>
        }
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
            aria-label="Path radius, in the model's units"
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
        @if (!curve.ribbon) {
          <div
            class="flex items-center gap-2"
            title="How far apart the stroke's stamps are, as a share of its width; 0 is one unbroken line"
          >
            <span class="text-foreground-muted w-16 shrink-0 text-xs">Spacing</span>
            <xui-slider
              class="min-w-0 flex-1"
              labelStepSize="0"
              aria-label="Path stamp spacing, as a share of its width"
              data-testid="surface-path-spacing"
              [max]="4"
              [min]="0"
              [stepSize]="0.05"
              [value]="curve.spacing"
              (valueChange)="setPath({ spacing: $event })"
            />
          </div>
          <div class="flex items-center gap-2" title="How far each stamp moves at random, as a share of the width">
            <span class="text-foreground-muted w-16 shrink-0 text-xs">Jitter</span>
            <xui-slider
              class="min-w-0 flex-1"
              labelStepSize="0"
              aria-label="Path stamp jitter"
              data-testid="surface-path-jitter"
              [disabled]="!curve.spacing"
              [max]="1"
              [min]="0"
              [stepSize]="0.05"
              [value]="curve.jitter"
              (valueChange)="setPath({ jitter: $event })"
            />
          </div>
          <div class="flex items-center gap-2" title="The shape each stamp takes, turned with the path">
            <span class="text-foreground-muted w-16 shrink-0 text-xs">Alpha</span>
            <xui-select
              class="min-w-0 flex-1"
              aria-label="Path stamp alpha"
              data-testid="surface-path-alpha"
              [disabled]="!curve.spacing"
              [filterable]="false"
              [items]="alphas()"
              [value]="curve.alpha || 'Round'"
              (valueChange)="$event && setPath({ alpha: $event === 'Round' ? null : $event })"
            />
          </div>
        }
        <div
          class="flex items-center gap-2"
          title="Lays the fill's picture along the path, rather than showing it through"
        >
          <span class="text-foreground-muted w-16 shrink-0 text-xs">Ribbon</span>
          <xui-switch
            aria-label="Lay the picture along the path"
            data-testid="surface-path-ribbon"
            [checked]="curve.ribbon"
            (checkedChange)="setPath({ ribbon: $event })"
          />
          @if (curve.ribbon) {
            <xui-numeric-input
              class="min-w-0 flex-1"
              size="sm"
              aria-label="How far along the path the picture repeats, in the model's units; 0 keeps its shape"
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
            aria-label="Widest angle a face takes the path at, in degrees"
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
    @if (placed(); as surface) {
      <div
        class="flex items-center gap-2"
        title="Copies it through a plane across the model's middle, or as the brush's symmetry copies a stroke"
      >
        <span class="text-foreground-muted w-16 shrink-0 text-xs">Symmetry</span>
        <xui-select
          class="min-w-0 flex-1"
          aria-label="How the decal or the path is copied"
          data-testid="surface-symmetry"
          [filterable]="false"
          [items]="symmetryChoices()"
          [value]="symmetryChoice()"
          (valueChange)="$event && chooseSymmetry($event)"
        />
        @if (surface.symmetry; as symmetry) {
          <span class="readout text-xs" data-testid="surface-symmetry-shown">{{ symmetryName(symmetry) }}</span>
        }
      </div>
    }
  `
})
export class SurfaceFields {
  protected readonly paint = inject(PaintStore);
  readonly layer = input.required<StackLayer>();
  protected readonly samplings = valuesOf(Samplings);
  protected readonly samplingName = labelsOf(Samplings);
  /** Whether the stack is on a mesh a click can land on. */
  protected readonly onMesh = computed(() => !!this.paint.state()?.model.trim());
  protected readonly decal = computed(() => this.layer().decal ?? null);
  protected readonly path = computed(() => this.layer().path ?? null);
  protected readonly count = computed(() => this.path()?.points.length ?? 0);
  /** The shipped brush alphas a path's stamps can take, round first. */
  protected readonly alphas = computed(() => this.paint.vocabulary()?.alphas.map(term => term.value) ?? ['Round']);
  /** The decal or the path the projection uses, once it is placed: what a symmetry copies. */
  protected readonly placed = computed(() => {
    const layer = this.layer();
    return layer.projection === 'Decal' ? this.decal() : layer.projection === 'Path' ? this.path() : null;
  });
  protected warpToGeometry(): void {
    void this.paint.warpToGeometry();
  }

  protected readonly warped = computed(() => isWarped(this.decal()?.warp));

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

  /** What the symmetry choice offers: none, a mirror on each axis, and the brush's own once it has one. */
  protected readonly symmetryChoices = computed(() => [
    ...SymmetryChoices,
    ...(this.paint.symmetry() ? [LikeTheBrush] : [])
  ]);

  /** The choice the kept symmetry is, or none of them when it is one the brush gave and the brush has none now. */
  protected readonly symmetryChoice = computed(() => {
    const symmetry = this.placed()?.symmetry;
    if (!symmetry) {
      return Off;
    }

    const plain = symmetry.kind === 'mirror' ? `Mirror on ${this.symmetryName(symmetry).slice(-1)}` : null;
    return plain && SymmetryChoices.includes(plain) ? plain : LikeTheBrush;
  });

  /**
   * Copies the decal or the path (#132), kept on it as a plane or a line in the model's units: through a plane across
   * the middle of the model's box, or as the brush's symmetry is set now. Off stops copying it. One edit either way.
   */
  protected chooseSymmetry(choice: string): void {
    const placed = this.placed();
    const axis = choice.startsWith('Mirror on ') ? choice.slice(-1).toLowerCase() : null;
    const brush = choice === LikeTheBrush ? this.paint.symmetry() : null;
    const wanted = axis ? { kind: 'mirror', axis, centre: 'model', offset: 0, count: 2 } : brush;
    const symmetry = placed && wanted ? surfaceSymmetry(wanted, placed.box) : null;
    if (choice !== Off && !symmetry) {
      return;
    }

    if (this.layer().projection === 'Decal') {
      this.setDecal({ symmetry });
    } else {
      this.setPath({ symmetry });
    }
  }

  /** What a kept symmetry is, in words: which way, round or across which axis, and how many for a radial one. */
  protected symmetryName(symmetry: StackSurfaceSymmetry): string {
    const lanes = symmetry.axis.map(Math.abs);
    const axis = ['X', 'Y', 'Z'][lanes.indexOf(Math.max(...lanes))] ?? 'X';
    return symmetry.kind === 'radial' ? `${counted(symmetry.count)} round ${axis}` : `Mirrored on ${axis}`;
  }

  protected copyPath(): void {
    const path = this.path();
    if (path) {
      this.paint.pathClip.set(path);
    }
  }

  /** The copied path onto this fill, as one edit; it keeps the box it was drawn in, since its points are in it. */
  protected pastePath(): void {
    const copied = this.paint.pathClip();
    if (copied) {
      void this.paint.edit({ op: 'setLayer', layer: this.layer().id, path: copied });
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
    return Math.round(toDegrees(radians));
  }

  protected radians(degrees: number | null): number {
    return toRadians(degrees ?? 0);
  }
}
