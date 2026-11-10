import type { StackDecal, StackView, ViewportPick } from '@patina/api-client';
import { describe, expect, it } from 'vitest';
import {
  Unwarped,
  decalFrame,
  decalOutline,
  extendedPath,
  onDecal,
  onDecalPlane,
  placedDecal,
  turned,
  warpGrid
} from './surface-frame';
import { project } from './symmetry-guide';

/** A camera on the z axis, two units out, looking back at the origin. */
const view: StackView = {
  eye: [0, 0, 2],
  right: [1, 0, 0],
  up: [0, 1, 0],
  forward: [0, 0, -1],
  verticalFieldOfView: Math.PI / 2,
  width: 400,
  height: 400
};

const decal: StackDecal = {
  centre: [0, 0, 0],
  facing: [0, 0, 1],
  up: [0, 1, 0.5],
  width: 1,
  height: 0.5,
  depth: 0.2,
  angle: 1.3,
  fade: 0.25,
  tiled: false,
  sampling: 'Bilinear',
  box: [],
  warp: []
};

function must<T>(value: T | null | undefined): T {
  if (value === null || value === undefined) {
    throw new Error('Expected a value.');
  }

  return value;
}

const close = (actual: readonly number[], expected: readonly number[]) =>
  actual.forEach((value, at) => expect(value).toBeCloseTo(expected[at] ?? Number.NaN, 5));

describe('surface frame', () => {
  it('squares a decal up to its facing, as the kernel does, and its right reads the right way round', () => {
    const frame = decalFrame(decal);
    close(frame.up, [0, 1, 0]);
    close(frame.right, [1, 0, 0]);
    close(frame.forward, [0, 0, 1]);
  });

  it('draws the square where the picture lands: its top left up and to the left, seen from the front', () => {
    const corners = decalOutline(decal, view);
    expect(corners).not.toBeNull();
    const [topLeft, topRight, bottomRight] = must(corners);
    expect(topLeft[0]).toBeLessThan(topRight[0]);
    expect(topLeft[1]).toBeLessThan(bottomRight[1]);
    // Twice as wide as it is tall, as the decal is.
    expect(topRight[0] - topLeft[0]).toBeCloseTo(2 * (bottomRight[1] - topRight[1]), 3);
  });

  it('finds a place on the picture back on the decal, which is what a warp point drags on', () => {
    const frame = decalFrame(decal);
    for (const [s, t] of [
      [0.25, 0.75],
      [0.5, 0.5],
      [0.9, 0.1]
    ] as const) {
      const at = project(view, onDecal(decal, frame, s, t));
      expect(at).not.toBeNull();
      const [x, y] = must(at);
      close(must(onDecalPlane(decal, view, x, y)), [s, t]);
    }
  });

  it('turns a decal counter-clockwise, seen from where it faces, by a positive angle', () => {
    const quarter = decalFrame(turned(decal, Math.PI / 2));
    // Up goes to the left: what was right is now up.
    close(quarter.up, [-1, 0, 0]);
    close(quarter.right, [0, 1, 0]);
  });

  it('puts the warp grid on the square, and an empty warp is the grid itself', () => {
    const grid = must(warpGrid(decal, view));
    const corners = must(decalOutline(decal, view));
    close(must(grid[0]), corners[0]);
    close(must(grid[8]), corners[2]);
    expect(Unwarped).toHaveLength(18);
  });

  it('places a decal where a pick met the model, facing out of the face, and grows a path point by point', () => {
    const hit: ViewportPick = {
      found: true,
      view,
      triangle: 3,
      u: 0.5,
      v: 0.5,
      position: [0.1, 0.2, 0.3],
      bounds: [0, 0, 0, 3, 4, 0],
      normal: [0, 0, 1]
    };
    const placed = placedDecal(hit, null);
    close(placed.centre, [0.1, 0.2, 0.3]);
    close(placed.facing, [0, 0, 1]);
    // A share of the model's size: its bounds' diagonal is five.
    expect(placed.width).toBeCloseTo(0.75, 5);
    expect(placed.box).toEqual([0, 0, 0, 3, 4, 0]);

    // Moving a placed decal keeps its size.
    expect(placedDecal(hit, { ...placed, width: 2 }).width).toBe(2);

    const once = extendedPath(hit, null);
    const twice = extendedPath({ ...hit, position: [1, 1, 0.3] }, once);
    expect(twice.points).toHaveLength(2);
    expect(twice.radius).toBe(once.radius);
    close(must(twice.points[1]).position, [1, 1, 0.3]);
  });
});
