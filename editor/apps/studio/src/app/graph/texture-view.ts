import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { ChannelView, HistogramChart, type PictureChannel, computeHistogram } from '@patina/studio-ui';
import { XuiButtonImports } from '@xui/button';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { BakeStore } from '../bake/bake-store';
import { Commands } from '../core/commands';
import { ProjectStore } from '../library/project-store';
import { GraphStore } from './graph-store';

const MinZoom = 0.25;
const MaxZoom = 16;
const WheelSpeed = 0.002;

const channels: readonly XuiSegmentedOption<PictureChannel>[] = [
  { value: 'rgba', label: 'RGBA' },
  { value: 'r', label: 'R' },
  { value: 'g', label: 'G' },
  { value: 'b', label: 'B' },
  { value: 'a', label: 'A' }
];

interface FullPicture {
  /** The preview it was asked for: a new one means this is out of date. */
  hash: string;
  url: string;
  width: number;
  height: number;
}

/** The pixels of an image a URL holds, or null where that cannot be done. */
async function pixelsOf(url: string): Promise<ImageData | null> {
  const image = new Image();
  image.src = url;
  try {
    await image.decode();
  } catch {
    return null;
  }

  const canvas = document.createElement('canvas');
  canvas.width = image.naturalWidth;
  canvas.height = image.naturalHeight;
  const context = canvas.getContext('2d', { willReadFrequently: true });
  context?.drawImage(image, 0, 0);
  return context?.getImageData(0, 0, canvas.width, canvas.height) ?? null;
}

/**
 * The selected node's picture at any zoom, its texels square, one channel at a time. What is shown is a reduction of at
 * most 256 pixels on a side until Full size asks for the image the graph computes (ADR 0005), and the readout says which.
 * The histogram is always counted from the reduction, the pixels already here, and says so.
 */
