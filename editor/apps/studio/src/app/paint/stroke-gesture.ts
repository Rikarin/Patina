import { signal } from '@angular/core';
import type { StackStamp } from '@patina/api-client';
import type { Aim, PaintStore, PaintTool } from './paint-store';

/** The tools a drag paints with as it moves, each with the brush. */
export const DragTools: ReadonlySet<PaintTool> = new Set(['brush', 'eraser', 'smudge', 'clone', 'projection']);

/** The steps a line snaps to with Cmd or Ctrl held, in radians. */
const SnapStep = Math.PI / 36;

/** What a press asks for beyond where it is. */
export interface PressKeys {
  /** A line from where the last stroke ended, which Shift asks for. */
  line?: boolean;
  /** That line at a whole number of snap steps, which Cmd or Ctrl asks for. */
  snap?: boolean;
}

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

  /** Where the brush is in the drag under way, which a lazy mouse keeps behind the pointer. */
  private brushAt: StackStamp | null = null;

  /**
   * Where the last stroke ended, what it was aimed through and the layer it painted: a Shift-click draws a line on from
   * there, and a brush that carries strokes on starts there (#126).
   */
  private ended: { stamp: StackStamp; aim: Aim | undefined; layer: string | null } | null = null;

  constructor(
    private readonly store: PaintStore,
    private readonly aim: () => Aim | undefined
  ) {}

  /** Whether a Shift-click here would draw a line, which needs a stroke that ended under this same view. */
  canLine(): boolean {
    return DragTools.has(this.store.tool()) && this.ended !== null && sameAim(this.ended.aim, this.aim());
  }

  press(stamp: StackStamp, keys: PressKeys = {}): void {
    if (keys.line && this.canLine()) {
      this.lineTo(stamp, keys.snap ?? false);
      return;
    }

    const tool = this.store.tool();
    if (tool === 'fill') {
      void this.store.fillAt(stamp, this.aim());
      return;
    }

    this.pressed = true;
    if (DragTools.has(tool)) {
      const resume = this.resumeAt(stamp);
      this.brushAt = resume ?? stamp;
      this.store.begin(stamp, this.aim(), resume ?? undefined);
      return;
    }

    if (tool === 'line') {
      this.placedOn = this.aim();
      this.points.set([stamp, stamp]);
    } else if (tool === 'path') {
      this.placedOn = this.points().length > 0 ? this.placedOn : this.aim();
      this.points.update(points => (sameSpot(points.at(-1), stamp) ? points : [...points, stamp]));
    }
  }

  /**
   * Where a press here carries the last stroke on from, or null for a fresh one: with the brush set to carry strokes
   * on, a press near where the last one's brush stood, under the same view and on the same layer, starts there.
   */
  resumeAt(stamp: StackStamp): StackStamp | null {
    const ended = this.ended;
    const aim = this.aim();
    if (!this.store.brush().resume || !ended) {
      return null;
    }

    if (ended.layer !== this.store.layer() || !sameAim(ended.aim, aim)) {
      return null;
    }

    const reach = this.store.resumeReach(aim !== undefined);
    return Math.hypot(stamp.x - ended.stamp.x, stamp.y - ended.stamp.y) <= reach ? ended.stamp : null;
  }

  /** A straight stroke from where the last one ended to here, as one undo step. */
  lineTo(stamp: StackStamp, snap: boolean): void {
    const from = this.ended?.stamp;
    if (!from) {
      return;
    }

    const to = snap ? snapped(from, stamp) : stamp;
    const aim = this.aim();
    this.ended = { stamp: to, aim, layer: this.store.layer() };
    void this.store.paintCurve([from, to], 'line', aim);
  }

  move(stamp: StackStamp): void {
    if (!this.pressed) {
      return;
    }

    const tool = this.store.tool();
    if (DragTools.has(tool)) {
      this.brushAt = this.brushAt ? trailed(this.brushAt, stamp, this.store.brush().lazy) : stamp;
      this.store.move(stamp);
      return;
    }

    if (tool === 'line') {
      this.points.update(([start]) => (start ? [start, stamp] : []));
    }
  }

  release(): void {
    if (!this.pressed) {
      return;
    }

    this.pressed = false;
    if (DragTools.has(this.store.tool())) {
      this.ended = this.brushAt ? { stamp: this.brushAt, aim: this.aim(), layer: this.store.layer() } : null;
      this.brushAt = null;
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
    const last = points.at(-1);
    if (last && points.length > 1) {
      this.ended = { stamp: last, aim: this.placedOn, layer: this.store.layer() };
    }

    void this.store.paintCurve(points, curve, this.placedOn);
  }
}

/** A double click is two presses in one place, and a second point there adds nothing. */
function sameSpot(last: StackStamp | undefined, stamp: StackStamp): boolean {
  return last?.x === stamp.x && last.y === stamp.y;
}

/** Positions on a picture mean the same only through the same camera, and the atlas has none. */
function sameAim(left: Aim | undefined, right: Aim | undefined): boolean {
  return JSON.stringify(left ?? null) === JSON.stringify(right ?? null);
}

/**
 * Where a brush on a string ends up when the pointer moves: the Worker's lazy mouse, `PaintStroke.Trailed` in Vixen,
 * which a line has to start from or it would begin where the pointer was rather than where the paint stopped.
 */
export function trailed(brush: StackStamp, pointer: StackStamp, lazy: number): StackStamp {
  const [dx, dy] = [pointer.x - brush.x, pointer.y - brush.y];
  const length = Math.hypot(dx, dy);
  if (length <= lazy) {
    return brush;
  }

  const share = (length - lazy) / length;
  return { ...pointer, x: brush.x + dx * share, y: brush.y + dy * share };
}

/** The end of a line turned to the nearest snap step about its start, keeping its length. */
export function snapped(from: StackStamp, to: StackStamp): StackStamp {
  const [dx, dy] = [to.x - from.x, to.y - from.y];
  const angle = Math.round(Math.atan2(dy, dx) / SnapStep) * SnapStep;
  const length = Math.hypot(dx, dy);
  return { ...to, x: from.x + Math.cos(angle) * length, y: from.y + Math.sin(angle) * length };
}
