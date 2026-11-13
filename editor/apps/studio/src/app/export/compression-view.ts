import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked
} from '@angular/core';
import { ApiError, type CompressionPreviewInfo, type ExportOptions } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { messageOf } from '../core/messages';
import { bytes, decibels, fromPercent, levels, percent } from '../core/numbers';
import { ExportStore } from './export-store';

type Picture = 'source' | 'written' | 'difference';

/**
 * What a file's encoding does (#217): a region of one exported file as an engine reads it, beside what it was made
 * from and their difference made loud enough to see, with the error per lane and what the file takes in GPU memory,
 * compressed and not. Everything is the Server's: it compresses the region from the maps a build left, exactly as the
 * file holds it, and the pictures come as PNGs over HTTP.
 */
@Component({
  selector: 'ptn-compression-view',
  imports: [XuiButtonImports, XuiCalloutImports, XuiSelectImports, XuiSliderImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'well flex flex-col gap-2 p-2 text-xs', 'data-testid': 'compression-view' },
  template: `
    <div class="flex items-center gap-2">
      <span class="min-w-0 flex-1 truncate font-medium">
        <span class="readout">{{ file() }}</span>
        @if (info(); as shown) {
          <span class="text-foreground-muted">
            · {{ shown.compression === 'none' ? shown.format : compressionLabel(shown) }} · {{ shown.width }} ×
            {{ shown.height }}
          </span>
        }
      </span>
      <button type="button" xuiButton size="sm" variant="ghost" data-testid="compression-close" (click)="closed.emit()">
        Close
      </button>
    </div>

    <div class="flex flex-wrap items-center gap-3">
      @if (levels().length > 1) {
        <span class="flex items-center gap-1.5">
          <span class="text-foreground-muted">Mip</span>
          <xui-select
            class="w-24"
            aria-label="Mip level"
            data-testid="compression-level"
            [filterable]="false"
            [itemText]="levelName"
            [items]="levels()"
            [value]="'' + level()"
            (valueChange)="level.set(+($event ?? 0))"
          />
        </span>
      }
      <span class="flex items-center gap-1.5">
        <span class="text-foreground-muted">Region</span>
        <xui-select
          class="w-24"
          aria-label="Region size"
          data-testid="compression-side"
          [filterable]="false"
          [itemText]="sideName"
          [items]="sides"
          [value]="'' + side()"
          (valueChange)="side.set(+($event ?? 256))"
        />
      </span>
      <span class="flex items-center gap-1.5">
        <span class="text-foreground-muted">Across</span>
        <xui-slider
          class="w-24"
          labelStepSize="0"
          [attr.aria-label]="'Region across'"
          [min]="0"
          [max]="100"
          [stepSize]="5"
          [value]="across()"
          (valueChange)="across.set($event)"
        />
        <span class="readout w-10 text-end">{{ share(fromPercent(across())) }}</span>
        <span class="text-foreground-muted">Down</span>
        <xui-slider
          class="w-24"
          labelStepSize="0"
          [attr.aria-label]="'Region down'"
          [min]="0"
          [max]="100"
          [stepSize]="5"
          [value]="down()"
          (valueChange)="down.set($event)"
        />
        <span class="readout w-10 text-end">{{ share(fromPercent(down())) }}</span>
      </span>
    </div>

    @if (unbuilt()) {
      <xui-callout color="warning" compact data-testid="compression-unbuilt">
        <span class="flex items-center gap-2">
          <span class="min-w-0 flex-1"
            >The maps are not built as they are now, so there is nothing to compress yet.</span
          >
          <button type="button" xuiButton size="sm" data-testid="compression-build" (click)="buildMaps()">
            Build Maps
          </button>
        </span>
      </xui-callout>
    } @else if (error(); as message) {
      <xui-callout color="error" compact data-testid="compression-error">{{ message }}</xui-callout>
    }

    @if (info(); as shown) {
      <div class="grid grid-cols-3 gap-2" data-testid="compression-pictures">
        @for (picture of pictures; track picture.id) {
          <figure class="flex min-w-0 flex-col gap-1">
            <div class="bg-surface-sunken aspect-square overflow-hidden rounded-sm">
              @if (urls()[picture.id]; as url) {
                <img
                  class="h-full w-full object-contain [image-rendering:pixelated]"
                  [attr.data-testid]="'compression-' + picture.id"
                  [src]="url"
                  [alt]="picture.label"
                />
              }
            </div>
            <figcaption class="text-foreground-muted truncate">
              {{ picture.id === 'difference' ? 'Difference × ' + shown.gain : picture.label }}
            </figcaption>
          </figure>
        }
      </div>

      <div class="grid grid-cols-[auto_1fr_1fr_1fr] gap-x-3 gap-y-0.5" data-testid="compression-errors">
        <span class="text-foreground-muted">Lane</span>
        <span class="text-foreground-muted">PSNR</span>
        <span class="text-foreground-muted">Largest error</span>
        <span class="text-foreground-muted">Mean error</span>
        @for (lane of shown.lanes; track lane.lane) {
          <span class="readout">{{ lane.lane.toUpperCase() }}</span>
          <span class="readout" data-testid="compression-psnr">{{ decibels(lane.psnr) }}</span>
          <span class="readout">{{ levelsOf(lane.maxError) }}</span>
          <span class="readout">{{ levelsOf(lane.meanError) }}</span>
        }
      </div>

      <div class="flex items-center gap-2" data-testid="compression-memory">
        <span class="text-foreground-muted">GPU memory</span>
        <span class="readout">{{ size(shown.bytes) }}</span>
        @if (shown.uncompressed !== shown.bytes) {
          <span class="text-foreground-muted">of {{ size(shown.uncompressed) }} uncompressed</span>
        }
      </div>
    }
  `
})
export class CompressionView {
  protected readonly store = inject(ExportStore);

