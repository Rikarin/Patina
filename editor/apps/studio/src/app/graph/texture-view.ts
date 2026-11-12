import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import { ChannelView, HistogramChart, type PictureChannel, computeHistogram } from '@patina/studio-ui';
import { XuiButtonImports } from '@xui/button';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { BakeStore } from '../bake/bake-store';
import { Commands } from '../core/commands';
import { termName } from '../core/labels';
import { ProjectStore } from '../library/project-store';
import { GraphStore } from './graph-store';

const MinZoom = 0.25;
const MaxZoom = 64;
const WheelSpeed = 0.002;
/** How much of the well a fitted picture fills, so its edge shows. */
const FitShare = 0.9;
/** The copies across and down that Tile shows, with the picture itself in the middle. */
const TileCount = 3;
const Tiles = Array.from({ length: TileCount * TileCount }, (_, index) => index);

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

/** A texel's four lanes as the decoded picture holds them, 0 to 1. */
export function texelText(pixels: ImageData, x: number, y: number): string {
  const at = (y * pixels.width + x) * 4;
  const lanes = [...pixels.data.slice(at, at + 4)].map(value => (value / 255).toFixed(3));
  return `${String(x)}, ${String(y)}  ${lanes.join(' ')}`;
}

/**
 * A node's picture at any zoom, its texels square, one channel at a time, over a checkerboard where it is see-through.
 * It follows the selected node, or the one pinned, and shows the picture at the size the graph computes it (ADR 0005):
 * the 256-pixel reduction is on screen until the full picture arrives, and the readout says which. Tile shows it three
 * by three, to see the seams. The histogram is counted from the reduction, the pixels already here, and says so.
 */
