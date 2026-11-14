import { signal } from '@angular/core';
import type { StackStamp } from '@patina/api-client';
import { describe, expect, it } from 'vitest';
import type { PaintStore } from './paint-store';
import { StrokeGesture } from './stroke-gesture';

/** Just enough of the store for a fill's lasso: the tool, the shape and where the outline goes. */
function lassoStore() {
  const filled: (readonly StackStamp[])[] = [];
  const store = {
    tool: signal('fill'),
    fillShape: signal('lasso'),
    picking: signal(null),
    fillOutline: (points: readonly StackStamp[]) => {
      filled.push(points);
      return Promise.resolve();
    },
  };

  return { store: store as unknown as PaintStore, filled };
}

function at(x: number, y: number): StackStamp {
  return { x, y } as StackStamp;
}

describe('StrokeGesture', () => {
  // #290: the lasso copied its points array at every corner, quadratic now that a lasso takes thousands, and the panes
  // wrote the whole outline out again each time. The array a lasso hands out is now the same one as it grows.
  it('grows a lasso in place and its outline a corner at a time', () => {
    const { store, filled } = lassoStore();
    const gesture = new StrokeGesture(store, () => undefined);

    gesture.press(at(0, 0));
    const first = gesture.points();
    for (let corner = 1; corner <= 5000; corner++) {
      gesture.move(at(corner, corner % 7));
    }

    expect(gesture.points()).toBe(first);
    expect(gesture.points()).toHaveLength(5001);
    expect(gesture.polyline()).toBe(
      gesture
        .points()
        .map(point => `${point.x},${point.y}`)
        .join(' ')
    );

    gesture.release();

    expect(filled).toHaveLength(1);
    expect(filled[0]).toHaveLength(5001);
    expect(gesture.points()).toHaveLength(0);
    expect(gesture.polyline()).toBe('');
  });

  it('keeps a filled outline as it was when the next one starts', () => {
    const { store, filled } = lassoStore();
    const gesture = new StrokeGesture(store, () => undefined);

    gesture.press(at(0, 0));
    gesture.move(at(10, 0));
    gesture.move(at(10, 10));
    gesture.release();
    gesture.press(at(50, 50));
    gesture.move(at(60, 50));

    expect(filled[0]).toEqual([at(0, 0), at(10, 0), at(10, 10)]);
  });
});
