import { signal } from '@angular/core';
import type { StackStamp } from '@patina/api-client';
import type { Aim, PaintStore } from './paint-store';

/**
 * What a pointer does to the brush, in whichever pane it is: a brush paints as it moves, a line runs from press to
 * release, and a path collects points until it is finished. The two panes differ only in what a position means and
 * whether there is an aim, so the rest is here once.
 */
export class StrokeGesture {
  private pressed = false;

  /** The points of a line or a path so far, for the pane to draw over itself. Nothing has been sent for these. */
  readonly points = signal<readonly StackStamp[]>([]);

  /** What the points of a line or a path were placed through: a position on a picture means nothing without it. */
  private placedOn: Aim | undefined;

  constructor(
    private readonly store: PaintStore,
    private readonly aim: () => Aim | undefined
  ) {}

  press(stamp: StackStamp): void {
    this.pressed = true;
    switch (this.store.tool()) {
      case 'brush':
      case 'eraser':
        this.store.begin(stamp, this.aim());
        break;
      case 'line':
        this.placedOn = this.aim();
        this.points.set([stamp, stamp]);
        break;
      case 'path':
        this.placedOn = this.points().length > 0 ? this.placedOn : this.aim();
        this.points.update(points => (sameSpot(points.at(-1), stamp) ? points : [...points, stamp]));
        break;
      case 'picker':
        break;
    }
  }

  move(stamp: StackStamp): void {
    if (!this.pressed) {
      return;
    }

    switch (this.store.tool()) {
      case 'brush':
      case 'eraser':
        this.store.move(stamp);
        break;
      case 'line':
        this.points.update(([start]) => (start ? [start, stamp] : []));
        break;
      case 'path':
      case 'picker':
        break;
    }
  }

  release(): void {
    if (!this.pressed) {
      return;
    }

    this.pressed = false;
    if (this.store.tool() === 'brush' || this.store.tool() === 'eraser') {
      this.store.end();
    } else if (this.store.tool() === 'line') {
      this.commit('line');
    }
  }

  /** Paints the path collected so far. */
  finish(): void {
    if (this.store.tool() === 'path') {
      this.commit('path');
    }
  }

  /** Lets go of whatever is under way: the points placed, and a mask waiting for its colour. */
  cancel(): void {
    this.points.set([]);
    this.store.picking.set(null);
  }

  private commit(curve: 'line' | 'path'): void {
    const points = this.points();
    this.points.set([]);
    void this.store.paintCurve(points, curve, this.placedOn);
  }
}

/** A double click is two presses in one place, and a second point there adds nothing. */
function sameSpot(last: StackStamp | undefined, stamp: StackStamp): boolean {
  return last?.x === stamp.x && last.y === stamp.y;
}
