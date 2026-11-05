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

  constructor(
    private readonly store: PaintStore,
    private readonly aim: () => Aim | undefined
  ) {}

  press(stamp: StackStamp): void {
    this.pressed = true;
    switch (this.store.tool()) {
      case 'brush':
        this.store.begin(stamp, this.aim());
        break;
      case 'line':
        this.points.set([stamp, stamp]);
        break;
      case 'path':
        this.points.update(points => [...points, stamp]);
        break;
    }
  }

  move(stamp: StackStamp): void {
    if (!this.pressed) {
      return;
    }

    switch (this.store.tool()) {
      case 'brush':
        this.store.move(stamp);
        break;
      case 'line':
        this.points.update(([start]) => (start ? [start, stamp] : []));
        break;
      case 'path':
        break;
    }
  }

  release(): void {
    if (!this.pressed) {
      return;
    }

    this.pressed = false;
    if (this.store.tool() === 'brush') {
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

  cancel(): void {
    this.points.set([]);
  }

  private commit(curve: 'line' | 'path'): void {
    const points = this.points();
    this.points.set([]);
    void this.store.paintCurve(points, curve, this.aim());
  }
}
