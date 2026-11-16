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
import { provideIcons } from '@ng-icons/core';
import { matMoreHoriz } from '@ng-icons/material-icons/baseline';
import type { GraphHistogram, GraphSample } from '@patina/api-client';
import { ChannelView, type Histogram, HistogramChart, type PictureChannel } from '@patina/studio-ui';
import { XuiButtonImports } from '@xui/button';
import { XuiMenuImports } from '@xui/menu';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { BakeStore } from '../bake/bake-store';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { IconImports } from '../core/icons';
import { termName } from '../core/labels';
import { messageOf } from '../core/messages';
import { amount, length, percent } from '../core/numbers';
import { ProjectStore } from '../library/project-store';
import { fitBar } from './bar-fit';
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
  /** The full picture's own hash, by which the Server saves it. */
  file: string;
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

/** How long the pointer rests on a texel before the Server is asked for it as stored, in milliseconds. */
const SampleWait = 150;

/** A texel as the graph stored it: its place in the picture at full size and every lane at its own precision. */
export function storedText(texel: GraphSample): string {
  const lanes = texel.values.map(value => amount(value, 5));
  return `${String(texel.x)}, ${String(texel.y)}  ${lanes.join(' ')}`;
}

/** What a picture's format holds of each lane, in the readout's words. */
export function precisionOf(format: string): string {
  return format.includes('32Float') ? '32-bit float' : format.includes('16Float') ? '16-bit float' : '8 bits';
}

/** A graph's physical size in centimetres, when it has one: both sides above zero. */
export interface PhysicalSize {
  width: number;
  height: number;
}

/**
 * The proportions one copy of a picture is drawn at, in the picture's own pixels across (#184): its pixels' own, or
 * with Physical on, its graph's physical size's, so a 2 m by 1 m wall laid out on a square map is shown twice as wide
 * as it is tall. The width stays the picture's, so the zoom readout still says how large its pixels are shown.
 */
export function proportionsOf(
  picture: { width: number; height: number },
  physical: PhysicalSize | null
): { width: number; height: number } {
  return physical ? { width: picture.width, height: (picture.width * physical.height) / physical.width } : picture;
}

/** The physical size a graph says, in the readout's words. */
export function physicalText(size: PhysicalSize): string {
  return `${length(size.width / 100)} × ${length(size.height / 100)}`;
}

/** A texel's four lanes as the decoded picture holds them, 0 to 1. */
export function texelText(pixels: ImageData, x: number, y: number): string {
  const at = (y * pixels.width + x) * 4;
  const lanes = [...pixels.data.slice(at, at + 4)].map(value => amount(value / 255, 3));
  return `${String(x)}, ${String(y)}  ${lanes.join(' ')}`;
}

/**
 * A node's picture at any zoom, its texels square, one channel at a time, over a checkerboard where it is see-through.
 * It follows the selected node, or the one pinned, and shows the picture at the size the graph computes it (ADR 0005):
 * the 256-pixel reduction is on screen until the full picture arrives, and the readout says which. Tile shows it three
 * by three, to see the seams. The histogram is the Server's, counted on the GPU from the picture at full size (#172),
 * and says so; the last one stays until the next picture's arrives.
 */
