import { ChangeDetectionStrategy, Component, type ElementRef, computed, effect, input, viewChild } from '@angular/core';
import { type Histogram, type PictureChannel, isolateChannel, peakOf } from './histogram';

const ViewWidth = 256;
const ViewHeight = 64;

/** On a root scale, so that one level holding most of a picture does not flatten all the others. */
function areaOf(counts: readonly number[], peak: number): string {
  let path = `M0 ${String(ViewHeight)}`;
  counts.forEach((count, level) => {
    path += `L${String(level)} ${String(ViewHeight - Math.sqrt(Math.min(count, peak) / peak) * ViewHeight)}`;
  });
  return `${path}L${String(ViewWidth)} ${String(ViewHeight)}Z`;
}

/** Levels of a picture, red, green and blue over each other. It shows what it is given and claims nothing about it. */
@Component({
  selector: 'ptn-histogram',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <svg
      class="well block size-full"
      preserveAspectRatio="none"
      role="img"
      viewBox="0 0 256 64"
      [attr.aria-label]="label()"
    >
      @if (areas(); as paths) {
        <path fill="#e5484d" fill-opacity="0.6" [attr.d]="paths.r" />
        <path fill="#30a46c" fill-opacity="0.6" [attr.d]="paths.g" />
        <path fill="#3e8ed0" fill-opacity="0.6" [attr.d]="paths.b" />
      }
    </svg>
  `
})
export class HistogramChart {
  readonly histogram = input.required<Histogram | null>();
  readonly label = input('Histogram');

  protected readonly areas = computed(() => {
    const histogram = this.histogram();
    if (!histogram) {
      return null;
    }

    const peak = peakOf(histogram);
    return { r: areaOf(histogram.r, peak), g: areaOf(histogram.g, peak), b: areaOf(histogram.b, peak) };
  });
}

/** Decoded pixels with one channel shown. The canvas keeps the picture's size; the host says how big it looks. */
@Component({
  selector: 'ptn-channel-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: '<canvas #surface class="block size-full" style="image-rendering: pixelated"></canvas>'
})
export class ChannelView {
  private readonly surface = viewChild.required<ElementRef<HTMLCanvasElement>>('surface');

  readonly picture = input.required<ImageData>();
  readonly channel = input.required<PictureChannel>();

  constructor() {
    effect(() => {
      const picture = this.picture();
      const element = this.surface().nativeElement;
      element.width = picture.width;
      element.height = picture.height;
      const pixels = isolateChannel(picture.data, this.channel());
      element
        .getContext('2d')
        ?.putImageData(new ImageData(new Uint8ClampedArray(pixels), picture.width, picture.height), 0, 0);
    });
  }
}