@Component({
  selector: 'ptn-texture-view',
  imports: [ChannelView, HistogramChart, XuiButtonImports, XuiNonIdealStateImports, XuiSegmentedControlImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    @if (bake.shown(); as map) {
      <div
        class="well relative min-h-0 flex-1 cursor-grab overflow-hidden"
        data-testid="texture-view"
        (dblclick)="reset()"
        (pointerdown)="grab($event)"
        (pointermove)="drag($event)"
        (pointerup)="drop()"
        (wheel)="zoomBy($event)"
      >
        <img
          class="absolute top-1/2 left-1/2 max-w-none"
          alt="The baked map"
          data-testid="texture-bake"
          draggable="false"
          style="image-rendering: pixelated"
          [src]="map.url"
          [style.height.px]="(256 * zoom() * map.height) / map.width"
          [style.transform]="transform()"
          [style.width.px]="256 * zoom()"
        />
      </div>
      <div class="chrome-bar flex h-7 shrink-0 items-center gap-2 px-2">
        <span class="readout min-w-0 truncate" data-testid="texture-size">
          {{ map.map }}, {{ map.width }} × {{ map.height }}, shown at 8 bits
        </span>
        <span class="readout ms-auto shrink-0" data-testid="texture-zoom">{{ percent() }}%</span>
      </div>
    } @else if (!project.graphInFront() || store.document() === null) {
      <xui-non-ideal-state
        class="flex-1"
        title="No map shown"
        description="This shows a node's picture in a graph, or a baked map. Choose a map in the Bake dialog to see it here."
        data-testid="texture-empty"
      >
        <button action type="button" xuiButton size="sm" [disabled]="!commands.enabled('bake')" (click)="commands.run('bake')">
          Bake…
        </button>
      </xui-non-ideal-state>
    } @else if (store.selectedNode() === null) {
      <xui-non-ideal-state
        class="flex-1"
        title="No node selected"
        description="Select a node in the graph to see its picture here."
        data-testid="texture-empty"
      />
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
          @if (channel() !== 'rgba' && pixels(); as decoded) {
            <ptn-channel-view
              class="absolute top-1/2 left-1/2 max-w-none"
              data-testid="texture-channel"
              [channel]="channel()"
              [picture]="decoded"
              [style.height.px]="picture.height * zoom()"
              [style.transform]="transform()"
              [style.width.px]="picture.width * zoom()"
            />
          } @else {
            <img
              class="absolute top-1/2 left-1/2 max-w-none"
              alt="The selected node's picture"
              data-testid="texture-image"
              draggable="false"
              style="image-rendering: pixelated"
              [attr.data-hash]="picture.hash"
              [src]="picture.url"
              [style.height.px]="picture.height * zoom()"
              [style.transform]="transform()"
              [style.width.px]="picture.width * zoom()"
            />
          }
        } @else {
          <p class="text-foreground-muted absolute inset-0 m-auto h-fit text-center">No picture yet.</p>
        }
      </div>
      @if (reduction(); as small) {
        <div class="flex shrink-0 flex-col gap-0.5 p-1">
          <ptn-histogram class="h-12" [histogram]="histogram()" [label]="histogramLabel()" />
          <span class="readout text-foreground-muted truncate" data-testid="texture-histogram-label">{{
            histogramLabel()
          }}</span>
        </div>
      }
      <div class="chrome-bar flex h-7 shrink-0 items-center gap-2 px-2">
        <span class="readout min-w-0 truncate" data-testid="texture-size">{{ sizeText() }}</span>
        <span class="readout ms-auto shrink-0" data-testid="texture-zoom">{{ percent() }}%</span>
      </div>
      <div class="chrome-bar flex h-8 shrink-0 items-center gap-2 px-2">
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="texture-full"
          [disabled]="loadingFull() || !reduction() || full() !== null"
          (click)="loadFull()"
        >
          Full size
        </button>
        <xui-segmented-control
          class="ms-auto min-w-0"
          size="sm"
          ariaLabel="Channel"
          [options]="channelOptions"
          [value]="channel()"
          (valueChange)="channel.set($event ?? 'rgba')"
        />
      </div>
    }
  `
})
export class TextureView {
  protected readonly store = inject(GraphStore);
  protected readonly bake = inject(BakeStore);
  protected readonly commands = inject(Commands);
  protected readonly project = inject(ProjectStore);
  protected readonly channelOptions = channels;
  private grabbed: { x: number; y: number } | null = null;
  private readonly offset = signal({ x: 0, y: 0 });
  private readonly reductionPixels = signal<ImageData | null>(null);
  private readonly fullPixels = signal<ImageData | null>(null);

  protected readonly zoom = signal(1);
  protected readonly channel = signal<PictureChannel>('rgba');
  protected readonly loadingFull = signal(false);
  protected readonly percent = computed(() => Math.round(this.zoom() * 100));
  protected readonly transform = computed(() => {
    const { x, y } = this.offset();
    return `translate(calc(-50% + ${String(x)}px), calc(-50% + ${String(y)}px))`;
  });
  protected readonly reduction = computed(() => {
    const node = this.store.selectedNode();
    return node ? this.store.thumbnail(node.id)() : null;
  });
  private readonly heldFull = signal<FullPicture | null>(null);
  protected readonly full = computed(() => {
    const held = this.heldFull();
    return held && held.hash === this.reduction()?.hash ? held : null;
  });
  protected readonly shown = computed(() => {
    const reduction = this.reduction();
    const full = this.full();
    return reduction && full ? { ...reduction, ...full } : reduction;
  });
  protected readonly pixels = computed(() => (this.full() ? this.fullPixels() : this.reductionPixels()));
  protected readonly histogram = computed(() => {
    const pixels = this.reductionPixels();
    return pixels ? computeHistogram(pixels.data) : null;
  });
  protected readonly histogramLabel = computed(() => {
    const reduction = this.reduction();
    return reduction
      ? `Histogram of the ${String(reduction.width)} × ${String(reduction.height)} reduction`
      : 'Histogram';
  });
  protected readonly sizeText = computed(() => {
    const picture = this.shown();
    if (!picture) {
      return '—';
    }

    return `${String(picture.width)} × ${String(picture.height)}${this.full() ? '' : ', a reduction'}`;
  });

  constructor() {
    // A node chosen after a map was shown is what someone now wants to look at.
    effect(() => {
      if (this.store.selectedNode() !== null) {
        untracked(() => {
          this.bake.hide();
        });
      }
    });

    // A new picture has its pixels read once. What arrives after the picture moved on is dropped.
    effect(() => {
      const url = this.reduction()?.url;
      untracked(() => {
        this.reductionPixels.set(null);
      });
      if (url) {
        void pixelsOf(url).then(pixels => {
          if (url === untracked(this.reduction)?.url) {
            this.reductionPixels.set(pixels);
          }
        });
      }
    });

    // A full picture outlives nothing: the object URL goes with the preview it was asked for.
    effect(onCleanup => {
      const url = this.heldFull()?.url;
      onCleanup(() => {
        if (url) {
          URL.revokeObjectURL(url);
        }
      });
    });
  }

  protected async loadFull(): Promise<void> {
    const node = this.store.selectedNode();
    const reduction = this.reduction();
    if (!node || !reduction) {
      return;
    }

    this.loadingFull.set(true);
    const port = this.store.document()?.previews.get(node.id)?.port ?? 'Out';
    const image = await this.store.fullImage(node.id, port);
    this.loadingFull.set(false);
    if (image && this.reduction()?.hash === reduction.hash) {
      const url = URL.createObjectURL(image.blob);
      this.heldFull.set({ hash: reduction.hash, url, width: image.width, height: image.height });
      this.fullPixels.set(await pixelsOf(url));
    }
  }

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