  /** The project, below projects/, and its id, which a build of its maps names. */
  readonly project = input.required<string>();
  readonly projectId = input.required<string>();
  readonly preset = input.required<string>();
  /** The file, by its template. */
  readonly file = input.required<string>();
  readonly set = input<string | null>(null);
  /** The export's changes to the preset, so the file is shown as it would be written. */
  readonly options = input<ExportOptions | null>(null);
  readonly closed = output();

  protected readonly level = signal(0);
  protected readonly side = signal(256);
  protected readonly across = signal(50);
  protected readonly down = signal(50);
  protected readonly info = signal<CompressionPreviewInfo | null>(null);
  protected readonly urls = signal<Partial<Record<Picture, string>>>({});
  protected readonly error = signal<string | null>(null);
  protected readonly unbuilt = signal(false);
  protected readonly sides = ['64', '128', '256', '512'];
  protected readonly pictures: readonly { id: Picture; label: string }[] = [
    { id: 'source', label: 'Before' },
    { id: 'written', label: 'As the file holds it' },
    { id: 'difference', label: 'Difference' }
  ];

  protected readonly levels = computed(() =>
    Array.from({ length: this.info()?.levels ?? 1 }, (_, level) => String(level))
  );
  protected readonly levelName = (level: string): string => (level === '0' ? 'Full size' : `Level ${level}`);
  protected readonly sideName = (side: string): string => `${side} px`;
  protected readonly size = bytes;

  /** The last ask, so an answer that arrives after a newer ask is dropped. */
  private asked = 0;
  /** Whether a build of the maps this started is running, after which the preview asks again. */
  private building = false;
  private readonly built = signal(0);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.release());
    effect(() => {
      const request = {
        project: this.project(),
        preset: this.preset(),
        file: this.file(),
        set: this.set(),
        options: this.options(),
        level: this.level(),
        side: this.side(),
        x: fromPercent(this.across()),
        y: fromPercent(this.down())
      };
      this.built();
      void untracked(() => this.show(request));
    });
    effect(() => {
      const state = this.store.build()?.state;
      if (this.building && (state === 'done' || state === 'failed' || state === 'cancelled')) {
        this.building = false;
        untracked(() => this.built.update(count => count + 1));
      }
    });
  }

  protected compressionLabel(shown: CompressionPreviewInfo): string {
    return this.store.compressionTerms().find(term => term.value === shown.compression)?.label ?? shown.compression;
  }

  protected readonly decibels = decibels;
  protected readonly levelsOf = levels;
  protected readonly share = percent;
  protected readonly fromPercent = fromPercent;

  /** Builds the project's maps, with no export, so there is something to compress; the preview asks again after. */
  protected async buildMaps(): Promise<void> {
    try {
      this.building = true;
      await this.store.start({ presets: [], projects: [this.projectId()] });
    } catch (failure) {
      this.building = false;
      this.error.set(messageOf(failure));
    }
  }

  private async show(request: Parameters<ExportStore['compression']>[0]): Promise<void> {
    const ask = ++this.asked;
    this.error.set(null);
    this.unbuilt.set(false);
    try {
      const info = await this.store.compression(request);
      const urls: Partial<Record<Picture, string>> = {};
      for (const picture of this.pictures) {
        urls[picture.id] = await this.store.picture(info.id, picture.id);
      }
      if (ask !== this.asked) {
        Object.values(urls).forEach(url => URL.revokeObjectURL(url));
        return;
      }
      this.release();
      this.info.set(info);
      this.urls.set(urls);
    } catch (failure) {
      if (ask !== this.asked) {
        return;
      }
      if (failure instanceof ApiError && failure.code === 'export-unbuilt') {
        this.unbuilt.set(true);
      } else {
        this.error.set(messageOf(failure));
      }
    }
  }

  private release(): void {
    Object.values(this.urls()).forEach(url => URL.revokeObjectURL(url));
  }
}