@Component({
  selector: 'ptn-texture-view',
  imports: [
    ChannelView,
    HistogramChart,
    IconImports,
    XuiButtonImports,
    XuiMenuImports,
    XuiNonIdealStateImports,
    XuiSegmentedControlImports,
    XuiSelectImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  viewProviders: [provideIcons({ matMoreHoriz })],
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
      <div class="pane-bar gap-2 px-2" role="group" aria-label="Picture">
        <span class="readout min-w-0 truncate" data-testid="texture-size">
          {{ mapName(map.map) }}, {{ map.width }} × {{ map.height }}, shown at 8 bits
        </span>
        <span class="readout ms-auto shrink-0" data-testid="texture-zoom">{{ percent() }}</span>
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
        (pointerleave)="leave()"
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
      @if (counted(); as levels) {
        <div class="flex shrink-0 flex-col gap-0.5 p-1">
          <ptn-histogram class="h-12" [histogram]="histogram()" [label]="histogramLabel()" />
          <span class="readout text-foreground-muted truncate" data-testid="texture-histogram-label">{{
            histogramLabel()
          }}</span>
        </div>
      }
      <div class="pane-bar gap-2 px-2" role="group" aria-label="Picture">
        <!-- It gives way to the zoom rather than running past the pane's end, and says all of itself on hover. -->
        <span class="readout min-w-0 truncate" data-testid="texture-size" [title]="sizeText()">{{ sizeText() }}</span>
        @if (stored(); as exact) {
          <span
            class="readout text-foreground-muted min-w-0 truncate"
            data-testid="texture-texel"
            data-stored="true"
            [title]="exact.title"
            >{{ exact.text }}</span
          >
        } @else if (texel(); as at) {
          <span
            class="readout text-foreground-muted min-w-0 truncate"
            data-testid="texture-texel"
            [title]="texelTitle"
            >{{ at }}</span
          >
        }
        <span class="readout ms-auto shrink-0" data-testid="texture-zoom">{{ percent() }}</span>
      </div>
      <!--
        The bar keeps to one line, as every pane's does (#200, #184). As it narrows, Copy Image and Save Image… say
        Copy and Save…, then the channel's switch becomes a list, which keeps its labels whole in a fraction of the room;
        the buttons keep their size and their titles say what they do, and at the last Copy and Save go into a menu.
        Which of those it takes is measured (bar-fit.ts).
      -->
      <div
        #bar
        class="pane-bar flex-nowrap gap-1 overflow-hidden px-2"
        role="toolbar"
        aria-label="2D View"
        data-testid="texture-bar"
        [attr.data-fit]="fit()"
      >
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="shrink-0"
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
          class="shrink-0"
          data-testid="texture-tile"
          title="Shows the picture three by three, to see whether it tiles"
          [active]="tiled()"
          [attr.aria-pressed]="tiled()"
          (click)="tiled.set(!tiled())"
        >
          Tile
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="shrink-0"
          data-testid="texture-physical"
          [active]="proportioned() !== null"
          [attr.aria-pressed]="proportioned() !== null"
          [disabled]="physicalSize() === null"
          [title]="physicalTitle()"
          (click)="physical.set(!physical())"
        >
          Physical
        </button>
        @if (fit() !== 'menu') {
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            class="shrink-0"
            data-testid="texture-copy"
            [disabled]="!picture"
            [title]="copyTitle()"
            (click)="copyImage()"
          >
            @if (copied() === 'copied') {
              Copied
            } @else if (copied() === 'refused') {
              Not Copied
            } @else {
              {{ fit() === 'full' ? 'Copy Image' : 'Copy' }}
            }
          </button>
          @if (canSave) {
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              class="shrink-0"
              data-testid="texture-save"
              [disabled]="!full()"
              [title]="saveTitle()"
              (click)="saveImage()"
            >
              {{ fit() === 'full' ? 'Save Image…' : 'Save…' }}
            </button>
          }
        } @else {
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            class="shrink-0"
            aria-label="More"
            title="Copy Image and Save Image…"
            data-testid="texture-more"
            [disabled]="!picture"
            [xuiMenuTriggerFor]="moreMenu"
          >
            <ng-icon xui size="sm" name="matMoreHoriz" />
          </button>
        }
        <span class="min-w-0 flex-1"></span>
        @if (fit() === 'full' || fit() === 'short') {
          <xui-segmented-control
            class="shrink-0"
            size="sm"
            aria-label="Channel"
            data-testid="texture-channels"
            [options]="channelOptions"
            [value]="channel()"
            (valueChange)="channel.set($event ?? 'rgba')"
          />
        } @else {
          <xui-select
            class="w-20 min-w-14 [&_button]:text-xs"
            style="--control-height-md: 1.5rem; --control-padding-md: 0.375rem"
            aria-label="Channel"
            data-testid="texture-channel-narrow"
            [filterable]="false"
            [itemText]="channelName"
            [items]="channelValues"
            [matchTargetWidth]="false"
            [value]="channel()"
            (valueChange)="channel.set($event ?? 'rgba')"
          />
        }
      </div>
      <ng-template #moreMenu>
        <xui-menu aria-label="More" data-testid="texture-more-menu">
          <button type="button" xuiMenuItem data-testid="texture-copy" (triggered)="copyImage()">Copy Image</button>
          @if (canSave) {
            <button type="button" xuiMenuItem data-testid="texture-save" [disabled]="!full()" (triggered)="saveImage()">
              Save Image…
            </button>
          }
        </xui-menu>
      </ng-template>
    }
  `
})
export class TextureView {
  protected readonly store = inject(GraphStore);
  protected readonly bake = inject(BakeStore);
  protected readonly commands = inject(Commands);
  protected readonly project = inject(ProjectStore);
  protected readonly channelOptions = channels;
  /** The channels as the narrow bar's list offers them, under the switch's own labels. */
  protected readonly channelValues = channels.map(option => option.value);
  protected readonly channelName = (value: PictureChannel): string =>
    channels.find(option => option.value === value)?.label ?? value;
  /** A bake map as the app calls it: Ambient Occlusion, not `ao` (#198). */
  protected readonly mapName = termName;
  protected readonly texelTitle =
    'The texel under the pointer, and its red, green, blue and alpha as decoded at 8 bits';
  private readonly well = viewChild<ElementRef<HTMLElement>>('well');
  private readonly sheet = viewChild<ElementRef<HTMLElement>>('sheet');
  private readonly bar = viewChild<ElementRef<HTMLElement>>('bar');
  /** How the 2D View's bar fits its pane, measured as it and what it says change size. */
  protected readonly fit = fitBar(this.bar, () => this.copied());
  private grabbed: { x: number; y: number } | null = null;
  private readonly offset = signal({ x: 0, y: 0 });
  private readonly reductionPixels = signal<ImageData | null>(null);
  private readonly fullPixels = signal<ImageData | null>(null);
  /** The well's size in pixels, which the picture is fitted to. */
  private readonly room = signal({ width: 0, height: 0 });
  /** The hash of the full picture last asked for, so one evaluation is asked for once. */
  private asked: string | null = null;
  /** The hash of the picture last counted, so one evaluation is counted once. */
  private countedHash: string | null = null;

  /** One on the picture's fitted size: the readout says what that is in the picture's own pixels. */
  protected readonly zoom = signal(1);
  protected readonly channel = signal<PictureChannel>('rgba');
  protected readonly tiled = signal(false);
  /** Whether a picture is drawn in its graph's physical proportions rather than its pixels' (#184). */
  protected readonly physical = signal(false);
  /** The graph's physical size, when it says one. */
  protected readonly physicalSize = computed((): PhysicalSize | null => {
    const info = this.store.document()?.info;
    const [width, height] = [info?.physicalWidth ?? 0, info?.physicalHeight ?? 0];
    return width > 0 && height > 0 ? { width, height } : null;
  });
  /** The physical size the picture is drawn in the proportions of, or null for its pixels' own. */
  protected readonly proportioned = computed(() => (this.physical() ? this.physicalSize() : null));
  protected readonly physicalTitle = computed(() => {
    const size = this.physicalSize();
    return size
      ? `Shows the picture in the proportions of the graph's physical size, ${physicalText(size)}, rather than its pixels'`
      : 'The graph has no physical size. Give it one in Graph Settings to see the picture in its proportions';
  });
  protected readonly loadingFull = signal(false);
  /** Whether the picture on screen was just copied, which the button says until the picture changes. */
  protected readonly copied = signal<'copied' | 'refused' | null>(null);
  protected readonly texel = signal<string | null>(null);
  /**
   * The texel under the pointer as the graph stored it, once the pointer has rested on it and the Server has read it
   * (#184): a height below zero or a light above one, which the preview's eight bits cannot hold, reads as it is.
   */
  protected readonly stored = signal<{ text: string; title: string } | null>(null);
  /** The texel the pointer is on, as the place a sample was asked for, so a late answer for another lands nowhere. */
  private pointed: string | null = null;
  private sampling: ReturnType<typeof setTimeout> | undefined;
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
    const shape = proportionsOf(picture, this.proportioned());
    const fit =
      width > 0 && height > 0 ? Math.min(width / (shape.width * sheet), height / (shape.height * sheet)) * FitShare : 1;
    const scale = fit * this.zoom();
    return { width: shape.width * scale, height: shape.height * scale };
  });
  /** How large the picture is shown, as a share of its own size. */
  protected readonly percent = computed(() => {
    const picture = this.shown();
    return percent(picture && picture.width > 0 ? this.displayed().width / picture.width : this.zoom());
  });
  protected readonly pixels = computed(() => (this.full() ? this.fullPixels() : this.reductionPixels()));
  /** The newest histogram the Server counted of the node on view, kept until the next one arrives. */
  protected readonly counted = signal<GraphHistogram | null>(null);
  protected readonly histogram = computed((): Histogram | null => {
    const levels = this.counted();
    return levels ? { r: levels.red, g: levels.green, b: levels.blue, luma: levels.luma } : null;
  });
  protected readonly histogramLabel = computed(() => {
    const levels = this.counted();
    return levels
      ? `Histogram of the ${String(levels.width)} × ${String(levels.height)} picture, counted on the GPU`
      : 'Histogram';
  });
  protected readonly sizeText = computed(() => {
    const picture = this.shown();
    if (!picture) {
      return '—';
    }

    const physical = this.proportioned();
    const shownAs = physical ? `, shown as ${physicalText(physical)}` : '';
    const size = `${String(picture.width)} × ${String(picture.height)}${shownAs}`;
    return this.full() ? size : `${size}, a reduction${this.loadingFull() ? ' until the full size comes' : ''}`;
  });
  protected readonly copyTitle = computed(() =>
    this.full()
      ? 'Copies the picture at the size the graph computes it, as a PNG'
      : 'Copies the picture on screen, as a PNG. Until the full size comes, that is the reduction'
  );
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
        this.copied.set(null);
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

    // Each new picture of the node on view is counted, at full size, by the Server. Another node's is not kept.
    effect(() => {
      const node = this.store.viewedNode();
      const hash = this.reduction()?.hash;
      if (node?.id !== untracked(this.counted)?.node) {
        this.counted.set(null);
      }

      if (node && hash && hash !== this.countedHash) {
        this.countedHash = hash;
        untracked(() => void this.count(node.id, hash));
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
      this.heldFull.set({ hash, file: image.hash, url, width: image.width, height: image.height });
      this.fullPixels.set(await pixelsOf(url));
    } else {
      // Asked again with the next picture, or on choosing the node again.
      this.asked = null;
    }
  }

  private async count(node: number, hash: string): Promise<void> {
    const port = this.store.document()?.previews.get(node)?.port ?? 'Out';
    const levels = await this.store.histogram(node, port);
    if (this.reduction()?.hash !== hash) {
      return;
    }

    if (levels) {
      this.counted.set(levels);
    } else {
      // Counted again with the next picture, or on choosing the node again.
      this.countedHash = null;
    }
  }

  /** Saving asks where, which only the desktop app can. */
  protected readonly canSave = desktop() !== undefined;
  protected readonly saveTitle = computed(
    () =>
      this.saveRefusal() ??
      (this.full() ? 'Saves the picture at the size the graph computes it, as a PNG' : 'Waiting for the full picture')
  );
  private readonly saveRefusal = signal<string | null>(null);

  /** Saves the full picture as a PNG where the person says (#184): the Server writes it from its cache. */
  protected async saveImage(): Promise<void> {
    const full = this.full();
    const name = this.store.viewedNode()?.type.split('/').pop() ?? 'Picture';
    const file = full ? await desktop()?.saveImage(`${name}.png`) : null;
    if (!full || !file) {
      return;
    }

    try {
      await this.store.savePicture(full.file, file);
      this.saveRefusal.set(null);
    } catch (failure) {
      this.saveRefusal.set(messageOf(failure));
    }
  }

  /** Puts the picture on screen on the clipboard as the PNG it is: the full size once it has come (#184). */
  protected async copyImage(): Promise<void> {
    const url = this.shown()?.url;
    if (!url) {
      return;
    }

    try {
      const blob = await (await fetch(url)).blob();
      await navigator.clipboard.write([new ClipboardItem({ [blob.type || 'image/png']: blob })]);
      this.copied.set('copied');
    } catch {
      // The clipboard is the system's, and it can say no; the button says so until the picture changes.
      this.copied.set('refused');
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
      this.leave();
      return;
    }

    const box = sheet.getBoundingClientRect();
    const [across, down] = [(event.clientX - box.left) / width, (event.clientY - box.top) / height];
    if (across < 0 || down < 0 || across >= this.across() || down >= this.across()) {
      this.leave();
      return;
    }

    const x = Math.min(pixels.width - 1, Math.floor((across % 1) * pixels.width));
    const y = Math.min(pixels.height - 1, Math.floor((down % 1) * pixels.height));
    this.texel.set(texelText(pixels, x, y));
    const place = `${String(across % 1)} ${String(down % 1)}`;
    if (place !== this.pointed) {
      this.pointed = place;
      this.stored.set(null);
      clearTimeout(this.sampling);
      this.sampling = setTimeout(() => void this.sample(across % 1, down % 1, place), SampleWait);
    }
  }

  protected leave(): void {
    this.texel.set(null);
    this.stored.set(null);
    this.pointed = null;
    clearTimeout(this.sampling);
  }

  /** Asks the Server for the texel the pointer rests on as stored, and shows it if the pointer is still there. */
  private async sample(u: number, v: number, place: string): Promise<void> {
    const node = this.store.viewedNode();
    if (!node) {
      return;
    }

    const port = this.store.document()?.previews.get(node.id)?.port ?? 'Out';
    const texel = await this.store.sample(node.id, port, u, v);
    if (texel && this.pointed === place) {
      const precision = precisionOf(texel.format);
      this.stored.set({
        text: storedText(texel),
        title: `The texel under the pointer, and its lanes as the graph stored it, at ${precision}`
      });
    }
  }
}
