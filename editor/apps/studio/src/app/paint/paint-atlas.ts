import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { channelName } from '../core/labels';
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
    @if (store.state(); as state) {
      <select
        class="well absolute top-3 left-3 z-10 h-7 px-1 text-sm"
        aria-label="Channel to look at"
        data-testid="paint-view-channel"
        (change)="look($event)"
      >
        <option value="">Painting channel</option>
        @for (channel of state.channels; track channel.usage) {
          <option [value]="channel.usage" [selected]="store.viewed() === channel.usage">
            {{ nameOf(channel.usage) }}
          </option>
        }
      </select>
      @if (store.mirror() !== 'none' && state.model) {
        <span class="readout absolute top-3 left-48 z-10 text-xs" data-testid="paint-mirror-mesh">
          Mirror paints on the mesh, not here
        </span>
      }
    }
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
        <div
          class="well aspect-square max-h-full max-w-full touch-none overflow-hidden"
          data-testid="paint-canvas"
          [style.transform]="transform()"
          (wheel)="wheel($event)"
        >
          <img
            class="size-full select-none"
            [class.cursor-crosshair]="store.canPaint() || store.picking() !== null"
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

  /** The picture's own zoom and pan, which leave where a stamp lands alone: a stamp reads the box as drawn. */
  private readonly scale = signal(1);
  private readonly shift = signal<readonly [number, number]>([0, 0]);
  private panning = false;
  protected readonly nameOf = channelName;
  protected readonly transform = computed(() => {
    const [x, y] = this.shift();
    return `translate(${x}px, ${y}px) scale(${this.scale()})`;
  });

  protected look(event: Event): void {
    this.store.viewChannel((event.target as HTMLSelectElement).value || null);
  }

  protected wheel(event: WheelEvent): void {
    event.preventDefault();
    this.scale.update(scale => Math.max(0.25, Math.min(16, scale * Math.exp(-event.deltaY * 0.001))));
  }

  protected down(event: PointerEvent): void {
    if (event.button === 1 || event.altKey) {
      (event.target as HTMLElement).setPointerCapture(event.pointerId);
      this.panning = true;
      return;
    }

    if (this.store.tool() === 'picker') {
      const box = (event.target as HTMLElement).getBoundingClientRect();
      void this.store.pickBrushColour((event.clientX - box.left) / box.width, (event.clientY - box.top) / box.height);
      return;
    }

    if (this.store.picking() !== null) {
      const box = (event.target as HTMLElement).getBoundingClientRect();
      void this.store.pickColourAt((event.clientX - box.left) / box.width, (event.clientY - box.top) / box.height);
      return;
    }

    if (!this.store.canPaint()) {
      return;
    }

    (event.target as HTMLElement).setPointerCapture(event.pointerId);
    this.painting = true;
    this.gesture.press(this.stamp(event), { line: event.shiftKey, snap: event.ctrlKey || event.metaKey });
  }

  protected move(event: PointerEvent): void {
    if (this.panning) {
      this.shift.update(([x, y]) => [x + event.movementX, y + event.movementY]);
    } else if (this.painting) {
      this.gesture.move(this.stamp(event));
    }
  }

  protected up(): void {
    this.panning = false;
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
