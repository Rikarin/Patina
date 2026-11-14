import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import type { StackSymmetry, StackTerm } from '@patina/api-client';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { labelIn, labelsOf, termName, valuesOf } from '../core/labels';
import { percent } from '../core/numbers';
import { PaintStore } from './paint-store';

/**
 * Symmetry, in a section of its own (#298): it copies a stroke, and a decal or a path set to follow the brush, so it
 * stays in reach while a fill is chosen and the brush's settings are not shown. It needs a mesh, and is the project's
 * rather than the brush's: a preset does not change it.
 */
@Component({
  selector: 'ptn-symmetry-section',
  imports: [XuiSelectImports, XuiSliderImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5', role: 'group', 'aria-label': 'Symmetry' },
  template: `
    <div class="flex items-center gap-2">
      <span
        class="text-foreground-muted w-16 shrink-0 text-xs"
        title="Copies strokes on the mesh, in the 3D view and the 2D view alike"
        >Kind</span
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
  `
})
export class SymmetrySection {
  protected readonly store = inject(PaintStore);
  protected readonly valuesOf = valuesOf;
  protected readonly labelsOf = labelsOf;
  protected readonly percent = percent;

  /** The words symmetry takes, with the labels the Server gives them, or the values in words until it answers. */
  protected readonly terms = computed(() => {
    const brush = this.store.vocabulary()?.brush;
    const plain = (values: readonly string[]): StackTerm[] => values.map(value => ({ value, label: termName(value) }));
    return {
      symmetries: brush?.symmetries ?? plain(['mirror', 'radial']),
      axes: brush?.axes ?? plain(['x', 'y', 'z']),
      centres: brush?.centres ?? plain(['model', 'origin']),
      maximumCount: brush?.maximumCount ?? 64
    };
  });

  /** Off first, then each kind of symmetry. */
  protected readonly symmetries = computed(() => ['', ...valuesOf(this.terms().symmetries)]);
  protected readonly symmetryName = computed(() => {
    const terms = this.terms().symmetries;
    return (kind: string) => (kind ? labelIn(terms, kind) : 'Off');
  });

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
