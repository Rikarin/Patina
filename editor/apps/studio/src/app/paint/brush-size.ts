import { percent } from '../core/numbers';
import { maximumRadius } from './paint-commands';
import type { PaintStore } from './paint-store';

/** The size slider's positions, which run on a log scale so a small brush is as easy to set as a big one. */
export const SizeSteps = 1000;

/** Where a radius stands on the size slider. */
export function sizePosition(paint: PaintStore): number {
  return Math.round((Math.log(Math.max(1, paint.radius())) / Math.log(maximumRadius(paint))) * SizeSteps);
}

/** The radius a position of the size slider stands for, a whole number from 1 to the largest. */
export function radiusAt(paint: PaintStore, position: number): number {
  return Math.max(1, Math.round(Math.exp((position / SizeSteps) * Math.log(maximumRadius(paint)))));
}

/** A size in its space's own words: pixels of the screen, texels of the map, or a share of the model. */
export function sizeShown(space: string, value: number): string {
  switch (space) {
    case 'texture':
      return `${Math.round(value)} tx`;
    case 'model':
      return percent(value / 1000, 1);
    default:
      return `${Math.round(value)} px`;
  }
}

/** What a size is, which depends on what it is measured in. */
export function sizeMeaning(space: string): string {
  switch (space) {
    case 'texture':
      return 'The radius of the brush in texels of the map, the same at any zoom, measured where a stroke starts.';
    case 'model':
      return 'The radius of the brush as a share of the model’s size, the same at any zoom and on any part of it.';
    default:
      return 'The radius of the brush in pixels of the screen, the same under the pointer at any zoom.';
  }
}
