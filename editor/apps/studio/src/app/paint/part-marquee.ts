import { computed, signal } from '@angular/core';
import { MarqueeSlack, combineOf } from './mesh-selection';
import type { Aim, PaintStore } from './paint-store';

interface Spot {
  x: number;
  y: number;
}

/**
 * A press on the model while a layer's meshes are being picked (#110): a click takes the mesh under it in or out, and a
 * drag draws a rectangle that joins every mesh showing inside it, by the mode the meshes list chose. Shift adds and
 * Ctrl removes, for either.
 */
export class PartMarquee {
  private readonly from = signal<Spot | null>(null);
  private readonly to = signal<Spot | null>(null);

  /** The rectangle being drawn, in the viewport's pixels, once the press has moved far enough to be a drag. */
  readonly box = computed(() => {
    const [from, to] = [this.from(), this.to()];
    if (!from || !to || !dragged(from, to)) {
      return null;
    }

    return {
      left: Math.min(from.x, to.x),
      top: Math.min(from.y, to.y),
      width: Math.abs(to.x - from.x),
      height: Math.abs(to.y - from.y)
    };
  });

  constructor(
    private readonly paint: PaintStore,
    private readonly aim: () => Aim
  ) {}

  /** Starts a click or a marquee, when meshes are being picked; false when the press is for something else. */
  press(event: PointerEvent, at: Spot): boolean {
    if (this.paint.pickingPart() === null || event.button !== 0) {
      return false;
    }

    this.from.set(at);
    this.to.set(at);
    return true;
  }

  /** Follows the pointer; false when no press of this kind is down. */
  move(at: Spot): boolean {
    if (!this.from()) {
      return false;
    }

    this.to.set(at);
    return true;
  }

  /** Ends the press as a click or a marquee; false when no press of this kind was down. */
  release(event: PointerEvent, at: Spot): boolean {
    const from = this.from();
    if (!from) {
      return false;
    }

    this.from.set(null);
    this.to.set(null);
    if (event.type === 'pointercancel') {
      return true;
    }

    if (dragged(from, at)) {
      const combine = combineOf(event, this.paint.partCombine());
      void this.paint.marqueePartsOnMesh({ ...from, pressure: 1 }, { ...at, pressure: 1 }, this.aim(), combine);
    } else {
      void this.paint.togglePartOnMesh({ ...at, pressure: 1 }, this.aim(), combineOf(event, 'toggle'));
    }

    return true;
  }
}

function dragged(from: Spot, to: Spot): boolean {
  return Math.hypot(to.x - from.x, to.y - from.y) >= MarqueeSlack;
}
