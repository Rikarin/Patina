/** A channel of a picture, or `rgba` for the picture as it is. */
export type PictureChannel = 'rgba' | 'r' | 'g' | 'b' | 'a';

/** How many pixels fall on each of the 256 levels, per channel. */
export interface Histogram {
  r: Uint32Array;
  g: Uint32Array;
  b: Uint32Array;
  /** Rec. 709 weights on the stored values. */
  luma: Uint32Array;
}

const Levels = 256;
const BytesPerPixel = 4;
const RedWeight = 0.2126;
const GreenWeight = 0.7152;
const BlueWeight = 0.0722;

function count(levels: Uint32Array, level: number): void {
  levels[level] = (levels[level] ?? 0) + 1;
}

/** Counts the levels of RGBA bytes. */
export function computeHistogram(pixels: Uint8ClampedArray): Histogram {
  const histogram: Histogram = {
    r: new Uint32Array(Levels),
    g: new Uint32Array(Levels),
    b: new Uint32Array(Levels),
    luma: new Uint32Array(Levels)
  };
  for (let at = 0; at + BytesPerPixel <= pixels.length; at += BytesPerPixel) {
    const red = pixels[at] ?? 0;
    const green = pixels[at + 1] ?? 0;
    const blue = pixels[at + 2] ?? 0;
    count(histogram.r, red);
    count(histogram.g, green);
    count(histogram.b, blue);
    count(histogram.luma, Math.round(RedWeight * red + GreenWeight * green + BlueWeight * blue));
  }

  return histogram;
}

/** The busiest level across the colour channels, which is what a chart scales to. */
export function peakOf(histogram: Histogram): number {
  return Math.max(1, ...histogram.r, ...histogram.g, ...histogram.b);
}

const Lanes: Record<Exclude<PictureChannel, 'rgba'>, number> = { r: 0, g: 1, b: 2, a: 3 };

/** One channel of RGBA bytes as an opaque grey picture. The picture as it is comes back untouched. */
export function isolateChannel(pixels: Uint8ClampedArray, channel: PictureChannel): Uint8ClampedArray {
  if (channel === 'rgba') {
    return pixels;
  }

  const lane = Lanes[channel];
  const grey = new Uint8ClampedArray(pixels.length);
  for (let at = 0; at + BytesPerPixel <= pixels.length; at += BytesPerPixel) {
    const level = pixels[at + lane] ?? 0;
    grey[at] = level;
    grey[at + 1] = level;
    grey[at + 2] = level;
    grey[at + 3] = 255;
  }

  return grey;
}
