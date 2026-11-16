import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import type { StackLayer, StackSurfacePath } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { messageOf } from '../core/messages';
import { count, figure } from '../core/numbers';
import { PaintStore } from './paint-store';

/** A path fill as the Paths pane lists it. */
export interface PathRow {
  layer: StackLayer;
  /** What the path does: a stroke along it, a ribbon laid along it, or a loop filled inside. */
  kind: 'Stroke' | 'Ribbon' | 'Loop';
  points: number;
  /** How long it is through its points, in the model's units, closed for a loop. */
  length: number;
}

/** Every fill of the stack laid along a path, top first as the stack reads (#130). */
export function pathRows(layers: readonly { layer: StackLayer }[]): PathRow[] {
  return layers
    .filter(({ layer }) => layer.kind === 'fill' && layer.projection === 'Path')
    .map(({ layer }) => {
      const path = layer.path ?? null;
      return {
        layer,
        kind: path?.filled ? 'Loop' : path?.ribbon ? 'Ribbon' : 'Stroke',
        points: path?.points.length ?? 0,
        length: path ? pathLength(path) : 0
      };
    });
}

/** How far a path runs through its points in a straight line from each to the next, back to the first for a loop. */
export function pathLength(path: StackSurfacePath): number {
  const points = path.points.map(point => point.position);
  const legs = path.filled && points.length > 2 ? [...points, points[0]] : points;
  let total = 0;
  for (let index = 1; index < legs.length; index++) {
    const [a, b] = [legs[index - 1] ?? [], legs[index] ?? []];
    total += Math.hypot((b[0] ?? 0) - (a[0] ?? 0), (b[1] ?? 0) - (a[1] ?? 0), (b[2] ?? 0) - (a[2] ?? 0));
  }

  return total;
}

/**
 * The stack's paths, each a fill laid along a curve on the mesh (#130): a click chooses one, which shows its points in
 * the viewport and its settings in Properties; New Path adds a fill along a path and starts taking its points from clicks
 * on the mesh.
 */
@Component({
  selector: 'ptn-paths',
  imports: [XuiButtonImports, XuiNonIdealStateImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col', 'data-testid': 'paths' },
  template: `
    <div class="pane-bar gap-1 px-1" role="toolbar" aria-label="Paths">
      <button
        type="button"
        xuiButton
        size="sm"
        data-testid="paths-new"
        [disabled]="!onMesh()"
        title="Adds a fill laid along a path on top, then each click on the mesh adds a point; Enter or Escape ends it"
        (click)="newPath()"
      >
        New Path
      </button>
      @if (chosen(); as row) {
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="paths-add-points"
          [variant]="adding() ? 'default' : 'ghost'"
          [disabled]="!onMesh()"
          title="Each click on the mesh in the viewport adds a point to the end of the chosen path"
          (click)="paint.placing.set(adding() ? null : 'path')"
        >
          {{ adding() ? 'Done' : 'Add Points' }}
        </button>
      }
    </div>
    <div class="min-h-0 flex-1 overflow-auto p-1" role="listbox" aria-label="Paths" data-testid="paths-list">
      @for (row of rows(); track row.layer.id) {
        <button
          type="button"
          role="option"
          class="border-hairline hover:bg-selection flex h-7 w-full items-center gap-2 border-b px-2 text-start text-sm"
          [attr.aria-selected]="row.layer.id === paint.layer()"
          [attr.data-testid]="'path-' + row.layer.id"
          [class.outline]="row.layer.id === paint.layer()"
          [class.text-foreground-muted]="!row.layer.enabled"
          (click)="choose(row.layer)"
        >
          <span class="min-w-0 flex-1 truncate">{{ row.layer.name }}</span>
          <span class="text-foreground-muted shrink-0 text-xs" data-testid="path-kind">{{ row.kind }}</span>
          <span
            class="readout text-foreground-muted shrink-0 text-xs"
            data-testid="path-points"
            title="How many points the path has"
            >{{ counted(row.points) }} {{ row.points === 1 ? 'point' : 'points' }}</span
          >
          <span
            class="readout text-foreground-muted w-12 shrink-0 text-end text-xs"
            data-testid="path-length"
            title="How long the path is through its points, in the model's units"
            >{{ figured(row.length) }}</span
          >
        </button>
      } @empty {
        <xui-non-ideal-state
          title="No paths"
          description="A path lays a fill along a curve you click on the mesh: a stroke, a ribbon or a filled loop. New Path starts one."
          data-testid="paths-empty"
        />
      }
    </div>
  `
})
export class PathsPane {
  protected readonly paint = inject(PaintStore);

  protected readonly rows = computed(() => pathRows(this.paint.layers()));
  protected readonly chosen = computed(() => this.rows().find(row => row.layer.id === this.paint.layer()) ?? null);
  protected readonly adding = computed(() => this.paint.placing() === 'path' && this.chosen() !== null);
  /** A path is clicked on the mesh, so a document with no model has nowhere to put one. */
  protected readonly onMesh = computed(() => !!this.paint.state()?.model.trim());

  protected readonly counted = count;
  protected readonly figured = figure;

  protected choose(layer: StackLayer): void {
    if (this.paint.layer() !== layer.id) {
      this.paint.placing.set(null);
    }

    this.paint.marked.set(new Set());
    this.paint.layer.set(layer.id);
    this.paint.masking.set(false);
  }

  /** A fill along a new path on top, chosen, taking its points from the next clicks on the mesh. */
  protected async newPath(): Promise<void> {
    const before = new Set(this.paint.layers().map(row => row.layer.id));
    try {
      await this.paint.addLayer('fill', 'Path');
      const layer = this.paint.layer();
      if (!layer || before.has(layer)) {
        return;
      }

      this.paint.masking.set(false);
      await this.paint.edit({ op: 'setLayer', layer, projection: 'Path' });
      this.paint.placing.set('path');
    } catch (failure) {
      this.paint.failure.set(messageOf(failure));
    }
  }
}