@Component({
  selector: 'ptn-texture-view',
  imports: [ChannelView, HistogramChart, XuiButtonImports, XuiNonIdealStateImports, XuiSegmentedControlImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  styles: `
    .checker {
      background: repeating-conic-gradient(#808080 0 25%, #b0b0b0 0 50%) 0 0 / 16px 16px;
    }
  `,
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
          alt="The mesh map"
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
          {{ mapName(map.map) }}, {{ map.width }} × {{ map.height }}, shown at 8 bits
        </span>
        <span class="readout ms-auto shrink-0" data-testid="texture-zoom">{{ percent() }}%</span>
      </div>
    } @else if (!project.graphInFront() || store.document() === null) {
      <xui-non-ideal-state
        class="flex-1"
        title="No map shown"
        description="This shows a node's picture in a graph, or a mesh map. Choose a map in the Bake dialog to see it here."
        data-testid="texture-empty"
      >
        <button
          action
          type="button"
          xuiButton
          size="sm"
          [disabled]="!commands.enabled('bake')"
          (click)="commands.run('bake')"
        >
          Bake…
        </button>
      </xui-non-ideal-state>
    } @else if (store.viewedNode() === null) {
      <xui-non-ideal-state
        class="flex-1"
        title="No node selected"
        description="Select a node in the graph to see its picture here."
        data-testid="texture-empty"
      />
    } @else {
      @let picture = shown();
      <div
        #well
        class="well relative min-h-0 flex-1 cursor-grab overflow-hidden"
        data-testid="texture-view"
        [attr.data-tiled]="tiled() || null"
        (dblclick)="reset()"
        (pointerdown)="grab($event)"
        (pointermove)="drag($event); hover($event)"
        (pointerleave)="texel.set(null)"
        (pointerup)="drop()"
        (wheel)="zoomBy($event)"
      >
        @if (picture) {
          @let side = displayed();
          <div
            #sheet
            class="absolute top-1/2 left-1/2 grid"
            [style.grid-template-columns]="'repeat(' + across() + ', ' + side.width + 'px)'"
            [style.transform]="transform()"
          >
            @for (tile of tiles(); track tile) {
              <div class="checker" [style.height.px]="side.height" [style.width.px]="side.width">
                @if (channel() !== 'rgba' && pixels(); as decoded) {
                  <ptn-channel-view
                    class="block size-full"
                    data-testid="texture-channel"
                    [channel]="channel()"
                    [picture]="decoded"
                  />
                } @else {
                  <img
                    class="block size-full max-w-none"
                    alt="The node's picture"
                    data-testid="texture-image"
                    draggable="false"
                    style="image-rendering: pixelated"
                    [attr.data-hash]="picture.hash"
                    [src]="picture.url"
                  />
                }
              </div>
            }
          </div>
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
        <span class="readout shrink-0" data-testid="texture-size">{{ sizeText() }}</span>
        @if (texel(); as at) {
          <span
            class="readout text-foreground-muted min-w-0 truncate"
            data-testid="texture-texel"
            [title]="texelTitle"
            >{{ at }}</span
          >
        }
        <span class="readout ms-auto shrink-0" data-testid="texture-zoom">{{ percent() }}%</span>
      </div>
      <div class="chrome-bar flex h-8 shrink-0 items-center gap-1 px-2">
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="texture-pin"
          [active]="store.pinned() !== null"
          [attr.aria-pressed]="store.pinned() !== null"
          [title]="pinTitle()"
          (click)="togglePin()"
        >
          Pin
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="texture-tile"
          title="Shows the picture three by three, to see whether it tiles"
          [active]="tiled()"
          [attr.aria-pressed]="tiled()"
          (click)="tiled.set(!tiled())"
        >
          Tile
        </button>
        <xui-segmented-control
          class="ms-auto min-w-0"
          size="sm"
          aria-label="Channel"
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
  /** A bake map as the app calls it: Ambient Occlusion, not `ao` (#198). */
  protected readonly mapName = termName;
  protected readonly texelTitle =
    'The texel under the pointer, and its red, green, blue and alpha as decoded at 8 bits';
  private readonly well = viewChild<ElementRef<HTMLElement>>('well');
  private readonly sheet = viewChild<ElementRef<HTMLElement>>('sheet');
  private grabbed: { x: number; y: number } | null = null;
  private readonly offset = signal({ x: 0, y: 0 });
  private readonly reductionPixels = signal<ImageData | null>(null);
  private readonly fullPixels = signal<ImageData | null>(null);
  /** The well's size in pixels, which the picture is fitted to. */
  private readonly room = signal({ width: 0, height: 0 });
  /** The hash of the full picture last asked for, so one evaluation is asked for once. */
  private asked: string | null = null;

  /** One on the picture's fitted size: the readout says what that is in the picture's own pixels. */
  protected readonly zoom = signal(1);
  protected readonly channel = signal<PictureChannel>('rgba');
  protected readonly tiled = signal(false);
  protected readonly loadingFull = signal(false);
  protected readonly texel = signal<string | null>(null);
  protected readonly across = computed(() => (this.tiled() ? TileCount : 1));
  protected readonly tiles = computed(() => (this.tiled() ? Tiles : Tiles.slice(0, 1)));

  protected readonly transform = computed(() => {
    const { x, y } = this.offset();
    return `translate(calc(-50% + ${String(x)}px), calc(-50% + ${String(y)}px))`;
  });
  protected readonly reduction = computed(() => {
    const node = this.store.viewedNode();
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

  /**
   * The size one copy of the picture is drawn at: fitted to the well at a zoom of one, with its copies when tiled,
   * whichever of the reduction or the full picture is on screen, so the full one arriving does not change it.
   */
  protected readonly displayed = computed(() => {
    const picture = this.shown();
    const { width, height } = this.room();
    if (!picture || picture.width <= 0 || picture.height <= 0) {
      return { width: 0, height: 0 };
    }

    // Tiled, the whole sheet of copies is what fits.
    const sheet = this.across();
    const fit =
      width > 0 && height > 0
        ? Math.min(width / (picture.width * sheet), height / (picture.height * sheet)) * FitShare
        : 1;
    const scale = fit * this.zoom();
    return { width: picture.width * scale, height: picture.height * scale };
  });
  protected readonly percent = computed(() => {
    const picture = this.shown();
    return picture && picture.width > 0
      ? Math.round((this.displayed().width / picture.width) * 100)
      : Math.round(this.zoom() * 100);
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

    const size = `${String(picture.width)} × ${String(picture.height)}`;
    return this.full() ? size : `${size}, a reduction${this.loadingFull() ? ' until the full size comes' : ''}`;
  });
  protected readonly pinTitle = computed(() => {
    const pinned = this.store.pinned();
    const type = pinned ? this.store.types().get(pinned.type) : undefined;
    return pinned
      ? `Showing ${type?.title ?? pinned.type} whatever is selected. Click to follow the selection again.`
      : 'Keeps showing this node, here and in the viewport, while you select others';
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

    // The node on view is shown at the size the graph computes it, from each evaluation that changes its picture.
    effect(() => {
      const node = this.store.viewedNode();
      const hash = this.reduction()?.hash;
      if (node && hash && hash !== this.asked) {
        this.asked = hash;
        untracked(() => void this.loadFull(node.id, hash));
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

    // The well comes and goes with the picture, and the picture is fitted to it.
    effect(onCleanup => {
      const element = this.well()?.nativeElement;
      if (!element) {
        return;
      }

      const observer = new ResizeObserver(() => {
        this.room.set({ width: element.clientWidth, height: element.clientHeight });
      });
      observer.observe(element);
      onCleanup(() => {
        observer.disconnect();
      });
    });
  }

  private async loadFull(node: number, hash: string): Promise<void> {
    this.loadingFull.set(true);
    const port = this.store.document()?.previews.get(node)?.port ?? 'Out';
    const image = await this.store.fullImage(node, port);
    if (this.reduction()?.hash !== hash) {
      return;
    }

    this.loadingFull.set(false);
    if (image) {
      const url = URL.createObjectURL(image.blob);
      this.heldFull.set({ hash, url, width: image.width, height: image.height });
      this.fullPixels.set(await pixelsOf(url));
    } else {
      // Asked again with the next picture, or on choosing the node again.
      this.asked = null;
    }
  }

  protected togglePin(): void {
    this.store.pin(this.store.pinned() ? null : (this.store.viewedNode()?.id ?? null));
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

  /** The texel under the pointer, from whichever copy of the picture it is over. */
  protected hover(event: PointerEvent): void {
    const sheet = this.sheet()?.nativeElement;
    const pixels = this.pixels();
    const { width, height } = this.displayed();
    if (!sheet || !pixels || width <= 0 || height <= 0) {
      this.texel.set(null);
      return;
    }

    const box = sheet.getBoundingClientRect();
    const [across, down] = [(event.clientX - box.left) / width, (event.clientY - box.top) / height];
    if (across < 0 || down < 0 || across >= this.across() || down >= this.across()) {
      this.texel.set(null);
      return;
    }

    const x = Math.min(pixels.width - 1, Math.floor((across % 1) * pixels.width));
    const y = Math.min(pixels.height - 1, Math.floor((down % 1) * pixels.height));
    this.texel.set(texelText(pixels, x, y));
  }
}
