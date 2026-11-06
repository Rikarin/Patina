import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { PaintStore, pressureOf, toTexels } from './paint-store';
import { StrokeGesture } from './stroke-gesture';

/** The stack flat, in UV space. It paints like the mesh does, and it is the whole picture for a layered material. */
@Component({
  selector: 'ptn-paint-atlas',
  imports: [XuiNonIdealStateImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 items-center justify-center overflow-hidden p-2' },
  template: `
    @if (store.picture(); as picture) {
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
    } @else {
      <xui-non-ideal-state class="flex-1" title="Nothing to show yet" description="The map appears when it has been made." />
    }
  `
})
export class PaintAtlas {
  protected readonly store = inject(PaintStore);
  protected readonly gesture = new StrokeGesture(this.store, () => undefined);

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
