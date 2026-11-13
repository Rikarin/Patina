import type { StackDecal, StackView, ViewportPick } from '@patina/api-client';
import { describe, expect, it } from 'vitest';
import {
  Unwarped,
  decalChannel,
  decalFrame,
  decalOutline,
  extendedPath,
  geometryLines,
  geometryWarp,
  halfwayAlong,
  isPicture,
  onDecal,
  onDecalPlane,
  pictureName,
  placedDecal,
  turned,
  underDecal,
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

describe('a picture dropped on the mesh (#133)', () => {
  it('colours the base color where the set has one', () => {
    expect(decalChannel(['roughness', 'baseColor'])).toBe('baseColor');
    expect(decalChannel(['roughness'])).toBe('roughness');
    expect(decalChannel([])).toBe('baseColor');
  });

  it('is a picture by its extension against the formats the engine reads', () => {
    expect(isPicture('C:\\Pictures\\Logo.PNG', ['.png', '.exr'])).toBe(true);
    expect(isPicture('library/projects/a/logo.exr', ['png', 'exr'])).toBe(true);
    expect(isPicture('crate.glb', ['.png'])).toBe(false);
    expect(isPicture('folder.png/readme', ['.png'])).toBe(false);
    expect(isPicture('anything.tga', null)).toBe(true);
  });

  it('names its layer after the file', () => {
    expect(pictureName('C:\\Pictures\\Worn Logo.png')).toBe('Worn Logo');
    expect(pictureName('projects/a/pictures/stripe.v2.exr')).toBe('stripe.v2');
  });
});

describe('Warp to Geometry (#134)', () => {
  /** The decal's square, laid flat: a place at (s, t) is where the decal says it is. */
  const flat = (s: number, t: number): [number, number, number] => [(s - 0.5) * 1, (0.5 - t) * 0.5, 0];

  it('leaves a flat surface unwarped', () => {
    const lines = geometryLines().map(line => line.map(({ s, t }) => flat(s, t)));
    const warp = geometryWarp(decal, lines);
    warp.forEach((value, at) => expect(value).toBeCloseTo(Unwarped[at] ?? 0, 6));
  });

  it('moves the middle toward where the surface turns away, so each half of the picture covers as much of it', () => {
    // The right half of each row bends back at 60°, so it is twice as long on the surface as across the square: of
    // the 1.5 the row runs, half is reached a quarter into the right half, at 0.625 across, not at the middle.
    const bent = (s: number, t: number): [number, number, number] =>
      s <= 0.5 ? flat(s, t) : [s - 0.5, (0.5 - t) * 0.5, -(s - 0.5) * Math.sqrt(3)];
    const lines = geometryLines().map(line => line.map(({ s, t }) => bent(s, t)));
    const warp = geometryWarp(decal, lines);

    for (const row of [0, 1, 2]) {
      expect(warp[(row * 3 + 1) * 2]).toBeCloseTo(0.625, 6);
      // The edges stay on the square's.
      expect(warp[row * 3 * 2]).toBe(0);
      expect(warp[(row * 3 + 2) * 2]).toBe(1);
    }

    // Down the columns nothing bends.
    for (const column of [0, 1, 2]) {
      expect(warp[(3 + column) * 2 + 1]).toBeCloseTo(0.5, 6);
    }
  });

  it('counts a stretch it could not find as flat', () => {
    expect(halfwayAlong([null, null, null], 1)).toBeCloseTo(0.5);
    expect(halfwayAlong([[0, 0, 0], null, [3, 0, 0]], 1)).toBeCloseTo(0.5);
    expect(halfwayAlong([], 1)).toBe(0.5);
  });

  it('takes only what a pick found under the decal, within its depth and facing it', () => {
    expect(underDecal(decal, [0.2, 0, 0.05], [0, 0, 1])).toBe(true);
    expect(underDecal(decal, [0.2, 0, 0.5], [0, 0, 1])).toBe(false);
    expect(underDecal(decal, [0.2, 0, 0], [0, 0, -1])).toBe(false);
  });
});
