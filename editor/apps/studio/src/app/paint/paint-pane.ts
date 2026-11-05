import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiInputImports } from '@xui/input';
import { BrushPanel } from './brush-panel';
import { LayerPanel } from './layer-panel';
import { PaintStore, pressureOf, toTexels } from './paint-store';
import { StrokeGesture } from './stroke-gesture';

/**
 * The stack, the brush, and the atlas to paint on. The picture under the brush is the Server's: the same plan that
 * exports, so nothing here composites anything, and a stroke shows when its stamps have come back as a new map. With a
 * mesh bound a stroke here carries on over the seams it crosses, and the Viewport pane paints the mesh itself.
 */
@Component({
  selector: 'ptn-paint',
  imports: [BrushPanel, LayerPanel, XuiButtonImports, XuiCalloutImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex min-h-0 flex-1 flex-col gap-2 overflow-x-hidden overflow-y-auto p-3">
      <div class="flex gap-1">
        <input
          class="min-w-0 flex-1"
          xuiInput
          autocomplete="off"
          aria-label="Stack file"
          data-testid="paint-path"
          placeholder="/path/to/stack.vxlayers"
          [value]="path()"
          (input)="path.set(text($event))"
        />
        <button type="button" xuiButton size="sm" data-testid="paint-open" (click)="store.open(path(), false)">
          Open
        </button>
        <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-new" (click)="store.open(path(), true)">
          New
        </button>
      </div>

      @if (store.failure(); as failure) {
        <xui-callout variant="error" data-testid="paint-failure">{{ failure }}</xui-callout>
      }

      @if (store.state(); as state) {
        <div class="flex items-center gap-1">
          <input
            class="min-w-0 flex-1"
            xuiInput
            size="sm"
            autocomplete="off"
            aria-label="Mesh file"
            data-testid="paint-mesh"
            placeholder="The mesh to paint on"
            [value]="mesh() ?? state.model"
            (input)="mesh.set(text($event))"
          />
          <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-mesh-viewport" (click)="adopt()">
            From viewport
          </button>
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="paint-bind"
            [disabled]="!(mesh() ?? state.model)"
            (click)="bind(state.model)"
          >
            Paint on mesh
          </button>
        </div>

        <div class="well relative aspect-square w-full shrink-0 touch-none overflow-hidden" data-testid="paint-canvas">
          @if (store.picture(); as picture) {
            <img
              class="size-full select-none"
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
          }
        </div>

        <div class="flex flex-wrap items-center gap-1">
          @for (channel of state.channels; track channel.usage) {
            <button
              type="button"
              xuiButton
              size="sm"
              [variant]="channel.usage === store.channel() ? 'default' : 'ghost'"
              [attr.data-testid]="'paint-channel-' + channel.usage"
              (click)="store.showChannel(channel.usage)"
            >
              {{ channel.usage }}
            </button>
          }
        </div>

        <ptn-brush-panel />

        <div class="flex items-center gap-1">
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            data-testid="paint-undo"
            [disabled]="!state.history.canUndo"
            (click)="store.act('undo')"
          >
            Undo
          </button>
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            data-testid="paint-redo"
            [disabled]="!state.history.canRedo"
            (click)="store.act('redo')"
          >
            Redo
          </button>
          <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-save" (click)="store.act('save')">
            Save
          </button>
        </div>

        <ptn-layer-panel />
      }
    </div>
  `
})
export class PaintPane {
  protected readonly store = inject(PaintStore);
  protected readonly path = signal('');
  /** What the mesh field says while it differs from the document, or null. */
  protected readonly mesh = signal<string | null>(null);
  protected readonly gesture = new StrokeGesture(this.store, () => undefined);

  private readonly side = computed<readonly [number, number]>(() => {
    const state = this.store.state();
    return [state?.width ?? 1, state?.height ?? 1];
  });

  private painting = false;

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async adopt(): Promise<void> {
    this.mesh.set(await this.store.viewportMesh());
  }

  protected async bind(held: string): Promise<void> {
    await this.store.bindMesh(this.mesh() ?? held);
    this.mesh.set(null);
  }

  protected down(event: PointerEvent): void {
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
