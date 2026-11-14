/** A channel of a picture, or `rgba` for the picture as it is. */
export type PictureChannel = 'rgba' | 'r' | 'g' | 'b' | 'a';

/**
 * How much of a picture falls on each of 256 levels, per channel: counts or shares, since a chart scales to its peak.
 * Whoever has the picture counts it; the Server does for a graph's node, on the GPU at full size.
 */
export interface Histogram {
  r: readonly number[];
  g: readonly number[];
  b: readonly number[];
  /** Rec. 709 weights on the stored values. */
  luma: readonly number[];
}

const BytesPerPixel = 4;

/** The busiest level across the colour channels, which is what a chart scales to. Nothing at all is not a division. */
export function peakOf(histogram: Histogram): number {
  const peak = Math.max(0, ...histogram.r, ...histogram.g, ...histogram.b);
  return peak > 0 ? peak : 1;
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
