import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { GraphStore } from './graph-store';

const MinZoom = 0.25;
const MaxZoom = 16;
const WheelSpeed = 0.002;

/**
 * The selected node's picture at any zoom, its texels square. Previews are at most 256 pixels on a side and there is no
 * full-size endpoint yet (ADR 0005), so past 1:1 this magnifies a reduction, and the readout says so.
 */
@Component({
  selector: 'ptn-texture-view',
  imports: [XuiNonIdealStateImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    @if (store.selectedNode() === null) {
      <xui-non-ideal-state class="flex-1" description="Select a node to see its picture." />
    } @else {
      @let picture = shown();
      <div
        class="well relative min-h-0 flex-1 cursor-grab overflow-hidden"
        data-testid="texture-view"
        (dblclick)="reset()"
        (pointerdown)="grab($event)"
        (pointermove)="drag($event)"
        (pointerup)="drop()"
        (wheel)="zoomBy($event)"
      >
        @if (picture) {
          <img
            class="absolute top-1/2 left-1/2 max-w-none"
            alt="The selected node's preview"
            data-testid="texture-image"
            draggable="false"
            style="image-rendering: pixelated"
            [attr.data-hash]="picture.hash"
            [src]="picture.url"
            [style.height.px]="picture.height * zoom()"
            [style.transform]="transform()"
            [style.width.px]="picture.width * zoom()"
          />
        } @else {
          <p class="text-foreground-muted absolute inset-0 m-auto h-fit text-center">No picture yet.</p>
        }
      </div>
      <div class="chrome-bar flex h-7 shrink-0 items-center gap-3 px-2">
        <span class="readout truncate">{{
          picture ? picture.width + ' × ' + picture.height + ', a reduction' : '—'
        }}</span>
        <span class="readout ms-auto shrink-0" data-testid="texture-zoom">{{ percent() }}%</span>
      </div>
    }
  `
})
export class TextureView {
  protected readonly store = inject(GraphStore);
  private grabbed: { x: number; y: number } | null = null;
  private readonly offset = signal({ x: 0, y: 0 });

  protected readonly zoom = signal(1);
  protected readonly percent = computed(() => Math.round(this.zoom() * 100));
  protected readonly transform = computed(() => {
    const { x, y } = this.offset();
    return `translate(calc(-50% + ${String(x)}px), calc(-50% + ${String(y)}px))`;
  });
  protected readonly shown = computed(() => {
    const node = this.store.selectedNode();
    return node ? this.store.thumbnail(node.id)() : null;
  });

  protected reset(): void {
    this.zoom.set(1);
    this.offset.set({ x: 0, y: 0 });
  }

  protected zoomBy(event: WheelEvent): void {
    event.preventDefault();
    this.zoom.update(zoom => Math.min(MaxZoom, Math.max(MinZoom, zoom * Math.exp(-event.deltaY * WheelSpeed))));
  }

  protected grab(event: PointerEvent): void {
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
    this.grabbed = { x: event.clientX, y: event.clientY };
  }

  protected drag(event: PointerEvent): void {
    const grabbed = this.grabbed;
    if (grabbed) {
      this.offset.update(({ x, y }) => ({ x: x + event.clientX - grabbed.x, y: y + event.clientY - grabbed.y }));
      this.grabbed = { x: event.clientX, y: event.clientY };
    }
  }

  protected drop(): void {
    this.grabbed = null;
  }
}
