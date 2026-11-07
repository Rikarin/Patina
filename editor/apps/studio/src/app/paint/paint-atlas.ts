import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { ProjectStore } from '../library/project-store';
import { PaintStore, pressureOf, toTexels } from './paint-store';
import { StrokeGesture } from './stroke-gesture';

/** The stack flat, in UV space. It paints like the mesh does, and it is the whole picture for a layered material. */
@Component({
  selector: 'ptn-paint-atlas',
  imports: [XuiButtonImports, XuiNonIdealStateImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'relative flex h-full min-h-0 items-center justify-center overflow-hidden p-2',
    '(document:keydown.escape)': 'gesture.cancel()'
  },
  template: `
    @if (tiling()) {
      <button
        class="absolute top-3 right-3 z-10"
        type="button"
        xuiButton
        size="sm"
        data-testid="paint-tile"
        [attr.aria-pressed]="tiled()"
        (click)="tiled.set(!tiled())"
      >
        {{ tiled() ? 'One' : 'Tile' }}
      </button>
    }
    @if (store.picture(); as picture) {
      @if (tiling() && tiled()) {
        <div
          class="well aspect-square h-full max-h-full max-w-full"
          data-testid="paint-tiled"
          [style.background-image]="'url(' + picture + ')'"
          [style.background-size]="'33.3333% 33.3333%'"
        ></div>
      } @else {
        <div class="well aspect-square max-h-full max-w-full touch-none overflow-hidden" data-testid="paint-canvas">
          <img
            class="size-full select-none"
            [class.cursor-crosshair]="store.picking() !== null"
            alt="The composited map"
            draggable="false"
            data-testid="paint-picture"
            [src]="picture"
            (pointerdown)="down($event)"
            (pointermove)="move($event)"
            (pointerup)="up()"
            (pointercancel)="up()"
            (dblclick)="gesture.finish()"
          />
        </div>
      }
    } @else {
      <xui-non-ideal-state
        class="flex-1"
        title="Nothing to show yet"
        description="The map appears when it has been made."
      />
    }
  `
})
export class PaintAtlas {
  protected readonly store = inject(PaintStore);
  protected readonly gesture = new StrokeGesture(this.store, () => undefined);
  private readonly project = inject(ProjectStore);

  /** A layered material has to meet itself at its edges, which only shows when it repeats. */
  protected readonly tiling = computed(() => this.project.active()?.kind === 'layered-material');
  protected readonly tiled = signal(false);

  private readonly side = computed<readonly [number, number]>(() => {
    const state = this.store.state();
    return [state?.width ?? 1, state?.height ?? 1];
  });

  private painting = false;

  protected down(event: PointerEvent): void {
    if (this.store.picking() !== null) {
      const box = (event.target as HTMLElement).getBoundingClientRect();
      void this.store.pickColourAt((event.clientX - box.left) / box.width, (event.clientY - box.top) / box.height);
      return;
    }

    (event.target as HTMLElement).setPointerCapture(event.pointerId);
    this.painting = true;
    this.gesture.press(this.stamp(event));
  }

  protected move(event: PointerEvent): void {
    if (this.painting) {
      this.gesture.move(this.stamp(event));
    }
  }

  protected up(): void {
    if (this.painting) {
      this.painting = false;
      this.gesture.release();
    }
  }

  private stamp(event: PointerEvent) {
    const box = (event.target as HTMLElement).getBoundingClientRect();
    return {
      ...toTexels(event.clientX - box.left, event.clientY - box.top, box.width, box.height, this.side()),
      pressure: pressureOf(event)
    };
  }
}
