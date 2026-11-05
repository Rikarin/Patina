import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiInputImports } from '@xui/input';
import { XuiSwitchImports } from '@xui/switch';
import { PaintStore, toTexels } from './paint-store';

/**
 * The layer list and a brush on the composited map. The picture under the brush is the Server's: the same plan that
 * exports, so nothing here composites anything, and a stroke shows when its stamps have come back as a new map.
 */
@Component({
  selector: 'ptn-paint',
  imports: [XuiButtonImports, XuiCalloutImports, XuiInputImports, XuiSwitchImports],
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
        <div class="well relative aspect-square w-full touch-none overflow-hidden" data-testid="paint-canvas">
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

        <div class="flex items-center gap-2">
          <span class="text-foreground-muted text-xs">Size</span>
          <input
            class="min-w-0 flex-1"
            type="range"
            min="1"
            max="256"
            aria-label="Brush size"
            data-testid="paint-size"
            [value]="store.radius()"
            (input)="store.radius.set(number($event))"
          />
          <input
            type="color"
            aria-label="Brush colour"
            data-testid="paint-colour"
            [value]="store.colour()"
            (input)="store.colour.set(text($event))"
          />
        </div>

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
          <span class="flex-1"></span>
          <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-add" (click)="store.addLayer()">
            Add layer
          </button>
        </div>

        <ul class="flex flex-col gap-0.5" aria-label="Layers" data-testid="paint-layers">
          @for (layer of store.layers(); track layer.id) {
            <li class="pane flex items-center gap-2 px-2 py-1" [attr.data-testid]="'paint-layer-' + layer.id">
              <xui-switch
                [checked]="layer.enabled"
                [attr.aria-label]="'Show ' + layer.name"
                (checkedChange)="store.setEnabled(layer, $event)"
              />
              <button
                type="button"
                class="min-w-0 flex-1 truncate text-left text-xs"
                [class.font-semibold]="layer.id === store.layer()"
                [disabled]="layer.kind !== 'paint'"
                (click)="store.layer.set(layer.id)"
              >
                {{ layer.name }}
              </button>
              <span class="readout">{{ layer.kind }}</span>
            </li>
          }
        </ul>
      }
    </div>
  `
})
export class PaintPane {
  protected readonly store = inject(PaintStore);
  protected readonly path = signal('');

  private readonly side = computed<readonly [number, number]>(() => {
    const state = this.store.state();
    return [state?.width ?? 1, state?.height ?? 1];
  });

  private painting = false;

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected number(event: Event): number {
    return Number((event.target as HTMLInputElement).value);
  }

  protected down(event: PointerEvent): void {
    (event.target as HTMLElement).setPointerCapture(event.pointerId);
    this.painting = true;
    this.store.begin(this.stamp(event));
  }

  protected move(event: PointerEvent): void {
    if (this.painting) {
      this.store.move(this.stamp(event));
    }
  }

  protected up(): void {
    if (this.painting) {
      this.painting = false;
      this.store.end();
    }
  }

  private stamp(event: PointerEvent) {
    const box = (event.target as HTMLElement).getBoundingClientRect();
    return toTexels(event.clientX - box.left, event.clientY - box.top, box.width, box.height, this.side());
  }
}
